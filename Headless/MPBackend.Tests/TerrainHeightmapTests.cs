using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;
using MPBackend.Core.Terrain;
using MPBackend.Server.Hosting;

namespace MPBackend.Tests
{
    /// <summary>
    /// Heightmap tiles for 3D map clients: the tile grid is Cesium's GeographicTilingScheme and the sample layout
    /// is row 0 north / column 0 west with inclusive edges. Heights themselves depend on SRTM downloads, so the
    /// endpoint test only asserts the shape of the answer.
    /// </summary>
    [TestClass]
    public class TerrainHeightmapTests
    {
        [TestMethod]
        public void TileRectangleFollowsTheGeographicScheme()
        {
            var west = TerrainService.TileRectangle(0, 0, 0);
            Assert.AreEqual((-180.0, -90.0, 0.0, 90.0), west);
            var east = TerrainService.TileRectangle(0, 1, 0);
            Assert.AreEqual((0.0, -90.0, 180.0, 90.0), east);

            // level 1: 4 x 2 tiles of 90 degrees
            Assert.AreEqual((-180.0, 0.0, -90.0, 90.0), TerrainService.TileRectangle(1, 0, 0));
            Assert.AreEqual((90.0, -90.0, 180.0, 0.0), TerrainService.TileRectangle(1, 3, 1));

            // a tile over Canberra at level 10 (width 180/1024 degrees)
            var t = TerrainService.TileRectangle(10, 1872, 713);
            Assert.AreEqual(180.0 / 1024, t.East - t.West, 1e-12);
            Assert.IsTrue(t.West <= 149.1652 && 149.1652 < t.East, "x covers the longitude");
            Assert.IsTrue(t.South <= -35.3632 && -35.3632 < t.North, "y covers the latitude");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TerrainService.TileRectangle(0, 2, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TerrainService.TileRectangle(0, 0, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TerrainService.TileRectangle(21, 0, 0));
        }

        [TestMethod]
        public void HeightmapPositionsRunNorthToSouthWestToEast()
        {
            var p = TerrainService.HeightmapPositions(1, 0, 0, 3).ToList();
            Assert.AreEqual(9, p.Count);
            Assert.AreEqual(new LatLng(90, -180), p[0], "row 0 / col 0 is the north-west corner");
            Assert.AreEqual(new LatLng(90, -135), p[1]);
            Assert.AreEqual(new LatLng(90, -90), p[2], "last column is the east edge");
            Assert.AreEqual(new LatLng(45, -180), p[3], "row 1 is half way south");
            Assert.AreEqual(new LatLng(0, -90), p[8], "last sample is the south-east corner");
        }

        [TestMethod]
        public async Task EndpointAnswersFloat32TilesAndRejectsBadTiles()
        {
            var backend = new Backend(new BackendOptions { DataDirectory = TestEnvironment.DataDirectory });
            var app = BackendWebApp.Create(backend,
                new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory },
                b => b.WebHost.UseUrls("http://127.0.0.1:0"));
            await app.StartAsync();
            try
            {
                var url = app.Urls.First() + "/api/v1/terrain/heightmap";
                using var http = new HttpClient();

                // wait=0: answer at once with whatever is cached (0 where nothing is), the shape is what matters
                var res = await http.GetAsync(url + "/10/1872/713?size=33&wait=0");
                Assert.AreEqual(HttpStatusCode.OK, res.StatusCode);
                Assert.AreEqual("application/octet-stream", res.Content.Headers.ContentType?.MediaType);
                var bytes = await res.Content.ReadAsByteArrayAsync();
                Assert.AreEqual(33 * 33 * sizeof(float), bytes.Length);
                var h = new float[33 * 33];
                Buffer.BlockCopy(bytes, 0, h, 0, bytes.Length);
                Assert.IsTrue(h.All(v => !float.IsNaN(v) && Math.Abs(v) < 10000), "plausible heights");

                // coarse tiles are flat and never wait for SRTM
                var coarse = await (await http.GetAsync(url + "/1/0/0?size=3&wait=60")).Content.ReadAsByteArrayAsync();
                Assert.AreEqual(9 * sizeof(float), coarse.Length);
                Assert.IsTrue(coarse.All(b => b == 0), "level 1 is flat");

                Assert.AreEqual(HttpStatusCode.BadRequest, (await http.GetAsync(url + "/0/2/0")).StatusCode, "x out of range");
                Assert.AreEqual(HttpStatusCode.BadRequest, (await http.GetAsync(url + "/0/0/0?size=1")).StatusCode, "size too small");
                Assert.AreEqual(HttpStatusCode.BadRequest, (await http.GetAsync(url + "/25/0/0")).StatusCode, "level too deep");
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
                backend.Dispose();
            }
        }
    }
}
