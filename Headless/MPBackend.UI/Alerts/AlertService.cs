using System.Text.RegularExpressions;
using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;
using MPBackend.UI.Services;

namespace MPBackend.UI.Alerts;

/// <summary>
/// Crew alerting system (spec cas + ardupilot.alerts): evaluates alert conditions from telemetry, sensor health,
/// parameters and STATUSTEXT, keeps the active list and history, handles acknowledgement, escalation and the
/// flight-phase inhibits. <see cref="Changed"/> fires on backend threads.
/// </summary>
public sealed class AlertService : IDisposable
{
    private static readonly string[] Fields =
    {
        "armed", "mode", "alt", "gpsstatus", "gpshdop", "satcount", "linkqualitygcs", "ekfstatus", "ekfvelv",
        "ekfposhor", "ekfposvert", "ekfcompv", "vibex", "vibey", "vibez", "vibeclip0", "landed_state", "fenceb_status",
        "battery_voltage", "battery_remaining", "battery_usedmah", "battery_temp", "ter_pend", "terrainactive",
        "failsafe", "ch3percent", "timeInAir", "climbrate"
    };

    private static readonly TimeSpan ConnectGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TransientLife = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PrearmLife = TimeSpan.FromSeconds(40); // ArduPilot repeats PreArm every ~30 s
    private static readonly HashSet<string> TakeoffNotInhibited = new(StringComparer.OrdinalIgnoreCase)
        { "MOTOR_FAIL", "THRUST_LOSS", "ENG_FAIL", "BATT_CRIT", "EKF_FS", "LINK_LOST" };
    private static readonly HashSet<string> LandingInhibited = new(StringComparer.OrdinalIgnoreCase)
        { "TERRAIN_CAUT", "FENCE_NEAR" };
    // modes that need a position estimate (GPS_LOST applies while armed in one of these)
    private static readonly HashSet<string> PositionModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOITER", "AUTO", "GUIDED", "RTL", "POSHOLD", "LAND", "SMARTRTL", "CIRCLE", "BRAKE", "FOLLOW", "AUTORTL",
        "CRUISE", "QLOITER", "QRTL", "QLAND", "TAKEOFF", "THERMAL", "AVOIDADSB", "ZIGZAG"
    };

    private readonly GcsSession session;
    private readonly IGcs gcs;
    private readonly object sync = new();
    private readonly List<ActiveAlert> alerts = new();
    private readonly List<ActiveAlert> history = new();
    private readonly Dictionary<string, object?> t = new();
    private readonly Dictionary<string, double?> prm = new();
    private IDisposable? stream;
    private VehicleInfo? vehicle;
    private DateTime connectedAt;
    private DateTime takeoffStart;
    private uint sensorsEnabled, sensorsHealth;
    private double lastClip = double.NaN;
    private bool wasInAir;
    private int version;

    public AlertService(GcsSession session, IGcs gcs)
    {
        this.session = session;
        this.gcs = gcs;
        session.Changed += OnSessionChanged;
        gcs.Published += OnPublished;
        OnSessionChanged();
    }

    public event Action? Changed;

    public FlightPhase Phase { get; private set; } = FlightPhase.Preflight;
    public bool Simulation { get; private set; }
    public bool WindowOpen { get; private set; }
    public bool ShowHistory { get; private set; }
    /// <summary>Unread INFO-level STATUSTEXT exists (HSI MSG flag).</summary>
    public bool UnreadMessages { get; private set; }

    /// <summary>Active, non-inhibited alerts in CAS order: level, then newest first.</summary>
    public IReadOnlyList<ActiveAlert> Active
    {
        get
        {
            lock (sync)
                return alerts.Where(a => !a.Inhibited).OrderBy(a => a.Level).ThenByDescending(a => a.Since).ToList();
        }
    }

    public IReadOnlyList<ActiveAlert> History
    {
        get
        {
            lock (sync)
                return history.OrderByDescending(a => a.ClearedAt).ToList();
        }
    }

    public bool HasUnacknowledged(AlertLevel level)
    {
        lock (sync)
            return alerts.Any(a => !a.Inhibited && !a.Acknowledged && a.Level == level);
    }

    /// <summary>Highest unacknowledged level, or null.</summary>
    public AlertLevel? Unacknowledged
    {
        get
        {
            lock (sync)
                return alerts.Where(a => !a.Inhibited && !a.Acknowledged).Select(a => (AlertLevel?)a.Level).Min();
        }
    }

    public void Acknowledge(AlertLevel? level = null)
    {
        lock (sync)
            foreach (var a in alerts.Where(a => level == null || a.Level == level))
                a.Acknowledged = true;
        UnreadMessages = false;
        Notify();
    }

    public void OpenWindow(bool open)
    {
        WindowOpen = open;
        if (!open)
            ShowHistory = false;
        Notify();
    }

    public void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
        Notify();
    }

    // ---- vehicle / data sources -------------------------------------------------------------------------

    private void OnSessionChanged()
    {
        var active = session.ActiveVehicle;
        if (active?.SysId == vehicle?.SysId && active?.CompId == vehicle?.CompId)
        {
            vehicle = active;
            return;
        }

        vehicle = active;
        stream?.Dispose();
        stream = null;
        lock (sync)
        {
            alerts.Clear();
            t.Clear();
            prm.Clear();
        }

        connectedAt = DateTime.UtcNow;
        if (active != null)
        {
            Simulation = session.Links.FirstOrDefault(l => l.Id == active.LinkId)?.Connection.Contains("127.0.0.1") == true;
            _ = LoadParamsAsync(active);
            stream = gcs.SubscribeTelemetry(active.SysId, active.CompId, Fields, 2, OnTelemetry);
        }

        Notify();
    }

    private void OnPublished(string topic, object data)
    {
        if (vehicle == null)
            return;
        if (topic == Topics.Params)
            _ = LoadParamsAsync(vehicle);
        else if (topic == Topics.Status && data is StatusMessage msg && msg.SysId == vehicle.SysId)
            OnStatusText(msg);
    }

    private async Task LoadParamsAsync(VehicleInfo veh)
    {
        try
        {
            foreach (var name in new[]
                     {
                         "BATT_LOW_VOLT", "BATT_CRT_VOLT", "BATT_LOW_MAH", "BATT_CRT_MAH", "BATT_CAPACITY",
                         "FS_EKF_THRESH", "GPS_HDOP_GOOD", "FS_GCS_ENABLE", "FS_GCS_ENABL"
                     })
            {
                var value = await gcs.GetParamAsync(veh.SysId, veh.CompId, name);
                lock (sync)
                    prm[name] = value;
            }
        }
        catch
        {
        }
    }

    private double Prm(string name, double fallback)
    {
        lock (sync)
            return prm.TryGetValue(name, out var v) && v is { } d ? d : fallback;
    }

    private double D(string k) => t.TryGetValue(k, out var o) && o != null ? Convert.ToDouble(o) : double.NaN;
    private bool B(string k) => t.TryGetValue(k, out var o) && o is true;
    private string Str(string k) => t.TryGetValue(k, out var o) && o is string s ? s : "";

    private async void OnTelemetry(TelemetryUpdate update)
    {
        lock (sync)
            foreach (var (k, v) in update.Changed)
                t[k] = v;

        if (vehicle != null)
        {
            try
            {
                var s = await gcs.GetSensorsAsync(vehicle.SysId, vehicle.CompId);
                sensorsEnabled = s.Enabled;
                sensorsHealth = s.Health;
            }
            catch
            {
            }
        }

        Evaluate();
    }

    // ---- evaluation --------------------------------------------------------------------------------------

    private void Evaluate()
    {
        var now = DateTime.UtcNow;
        var v = vehicle;
        if (v == null)
            return;

        lock (sync)
        {
            var armed = B("armed");
            var mode = Norm(Str("mode"));
            var alt = D("alt");
            var landed = (int)(double.IsNaN(D("landed_state")) ? 0 : D("landed_state"));
            var linkAge = (now - v.LastPacket.ToUniversalTime()).TotalSeconds;
            var fixedWing = (v.Type ?? "").ToUpperInvariant().Contains("FIXED_WING") || (v.Firmware ?? "").Contains("Plane");

            // flight phase (spec flightPhases)
            var inAir = landed == 2 || landed == 3 || landed == 4 || (landed == 0 && armed && alt > 2);
            if (inAir) wasInAir = true;
            var previous = Phase;
            Phase = !armed ? (wasInAir ? FlightPhase.Postflight : FlightPhase.Preflight)
                : landed == 4 || mode == "LAND" || mode == "QLAND" ? FlightPhase.Landing
                : mode.Contains("RTL") ? FlightPhase.Return
                : landed == 3 ? FlightPhase.Takeoff
                : inAir ? FlightPhase.Enroute
                : FlightPhase.Armed;
            if (!armed) wasInAir = wasInAir && Phase == FlightPhase.Postflight;
            if (Phase == FlightPhase.Takeoff && previous != FlightPhase.Takeoff) takeoffStart = now;
            if (previous == FlightPhase.Armed && Phase == FlightPhase.Enroute && (now - takeoffStart).TotalSeconds > 120)
                takeoffStart = now; // no landed_state TAKEOFF reported: start the inhibit window at lift-off
            var takeoffInhibit = armed && inAir && (now - takeoffStart).TotalSeconds < 60 && alt < (fixedWing ? 30 : 10);
            var landingInhibit = Phase == FlightPhase.Landing && alt < (fixedWing ? 15 : 5);

            // ---- conditions from the spec's alert table ----
            var ekfThresh = Prm("FS_EKF_THRESH", 0.8);
            var ekfMax = new[] { D("ekfvelv"), D("ekfposhor"), D("ekfposvert"), D("ekfcompv") }.Where(x => !double.IsNaN(x)).DefaultIfEmpty(0).Max();
            Set("EKF_FS", ekfMax > ekfThresh && armed, $"variance {ekfMax:0.00} > {ekfThresh:0.0}");

            var volt = D("battery_voltage");
            var remaining = D("battery_remaining");
            var used = D("battery_usedmah");
            var capacity = Prm("BATT_CAPACITY", 0);
            var lowV = Prm("BATT_LOW_VOLT", 0);
            var crtV = Prm("BATT_CRT_VOLT", 0);
            var lowMah = Prm("BATT_LOW_MAH", 0);
            var crtMah = Prm("BATT_CRT_MAH", 0);
            var haveParams = lowV > 0 || lowMah > 0 || crtV > 0 || crtMah > 0;
            var crit = crtV > 0 && volt > 0 && volt < crtV || crtMah > 0 && capacity > 0 && capacity - used < crtMah
                       || !haveParams && remaining <= 15;
            var low = lowV > 0 && volt > 0 && volt < lowV || lowMah > 0 && capacity > 0 && capacity - used < lowMah
                      || !haveParams && remaining <= 30;
            Set("BATT_CRIT", crit, $"{volt:0.0} V, {remaining:0}%");
            Set("BATT_LOW", low && !crit, $"{volt:0.0} V, {remaining:0}%");
            Set("BATT_TEMP", D("battery_temp") > 55, $"{D("battery_temp"):0}°C", D("battery_temp") > 60 ? AlertLevel.Warning : AlertLevel.Caution);

            Set("LINK_LOST", linkAge > 3, $"no heartbeat for {linkAge:0} s");
            var lq = D("linkqualitygcs");
            Set("LINK_WEAK", linkAge <= 3 && lq > 0 && lq < 80, $"link quality {lq:0}%");
            var gcsFs = Prm("FS_GCS_ENABLE", Prm("FS_GCS_ENABL", 0)) > 0;
            Set("GCS_FS_PENDING", armed && gcsFs && linkAge > 1 && linkAge <= 3, $"heartbeat age {linkAge:0.0} s");

            Set("RC_FS", Enabled(SensorBit.RcReceiver) && !Healthy(SensorBit.RcReceiver) && armed, "RC receiver unhealthy");

            var fix = D("gpsstatus");
            var positionMode = PositionModes.Contains(mode);
            Set("GPS_LOST", armed && positionMode && fix < 3, $"fix type {fix:0}");
            var hdopGood = Prm("GPS_HDOP_GOOD", 140) / 100;
            Set("GPS_DEGRADED", fix >= 3 && (D("gpshdop") > hdopGood || D("satcount") < 6), $"HDOP {D("gpshdop"):0.0}, {D("satcount"):0} sats");
            Set("COMPASS", D("ekfcompv") > 0.8 || Enabled(SensorBit.Compass) && !Healthy(SensorBit.Compass), $"compass variance {D("ekfcompv"):0.00}");
            Set("ASPD_FAIL", fixedWing && Enabled(SensorBit.DifferentialPressure) && !Healthy(SensorBit.DifferentialPressure), "airspeed sensor unhealthy");

            var vibe = new[] { D("vibex"), D("vibey"), D("vibez") }.Where(x => !double.IsNaN(x)).DefaultIfEmpty(0).Max();
            var clip = D("vibeclip0");
            var clipping = !double.IsNaN(lastClip) && clip > lastClip;
            lastClip = clip;
            Set("VIBE_HIGH", vibe > 30 || clipping && armed, $"{vibe:0} m/s²{(clipping ? ", clipping" : "")}");

            Set("FENCE_BREACH", D("fenceb_status") == 1, "fence breached");
            Set("TERRAIN_NA", B("terrainactive") && D("ter_pend") > 0, $"{D("ter_pend"):0} blocks pending");
            Set("SIMULATOR", Simulation, "SITL on localhost");
            Set("TO_INHIBIT", takeoffInhibit, "takeoff inhibit window");

            if (armed)
                Clear("PREARM");

            // ---- inhibits and expiry ----
            foreach (var a in alerts)
            {
                a.Inhibited = takeoffInhibit && a.Level is AlertLevel.Caution or AlertLevel.Advisory && !TakeoffNotInhibited.Contains(a.Id)
                              || landingInhibit && LandingInhibited.Contains(a.Id)
                              || !armed && !inAir && a.Id.StartsWith("TERRAIN_") && a.Id != "TERRAIN_NA";
                if (a.ExpiresAt != null && now > a.ExpiresAt)
                    ClearAlert(a, now);
            }

            alerts.RemoveAll(a => a.ClearedAt != null);
        }

        Notify();
    }

    private void OnStatusText(StatusMessage msg)
    {
        var text = msg.Text.Trim();
        var level = msg.Severity switch
        {
            <= MAVLink.MAV_SEVERITY.CRITICAL => AlertLevel.Warning,
            MAVLink.MAV_SEVERITY.ERROR or MAVLink.MAV_SEVERITY.WARNING => AlertLevel.Caution,
            MAVLink.MAV_SEVERITY.NOTICE => AlertLevel.Advisory,
            _ => AlertLevel.Status
        };

        lock (sync)
        {
            if (text.StartsWith("PreArm:", StringComparison.OrdinalIgnoreCase))
            {
                if (!B("armed"))
                    Raise("PREARM", AlertLevel.Advisory, "PREARM", text.Substring(7).Trim(), PrearmLife);
                return;
            }

            // "EKF Failsafe Cleared", "Radio Failsafe Cleared", "Fence breach cleared": end the condition
            if (Regex.IsMatch(text, @"\bcleared\b", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(text, "EKF", RegexOptions.IgnoreCase)) Clear("EKF_FS");
                else if (Regex.IsMatch(text, "Radio|Throttle", RegexOptions.IgnoreCase)) Clear("RC_FS");
                else if (Regex.IsMatch(text, "Fence", RegexOptions.IgnoreCase)) Clear("FENCE_BREACH");
                Raise("MSG:" + Cas(text), AlertLevel.Advisory, Cas(text), text, TimeSpan.FromSeconds(10));
            }
            else if (Regex.IsMatch(text, "EKF (Failsafe|variance)", RegexOptions.IgnoreCase))
                Raise("EKF_FS", AlertLevel.Warning, "EKF FAILSAFE", text, TransientLife);
            else if (Regex.IsMatch(text, "(Radio|Throttle) Failsafe", RegexOptions.IgnoreCase))
                Raise("RC_FS", AlertLevel.Warning, "RC FAILSAFE", text, TransientLife);
            else if (Regex.Match(text, @"Potential Thrust Loss \((\d+)\)", RegexOptions.IgnoreCase) is { Success: true } m)
                Raise("THRUST_LOSS", AlertLevel.Warning, $"MOTOR {m.Groups[1].Value} THRUST", text, TransientLife);
            else if (Regex.IsMatch(text, "Yaw Imbalance", RegexOptions.IgnoreCase))
                Raise("YAW_IMBALANCE", AlertLevel.Caution, "YAW IMBALANCE", text, TransientLife);
            else if (Regex.IsMatch(text, "Thrust loss|Crash: Disarming", RegexOptions.IgnoreCase))
                Raise("MOTOR_FAIL", AlertLevel.Warning, "MOTOR FAIL", text, TransientLife);
            else if (Regex.IsMatch(text, "Fence breach", RegexOptions.IgnoreCase))
                Raise("FENCE_BREACH", AlertLevel.Warning, "FENCE BREACH", text, TransientLife);
            else if (Regex.IsMatch(text, "Parachute", RegexOptions.IgnoreCase))
                Raise("CHUTE_RELEASED", AlertLevel.Warning, "PARACHUTE", text, TransientLife);
            else if (level == AlertLevel.Status)
            {
                if (msg.Severity == MAVLink.MAV_SEVERITY.INFO)
                    UnreadMessages = true;
                return;
            }
            else
            {
                // any other STATUSTEXT of NOTICE or above: transient CAS message with the text itself
                Raise("MSG:" + Cas(text), level, Cas(text), text, level == AlertLevel.Advisory ? TimeSpan.FromSeconds(10) : TransientLife);
            }
        }

        Notify();
    }

    // ---- list maintenance (callers hold sync) -----------------------------------------------------------------

    private void Set(string id, bool condition, string? detail, AlertLevel? levelOverride = null)
    {
        var def = AlertTable.Get(id);
        if (def == null)
            return;
        if (condition)
            Raise(id, levelOverride ?? def.Level, def.Text, detail, null, def.Action);
        else
            Clear(id);
    }

    private void Raise(string id, AlertLevel level, string text, string? detail, TimeSpan? life, string? action = null)
    {
        var now = DateTime.UtcNow;
        var existing = alerts.FirstOrDefault(a => a.Id == id);
        if (existing != null)
        {
            existing.Description = detail;
            if (life != null)
                existing.ExpiresAt = now + life;
            // cas.window.escalation: caution → warning needs a new acknowledgement; downgrade does not
            if (level < existing.Level)
                existing.Acknowledged = false;
            existing.Level = level;
            return;
        }

        // cas.window.connectPolicy: conditions already present at connect are shown acknowledged, except warnings
        var atConnect = now - connectedAt < ConnectGrace && level != AlertLevel.Warning;
        var transient = life != null;
        alerts.Add(new ActiveAlert
        {
            Id = id, Level = level, Text = Cas(text), Description = detail, Action = action ?? AlertTable.Get(id)?.Action,
            Acknowledged = atConnect || level >= AlertLevel.Advisory && !transient && id != "PREARM",
            ExpiresAt = life == null ? null : now + life
        });
        version++;
    }

    private void Clear(string id)
    {
        var a = alerts.FirstOrDefault(x => x.Id == id);
        if (a != null)
            ClearAlert(a, DateTime.UtcNow);
    }

    private void ClearAlert(ActiveAlert a, DateTime now)
    {
        if (a.ClearedAt != null)
            return;
        a.ClearedAt = now;
        history.Insert(0, a);
        if (history.Count > 50)
            history.RemoveRange(50, history.Count - 50);
        version++;
    }

    /// <summary>CAS format: upper case, at most 18 characters.</summary>
    private static string Cas(string text)
    {
        var s = text.Trim().ToUpperInvariant();
        return s.Length <= 18 ? s : s.Substring(0, 18);
    }

    private static string Norm(string mode) => mode.Replace("_", "").Replace(" ", "").ToUpperInvariant();

    // MAV_SYS_STATUS_SENSOR bits
    private enum SensorBit : uint
    {
        Compass = 1 << 2,
        DifferentialPressure = 1 << 4,
        RcReceiver = 1 << 16
    }

    private bool Enabled(SensorBit bit) => (sensorsEnabled & (uint)bit) != 0;
    private bool Healthy(SensorBit bit) => (sensorsHealth & (uint)bit) != 0;

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        gcs.Published -= OnPublished;
        stream?.Dispose();
    }
}
