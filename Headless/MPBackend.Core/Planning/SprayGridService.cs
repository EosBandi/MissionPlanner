using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MPBackend.Core.Terrain;

namespace MPBackend.Core.Planning
{
    /// <summary>
    /// Spray-grid mission planning (the SprayGrid-Soleon plugin as a service). Preview is synchronous and needs no
    /// terrain; building the mission may wait for the backend's SRTM tiles.
    /// </summary>
    public sealed class SprayGridService
    {
        public SprayGridPreview Preview(SprayGridRequest request) => SprayPlanner.Preview(request);

        public async Task<SprayMissionResult> BuildMissionAsync(SprayGridRequest request, CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentException("request body is required");

            IElevationSampler sampler;
            bool tilesReady = true;
            switch (request.ElevationSource)
            {
                case SprayElevationSource.Backend:
                    tilesReady = await SrtmElevationSampler.WarmUpAsync(TilePoints(request),
                        TimeSpan.FromSeconds(Math.Max(0, request.TerrainWait)), ct).ConfigureAwait(false);
                    sampler = new SrtmElevationSampler();
                    break;
                case SprayElevationSource.Raster:
                    sampler = new RasterElevationSampler(request.Elevation);
                    break;
                default:
                    sampler = NoElevationSampler.Instance;
                    break;
            }

            var result = await Task.Run(() => SprayPlanner.BuildMission(request, sampler), ct).ConfigureAwait(false);
            if (!tilesReady)
                result.Warnings.Insert(0, "terrain tiles were still downloading; retry for complete terrain data");
            return result;
        }

        /// <summary>Polygon vertices, obstacle vertices and home: one srtm tile request per 1° cell touched.</summary>
        private static IEnumerable<LatLng> TilePoints(SprayGridRequest r)
        {
            var pts = new List<LatLng>();
            if (r.Polygon != null)
                pts.AddRange(r.Polygon.Where(p => p != null));
            if (r.Obstacles != null)
                pts.AddRange(r.Obstacles.Where(o => o != null).SelectMany(o => o).Where(p => p != null));
            if (r.Home != null)
                pts.Add(new LatLng(r.Home.Lat, r.Home.Lng));
            // one representative per 1° tile is enough
            return pts.GroupBy(p => (Math.Floor(p.Lat), Math.Floor(p.Lng))).Select(g => g.First());
        }
    }
}
