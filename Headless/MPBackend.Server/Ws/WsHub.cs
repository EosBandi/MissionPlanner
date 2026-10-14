using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Server.Ws;

/// <summary>
/// Client → server message. <c>op</c> is subscribe | unsubscribe | ping | manual_control | manual_control_stop.
/// </summary>
public sealed class WsRequest
{
    public string Op { get; set; } = "";
    public string Topic { get; set; } = "";
    public uint? Sysid { get; set; }
    public byte? Compid { get; set; }
    /// <summary>telemetry: CurrentState field names (all when empty).</summary>
    public string[]? Fields { get; set; }
    /// <summary>telemetry: updates per second (1-50, default 10).</summary>
    public double? Rate { get; set; }
    /// <summary>mavlink: message names (e.g. ATTITUDE) or ids; all when empty.</summary>
    public string[]? Msgs { get; set; }
    /// <summary>manual_control: RC PWM per channel (null = untouched, 0 = release).</summary>
    public int?[]? Channels { get; set; }
    /// <summary>manual_control: channel number → -1..1 using the vehicle's RC calibration.</summary>
    public Dictionary<int, double>? Normalized { get; set; }
    /// <summary>manual_control: MANUAL_CONTROL axes instead of RC overrides.</summary>
    public MPBackend.Core.Control.ManualAxes? Manual { get; set; }
}

/// <summary>
/// WebSocket fan-out of backend events, per-client telemetry streams and raw MAVLink.
///
/// Server → client envelopes are <c>{"topic": ..., "data": ...}</c> plus topic specific keys:
/// telemetry adds sysid/compid/full (data holds only changed fields unless full=true),
/// mavlink adds linkId/sysid/compid/msg.
/// </summary>
public sealed class WsHub
{
    public const string Telemetry = "telemetry";
    public const string Mavlink = "mavlink";

    public static readonly IReadOnlyList<string> Topics = new[]
        { Core.Topics.Links, Core.Topics.Vehicles, Core.Topics.Status, Core.Topics.Jobs, Core.Topics.Params, Core.Topics.Missions, Core.Topics.Calibration, Core.Topics.Rtk, Core.Topics.Adsb, Telemetry, Mavlink };

    private readonly Backend backend;
    private readonly ILogger<WsHub> logger;
    private readonly ConcurrentDictionary<Guid, WsClient> clients = new();

    public WsHub(Backend backend, ILogger<WsHub> logger)
    {
        this.backend = backend;
        this.logger = logger;
        backend.Events.Published += OnEvent;
        backend.Links.PacketReceived += OnPacket;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        var client = new WsClient(socket, backend, logger);
        clients[client.Id] = client;
        try
        {
            client.Send(new { topic = "welcome", data = new { topics = Topics } });
            await client.RunAsync(ct);
        }
        finally
        {
            clients.TryRemove(client.Id, out _);
            client.Dispose();
        }
    }

    private void OnEvent(string topic, object data)
    {
        byte[]? payload = null;
        foreach (var client in clients.Values)
        {
            if (!client.IsSubscribed(topic))
                continue;
            payload ??= Serialize(new { topic, data });
            client.Enqueue(payload);
        }
    }

    private void OnPacket(Link link, MAVLink.MAVLinkMessage message)
    {
        byte[]? payload = null;
        foreach (var client in clients.Values)
        {
            if (!client.WantsPacket(message))
                continue;
            payload ??= Serialize(new
            {
                topic = Mavlink,
                linkId = link.Id,
                sysid = message.sysid,
                compid = message.compid,
                msg = message.msgtypename,
                data = message.data
            });
            client.Enqueue(payload);
        }
    }

    internal static byte[] Serialize(object value) => JsonSerializer.SerializeToUtf8Bytes(value, Json.Options);

    private sealed class WsClient : IDisposable
    {
        private static readonly TimeSpan FullResync = TimeSpan.FromSeconds(5);

        private readonly WebSocket socket;
        private readonly Backend backend;
        private readonly ILogger logger;
        private readonly CancellationTokenSource cts = new();
        // drop the oldest frames if the client cannot keep up rather than growing without bound
        private readonly Channel<byte[]> outbound = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        private readonly ConcurrentDictionary<string, byte> topics = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> telemetry = new();
        private volatile MavlinkFilter? mavlinkFilter;

        public WsClient(WebSocket socket, Backend backend, ILogger logger)
        {
            this.socket = socket;
            this.backend = backend;
            this.logger = logger;
        }

        public Guid Id { get; } = Guid.NewGuid();

        public bool IsSubscribed(string topic) => topics.ContainsKey(topic);

        public bool WantsPacket(MAVLink.MAVLinkMessage msg) => mavlinkFilter?.Matches(msg) == true;

        public void Enqueue(byte[] payload) => outbound.Writer.TryWrite(payload);

        public void Send(object value) => Enqueue(Serialize(value));

        public async Task RunAsync(CancellationToken requestAborted)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, cts.Token);
            var sender = SendLoopAsync(linked.Token);
            try
            {
                await ReceiveLoopAsync(linked.Token);
            }
            finally
            {
                cts.Cancel();
                outbound.Writer.TryComplete();
                await sender.ContinueWith(_ => { });
            }
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var payload in outbound.Reader.ReadAllAsync(ct))
                    await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
            {
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var message = new MemoryStream();
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, ct);
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                try
                {
                    Handle(JsonSerializer.Deserialize<WsRequest>(text, Json.Options)!);
                }
                catch (Exception ex)
                {
                    Send(new { topic = "error", data = ex.Message });
                }
            }
        }

        private void Handle(WsRequest req)
        {
            switch (req.Op?.ToLowerInvariant())
            {
                case "ping":
                    Send(new { topic = "pong", data = DateTime.UtcNow });
                    return;
                case "subscribe":
                    Subscribe(req);
                    return;
                case "unsubscribe":
                    Unsubscribe(req);
                    return;
                case "manual_control":
                    // high rate input: no reply unless it fails
                    backend.ManualControl.Update(RequireId(req.Sysid), RequireId(req.Compid),
                        new MPBackend.Core.Control.ManualControlInput
                            { Channels = req.Channels, Normalized = req.Normalized, Manual = req.Manual });
                    return;
                case "manual_control_stop":
                    backend.ManualControl.Stop(RequireId(req.Sysid), RequireId(req.Compid));
                    return;
                default:
                    throw new ArgumentException($"unknown op '{req.Op}'");
            }
        }

        private static T RequireId<T>(T? id) where T : struct =>
            id ?? throw new ArgumentException("sysid and compid are required");

        private void Subscribe(WsRequest req)
        {
            switch (req.Topic)
            {
                case Telemetry:
                    if (req.Sysid == null || req.Compid == null)
                        throw new ArgumentException("telemetry needs sysid and compid");
                    StartTelemetry(req.Sysid.Value, req.Compid.Value, req.Fields,
                        Math.Clamp(req.Rate ?? 10, 1, 50));
                    break;
                case Mavlink:
                    mavlinkFilter = new MavlinkFilter(req.Sysid, req.Compid, req.Msgs);
                    break;
                default:
                    if (!WsHub.Topics.Contains(req.Topic))
                        throw new ArgumentException($"unknown topic '{req.Topic}'");
                    topics[req.Topic] = 0;
                    break;
            }

            Send(new { topic = "subscribed", data = new { req.Topic, req.Sysid, req.Compid } });
        }

        private void Unsubscribe(WsRequest req)
        {
            switch (req.Topic)
            {
                case Telemetry:
                    foreach (var key in telemetry.Keys.Where(k =>
                                 req.Sysid == null || k == TelemetryKey(req.Sysid.Value, req.Compid ?? 0)))
                        if (telemetry.TryRemove(key, out var t))
                            t.Cancel();
                    break;
                case Mavlink:
                    mavlinkFilter = null;
                    break;
                default:
                    topics.TryRemove(req.Topic, out _);
                    break;
            }
        }

        private static string TelemetryKey(uint sysid, byte compid) => $"{sysid}/{compid}";

        /// <summary>
        /// Stream CurrentState at the requested rate. Only changed fields are sent, with a full snapshot
        /// first and every 5 s so a client that dropped frames resynchronises.
        /// </summary>
        private void StartTelemetry(uint sysid, byte compid, string[]? fields, double rate)
        {
            var key = TelemetryKey(sysid, compid);
            var tcs = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            if (telemetry.TryRemove(key, out var previous))
                previous.Cancel();
            telemetry[key] = tcs;

            _ = Task.Run(async () =>
            {
                var last = new Dictionary<string, object?>();
                var lastFull = DateTime.MinValue;
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1 / rate));
                try
                {
                    while (await timer.WaitForNextTickAsync(tcs.Token))
                    {
                        Dictionary<string, object> snapshot;
                        try
                        {
                            snapshot = backend.Vehicles.GetState(sysid, compid, fields);
                        }
                        catch (VehicleNotFoundException)
                        {
                            continue;
                        }

                        var full = DateTime.UtcNow - lastFull > FullResync;
                        var changed = full
                            ? snapshot
                            : snapshot.Where(kv => !last.TryGetValue(kv.Key, out var old) || !Equals(old, kv.Value))
                                .ToDictionary(kv => kv.Key, kv => kv.Value);
                        if (full)
                            lastFull = DateTime.UtcNow;
                        foreach (var kv in changed)
                            last[kv.Key] = kv.Value;

                        if (changed.Count > 0)
                            Send(new { topic = Telemetry, sysid, compid, full, data = changed });
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "telemetry stream {Key} failed", key);
                }
            });
        }

        public void Dispose()
        {
            cts.Cancel();
            foreach (var t in telemetry.Values)
                t.Cancel();
        }
    }

    private sealed class MavlinkFilter
    {
        private readonly uint? sysid;
        private readonly byte? compid;
        private readonly HashSet<uint>? ids;

        public MavlinkFilter(uint? sysid, byte? compid, string[]? msgs)
        {
            this.sysid = sysid;
            this.compid = compid;
            if (msgs is { Length: > 0 })
            {
                ids = new HashSet<uint>();
                foreach (var m in msgs)
                {
                    if (uint.TryParse(m, out var id))
                        ids.Add(id);
                    else if (Enum.TryParse<MAVLink.MAVLINK_MSG_ID>(m, true, out var named))
                        ids.Add((uint)named);
                    else
                        throw new ArgumentException($"unknown MAVLink message '{m}'");
                }
            }
        }

        public bool Matches(MAVLink.MAVLinkMessage msg) =>
            (sysid == null || msg.sysid == sysid) &&
            (compid == null || msg.compid == compid) &&
            (ids == null || ids.Contains(msg.msgid));
    }
}
