using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;
using MPBackend.Server.Hosting;

namespace MPBackend.Tests.Planning
{
    /// <summary>/api/v1/planning/spraygrid over the in-process host: JSON preview, file export, 400 on bad input.</summary>
    [TestClass]
    public class PlanningEndpointTests
    {
        private const string Request = @"{
            ""polygon"": [ {""lat"": -35.3632, ""lng"": 149.1652}, {""lat"": -35.36266, ""lng"": 149.1652},
                           {""lat"": -35.36266, ""lng"": 149.1663}, {""lat"": -35.3632, ""lng"": 149.1663} ],
            ""home"": { ""lat"": -35.3634, ""lng"": 149.165 },
            ""lineDistance"": 5, ""angle"": 0, ""litersPerHa"": 20, ""sprayBarWidth"": 4, ""flySpeed"": 5,
            ""altitude"": 3, ""gpsOffset"": 0.5, ""altitudeReference"": ""Relative"",
            ""splitBy"": ""Segments"", ""splitValue"": 2, ""elevationSource"": ""None"" }";

        [TestMethod]
        public async Task PreviewMissionAndFileExport()
        {
            var backend = new Backend(new BackendOptions { DataDirectory = TestEnvironment.DataDirectory });
            var app = BackendWebApp.Create(backend,
                new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory },
                b => b.WebHost.UseUrls("http://127.0.0.1:0"));
            await app.StartAsync();
            try
            {
                var url = app.Urls.First() + "/api/v1/planning/spraygrid";
                using var http = new HttpClient();

                var preview = await http.PostAsync(url + "/preview", new StringContent(Request, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, preview.StatusCode, await preview.Content.ReadAsStringAsync());
                using (var doc = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
                {
                    var root = doc.RootElement;
                    Assert.IsTrue(root.GetProperty("points").GetArrayLength() > 10);
                    Assert.AreEqual("S", root.GetProperty("points")[0].GetProperty("tag").GetString());
                    Assert.IsTrue(root.GetProperty("stats").GetProperty("strips").GetInt32() > 10);
                    Assert.IsTrue(root.GetProperty("stats").GetProperty("areaHa").GetDouble() > 0.5);
                }

                var mission = await http.PostAsync(url + "/mission", new StringContent(Request, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, mission.StatusCode, await mission.Content.ReadAsStringAsync());
                using (var doc = JsonDocument.Parse(await mission.Content.ReadAsStringAsync()))
                {
                    var root = doc.RootElement;
                    var items = root.GetProperty("mission").GetProperty("items");
                    Assert.AreEqual("DO_JUMP", items[0].GetProperty("commandName").GetString());
                    Assert.AreEqual("GLOBAL_RELATIVE_ALT", items[0].GetProperty("frame").GetString(), "enums as strings");
                    Assert.AreEqual(2, root.GetProperty("segmentStartSeq").GetArrayLength());
                    Assert.AreEqual(-35.3634, root.GetProperty("mission").GetProperty("home").GetProperty("lat").GetDouble(), 1e-9);
                }

                var file = await http.PostAsync(url + "/mission?format=waypoints", new StringContent(Request, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, file.StatusCode);
                var text = await file.Content.ReadAsStringAsync();
                StringAssert.StartsWith(text, "QGC WPL 110");
                Assert.IsTrue(text.Contains("\t217\t"), "sprayer script messages in the file");
                Assert.AreEqual("spraygrid.waypoints", file.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

                var plan = await http.PostAsync(url + "/mission?format=plan", new StringContent(Request, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, plan.StatusCode);
                StringAssert.Contains(await plan.Content.ReadAsStringAsync(), "\"fileType\"");

                var bad = await http.PostAsync(url + "/preview", new StringContent(@"{ ""polygon"": [] }", Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.BadRequest, bad.StatusCode);

                var badFormat = await http.PostAsync(url + "/mission?format=docx", new StringContent(Request, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.BadRequest, badFormat.StatusCode);
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
