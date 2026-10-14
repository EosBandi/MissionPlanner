using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using MPBackend.Core.Jobs;

namespace MPBackend.Core.Links
{
    public sealed record LinkEvent(string Event, LinkInfo Link);

    public sealed record VehicleEvent(string Event, int LinkId, uint SysId, byte CompId);

    /// <summary>
    /// Owns all MAVLink links. Port of the connection lifecycle in MainV2.doConnect / doDisconnect.
    /// </summary>
    public sealed class LinkManager : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(LinkManager));

        /// <summary>Serialises transport opens that read their endpoint from the shared CommsBase settings.</summary>
        private static readonly object transportOpenLock = new object();

        private readonly ConcurrentDictionary<int, Link> links = new ConcurrentDictionary<int, Link>();
        private readonly EventHub events;
        private readonly JobManager jobs;
        private int nextId;

        public LinkManager(EventHub events, JobManager jobs)
        {
            this.events = events;
            this.jobs = jobs;
        }

        /// <summary>
        /// Raised for every packet received on any link (reader thread; keep handlers cheap).
        /// </summary>
        public event Action<Link, MAVLink.MAVLinkMessage> PacketReceived;

        /// <summary>
        /// Raised once a link has completed the heartbeat/version handshake and its reader is running.
        /// </summary>
        public event Action<Link> Connected;

        public IReadOnlyList<Link> Links => links.Values.OrderBy(l => l.Id).ToList();

        public Link Get(int id) => links.TryGetValue(id, out var link) ? link : null;

        public static IReadOnlyList<SerialPortInfo> GetSerialPorts() =>
            SerialPort.GetPortNames().Select(p => new SerialPortInfo(p, SafeNiceName(p))).ToList();

        /// <summary>
        /// Start connecting. Returns the new link (state Connecting) and the job tracking the connect.
        /// </summary>
        public (Link link, Job job) Connect(ConnectionRequest request)
        {
            var link = new Link(Interlocked.Increment(ref nextId), request);
            links[link.Id] = link;
            WireEvents(link);
            PublishLink("added", link);

            var job = jobs.Start("connect", $"Connect {request}", ctx => ConnectAsync(link, ctx), link.Id);
            return (link, job);
        }

        public async Task<bool> DisconnectAsync(int id)
        {
            if (!links.TryRemove(id, out var link))
                return false;

            await link.StopPumpAsync().ConfigureAwait(false);
            link.Dispose();
            link.State = LinkState.Disconnected;
            PublishLink("removed", link);
            return true;
        }

        /// <summary>
        /// Find the link carrying a vehicle. Prefers connected links, then the lowest link id.
        /// </summary>
        public (Link link, MAVState mav) FindVehicle(uint sysid, byte compid, int? linkId = null)
        {
            foreach (var link in Links.Where(l => linkId == null || l.Id == linkId)
                         .OrderBy(l => l.State == LinkState.Connected ? 0 : 1))
            {
                var mav = link.Vehicles.FirstOrDefault(m => m.sysid == sysid && m.compid == compid);
                if (mav != null)
                    return (link, mav);
            }

            return (null, null);
        }

        private async Task<object> ConnectAsync(Link link, JobContext ctx)
        {
            var mav = link.Mav;
            var request = link.Request;

            try
            {
                using var cancelReg = ctx.Token.Register(() =>
                {
                    if (mav.BaseStream is UdpSerial udp)
                        udp.CancelConnect = true;
                    try
                    {
                        mav.Close();
                    }
                    catch
                    {
                    }
                });

                ctx.Report(-1, "Opening " + request);
                mav.BaseStream = CreateTransport(request);
                // network transports are opened here because their own Open() would prompt for host/port.
                // Serial ports never prompt and must be opened by MP: OpenBg sets ReadBufferSize first, which a
                // Windows serial port rejects once it is open.
                if (request.Type != LinkType.Serial)
                    await Task.Run(() => OpenTransport(mav.BaseStream, request), ctx.Token).ConfigureAwait(false);
                ctx.Token.ThrowIfCancellationRequested();

                if (HeadlessHost.Options.RecordTlogs)
                    CreateTlogs(link);

                ctx.Report(-1, "Waiting for heartbeat");
                // skipconnectedcheck=true stops Open returning early when we pre-opened the transport (OpenBg's own
                // BaseStream.Open() is then a no-op); for serial, OpenBg opens the port itself. Either way it then
                // runs the heartbeat wait, version and stream request sequence without any prompts
                await Task.Run(() => mav.Open(false, true, false), ctx.Token)
                    .ConfigureAwait(false);
                ctx.Token.ThrowIfCancellationRequested();

                if (!mav.BaseStream.IsOpen)
                    throw new IOException("No heartbeat received");

                link.ConnectedAt = DateTime.UtcNow;
                link.State = LinkState.Connected;
                link.StartPump();
                ctx.Report(100, $"Connected to {mav.MAV.sysid}/{mav.MAV.compid} {mav.MAV.aptype}");
                PublishLink("connected", link);
                log.Info($"link {link.Id} connected to {mav.MAV.sysid}/{mav.MAV.compid} {mav.MAV.aptype}");

                Connected?.Invoke(link);

                return link.ToInfo();
            }
            catch (Exception ex)
            {
                link.State = ctx.Token.IsCancellationRequested ? LinkState.Disconnected : LinkState.Failed;
                link.Error = ex.Message;
                try
                {
                    mav.Close();
                }
                catch
                {
                }

                PublishLink("failed", link);
                throw;
            }
        }

        private static ICommsSerial CreateTransport(ConnectionRequest request)
        {
            switch (request.Type)
            {
                case LinkType.Serial:
                    return new SerialPort { PortName = request.Port, BaudRate = request.Baud };
                case LinkType.Tcp:
                    // a non-empty Host skips TcpSerial's settings/prompt lookup
                    return new TcpSerial { Host = request.Host, Port = request.Port };
                case LinkType.Udp:
                    return new UdpSerial { Port = request.Port };
                case LinkType.UdpClient:
                    return new UdpSerialConnect();
                case LinkType.WebSocket:
                    return new MissionPlanner.Comms.WebSocket();
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Type));
            }
        }

        private static void OpenTransport(ICommsSerial stream, ConnectionRequest request)
        {
            switch (stream)
            {
                case UdpSerialConnect udpcl:
                    udpcl.Open(request.Host, request.Port);
                    break;
                case MissionPlanner.Comms.WebSocket ws:
                    // WebSocket.Open() reads its URL from the CommsBase settings hook
                    lock (transportOpenLock)
                    {
                        HeadlessHost.CommsOverrides["WS_url"] = request.Url;
                        try
                        {
                            ws.Open();
                        }
                        finally
                        {
                            HeadlessHost.CommsOverrides.TryRemove("WS_url", out _);
                        }
                    }

                    break;
                default:
                    // UdpSerial blocks here until the first datagram arrives (cancel via CancelConnect)
                    stream.Open();
                    break;
            }

            if (!stream.IsOpen)
                throw new IOException($"Failed to open {request}");
        }

        private static void CreateTlogs(Link link)
        {
            try
            {
                var dir = Settings.Instance.LogDir;
                Directory.CreateDirectory(dir);
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                var tlog = Path.Combine(dir, $"{stamp}-link{link.Id}.tlog");
                var rlog = Path.ChangeExtension(tlog, ".rlog");
                for (var n = 1; File.Exists(tlog); n++)
                {
                    tlog = Path.Combine(dir, $"{stamp}-link{link.Id}-{n}.tlog");
                    rlog = Path.ChangeExtension(tlog, ".rlog");
                }

                link.Mav.logfile = new BufferedStream(File.Open(tlog, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.Read));
                link.Mav.rawlogfile = new BufferedStream(File.Open(rlog, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.Read));
                link.TlogPath = tlog;
            }
            catch (Exception ex)
            {
                // soft fail like MainV2
                log.Error("failed to create tlog", ex);
            }
        }

        private void WireEvents(Link link)
        {
            link.Mav.MAVDetected += (sender, id) =>
                events.Publish(Topics.Vehicles, new VehicleEvent("detected", link.Id, id.Item1, id.Item2));

            link.Mav.OnPacketReceived += (sender, message) =>
            {
                if (message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.STATUSTEXT)
                    events.Publish(Topics.Status, Link.ParseStatusText(link.Id, message));

                PacketReceived?.Invoke(link, message);
            };

            link.PumpStopped += l =>
            {
                l.State = LinkState.Disconnected;
                l.Error = "Connection lost";
                PublishLink("lost", l);
            };
        }

        private void PublishLink(string evt, Link link) =>
            events.Publish(Topics.Links, new LinkEvent(evt, link.ToInfo()));

        private static string SafeNiceName(string port)
        {
            try
            {
                return SerialPort.GetNiceName(port);
            }
            catch
            {
                return port;
            }
        }

        public void Dispose()
        {
            foreach (var id in links.Keys.ToList())
                DisconnectAsync(id).Wait(TimeSpan.FromSeconds(3));
        }
    }

    public sealed record SerialPortInfo(string Name, string Description);
}
