using MPBackend.Core;
using MPBackend.Core.Missions;
using MPBackend.Core.Vehicles;
using MPBackend.UI.Services;

namespace MPBackend.UI.Map;

/// <summary>
/// Mission, fence and rally lists of the active vehicle for the map overlays. Downloaded when the vehicle appears
/// and again whenever the backend reports an upload or download (missions topic).
/// </summary>
public sealed class MissionStore : IDisposable
{
    private readonly GcsSession session;
    private readonly IGcs gcs;
    private VehicleInfo? vehicle;
    private bool loading;

    public MissionStore(GcsSession session, IGcs gcs)
    {
        this.session = session;
        this.gcs = gcs;
        session.Changed += OnSessionChanged;
        gcs.Published += OnPublished;
        OnSessionChanged();
    }

    public event Action? Changed;

    public MissionPlan? Mission { get; private set; }
    public MissionPlan? Fence { get; private set; }
    public MissionPlan? Rally { get; private set; }
    public string? Error { get; set; }

    private void OnSessionChanged()
    {
        var active = session.ActiveVehicle;
        if (active?.SysId == vehicle?.SysId && active?.CompId == vehicle?.CompId)
        {
            vehicle = active;
            return;
        }

        vehicle = active;
        Mission = Fence = Rally = null;
        if (active != null && active.ParamsReceived >= active.ParamsTotal)
            _ = ReloadAsync();
        Changed?.Invoke();
    }

    private void OnPublished(string topic, object data)
    {
        if (vehicle == null)
            return;
        // after the parameter download the link is free: fetch the lists; refetch after uploads from elsewhere
        if (topic == Topics.Params && Mission == null && !loading)
            _ = ReloadAsync();
        else if (topic == Topics.Missions && data is MissionEvent e && e.Event == "uploaded")
            _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var v = vehicle;
        if (v == null || loading)
            return;
        loading = true;
        try
        {
            Mission = await gcs.GetMissionAsync(v.SysId, v.CompId, MAVLink.MAV_MISSION_TYPE.MISSION);
            Changed?.Invoke();
            Fence = await gcs.GetMissionAsync(v.SysId, v.CompId, MAVLink.MAV_MISSION_TYPE.FENCE);
            Rally = await gcs.GetMissionAsync(v.SysId, v.CompId, MAVLink.MAV_MISSION_TYPE.RALLY);
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            loading = false;
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        gcs.Published -= OnPublished;
    }
}
