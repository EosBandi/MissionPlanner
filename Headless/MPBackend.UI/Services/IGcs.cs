using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.UI.Services;

/// <summary>One telemetry tick: the CurrentState fields that changed (all of them when <see cref="Full"/>).</summary>
public sealed record TelemetryUpdate(bool Full, IReadOnlyDictionary<string, object?> Changed);

/// <summary>
/// Everything the screens need from the backend. <see cref="InProcessGcs"/> wraps <c>MPBackend.Core.Backend</c>
/// directly; a remote implementation can map the same calls onto MPBackend.Server's REST/WebSocket API.
/// Events and telemetry callbacks arrive on backend threads: components must marshal with InvokeAsync.
/// </summary>
public interface IGcs
{
    /// <summary>Backend event bus: (topic, payload). Topics are <c>MPBackend.Core.Topics</c>.</summary>
    event Action<string, object>? Published;

    Task<IReadOnlyList<LinkInfo>> GetLinksAsync();
    Task<LinkInfo> ConnectAsync(string connection);
    Task DisconnectAsync(int linkId);
    Task<IReadOnlyList<SerialPortInfo>> GetSerialPortsAsync();

    Task<IReadOnlyList<VehicleInfo>> GetVehiclesAsync();
    Task<IReadOnlyList<FlightMode>> GetModesAsync(uint sysid, byte compid);
    Task<CommandResult> SetModeAsync(uint sysid, byte compid, string mode);
    Task<CommandResult> ArmAsync(uint sysid, byte compid, bool arm, bool force = false);
    Task<IReadOnlyList<StatusTextEntry>> GetMessagesAsync(uint sysid, byte compid);
    Task<IReadOnlyList<Job>> GetJobsAsync();

    /// <summary>Current value of a vehicle parameter, null when unknown or not downloaded yet.</summary>
    Task<double?> GetParamAsync(uint sysid, byte compid, string name);

    /// <summary>All parameters of the vehicle (sorted by name).</summary>
    Task<IReadOnlyList<MPBackend.Core.Params.ParamValue>> GetParamsAsync(uint sysid, byte compid);

    /// <summary>apm.pdef.xml metadata for the vehicle's parameters (empty until downloaded).</summary>
    Task<IReadOnlyDictionary<string, MPBackend.Core.Params.ParamMetadata>> GetParamMetadataAsync(uint sysid, byte compid);

    /// <summary>Write many parameters and wait for the vehicle to confirm each.</summary>
    Task<IReadOnlyList<MPBackend.Core.Params.ParamSetResult>> WriteParamsAsync(uint sysid, byte compid, IReadOnlyDictionary<string, double> values);

    /// <summary>Re-download the full parameter list (ignoring the cache) and wait for it.</summary>
    Task RefreshParamsAsync(uint sysid, byte compid);

    /// <summary>Differences between a .param / .parm / QGC file's values and the vehicle.</summary>
    Task<IReadOnlyList<MPBackend.Core.Params.ParamDiff>> CompareParamsAsync(uint sysid, byte compid, string fileText);

    /// <summary>Save the vehicle's parameters as a Mission Planner .param file; returns the path written.</summary>
    Task<string> SaveParamFileAsync(uint sysid, byte compid);

    /// <summary>SYS_STATUS sensor bitmasks (MAV_SYS_STATUS_SENSOR): present, enabled, healthy.</summary>
    Task<(uint Present, uint Enabled, uint Health)> GetSensorsAsync(uint sysid, byte compid);

    /// <summary>Home position reported by the vehicle (null until known).</summary>
    Task<(double Lat, double Lng)?> GetHomeAsync(uint sysid, byte compid);

    /// <summary>Guided "fly to here"; altitude in metres above home.</summary>
    Task<CommandResult> GotoAsync(uint sysid, byte compid, double lat, double lng, float alt);

    /// <summary>
    /// Operator-selected target in GUIDED (spec selectedValueEditing.commands): heading (CONDITION_YAW on
    /// copters, GUIDED_CHANGE_HEADING on planes), altitude above home (guided reposition / GUIDED_CHANGE_ALTITUDE)
    /// or speed (DO_CHANGE_SPEED, ground speed on copters, airspeed on planes).
    /// </summary>
    Task<CommandResult> SetGuidedTargetAsync(uint sysid, byte compid, Pfd.SelectedKind kind, double value);

    /// <summary>Download the mission, fence or rally list from the vehicle.</summary>
    Task<MPBackend.Core.Missions.MissionPlan> GetMissionAsync(uint sysid, byte compid, MAVLink.MAV_MISSION_TYPE type);

    /// <summary>MISSION_SET_CURRENT: make <paramref name="seq"/> the active mission item.</summary>
    Task<bool> SetCurrentWaypointAsync(uint sysid, byte compid, ushort seq);

    /// <summary>MAV_CMD_DO_PAUSE_CONTINUE: pause (hold position) or continue the mission.</summary>
    Task<CommandResult> PauseContinueAsync(uint sysid, byte compid, bool resume);

    /// <summary>Map tile providers known to the backend (GMap.NET).</summary>
    Task<IReadOnlyList<MPBackend.Core.Maps.MapProviderInfo>> GetMapProvidersAsync();

    /// <summary>One XYZ tile from the backend's cache or the provider; null when unavailable (offline).</summary>
    Task<(byte[] Data, string ContentType)?> GetTileAsync(string provider, int zoom, long x, long y);

    /// <summary>
    /// Stream CurrentState fields (all when <paramref name="fields"/> is null) at <paramref name="rate"/> Hz.
    /// Only changed fields are delivered, with a full snapshot first and every 5 s. Dispose to stop.
    /// </summary>
    IDisposable SubscribeTelemetry(uint sysid, byte compid, IReadOnlyList<string>? fields, double rate,
        Action<TelemetryUpdate> handler);
}
