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
