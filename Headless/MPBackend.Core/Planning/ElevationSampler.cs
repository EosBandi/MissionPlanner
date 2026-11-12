using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Utilities;
using MPBackend.Core.Terrain;

namespace MPBackend.Core.Planning
{
    /// <summary>Terrain height source for the spray planner. Returns metres AMSL, null where unknown.</summary>
    public interface IElevationSampler
    {
        bool Available { get; }
        double? Sample(double lat, double lng);
    }

    /// <summary>No terrain data: every sample is unknown.</summary>
    public sealed class NoElevationSampler : IElevationSampler
    {
        public static readonly NoElevationSampler Instance = new NoElevationSampler();
        public bool Available => false;
        public double? Sample(double lat, double lng) => null;
    }

    /// <summary>Mission Planner's srtm store (GeoTIFF/DTED files first, SRTM tiles downloaded on demand).</summary>
    public sealed class SrtmElevationSampler : IElevationSampler
    {
        public bool Available => true;

        public double? Sample(double lat, double lng)
        {
            var r = srtm.getAltitude(lat, lng);
            switch (r.currenttype)
            {
                case srtm.tiletype.valid: return r.alt;
                case srtm.tiletype.ocean: return 0;
                default: return null;
            }
        }

        /// <summary>
        /// srtm queues a background download the first time a tile is asked for and answers "invalid" until it is
        /// there; poll the given points until they all answer or <paramref name="wait"/> runs out.
        /// </summary>
        public static async Task<bool> WarmUpAsync(IEnumerable<LatLng> points, TimeSpan wait, CancellationToken ct)
        {
            var list = points.ToList();
            var deadline = DateTime.UtcNow + wait;
            while (true)
            {
                if (list.All(p => srtm.getAltitude(p.Lat, p.Lng).currenttype != srtm.tiletype.invalid))
                    return true;
                if (DateTime.UtcNow >= deadline)
                    return false;
                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Bilinear lookup in a client-supplied <see cref="ElevationRaster"/>.</summary>
    public sealed class RasterElevationSampler : IElevationSampler
    {
        private readonly ElevationRaster r;

        public RasterElevationSampler(ElevationRaster raster)
        {
            if (raster == null)
                throw new ArgumentException("elevation raster is required for the Raster elevation source");
            if (raster.Cols < 2 || raster.Rows < 2)
                throw new ArgumentException("elevation raster needs at least 2 columns and 2 rows");
            if (raster.Heights == null || raster.Heights.Length != raster.Cols * raster.Rows)
                throw new ArgumentException($"elevation raster needs {raster.Cols * raster.Rows} heights (rows*cols)");
            if (!(raster.North > raster.South) || !(raster.East > raster.West))
                throw new ArgumentException("elevation raster bounds must have north > south and east > west");
            r = raster;
        }

        public bool Available => true;

        public double? Sample(double lat, double lng)
        {
            // fractional cell coordinates; row 0 = north, col 0 = west
            double fx = (lng - r.West) / (r.East - r.West) * (r.Cols - 1);
            double fy = (r.North - lat) / (r.North - r.South) * (r.Rows - 1);
            if (fx < -1e-9 || fy < -1e-9 || fx > r.Cols - 1 + 1e-9 || fy > r.Rows - 1 + 1e-9)
                return null;
            fx = Math.Max(0, Math.Min(r.Cols - 1, fx));
            fy = Math.Max(0, Math.Min(r.Rows - 1, fy));
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            int x1 = Math.Min(x0 + 1, r.Cols - 1), y1 = Math.Min(y0 + 1, r.Rows - 1);
            double tx = fx - x0, ty = fy - y0;

            var h00 = r.Heights[y0 * r.Cols + x0];
            var h10 = r.Heights[y0 * r.Cols + x1];
            var h01 = r.Heights[y1 * r.Cols + x0];
            var h11 = r.Heights[y1 * r.Cols + x1];
            if (h00 == null || h10 == null || h01 == null || h11 == null)
            {
                // fall back to the nearest known corner
                var nearest = new[] { (h00, tx + ty), (h10, 1 - tx + ty), (h01, tx + 1 - ty), (h11, 2 - tx - ty) }
                    .Where(c => c.Item1 != null).OrderBy(c => c.Item2).Select(c => c.Item1).FirstOrDefault();
                return nearest;
            }

            double top = h00.Value + (h10.Value - h00.Value) * tx;
            double bottom = h01.Value + (h11.Value - h01.Value) * tx;
            return top + (bottom - top) * ty;
        }
    }
}
