using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using MPBackend.Core.Links;

namespace MPBackend.Core.Gps
{
    public enum RtkSourceType
    {
        /// <summary>NTRIP caster: Url = ntrip://user:pass@host:port/mountpoint</summary>
        Ntrip,
        /// <summary>GPS base receiver on a COM port (Port, Baud)</summary>
        Serial,
        /// <summary>TCP client to Host:Port</summary>
        Tcp,
        /// <summary>UDP listen on Port</summary>
        Udp,
        /// <summary>UDP client to Host:Port</summary>
        UdpClient
    }

    public sealed class RtkStartRequest
    {
        public RtkSourceType Type { get; set; }
        public string Url { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public int Baud { get; set; } = 115200;
        /// <summary>NTRIP rev1 instead of rev2.</summary>
        public bool NtripV1 { get; set; }
        /// <summary>Send GPS_RTCM_DATA (default) instead of the legacy GPS_INJECT_DATA.</summary>
        public bool RtcmMessage { get; set; } = true;
    }

    public sealed record SurveyInStatus(bool Valid, bool Active, uint Duration, uint Observations, double MeanAccuracy);

    public sealed record RtkStatus(
        bool Running,
        string Source,
        string Error,
        long BytesReceived,
        int BpsIn,
        int BpsSent,
        /// <summary>GPS_RTCM_DATA / GPS_INJECT_DATA frames handed to vehicles.</summary>
        long Injected,
        IReadOnlyDictionary<string, int> Messages,
        /// <summary>Seconds since the last message per group: gps, glonass, galileo, beidou, base.</summary>
        IReadOnlyDictionary<string, double> LastSeen,
        GeoPointValue BasePosition,
        SurveyInStatus SurveyIn);

    public sealed record GeoPointValue(double Lat, double Lng, double Alt);

    /// <summary>
    /// RTK/DGPS correction injection: port of ConfigSerialInjectGPS. Reads a correction source, splits it into
    /// RTCM3 / SBP (forwarded) and UBX / NMEA (parsed only) frames, and injects each frame into every connected
    /// vehicle via GPS_RTCM_DATA. Also extracts the base position (RTCM 1005/1006) and drives u-blox survey-in.
    /// </summary>
    public sealed class RtkInjectService : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(RtkInjectService));

        private readonly LinkManager links;
        private readonly EventHub events;
        private readonly object sync = new();

        private ICommsSerial port;
        private RtkStartRequest request;
        private CancellationTokenSource cts;
        private Task loop;
        private string error;

        // parsers (one instance each, like MP)
        private readonly rtcm3 rtcm = new rtcm3();
        private readonly sbp sbpParser = new sbp();
        private readonly Ubx ubx = new Ubx();
        private readonly nmea nmeaParser = new nmea();

        private readonly ConcurrentDictionary<string, int> messages = new();
        private readonly ConcurrentDictionary<string, DateTime> lastSeen = new();
        private long bytesReceived;
        private long injected;
        private int bpsIn, bpsSent, bpsInCounter, bpsSentCounter;
        private GeoPointValue basePosition;
        private SurveyInStatus surveyIn;

        public RtkInjectService(LinkManager links, EventHub events)
        {
            this.links = links;
            this.events = events;
        }

        public bool Running => loop != null && !loop.IsCompleted;

        public RtkStatus Start(RtkStartRequest req)
        {
            lock (sync)
            {
                StopInternal();
                Reset();
                request = req;
                error = null;
                port = Open(req);
                cts = new CancellationTokenSource();
                var token = cts.Token;
                loop = Task.Factory.StartNew(() => MainLoop(token), token, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                _ = Task.Run(() => StatsLoop(token));
            }

            return GetStatus();
        }

        public RtkStatus Stop()
        {
            lock (sync)
                StopInternal();
            var s = GetStatus();
            events.Publish(Topics.Rtk, s);
            return s;
        }

        public RtkStatus GetStatus()
        {
            var now = DateTime.UtcNow;
            return new RtkStatus(Running, Describe(request), error, Interlocked.Read(ref bytesReceived), bpsIn, bpsSent,
                Interlocked.Read(ref injected),
                new Dictionary<string, int>(messages),
                lastSeen.ToDictionary(kv => kv.Key, kv => Math.Round((now - kv.Value).TotalSeconds, 1)),
                basePosition, surveyIn);
        }

        /// <summary>
        /// Configure a u-blox M8P/F9P base (on a serial source) for survey-in: it averages its own position until
        /// both the duration (s) and accuracy (m) are met, then outputs RTCM 1005 + MSM.
        /// </summary>
        public void UbloxSurveyIn(int durationSeconds, double accuracyMetres, bool m8p130Plus = false)
        {
            var p = RequireSerialPort();
            surveyIn = null;
            basePosition = null;
            ubx.SetupBasePos(p, PointLatLngAlt.Zero, 0, 0, true);
            ubx.SetupM8P(p, m8p130Plus);
            ubx.SetupBasePos(p, PointLatLngAlt.Zero, durationSeconds, accuracyMetres, false);
        }

        /// <summary>Configure a u-blox base with a known (surveyed) position.</summary>
        public void UbloxFixedBase(double lat, double lng, double alt, bool m8p130Plus = false)
        {
            var p = RequireSerialPort();
            ubx.SetupBasePos(p, PointLatLngAlt.Zero, 0, 0, true);
            ubx.SetupM8P(p, m8p130Plus);
            ubx.SetupBasePos(p, new PointLatLngAlt(lat, lng, alt), 0, 0, false);
        }

        private ICommsSerial RequireSerialPort()
        {
            var p = port;
            if (p == null || !Running || request?.Type != RtkSourceType.Serial)
                throw new InvalidOperationException("u-blox setup needs a running serial RTK source");
            return p;
        }

        private ICommsSerial Open(RtkStartRequest req)
        {
            ICommsSerial p;
            switch (req.Type)
            {
                case RtkSourceType.Ntrip:
                    var ntrip = new CommsNTRIP { ntrip_v1 = req.NtripV1 };
                    // position for the GGA sentence some casters (VRS) require
                    var pos = VehiclePosition();
                    if (pos != null)
                    {
                        ntrip.lat = pos.Lat;
                        ntrip.lng = pos.Lng;
                        ntrip.alt = pos.Alt;
                    }

                    ntrip.Open(req.Url ?? throw new ArgumentException("url is required for NTRIP"));
                    return ntrip;
                case RtkSourceType.Serial:
                    p = new SerialPort { PortName = req.Port, BaudRate = req.Baud };
                    break;
                case RtkSourceType.Tcp:
                    p = new TcpSerial { Host = req.Host, Port = req.Port };
                    break;
                case RtkSourceType.Udp:
                    // UdpSerial.Open waits for the first datagram; do that on the reader thread
                    return new UdpSerial { Port = req.Port };
                case RtkSourceType.UdpClient:
                    var cl = new UdpSerialConnect();
                    cl.Open(req.Host, req.Port);
                    return cl;
                default:
                    throw new ArgumentOutOfRangeException(nameof(req.Type));
            }

            p.Open();
            return p;
        }

        private void MainLoop(CancellationToken token)
        {
            var lastRecv = DateTime.UtcNow;
            bool isRtcm = false, isSbp = false;
            var buffer = new byte[request.RtcmMessage ? 180 : 110];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (port is UdpSerial udp && !udp.IsOpen)
                        udp.Open();

                    // reconnect after 10 s without data (not for NTRIP/UDP, which manage themselves)
                    if ((DateTime.UtcNow - lastRecv).TotalSeconds > 10 || !port.IsOpen)
                    {
                        if (!(port is CommsNTRIP || port is UdpSerialConnect || port is UdpSerial))
                        {
                            log.Warn("RTK source reconnecting");
                            try
                            {
                                port.Close();
                                port.Open();
                            }
                            catch (Exception ex)
                            {
                                error = "reconnect failed: " + ex.Message;
                                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                            }
                        }

                        lastRecv = DateTime.UtcNow;
                    }

                    while (port.BytesToRead > 0 && !token.IsCancellationRequested)
                    {
                        var read = port.Read(buffer, 0, Math.Min(buffer.Length, port.BytesToRead));
                        if (read <= 0)
                            break;
                        lastRecv = DateTime.UtcNow;
                        error = null;
                        Interlocked.Add(ref bytesReceived, read);
                        Interlocked.Add(ref bpsInCounter, read);

                        // unknown framing: forward raw (MP does the same)
                        if (!(isRtcm || isSbp))
                            Send(buffer, read);

                        for (var a = 0; a < read; a++)
                        {
                            int seen;
                            try
                            {
                                seen = rtcm.Read(buffer[a]);
                            }
                            catch (Exception ex)
                            {
                                // MP's rtcm3 decodes MSM content after the CRC check; a frame it cannot decode
                                // must not take the rest of the buffer with it
                                log.Debug("RTCM decode", ex);
                                rtcm.resetParser();
                                Count("RtcmDecodeError");
                                seen = -1;
                            }

                            if (seen > 0)
                            {
                                sbpParser.resetParser();
                                ubx.resetParser();
                                nmeaParser.resetParser();
                                isRtcm = true;
                                Send(rtcm.packet, rtcm.length);
                                Count("Rtcm" + seen);
                                SeenRtcm(seen);
                            }

                            if ((seen = sbpParser.read(buffer[a])) > 0)
                            {
                                rtcm.resetParser();
                                ubx.resetParser();
                                nmeaParser.resetParser();
                                isSbp = true;
                                Send(sbpParser.packet, sbpParser.length);
                                Count("Sbp" + seen.ToString("X4"));
                            }

                            if ((seen = ubx.Read(buffer[a])) > 0)
                            {
                                rtcm.resetParser();
                                sbpParser.resetParser();
                                nmeaParser.resetParser();
                                ProcessUbx();
                                Count("Ubx" + seen.ToString("X4"));
                            }

                            if ((seen = nmeaParser.Read(buffer[a])) > 0)
                            {
                                rtcm.resetParser();
                                sbpParser.resetParser();
                                ubx.resetParser();
                                Count("NMEA");
                            }
                        }
                    }

                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    log.Error("RTK loop", ex);
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }
            }
        }

        /// <summary>
        /// Inject into every vehicle on every link. GPS_RTCM_DATA has no target, so once per link is enough when
        /// all vehicles on it use the same MAVLink version (MP's GetCanJustSendOnce).
        /// </summary>
        private void Send(byte[] data, int length)
        {
            Interlocked.Add(ref bpsSentCounter, length);
            foreach (var link in links.Links.Where(l => l.State == LinkState.Connected))
            {
                var vehicles = link.Vehicles;
                var once = request.RtcmMessage && vehicles.Select(v => v.mavlinkv2).Distinct().Count() <= 1;
                foreach (var mav in vehicles)
                {
                    try
                    {
                        link.Mav.InjectGpsData(mav.sysid, mav.compid, data, (ushort)length, request.RtcmMessage);
                        Interlocked.Increment(ref injected);
                    }
                    catch (Exception ex)
                    {
                        log.Debug(ex);
                    }

                    if (once)
                        break;
                }
            }
        }

        private void SeenRtcm(int msg)
        {
            string group = msg switch
            {
                >= 1001 and <= 1004 or >= 1071 and <= 1077 => "gps",
                1005 or 1006 or 4072 => "base",
                >= 1009 and <= 1012 or >= 1081 and <= 1087 => "glonass",
                >= 1091 and <= 1097 => "galileo",
                >= 1121 and <= 1127 => "beidou",
                _ => null
            };
            if (group != null)
                lastSeen[group] = DateTime.UtcNow;

            if (msg == 1005 || msg == 1006)
            {
                try
                {
                    double[] ecef;
                    if (msg == 1005)
                    {
                        var b = new rtcm3.type1005();
                        b.Read(rtcm.packet);
                        ecef = b.ecefposition;
                    }
                    else
                    {
                        var b = new rtcm3.type1006();
                        b.Read(rtcm.packet);
                        ecef = b.ecefposition;
                    }

                    var llh = new double[3];
                    rtcm3.ecef2pos(ecef, ref llh);
                    basePosition = new GeoPointValue(llh[0] * rtcm3.R2D, llh[1] * rtcm3.R2D, llh[2]);
                    foreach (var mav in links.Links.SelectMany(l => l.Vehicles))
                        mav.cs.Base = new PointLatLngAlt(basePosition.Lat, basePosition.Lng, basePosition.Alt);
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }
        }

        private void ProcessUbx()
        {
            // NAV-SVIN: survey-in progress
            if (ubx.@class == 0x01 && ubx.subclass == 0x3b)
            {
                var svin = ubx.packet.ByteArrayToStructure<Ubx.ubx_nav_svin>(6);
                surveyIn = new SurveyInStatus(svin.valid == 1, svin.active == 1, svin.dur, svin.obs,
                    svin.meanAcc / 10000.0);
            }
        }

        private async Task StatsLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                bpsIn = Interlocked.Exchange(ref bpsInCounter, 0);
                bpsSent = Interlocked.Exchange(ref bpsSentCounter, 0);

                // keep the NTRIP GGA position current
                if (port is CommsNTRIP ntrip && VehiclePosition() is { } pos)
                {
                    ntrip.lat = pos.Lat;
                    ntrip.lng = pos.Lng;
                    ntrip.alt = pos.Alt;
                }

                events.Publish(Topics.Rtk, GetStatus());
            }
        }

        private PointLatLngAlt VehiclePosition()
        {
            var cs = links.Links.Where(l => l.State == LinkState.Connected)
                .SelectMany(l => l.Vehicles).Select(v => v.cs)
                .FirstOrDefault(c => c.lat != 0 || c.lng != 0);
            return cs == null ? null : new PointLatLngAlt(cs.lat, cs.lng, cs.altasl);
        }

        private void Count(string name) => messages.AddOrUpdate(name, 1, (_, n) => n + 1);

        private void Reset()
        {
            messages.Clear();
            lastSeen.Clear();
            bytesReceived = 0;
            injected = 0;
            bpsIn = bpsSent = bpsInCounter = bpsSentCounter = 0;
            basePosition = null;
            surveyIn = null;
            rtcm.resetParser();
            sbpParser.resetParser();
            ubx.resetParser();
            nmeaParser.resetParser();
        }

        private void StopInternal()
        {
            cts?.Cancel();
            try
            {
                if (port is UdpSerial udp)
                    udp.CancelConnect = true;
                port?.Close();
            }
            catch
            {
            }

            try
            {
                loop?.Wait(TimeSpan.FromSeconds(3));
            }
            catch
            {
            }

            port = null;
            loop = null;
        }

        private static string Describe(RtkStartRequest r)
        {
            if (r == null)
                return null;
            return r.Type switch
            {
                // never echo NTRIP credentials
                RtkSourceType.Ntrip => "ntrip:" + StripCredentials(r.Url),
                RtkSourceType.Serial => $"serial:{r.Port}:{r.Baud}",
                RtkSourceType.Udp => $"udp:{r.Port}",
                _ => $"{r.Type.ToString().ToLowerInvariant()}:{r.Host}:{r.Port}"
            };
        }

        private static string StripCredentials(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            var at = url.LastIndexOf('@');
            var scheme = url.IndexOf("://", StringComparison.Ordinal);
            return at > 0 && scheme > 0 ? url.Substring(0, scheme + 3) + url.Substring(at + 1) : url;
        }

        public void Dispose()
        {
            lock (sync)
                StopInternal();
        }
    }
}
