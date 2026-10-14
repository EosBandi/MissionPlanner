using MPBackend.Core;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.UI.Services;

/// <summary>
/// <see cref="IGcs"/> over an in-process <see cref="Backend"/>: no serialisation, events straight from the EventHub.
/// </summary>
public sealed class InProcessGcs : IGcs, IDisposable
{
    private readonly Backend backend;

    public InProcessGcs(Backend backend)
    {
        this.backend = backend;
        backend.Events.Published += OnPublished;
    }

    public event Action<string, object>? Published;

    private void OnPublished(string topic, object data) => Published?.Invoke(topic, data);

    public Task<IReadOnlyList<LinkInfo>> GetLinksAsync() =>
        Task.FromResult<IReadOnlyList<LinkInfo>>(backend.Links.Links.Select(l => l.ToInfo()).ToList());

    public Task<LinkInfo> ConnectAsync(string connection)
    {
        var (link, _) = backend.Links.Connect(ConnectionRequest.Parse(connection));
        return Task.FromResult(link.ToInfo());
    }

    public Task DisconnectAsync(int linkId) => backend.Links.DisconnectAsync(linkId);

    // port enumeration can block (WMI nice-name lookup on Windows), keep it off the UI thread
    public Task<IReadOnlyList<SerialPortInfo>> GetSerialPortsAsync() =>
        Task.Run(() => LinkManager.GetSerialPorts());

    public Task<IReadOnlyList<VehicleInfo>> GetVehiclesAsync() => Task.FromResult(backend.Vehicles.List());

    public Task<IReadOnlyList<FlightMode>> GetModesAsync(uint sysid, byte compid) =>
        Task.FromResult(backend.Vehicles.GetModes(sysid, compid));

    public Task<CommandResult> SetModeAsync(uint sysid, byte compid, string mode) =>
        backend.Vehicles.SetModeAsync(sysid, compid, mode);

    public Task<CommandResult> ArmAsync(uint sysid, byte compid, bool arm, bool force = false) =>
        backend.Vehicles.ArmAsync(sysid, compid, arm, force);

    public Task<IReadOnlyList<StatusTextEntry>> GetMessagesAsync(uint sysid, byte compid) =>
        Task.FromResult(backend.Vehicles.GetMessages(sysid, compid));

    public Task<IReadOnlyList<Job>> GetJobsAsync() => Task.FromResult(backend.Jobs.List());

    public Task<double?> GetParamAsync(uint sysid, byte compid, string name)
    {
        try
        {
            return Task.FromResult(backend.Params.Get(sysid, compid, name)?.Value);
        }
        catch (VehicleNotFoundException)
        {
            return Task.FromResult<double?>(null);
        }
    }

    public Task<(uint Present, uint Enabled, uint Health)> GetSensorsAsync(uint sysid, byte compid)
    {
        try
        {
            var cs = backend.Vehicles.Resolve(sysid, compid).mav.cs;
            return Task.FromResult((cs.sensors_present.Value, cs.sensors_enabled.Value, cs.sensors_health.Value));
        }
        catch (VehicleNotFoundException)
        {
            return Task.FromResult((0u, 0u, 0u));
        }
    }

    public Task<CommandResult> GotoAsync(uint sysid, byte compid, double lat, double lng, float alt) =>
        backend.Vehicles.GotoAsync(sysid, compid, lat, lng, alt);

    public Task<MPBackend.Core.Missions.MissionPlan> GetMissionAsync(uint sysid, byte compid, MAVLink.MAV_MISSION_TYPE type) =>
        backend.Missions.DownloadAsync(sysid, compid, type);

    public Task<bool> SetCurrentWaypointAsync(uint sysid, byte compid, ushort seq) =>
        backend.Missions.SetCurrentAsync(sysid, compid, seq);

    public Task<CommandResult> PauseContinueAsync(uint sysid, byte compid, bool resume) =>
        backend.Vehicles.CommandAsync(sysid, compid, MAVLink.MAV_CMD.DO_PAUSE_CONTINUE,
            new[] { resume ? 1f : 0f, 0, 0, 0, 0, 0, 0 });

    public Task<IReadOnlyList<MPBackend.Core.Maps.MapProviderInfo>> GetMapProvidersAsync() =>
        Task.FromResult(backend.Tiles.Providers());

    // tiles may be downloaded synchronously by GMap.NET: keep that off the UI thread
    public Task<(byte[] Data, string ContentType)?> GetTileAsync(string provider, int zoom, long x, long y) =>
        Task.Run<(byte[], string)?>(() =>
        {
            try
            {
                var tile = backend.Tiles.GetTile(provider, zoom, x, y);
                return tile == null ? null : (tile.Data, tile.ContentType);
            }
            catch (Exception)
            {
                return null;
            }
        });

    public Task<(double Lat, double Lng)?> GetHomeAsync(uint sysid, byte compid)
    {
        try
        {
            var home = backend.Vehicles.Resolve(sysid, compid).mav.cs.HomeLocation;
            return Task.FromResult<(double, double)?>(home == null || home.Lat == 0 && home.Lng == 0 ? null : (home.Lat, home.Lng));
        }
        catch (VehicleNotFoundException)
        {
            return Task.FromResult<(double, double)?>(null);
        }
    }

    public IDisposable SubscribeTelemetry(uint sysid, byte compid, IReadOnlyList<string>? fields, double rate,
        Action<TelemetryUpdate> handler) =>
        new TelemetrySampler(backend, sysid, compid, fields, rate, handler);

    public void Dispose() => backend.Events.Published -= OnPublished;

    /// <summary>
    /// Same sampling as MPBackend.Server's WebSocket telemetry topic: poll CurrentState at a fixed rate and
    /// deliver the changed fields, resynchronising with a full snapshot every 5 s.
    /// </summary>
    private sealed class TelemetrySampler : IDisposable
    {
        private static readonly TimeSpan FullResync = TimeSpan.FromSeconds(5);
        private readonly CancellationTokenSource cts = new();

        public TelemetrySampler(Backend backend, uint sysid, byte compid, IReadOnlyList<string>? fields,
            double rate, Action<TelemetryUpdate> handler)
        {
            rate = Math.Clamp(rate, 1, 50);
            var token = cts.Token;
            _ = Task.Run(async () =>
            {
                var last = new Dictionary<string, object?>();
                var lastFull = DateTime.MinValue;
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1 / rate));
                try
                {
                    while (await timer.WaitForNextTickAsync(token))
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
                        var changed = new Dictionary<string, object?>();
                        foreach (var (name, value) in snapshot)
                        {
                            if (full || !last.TryGetValue(name, out var old) || !Equals(old, value))
                                changed[name] = value;
                            last[name] = value;
                        }

                        if (full)
                            lastFull = DateTime.UtcNow;
                        if (changed.Count > 0)
                            handler(new TelemetryUpdate(full, changed));
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        public void Dispose() => cts.Cancel();
    }
}
