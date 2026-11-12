using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.MapProviders;
using MPBackend.Core.Jobs;

namespace MPBackend.Core.Maps
{
    public sealed record MapProviderInfo(string Name, Guid Id, int MinZoom, int MaxZoom);

    public sealed record MapTile(byte[] Data, string ContentType);

    /// <summary>
    /// Map tiles from GMap.NET's providers (the ones MP's map uses), with an on-disk cache in MP's layout so an
    /// existing Mission Planner gmapcache can be reused, and an offline (cache only) mode.
    /// </summary>
    public sealed class TileService
    {
        private const int MaxPrefetchTiles = 50000;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TileService));

        private readonly JobManager jobs;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, bool> initialised = new();

        public TileService(JobManager jobs, BackendOptions options)
        {
            this.jobs = jobs;
            // as Program.cs: identify ourselves (some tile servers reject generic agents) and use a Google key if set
            GMapProvider.UserAgent = MissionPlanner.Utilities.Settings.Instance.UserAgent;
            if (MissionPlanner.Utilities.Settings.Instance["GoogleApiKey"] != null)
                GoogleMapProvider.APIKey = MissionPlanner.Utilities.Settings.Instance["GoogleApiKey"];
            GMapProvider.TileImageProxy = new RawImageProxy();
            GMaps.Instance.PrimaryCache = new TileDiskCache(options.MapCacheDirectory ??
                                                            Path.Combine(options.DataDirectory, "gmapcache"));
            GMaps.Instance.Mode = options.MapAccessMode;
            GMaps.Instance.UseMemoryCache = true;
        }

        public AccessMode Mode
        {
            get => GMaps.Instance.Mode;
            set => GMaps.Instance.Mode = value;
        }

        public IReadOnlyList<MapProviderInfo> Providers() =>
            GMapProviders.List
                .Where(p => p != null && !string.IsNullOrEmpty(p.Name) && p.Projection != null)
                .Select(p => new MapProviderInfo(p.Name, p.Id, p.MinZoom, p.MaxZoom ?? 24))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public GMapProvider FindProvider(string name)
        {
            var p = GMapProviders.List.FirstOrDefault(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? (Guid.TryParse(name, out var id) ? GMapProviders.TryGetProvider(id) : null)
                    ?? throw new KeyNotFoundException($"unknown map provider '{name}'");

            // the WinForms map control calls this when a provider is selected; e.g. Google looks up its current
            // tile version here, without which tile URLs 404
            if (initialised.TryAdd(p.Id, true))
            {
                try
                {
                    p.OnInitialized();
                }
                catch (Exception ex)
                {
                    log.Warn($"map provider {p.Name} init", ex);
                }
            }

            return p;
        }

        /// <summary>
        /// One tile (XYZ / slippy-map numbering). Null when not available (e.g. offline and not cached).
        /// </summary>
        public MapTile GetTile(string provider, int zoom, long x, long y)
        {
            var p = FindProvider(provider);
            using var image = GMaps.Instance.GetImageFrom(p, new GPoint(x, y), zoom, out var error);
            if (image?.Data == null || image.Data.Length == 0)
            {
                if (error != null && GMaps.Instance.Mode != AccessMode.CacheOnly)
                    throw new IOException($"{p.Name} z{zoom}/{x}/{y}: {error.Message}", error);
                return null;
            }

            var bytes = image.Data.ToArray();
            // some providers answer "no tile here" with a gzip-compressed stub that GMap stores as-is
            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
                bytes = Gunzip(bytes);
            var contentType = Sniff(bytes);
            // anything that is not an image is a provider error page or stub: report it as a missing tile
            return contentType == "application/octet-stream" ? null : new MapTile(bytes, contentType);
        }

        private static byte[] Gunzip(byte[] data)
        {
            try
            {
                using var input = new System.IO.Compression.GZipStream(new MemoryStream(data),
                    System.IO.Compression.CompressionMode.Decompress);
                using var output = new MemoryStream();
                input.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception)
            {
                return data;
            }
        }

        /// <summary>
        /// Download every tile in a lat/lng box for a zoom range into the cache (MP's "prefetch map").
        /// </summary>
        public Job StartPrefetch(string provider, double north, double west, double south, double east, int minZoom,
            int maxZoom)
        {
            var p = FindProvider(provider);
            var rect = RectLatLng.FromLTRB(west, north, east, south);
            var tiles = new List<(int zoom, GPoint pos)>();
            for (var z = minZoom; z <= maxZoom; z++)
            {
                tiles.AddRange(p.Projection.GetAreaTileList(rect, z, 0).Select(t => (z, t)));
                if (tiles.Count > MaxPrefetchTiles)
                    throw new ArgumentException($"area too large: more than {MaxPrefetchTiles} tiles");
            }

            return jobs.Start("tiles.prefetch", $"Prefetch {tiles.Count} {p.Name} tiles", async ctx =>
            {
                var done = 0;
                var failed = 0;
                var cache = GMaps.Instance.PrimaryCache;
                await Parallel.ForEachAsync(tiles,
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ctx.Token },
                    (t, _) =>
                    {
                        if (!cache.CheckImageFromCache(p.DbId, t.pos, t.zoom))
                        {
                            using var img = p.GetTileImage(t.pos, t.zoom);
                            if (img?.Data != null)
                                cache.PutImageToCache(img.Data.ToArray(), p.DbId, t.pos, t.zoom);
                            else
                                Interlocked.Increment(ref failed);
                        }

                        var n = Interlocked.Increment(ref done);
                        ctx.Report(n * 100 / tiles.Count, $"{n}/{tiles.Count}");
                        return ValueTask.CompletedTask;
                    }).ConfigureAwait(false);
                return new { tiles = tiles.Count, failed };
            });
        }

        private static string Sniff(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
            if (b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8) return "image/jpeg";
            if (b.Length > 3 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "image/gif";
            if (b.Length > 11 && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
            return "application/octet-stream";
        }

        /// <summary>Tiles are passed through as encoded bytes; no decoding is needed headless.</summary>
        private sealed class RawImageProxy : PureImageProxy
        {
            public override PureImage FromStream(Stream stream) =>
                new RawImage { Data = stream as MemoryStream ?? Copy(stream) };

            public override bool Save(Stream stream, PureImage image)
            {
                image.Data.Position = 0;
                image.Data.CopyTo(stream);
                return true;
            }

            private static MemoryStream Copy(Stream s)
            {
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        private sealed class RawImage : PureImage
        {
            public override void Dispose() => Data?.Dispose();
        }

        /// <summary>
        /// Same layout as MP's Maps.MyImageCache: {root}/TileDBv3/{lang}/{provider}/{zoom}/{y}/{x}.jpg.
        /// </summary>
        private sealed class TileDiskCache : PureImageCache
        {
            private readonly string root;

            public TileDiskCache(string cacheRoot)
            {
                root = Path.Combine(cacheRoot, "TileDBv3", GMapProvider.LanguageStr);
                Directory.CreateDirectory(root);
            }

            private string FileFor(int type, GPoint pos, int zoom)
            {
                var provider = GMapProviders.TryGetProvider(type)?.Name ?? type.ToString();
                return Path.Combine(root, provider, zoom.ToString(), pos.Y.ToString(), pos.X + ".jpg");
            }

            public bool PutImageToCache(byte[] tile, int type, GPoint pos, int zoom)
            {
                try
                {
                    var file = FileFor(type, pos, zoom);
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllBytes(file, tile);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public PureImage GetImageFromCache(int type, GPoint pos, int zoom)
            {
                var file = FileFor(type, pos, zoom);
                if (!File.Exists(file))
                    return null;
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    return new RawImage { Data = new MemoryStream(bytes, 0, bytes.Length, false, true) };
                }
                catch
                {
                    return null;
                }
            }

            public bool CheckImageFromCache(int type, GPoint pos, int zoom) => File.Exists(FileFor(type, pos, zoom));

            public int DeleteOlderThan(DateTime date, int? type)
            {
                var dir = type.HasValue
                    ? Path.Combine(root, GMapProviders.TryGetProvider(type.Value)?.Name ?? "")
                    : root;
                if (!Directory.Exists(dir))
                    return 0;
                var deleted = 0;
                foreach (var f in Directory.EnumerateFiles(dir, "*.jpg", SearchOption.AllDirectories))
                {
                    if (File.GetLastWriteTimeUtc(f) < date.ToUniversalTime())
                    {
                        File.Delete(f);
                        deleted++;
                    }
                }

                return deleted;
            }
        }
    }
}
