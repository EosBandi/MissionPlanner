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

    public Task<IReadOnlyList<MPBackend.Core.Params.ParamValue>> GetParamsAsync(uint sysid, byte compid) =>
        Task.Run(() => backend.Params.List(sysid, compid));

    public Task<IReadOnlyDictionary<string, MPBackend.Core.Params.ParamMetadata>> GetParamMetadataAsync(uint sysid, byte compid) =>
        Task.Run(() => backend.Params.GetMetadata(sysid, compid));

    public async Task<IReadOnlyList<MPBackend.Core.Params.ParamSetResult>> WriteParamsAsync(uint sysid, byte compid,
        IReadOnlyDictionary<string, double> values)
    {
        var job = backend.Params.StartBulkSet(sysid, compid, values);
        job = await backend.Jobs.WaitAsync(job, TimeSpan.FromSeconds(10 + values.Count * 2));
        if (job.State == MPBackend.Core.Jobs.JobState.Failed)
            throw new InvalidOperationException(job.Error ?? "write failed");
        if (job.State != MPBackend.Core.Jobs.JobState.Succeeded)
            throw new TimeoutException("parameter write did not finish");

        // the job result is the REST shape { written, failed[], rebootRequired[] }: turn it back into per-name results
        var result = job.Result!;
        var failed = result.GetType().GetProperty("failed")?.GetValue(result) as IEnumerable<MPBackend.Core.Params.ParamSetResult>
                     ?? Array.Empty<MPBackend.Core.Params.ParamSetResult>();
        var reboot = result.GetType().GetProperty("rebootRequired")?.GetValue(result) as IEnumerable<string> ?? Array.Empty<string>();
        var failedByName = failed.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var rebootSet = new HashSet<string>(reboot, StringComparer.Ordinal);
        return values.Keys.Select(name => failedByName.TryGetValue(name, out var f)
                ? f
                : new MPBackend.Core.Params.ParamSetResult(name, true, backend.Params.Get(sysid, compid, name)?.Value, null, rebootSet.Contains(name)))
            .ToList();
    }

    public async Task RefreshParamsAsync(uint sysid, byte compid)
    {
        var job = backend.Params.StartDownload(sysid, compid, allowCache: false);
        job = await backend.Jobs.WaitAsync(job, TimeSpan.FromMinutes(3));
        if (job.State == MPBackend.Core.Jobs.JobState.Failed)
            throw new InvalidOperationException(job.Error ?? "download failed");
    }

    public Task<IReadOnlyList<MPBackend.Core.Params.ParamDiff>> CompareParamsAsync(uint sysid, byte compid, string fileText) =>
        Task.Run(() => backend.Params.Compare(sysid, compid, MPBackend.Core.Params.ParamService.ParseParamFile(fileText)));

    public Task<string> SaveParamFileAsync(uint sysid, byte compid) => Task.Run(() =>
    {
        var dir = Path.Combine(HeadlessHost.Options.DataDirectory, "params");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{sysid}-{compid}-{DateTime.Now:yyyyMMdd-HHmmss}.param");
        File.WriteAllText(path, backend.Params.ExportParamFile(sysid, compid));
        return path;
    });

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
