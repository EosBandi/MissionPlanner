using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;
using MPBackend.Core.Gps;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;

namespace MPBackend.Tests
{
    /// <summary>
    /// RTK and ADS-B against in-process TCP feeds (no vehicle needed).
    /// </summary>
    [TestClass]
    public class GpsServicesTests
    {
        [TestMethod]
        [Timeout(30_000)]
        public async Task RtkDecodesBasePositionAndCountsMessages()
        {
            var frame = Rtcm1005(-35.363261, 149.165230, 584.0);
            using var feed = new TcpFeed(_ => frame);
            var events = new EventHub();
            using var rtk = new RtkInjectService(new LinkManager(events, new JobManager(events)), events);

            rtk.Start(new RtkStartRequest { Type = RtkSourceType.Tcp, Host = "127.0.0.1", Port = feed.Port.ToString() });
            var status = await Until(() => rtk.GetStatus(), s => s.BasePosition != null && s.Messages.ContainsKey("Rtcm1005"));

            Assert.AreEqual(-35.363261, status.BasePosition.Lat, 1e-6);
            Assert.AreEqual(149.165230, status.BasePosition.Lng, 1e-6);
            Assert.AreEqual(584.0, status.BasePosition.Alt, 0.01);
            Assert.IsTrue(status.LastSeen.ContainsKey("base"));
            Assert.IsFalse(rtk.Stop().Running);
        }

        [TestMethod]
        public void RtkStatusNeverExposesNtripCredentials()
        {
            var events = new EventHub();
            using var rtk = new RtkInjectService(new LinkManager(events, new JobManager(events)), events);
            try
            {
                // nothing listens here; the open fails but the source description must already be sanitised
                rtk.Start(new RtkStartRequest { Type = RtkSourceType.Ntrip, Url = "ntrip://user:secret@127.0.0.1:1/MP" });
            }
            catch
            {
            }

            StringAssert.DoesNotMatch(rtk.GetStatus().Source ?? "", new System.Text.RegularExpressions.Regex("secret"));
        }

        [TestMethod]
        [Timeout(30_000)]
        public async Task AdsbReadsSbsFeed()
        {
            using var feed = new TcpFeed(_ => Encoding.ASCII.GetBytes(
                "MSG,1,1,1,7C6B2D,1,2026/10/08,22:30:00.000,2026/10/08,22:30:00.000,QFA123,,,,,,,,,,,\r\n" +
                "MSG,3,1,1,7C6B2D,1,2026/10/08,22:30:00.000,2026/10/08,22:30:00.000,,3000,,,-35.36,149.17,,,0,0,0,0\r\n"));
            var events = new EventHub();
            using var adsb = new AdsbService(new LinkManager(events, new JobManager(events)), events);
            adsb.Configure(new AdsbConfig { Enabled = true, Server = "127.0.0.1", Port = feed.Port, ForwardToVehicle = false });

            var plane = await Until(() => adsb.List().FirstOrDefault(a => a.Icao == "7C6B2D"),
                a => a != null && a.CallSign == "QFA123");
            Assert.AreEqual(-35.36, plane.Lat, 1e-6);
            Assert.AreEqual(914, plane.Alt, 1); // 3000 ft
            Assert.AreEqual("external", plane.Source);

            adsb.Configure(new AdsbConfig { Enabled = false });
        }

        private static async Task<T> Until<T>(Func<T> probe, Func<T, bool> done)
        {
            while (true)
            {
                var v = probe();
                if (done(v))
                    return v;
                await Task.Delay(100);
            }
        }

        /// <summary>RTCM3 1005 (stationary base, ECEF in 0.1 mm) framed with CRC-24Q.</summary>
        private static byte[] Rtcm1005(double lat, double lon, double h)
        {
            const double a = 6378137.0, f = 1 / 298.257223563;
            var e2 = f * (2 - f);
            double la = lat * Math.PI / 180, lo = lon * Math.PI / 180;
            var n = a / Math.Sqrt(1 - e2 * Math.Sin(la) * Math.Sin(la));
            var x = (long)Math.Round((n + h) * Math.Cos(la) * Math.Cos(lo) * 10000);
            var y = (long)Math.Round((n + h) * Math.Cos(la) * Math.Sin(lo) * 10000);
            var z = (long)Math.Round((n * (1 - e2) + h) * Math.Sin(la) * 10000);

            var bits = BigInteger.Zero;
            var count = 0;
            void Put(long v, int width)
            {
                bits = (bits << width) | (new BigInteger(v) & ((BigInteger.One << width) - 1));
                count += width;
            }

            Put(1005, 12); Put(42, 12); Put(0, 6); Put(1, 1); Put(1, 1); Put(1, 1); Put(0, 1);
            Put(x, 38); Put(0, 1); Put(0, 1); Put(y, 38); Put(0, 2); Put(z, 38);
            var payload = (bits << ((8 - count % 8) % 8)).ToByteArray(isUnsigned: true, isBigEndian: true);
            payload = new byte[(count + 7) / 8 - payload.Length].Concat(payload).ToArray();

            var body = new byte[] { 0xD3, (byte)((payload.Length >> 8) & 0x03), (byte)payload.Length }
                .Concat(payload).ToArray();
            uint crc = 0;
            foreach (var b in body)
            {
                crc ^= (uint)b << 16;
                for (var i = 0; i < 8; i++)
                {
                    crc <<= 1;
                    if ((crc & 0x1000000) != 0)
                        crc ^= 0x1864CFB;
                }
            }

            return body.Concat(new[] { (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc }).ToArray();
        }

        /// <summary>Loopback TCP server that sends a payload to each client once a second.</summary>
        private sealed class TcpFeed : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource cts = new CancellationTokenSource();

            public TcpFeed(Func<int, byte[]> payload)
            {
                listener.Start();
                _ = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync(cts.Token);
                        _ = Task.Run(async () =>
                        {
                            using var c = client;
                            var stream = c.GetStream();
                            for (var i = 0; !cts.IsCancellationRequested; i++)
                            {
                                await stream.WriteAsync(payload(i), cts.Token);
                                await Task.Delay(1000, cts.Token);
                            }
                        });
                    }
                });
            }

            public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

            public void Dispose()
            {
                cts.Cancel();
                listener.Stop();
            }
        }
    }
}
