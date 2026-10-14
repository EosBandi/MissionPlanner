using MPBackend.Core;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.UI.Services;

/// <summary>
/// App-wide vehicle state shared by all screens: links, vehicles, the active vehicle, its status-strip
/// telemetry, STATUSTEXT history and jobs. Raises <see cref="Changed"/> (on backend threads) when anything moves.
/// </summary>
public sealed class GcsSession : IDisposable
{
    /// <summary>CurrentState fields the Vehicle Status Strip needs, sampled at 2 Hz.</summary>
    public static readonly string[] StatusFields =
    {
        "armed", "mode", "timeInAir", "battery_voltage", "current", "battery_remaining",
        "linkqualitygcs", "rssi", "remrssi", "gpsstatus", "satcount", "gpshdop", "ekfstatus", "firmware"
    };

    private const int MaxMessages = 300;
    private readonly IGcs gcs;
    private readonly object sync = new();
    private readonly Dictionary<string, object?> status = new();
    private readonly List<StatusMessage> messages = new();
    private readonly Dictionary<string, Job> jobs = new();
    private IDisposable? statusStream;
    private Timer? refresh;
    private bool initialised;

    public GcsSession(IGcs gcs)
    {
        this.gcs = gcs;
        gcs.Published += OnPublished;
    }

    public event Action? Changed;

    public IGcs Gcs => gcs;
    public IReadOnlyList<LinkInfo> Links { get; private set; } = Array.Empty<LinkInfo>();
    public IReadOnlyList<VehicleInfo> Vehicles { get; private set; } = Array.Empty<VehicleInfo>();
    public VehicleInfo? ActiveVehicle { get; private set; }
    public IReadOnlyList<FlightMode> ActiveModes { get; private set; } = Array.Empty<FlightMode>();

    /// <summary>Latest values of <see cref="StatusFields"/> for the active vehicle (empty when none).</summary>
    public IReadOnlyDictionary<string, object?> Status
    {
        get
        {
            lock (sync)
                return new Dictionary<string, object?>(status);
        }
    }

    public IReadOnlyList<StatusMessage> Messages
    {
        get
        {
            lock (sync)
                return messages.ToList();
        }
    }

    public IReadOnlyList<Job> Jobs
    {
        get
        {
            lock (sync)
                return jobs.Values.OrderByDescending(j => j.StartedAt).ToList();
        }
    }

    public async Task InitialiseAsync()
    {
        if (initialised)
            return;
        initialised = true;
        await RefreshAsync();
        foreach (var job in await gcs.GetJobsAsync())
            lock (sync)
                jobs[job.Id] = job;
        // VehicleInfo.LastPacket / link quality are snapshots: refresh once a second for the status strip
        refresh = new Timer(_ => _ = RefreshVehiclesAsync(), null, 1000, 1000);
    }

    public async Task RefreshAsync()
    {
        Links = await gcs.GetLinksAsync();
        await RefreshVehiclesAsync();
    }

    public async Task RefreshVehiclesAsync()
    {
        try
        {
            Vehicles = await gcs.GetVehiclesAsync();
        }
        catch
        {
            return;
        }

        var active = ActiveVehicle;
        var still = active == null
            ? null
            : Vehicles.FirstOrDefault(v => v.SysId == active.SysId && v.CompId == active.CompId);
        if (still != null)
            ActiveVehicle = still;
        else if (Vehicles.Count > 0)
            await SetActiveVehicleAsync(Vehicles[0]);
        else if (active != null)
            await SetActiveVehicleAsync(null);
        Notify();
    }

    public async Task SetActiveVehicleAsync(VehicleInfo? vehicle)
    {
        var changed = vehicle?.SysId != ActiveVehicle?.SysId || vehicle?.CompId != ActiveVehicle?.CompId;
        ActiveVehicle = vehicle;
        if (!changed)
            return;

        statusStream?.Dispose();
        statusStream = null;
        lock (sync)
            status.Clear();
        ActiveModes = Array.Empty<FlightMode>();

        if (vehicle != null)
        {
            try
            {
                ActiveModes = await gcs.GetModesAsync(vehicle.SysId, vehicle.CompId);
            }
            catch
            {
            }

            statusStream = gcs.SubscribeTelemetry(vehicle.SysId, vehicle.CompId, StatusFields, 2, update =>
            {
                lock (sync)
                    foreach (var (k, v) in update.Changed)
                        status[k] = v;
                Notify();
            });
        }

        Notify();
    }

    public Task<CommandResult> ArmAsync(bool arm) =>
        WithVehicle(v => gcs.ArmAsync(v.SysId, v.CompId, arm));

    public Task<CommandResult> SetModeAsync(string mode) =>
        WithVehicle(v => gcs.SetModeAsync(v.SysId, v.CompId, mode));

    private async Task<CommandResult> WithVehicle(Func<VehicleInfo, Task<CommandResult>> op)
    {
        var v = ActiveVehicle;
        if (v == null)
            return new CommandResult(false, "no vehicle");
        try
        {
            var result = await op(v);
            if (!result.Accepted)
                AddMessage(new StatusMessage(v.LinkId, v.SysId, v.CompId, MAVLink.MAV_SEVERITY.WARNING,
                    "GCS: " + result.Message, DateTime.UtcNow));
            return result;
        }
        catch (Exception ex)
        {
            AddMessage(new StatusMessage(v.LinkId, v.SysId, v.CompId, MAVLink.MAV_SEVERITY.ERROR,
                "GCS: " + ex.Message, DateTime.UtcNow));
            return new CommandResult(false, ex.Message);
        }
    }

    private void OnPublished(string topic, object data)
    {
        switch (topic)
        {
            case Topics.Links:
                _ = RefreshAsync();
                break;
            case Topics.Vehicles:
                _ = RefreshVehiclesAsync();
                break;
            case Topics.Status when data is StatusMessage msg:
                AddMessage(msg);
                break;
            case Topics.Jobs when data is Job job:
                lock (sync)
                    jobs[job.Id] = job;
                Notify();
                break;
        }
    }

    private void AddMessage(StatusMessage msg)
    {
        lock (sync)
        {
            messages.Add(msg);
            if (messages.Count > MaxMessages)
                messages.RemoveRange(0, messages.Count - MaxMessages);
        }

        Notify();
    }

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        gcs.Published -= OnPublished;
        refresh?.Dispose();
        statusStream?.Dispose();
    }
}
