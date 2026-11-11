using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;
using MPBackend.Server.Hosting;

namespace MPBackend.Tests
{
    /// <summary>
    /// The REST host built by <see cref="BackendWebApp"/> (shared by the console server and the tray app) comes
    /// up on an ephemeral port and answers the API.
    /// </summary>
    [TestClass]
    public class WebHostTests
    {
        [TestMethod]
        public async Task HostServesInfoAndLinks()
        {
            var backend = new Backend(new BackendOptions { DataDirectory = TestEnvironment.DataDirectory });
            var app = BackendWebApp.Create(backend,
                new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory },
                b => b.WebHost.UseUrls("http://127.0.0.1:0"));
            await app.StartAsync();
            try
            {
                var url = app.Urls.First();
                Assert.IsFalse(url.EndsWith(":0"), "Kestrel should report the bound port, got " + url);

                using var http = new HttpClient();
                var info = await http.GetAsync(url + "/api/v1/info");
                Assert.AreEqual(HttpStatusCode.OK, info.StatusCode);
                var body = await info.Content.ReadAsStringAsync();
                StringAssert.Contains(body, "dataDirectory");

                var links = await http.GetStringAsync(url + "/api/v1/links");
                Assert.AreEqual("[]", links);

                var missing = await http.GetAsync(url + "/api/v1/vehicles/1/1");
                Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode, "unknown vehicle maps to 404");
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
