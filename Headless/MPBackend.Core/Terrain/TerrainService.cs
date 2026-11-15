using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Utilities;

namespace MPBackend.Core.Terrain
{
    public enum ElevationStatus
    {
        Valid,
        Ocean,
        /// <summary>Tile not available yet (still downloading) or outside coverage.</summary>
        Unavailable
    }

    /// <param name="Alt">Terrain height AMSL in metres (null when unavailable).</param>
    /// <param name="GeoidUndulation">EGM96 geoid height above the WGS84 ellipsoid at this point (metres).</param>
    public sealed record Elevation(double Lat, double Lng, double? Alt, ElevationStatus Status, string Source,
        double GeoidUndulation);

    /// <param name="Distance">Metres from the first point along the path.</param>
    public sealed record ProfilePoint(double Distance, double Lat, double Lng, double? Alt);

    public sealed record LatLng(double Lat, double Lng);

    /// <summary>
    /// Terrain heights from MP's srtm class (GeoTIFF/DTED in the srtm folder first, then SRTM .hgt tiles which are
    /// downloaded on demand). TERRAIN_REQUEST from vehicles is answered automatically by MP's TerrainFollow.
    /// </summary>
    public sealed class TerrainService
    {
        private const int MaxProfileSamples = 2000;

        public async Task<Elevation> GetElevationAsync(double lat, double lng, TimeSpan wait,
            CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow + wait;
            while (true)
            {
                var r = srtm.getAltitude(lat, lng);
                if (r.currenttype != srtm.tiletype.invalid || DateTime.UtcNow >= deadline)
                    return ToElevation(lat, lng, r);
                // srtm queues a background download of the tile and answers invalid until it is ready
                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Heights sampled every <paramref name="spacing"/> metres along a polyline (MP's elevation graph).
        /// </summary>
        public async Task<IReadOnlyList<ProfilePoint>> GetProfileAsync(IReadOnlyList<LatLng> path, double spacing,
            TimeSpan wait, CancellationToken ct = default)
        {
            if (path == null || path.Count == 0)
                return Array.Empty<ProfilePoint>();

            var total = 0.0;
            for (var i = 1; i < path.Count; i++)
                total += Distance(path[i - 1], path[i]);
            spacing = Math.Max(spacing, total / MaxProfileSamples);
            if (spacing <= 0)
                spacing = 1;

            var samples = new List<(double dist, double lat, double lng)> { (0, path[0].Lat, path[0].Lng) };
            var travelled = 0.0;
            for (var i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                var len = Distance(a, b);
                // first sample of this leg at the next multiple of spacing
                var next = Math.Ceiling(travelled / spacing + 1e-9) * spacing;
                for (; next < travelled + len; next += spacing)
                {
                    var f = (next - travelled) / len;
                    samples.Add((next, a.Lat + (b.Lat - a.Lat) * f, a.Lng + (b.Lng - a.Lng) * f));
                }

                travelled += len;
                samples.Add((travelled, b.Lat, b.Lng));
            }

            var deadline = DateTime.UtcNow + wait;
            while (true)
            {
                var results = samples.Select(s =>
                {
                    var r = srtm.getAltitude(s.lat, s.lng);
                    return (s, r);
                }).ToList();

                if (results.All(x => x.r.currenttype != srtm.tiletype.invalid) || DateTime.UtcNow >= deadline)
                    return results.Select(x => new ProfilePoint(x.s.dist, x.s.lat, x.s.lng,
                        x.r.currenttype == srtm.tiletype.invalid ? null : x.r.alt)).ToList();

                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }

        public static double GeoidUndulation(double lat, double lng) => GeoidHeightsDotNet.GeoidHeights.undulation(lat, lng);

        public const int MinHeightmapSize = 2;
        public const int MaxHeightmapSize = 257;
        public const int MaxHeightmapLevel = 20;
        /// <summary>Tiles coarser than this come back flat (0) without touching the SRTM store: a whole-globe
        /// view would otherwise queue downloads of every 1x1 degree SRTM tile on earth.</summary>
        public const int MinSampledLevel = 9;

        /// <summary>
        /// The geographic (WGS84 degrees) tile rectangle of Cesium's GeographicTilingScheme: two tiles across and one
        /// down at level 0, each level splits a tile in four, x grows eastward from -180 and y grows southward from +90.
        /// Returns (West, South, East, North) in degrees.
        /// </summary>
        public static (double West, double South, double East, double North) TileRectangle(int level, int x, int y)
        {
            if (level < 0 || level > MaxHeightmapLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            var tilesX = 2 << level; // 2 * 2^level
            var tilesY = 1 << level;
            if (x < 0 || x >= tilesX)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= tilesY)
                throw new ArgumentOutOfRangeException(nameof(y));
            var width = 180.0 / tilesY; // 180 / 2^level
            var west = -180.0 + x * width;
            var north = 90.0 - y * width;
            return (west, north - width, west + width, north);
        }

        /// <summary>
        /// Sample positions of a <paramref name="size"/> x <paramref name="size"/> heightmap over a tile: row 0 is the
        /// north edge, column 0 the west edge, both edges inclusive (Cesium's heightmap layout).
        /// </summary>
        public static IEnumerable<LatLng> HeightmapPositions(int level, int x, int y, int size)
        {
            if (size < MinHeightmapSize || size > MaxHeightmapSize)
                throw new ArgumentOutOfRangeException(nameof(size));
            var (west, south, east, north) = TileRectangle(level, x, y);
            for (var r = 0; r < size; r++)
            {
                var lat = north - (north - south) * r / (size - 1);
                for (var c = 0; c < size; c++)
                    yield return new LatLng(lat, west + (east - west) * c / (size - 1));
            }
        }

        /// <summary>
        /// A heightmap tile for a 3D map client (Cesium's CustomHeightmapTerrainProvider): <paramref name="size"/>²
        /// heights in metres above the WGS84 ellipsoid (SRTM AMSL plus the EGM96 undulation), row 0 north, column 0
        /// west. Waits up to <paramref name="wait"/> for SRTM tiles still downloading; heights still unknown after
        /// that, and ocean, are 0, as is every tile coarser than <see cref="MinSampledLevel"/>.
        /// </summary>
        public async Task<float[]> GetHeightmapAsync(int level, int x, int y, int size, TimeSpan wait,
            CancellationToken ct = default)
        {
            var positions = HeightmapPositions(level, x, y, size).ToList();
            if (level < MinSampledLevel)
                return new float[positions.Count];
            // The EGM96 evaluation costs milliseconds per point; the geoid changes by centimetres across a tile of
            // level 9 or finer, so evaluate the four corners and interpolate.
            var (west, south, east, north) = TileRectangle(level, x, y);
            double uNW = GeoidUndulation(north, west), uNE = GeoidUndulation(north, east);
            double uSW = GeoidUndulation(south, west), uSE = GeoidUndulation(south, east);
            double Undulation(int index)
            {
                var fr = (double)(index / size) / (size - 1); // 0 at the north edge
                var fc = (double)(index % size) / (size - 1); // 0 at the west edge
                var n = uNW + (uNE - uNW) * fc;
                var s = uSW + (uSE - uSW) * fc;
                return n + (s - n) * fr;
            }

            var deadline = DateTime.UtcNow + wait;
            while (true)
            {
                var results = positions.Select(p => srtm.getAltitude(p.Lat, p.Lng)).ToList();
                if (results.All(r => r.currenttype != srtm.tiletype.invalid) || DateTime.UtcNow >= deadline)
                {
                    var heights = new float[positions.Count];
                    for (var i = 0; i < heights.Length; i++)
                    {
                        var r = results[i];
                        heights[i] = r.currenttype == srtm.tiletype.valid ? (float)(r.alt + Undulation(i)) : 0f;
                    }

                    return heights;
                }

                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }

        private static Elevation ToElevation(double lat, double lng, srtm.altresponce r) => new Elevation(
            lat, lng,
            r.currenttype == srtm.tiletype.invalid ? null : r.alt,
            r.currenttype switch
            {
                srtm.tiletype.valid => ElevationStatus.Valid,
                srtm.tiletype.ocean => ElevationStatus.Ocean,
                _ => ElevationStatus.Unavailable
            },
            r.altsource,
            GeoidUndulation(lat, lng));

        private static double Distance(LatLng a, LatLng b) =>
            new PointLatLngAlt(a.Lat, a.Lng).GetDistance(new PointLatLngAlt(b.Lat, b.Lng));
    }
}
