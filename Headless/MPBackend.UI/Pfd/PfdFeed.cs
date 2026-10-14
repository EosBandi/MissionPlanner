using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;
using MPBackend.UI.Services;

namespace MPBackend.UI.Pfd;

/// <summary>
/// One PFD frame. Angles in degrees, speeds m/s, altitudes m (REL = above home unless stated).
/// </summary>
public sealed record PfdState
{
    public bool HasVehicle { get; init; }
    public bool LinkLost { get; init; }
    public double LinkAge { get; init; }
    public bool AttitudeValid { get; init; }
    public bool PositionValid { get; init; }

    public double Roll { get; init; }
    public double Pitch { get; init; }
    public double Heading { get; init; }
    public double Track { get; init; }
    /// <summary>Yaw rate, deg/s (finite difference of heading, smoothed).</summary>
    public double TurnRate { get; init; }
    public double Lat { get; init; }
    public double Lng { get; init; }
    public double NavRoll { get; init; }
    public double NavPitch { get; init; }
    /// <summary>Lateral acceleration, mG (SCALED_IMU yacc), for the slip/skid bar.</summary>
    public double LateralAccel { get; init; }

    public double Airspeed { get; init; }
    public double GroundSpeed { get; init; }
    /// <summary>Speed acceleration, m/s² (smoothed), for the 6 s trend vector.</summary>
    public double SpeedTrend { get; init; }
    public double TargetSpeed { get; init; }
    public bool TargetSpeedFromVehicle { get; init; }

    public double Alt { get; init; }
    public double AltAsl { get; init; }
    public double TargetAlt { get; init; }
    public bool TargetAltValid { get; init; }
    public double ClimbRate { get; init; }
    public double Agl { get; init; }
    public bool AglValid { get; init; }

    public double NavBearing { get; init; }
    public double TargetBearing { get; init; }
    public double WpDist { get; init; }
    public double Xtrack { get; init; }
    public double AltError { get; init; }
    public double AspdError { get; init; }
    public int WpNo { get; init; }

    public double WindDir { get; init; }
    public double WindSpeed { get; init; }
    public bool WindValid { get; init; }
    public double Oat { get; init; }
    public double FlightTime { get; init; }
    public double DistToHome { get; init; }
    public double HomeBearing { get; init; }
    public bool HomeValid { get; init; }

    public string Mode { get; init; } = "";
    public bool Armed { get; init; }
    public bool Failsafe { get; init; }
    public double EkfStatus { get; init; }
    public bool Simulation { get; init; }

    public static readonly PfdState Empty = new();
}

/// <summary>
/// Samples the active vehicle at 20 Hz and turns CurrentState fields into <see cref="PfdState"/> frames. Also
/// resolves the vehicle profile (thresholds from parameters) when the parameter download completes.
/// <see cref="Updated"/> fires on the sampler thread.
/// </summary>
public sealed class PfdFeed : IDisposable
{
    public const double Rate = 20;
    private static readonly TimeSpan AttitudeStale = TimeSpan.FromMilliseconds(1000); // spec: 500 ms; MP streams ATTITUDE at ~10 Hz
    private static readonly TimeSpan PositionStale = TimeSpan.FromMilliseconds(3000);
    private static readonly TimeSpan LinkLostAfter = TimeSpan.FromSeconds(3);

    private static readonly string[] Fields =
    {
        "roll", "pitch", "yaw", "groundcourse", "nav_roll", "nav_pitch", "airspeed", "groundspeed", "targetairspeed",
        "alt", "altasl", "targetalt", "climbrate", "sonarrange", "ter_curalt", "nav_bearing", "target_bearing",
        "wp_dist", "xtrack_error", "alt_error", "aspd_error", "wpno", "wind_dir", "wind_vel", "press_temp",
        "timeInAir", "DistToHome", "lat", "lng", "mode", "armed", "failsafe", "ekfstatus", "ay"
    };

    private readonly GcsSession session;
    private readonly IGcs gcs;
    private readonly object sync = new();
    private readonly Dictionary<string, object?> v = new();
    private IDisposable? stream;
    private VehicleInfo? vehicle;
    private DateTime lastAttitude, lastPosition, lastTick, lastHomeTry;
    private double prevHeading = double.NaN, prevSpeed = double.NaN, turnRate, speedTrend;
    private (double Lat, double Lng)? home;

    public PfdFeed(GcsSession session, IGcs gcs)
    {
        this.session = session;
        this.gcs = gcs;
        session.Changed += OnSessionChanged;
        gcs.Published += OnPublished;
        OnSessionChanged();
    }

    public event Action? Updated;

    public PfdState State { get; private set; } = PfdState.Empty;
    public VehicleProfile Profile { get; private set; } = VehicleProfile.Default("multirotor");

    private void OnSessionChanged()
    {
        var active = session.ActiveVehicle;
        if (active?.SysId == vehicle?.SysId && active?.CompId == vehicle?.CompId)
        {
            vehicle = active; // refreshed snapshot (LastPacket)
            return;
        }

        vehicle = active;
        stream?.Dispose();
        stream = null;
        lock (sync)
            v.Clear();
        prevHeading = prevSpeed = double.NaN;
        home = null;
        Profile = VehicleProfile.Default(VehicleProfile.KeyFor(active?.Type, active?.Firmware));
        State = PfdState.Empty;

        if (active != null)
        {
            _ = LoadProfileAsync(active);
            _ = LoadHomeAsync(active);
            stream = gcs.SubscribeTelemetry(active.SysId, active.CompId, Fields, Rate, OnTelemetry);
        }

        Updated?.Invoke();
    }

    private void OnPublished(string topic, object data)
    {
        if (topic != Topics.Params || vehicle == null)
            return;
        // parameter download finished (or values changed): rebuild the profile thresholds
        _ = LoadProfileAsync(vehicle);
        _ = LoadHomeAsync(vehicle);
    }

    private async Task LoadProfileAsync(VehicleInfo veh)
    {
        try
        {
            var cache = new Dictionary<string, double?>();
            var any = false;
            foreach (var name in new[]
                     {
                         "AIRSPEED_MIN", "ARSPD_FBW_MIN", "AIRSPEED_MAX", "ARSPD_FBW_MAX", "AIRSPEED_CRUISE", "TRIM_ARSPD_CM",
                         "TKOFF_ROTATE_SPD", "TECS_CLMB_MAX", "TECS_SINK_MAX", "LIM_ROLL_CD", "Q_ASSIST_SPEED",
                         "WPNAV_SPEED", "WP_SPD", "LOIT_SPEED", "LOIT_SPEED_MS", "RTL_SPEED", "RTL_SPEED_MS", "PILOT_SPEED_UP", "PILOT_SPD_UP",
                         "PILOT_SPEED_DN", "PILOT_SPD_DN", "LAND_SPEED", "LAND_SPD_MS", "ANGLE_MAX", "ATC_ANGLE_MAX",
                         "FENCE_ENABLE", "FENCE_TYPE", "FENCE_ALT_MAX", "FENCE_ALT_MIN"
                     })
            {
                var value = await gcs.GetParamAsync(veh.SysId, veh.CompId, name);
                cache[name] = value;
                any |= value != null;
            }

            if (!any)
                return;
            Profile = VehicleProfile.Build(VehicleProfile.KeyFor(veh.Type, veh.Firmware),
                n => cache.TryGetValue(n, out var x) ? x : null, true);
            Updated?.Invoke();
        }
        catch
        {
            // keep defaults
        }
    }

    private async Task LoadHomeAsync(VehicleInfo veh)
    {
        try
        {
            home = await gcs.GetHomeAsync(veh.SysId, veh.CompId);
        }
        catch
        {
        }
    }

    private void OnTelemetry(TelemetryUpdate update)
    {
        var now = DateTime.UtcNow;
        lock (sync)
        {
            foreach (var (k, val) in update.Changed)
                v[k] = val;
            if (!update.Full)
            {
                if (update.Changed.ContainsKey("roll") || update.Changed.ContainsKey("pitch") || update.Changed.ContainsKey("yaw"))
                    lastAttitude = now;
                if (update.Changed.ContainsKey("lat") || update.Changed.ContainsKey("lng") || update.Changed.ContainsKey("alt"))
                    lastPosition = now;
            }
            else if (lastAttitude == default)
            {
                lastAttitude = lastPosition = now;
            }

            State = Build(now);
        }

        Updated?.Invoke();
    }

    private double D(string k) => v.TryGetValue(k, out var o) && o != null ? Convert.ToDouble(o) : double.NaN;
    private double D0(string k) { var d = D(k); return double.IsNaN(d) ? 0 : d; }

    private PfdState Build(DateTime now)
    {
        var dt = lastTick == default ? 1 / Rate : Math.Max(0.01, (now - lastTick).TotalSeconds);
        lastTick = now;

        var heading = Svg.Norm(D0("yaw"));
        var speed = Profile.FixedWing ? D0("airspeed") : D0("groundspeed");
        if (!double.IsNaN(prevHeading))
        {
            // smooth over ~0.5 s
            var k = Math.Min(1, dt / 0.5);
            turnRate += (Svg.Wrap180(heading - prevHeading) / dt - turnRate) * k;
            speedTrend += ((speed - prevSpeed) / dt - speedTrend) * k;
        }

        prevHeading = heading;
        prevSpeed = speed;

        var linkAge = vehicle == null ? double.NaN : (now - vehicle.LastPacket.ToUniversalTime()).TotalSeconds;
        var lat = D("lat");
        var lng = D("lng");
        // HOME_POSITION arrives once home is set (first fix / arming): keep asking until we have it
        if (home == null && vehicle != null && now - lastHomeTry > TimeSpan.FromSeconds(5))
        {
            lastHomeTry = now;
            _ = LoadHomeAsync(vehicle);
        }
        var homeValid = home != null && !double.IsNaN(lat) && lat != 0;
        var homeBearing = homeValid ? Bearing(lat, lng, home!.Value.Lat, home.Value.Lng) : 0;
        var sonar = D("sonarrange");
        var terrain = D("ter_curalt");
        var aglValid = sonar > 0 || terrain > 0;
        var targetSpeed = D("targetairspeed");
        var mode = v.TryGetValue("mode", out var m) && m is string ms ? ms : "";

        return new PfdState
        {
            HasVehicle = vehicle != null,
            LinkLost = linkAge > LinkLostAfter.TotalSeconds,
            LinkAge = linkAge,
            AttitudeValid = now - lastAttitude < AttitudeStale,
            PositionValid = now - lastPosition < PositionStale && lat != 0,
            Lat = double.IsNaN(lat) ? 0 : lat, Lng = double.IsNaN(lng) ? 0 : lng,
            Roll = D0("roll"), Pitch = D0("pitch"), Heading = heading, Track = Svg.Norm(D0("groundcourse")),
            TurnRate = turnRate, NavRoll = D0("nav_roll"), NavPitch = D0("nav_pitch"), LateralAccel = D0("ay"),
            Airspeed = D0("airspeed"), GroundSpeed = D0("groundspeed"), SpeedTrend = speedTrend,
            TargetSpeed = targetSpeed > 0 ? targetSpeed : Profile.DefaultTargetSpeed, TargetSpeedFromVehicle = targetSpeed > 0,
            Alt = D0("alt"), AltAsl = D0("altasl"), TargetAlt = D0("targetalt"), TargetAltValid = D("targetalt") > 0,
            ClimbRate = D0("climbrate"), Agl = sonar > 0 ? sonar : terrain, AglValid = aglValid,
            NavBearing = D0("nav_bearing"), TargetBearing = D0("target_bearing"), WpDist = D0("wp_dist"),
            Xtrack = D0("xtrack_error"), AltError = D0("alt_error"), AspdError = D0("aspd_error"), WpNo = (int)D0("wpno"),
            WindDir = D0("wind_dir"), WindSpeed = D0("wind_vel"), WindValid = D("wind_vel") > 0,
            Oat = D0("press_temp") / 100, FlightTime = D0("timeInAir"),
            DistToHome = D0("DistToHome"), HomeBearing = homeBearing, HomeValid = homeValid,
            Mode = mode, Armed = v.TryGetValue("armed", out var a) && a is true,
            Failsafe = v.TryGetValue("failsafe", out var f) && f is true, EkfStatus = D0("ekfstatus"),
            Simulation = IsSitl()
        };
    }

    private bool IsSitl()
    {
        // SITL identifies itself through the link type only; a tcp link to localhost is our dev simulator
        var link = session.Links.FirstOrDefault(l => l.Id == vehicle?.LinkId);
        return link != null && link.Connection.Contains("127.0.0.1");
    }

    private static double Bearing(double lat1, double lng1, double lat2, double lng2)
    {
        var p1 = lat1 * Svg.Deg;
        var p2 = lat2 * Svg.Deg;
        var dl = (lng2 - lng1) * Svg.Deg;
        var y = Math.Sin(dl) * Math.Cos(p2);
        var x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl);
        return Svg.Norm(Math.Atan2(y, x) / Svg.Deg);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        gcs.Published -= OnPublished;
        stream?.Dispose();
    }
}
