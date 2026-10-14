namespace MPBackend.UI.Pfd;

public sealed record SpeedBand(double From, double To, string Fill);

public sealed record RefMark(string Label, double Value);

/// <summary>
/// Vehicle profile (spec <c>profiles.fixedWing / multirotor / vtol</c>): tape spacing, colour bands, limits and
/// attitude thresholds, resolved from the vehicle's parameters at connect time. Values fall back to the design
/// guide's demo parameters until the parameter download has completed.
/// </summary>
public sealed class VehicleProfile
{
    public string Key { get; init; } = "multirotor";
    public bool FixedWing { get; init; }
    public bool Vtol { get; init; }
    public string SpeedLabel { get; init; } = "GS";
    public double SpeedSpan { get; init; }
    public double SpeedMajor { get; init; }
    public double SpeedMinor { get; init; }
    public IReadOnlyList<SpeedBand> Bands { get; init; } = Array.Empty<SpeedBand>();
    public IReadOnlyList<RefMark> SpeedRefs { get; init; } = Array.Empty<RefMark>();
    public double Overspeed { get; init; }
    public double Caution { get; init; }
    public double VsRange { get; init; }
    public double VsMajor { get; init; }
    public double VsMinor { get; init; }
    public double VsMax { get; init; }
    public double VsMin { get; init; }
    public IReadOnlyList<RefMark> VsMarks { get; init; } = Array.Empty<RefMark>();
    public double AltSpan { get; init; }
    public double AltLabel { get; init; }
    public double AltMinor { get; init; }
    public double ExtremeRoll { get; init; }
    public double ExtremePitchUp { get; init; }
    public double ExtremePitchDown { get; init; }
    public double ChevronUp { get; init; }
    public double ChevronDown { get; init; }
    public bool SlipSkid { get; init; }
    public double FpmMinSpeed { get; init; }
    public double CdiFullScale { get; init; }
    public double VdiDot { get; init; }
    public double AlertApproach1 { get; init; }
    public double AlertApproach2 { get; init; }
    public double AlertDeviation { get; init; }
    public double FloorShowWithin { get; init; }
    public double FloorWhiteWithin { get; init; }
    public double FloorClearAbove { get; init; }
    /// <summary>FENCE_ALT_MAX when an altitude fence is enabled.</summary>
    public double? FenceAltMax { get; init; }
    /// <summary>Minimum altitude bug: FENCE_ALT_MIN when enabled (copter), else none.</summary>
    public double? FloorAlt { get; init; }
    /// <summary>Default selected speed when the vehicle reports no target (WPNAV_SPEED / AIRSPEED_CRUISE).</summary>
    public double DefaultTargetSpeed { get; init; }
    public bool FromParams { get; init; }
    /// <summary>Map range steps (top-to-bottom distance), spec profiles.*.mapRangeSteps_m.</summary>
    public IReadOnlyList<double> MapRangeSteps { get; init; } = Array.Empty<double>();
    public double DefaultMapRange { get; init; }
    /// <summary>Default map orientation (spec mfd.navMap.orientationDefault).</summary>
    public string DefaultOrientation { get; init; } = "NORTH UP";
    /// <summary>Track vector length on the map, seconds ahead.</summary>
    public double TrackVectorSeconds { get; init; } = 30;

    public const string Barber = "url(#barber)";

    /// <summary>Profile key from the HEARTBEAT type name (MAV_TYPE) and firmware.</summary>
    public static string KeyFor(string? mavType, string? firmware)
    {
        var t = (mavType ?? "").ToUpperInvariant();
        if (t.StartsWith("VTOL")) return "vtol";
        if (t.Contains("FIXED_WING")) return "fixedWing";
        if (t.Contains("ROTOR") || t.Contains("COPTER") || t.Contains("HELI") || t.Contains("COAXIAL")) return "multirotor";
        return (firmware ?? "").Contains("Plane", StringComparison.OrdinalIgnoreCase) ? "fixedWing" : "multirotor";
    }

    /// <summary>Build a profile; <paramref name="param"/> returns null for unknown parameters.</summary>
    public static VehicleProfile Build(string key, Func<string, double?> param, bool fromParams)
    {
        double P(string name, double fallback, double scale = 1) => (param(name) ?? fallback) * scale;
        double? Opt(string name, double scale = 1) => param(name) is { } v ? v * scale : null;

        if (key == "fixedWing" || key == "vtol")
        {
            // ArduPlane 4.5 renamed ARSPD_FBW_* / TRIM_ARSPD_CM to AIRSPEED_*
            var min = param("AIRSPEED_MIN") ?? param("ARSPD_FBW_MIN") ?? 12;
            var max = param("AIRSPEED_MAX") ?? param("ARSPD_FBW_MAX") ?? 28;
            var cruise = param("AIRSPEED_CRUISE") ?? (param("TRIM_ARSPD_CM") is { } c ? c / 100 : 18);
            var climb = P("TECS_CLMB_MAX", 5);
            var sink = P("TECS_SINK_MAX", 5);
            var limRoll = P("LIM_ROLL_CD", 4500) / 100;
            var fenceOn = (param("FENCE_ENABLE") ?? 0) > 0;
            var qAssist = Opt("Q_ASSIST_SPEED");
            var refs = new List<RefMark>
            {
                new("C", cruise), new("R", P("TKOFF_ROTATE_SPD", 13)), new("S", min)
            };
            if (key == "vtol" && qAssist > 0)
                refs.Add(new RefMark("Q", qAssist.Value));
            return new VehicleProfile
            {
                Key = key, FixedWing = true, Vtol = key == "vtol", SpeedLabel = "IAS",
                SpeedSpan = 30, SpeedMajor = 5, SpeedMinor = 1,
                Bands = new[]
                {
                    new SpeedBand(0, min, "var(--p-red)"),
                    new SpeedBand(min, max * 0.9, "var(--p-green)"),
                    new SpeedBand(max * 0.9, max, "var(--p-yellow)"),
                    new SpeedBand(max, 999, Barber)
                },
                SpeedRefs = refs, Overspeed = max, Caution = max * 0.9,
                VsRange = 10, VsMajor = 5, VsMinor = 1, VsMax = climb, VsMin = -sink,
                AltSpan = 120, AltLabel = 20, AltMinor = 5,
                ExtremeRoll = limRoll + 20, ExtremePitchUp = 30, ExtremePitchDown = 20, ChevronUp = 50, ChevronDown = 30,
                SlipSkid = true, FpmMinSpeed = 8, CdiFullScale = 100, VdiDot = 5,
                AlertApproach1 = 50, AlertApproach2 = 15, AlertDeviation = 15,
                FloorShowWithin = 100, FloorWhiteWithin = 10, FloorClearAbove = 5,
                FenceAltMax = fenceOn ? Opt("FENCE_ALT_MAX") : null,
                FloorAlt = fenceOn && (Opt("FENCE_ALT_MIN") ?? 0) > 0 ? Opt("FENCE_ALT_MIN") : null,
                DefaultTargetSpeed = cruise, FromParams = fromParams,
                MapRangeSteps = new double[] { 100, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000, 7500, 10000, 15000, 20000, 30000, 50000, 75000, 100000, 150000, 200000 },
                DefaultMapRange = 2000, DefaultOrientation = "TRK UP", TrackVectorSeconds = 60
            };
        }

        // ArduCopter 4.7 renamed the cm/s speed parameters to m/s ones (WPNAV_SPEED -> WP_SPD, LOIT_SPEED ->
        // LOIT_SPEED_MS, PILOT_SPEED_UP -> PILOT_SPD_UP, LAND_SPEED -> LAND_SPD_MS, ANGLE_MAX -> ATC_ANGLE_MAX in deg)
        double Speed(string cms, string ms, double fallback) =>
            param(cms) is { } c ? c / 100 : param(ms) ?? fallback;
        var wp = Speed("WPNAV_SPEED", "WP_SPD", 10);
        var loit = Speed("LOIT_SPEED", "LOIT_SPEED_MS", 12.5);
        var rtl = Speed("RTL_SPEED", "RTL_SPEED_MS", 0);
        var up = Speed("PILOT_SPEED_UP", "PILOT_SPD_UP", 2.5);
        var dnRaw = Speed("PILOT_SPEED_DN", "PILOT_SPD_DN", 0);
        var dn = dnRaw > 0 ? dnRaw : up;
        var angle = param("ANGLE_MAX") is { } am ? am / 100 : param("ATC_ANGLE_MAX") ?? 30;
        var caution = Math.Max(loit, 1.25 * wp);
        var fence = (param("FENCE_ENABLE") ?? 0) > 0;
        var fenceType = (int)(param("FENCE_TYPE") ?? 0);
        return new VehicleProfile
        {
            Key = "multirotor", FixedWing = false, SpeedLabel = "GS",
            SpeedSpan = 20, SpeedMajor = 2, SpeedMinor = 1,
            Bands = new[] { new SpeedBand(0, wp, "var(--p-green)"), new SpeedBand(wp, caution, "var(--p-yellow)") },
            SpeedRefs = new[] { new RefMark("W", wp), new RefMark("L", loit), new RefMark("R", rtl > 0 ? rtl : wp) },
            Overspeed = 999, Caution = caution,
            VsRange = 5, VsMajor = 1, VsMinor = 0.5, VsMax = up, VsMin = -dn,
            VsMarks = new[] { new RefMark("LND", -Speed("LAND_SPEED", "LAND_SPD_MS", 0.5)) },
            AltSpan = 40, AltLabel = 10, AltMinor = 2,
            ExtremeRoll = angle + 15, ExtremePitchUp = angle + 15, ExtremePitchDown = angle + 15, ChevronUp = 60, ChevronDown = 60,
            SlipSkid = false, FpmMinSpeed = 1.5, CdiFullScale = 20, VdiDot = 1,
            AlertApproach1 = 10, AlertApproach2 = 3, AlertDeviation = 3,
            FloorShowWithin = 20, FloorWhiteWithin = 3, FloorClearAbove = 1.5,
            // FENCE_TYPE bit 0 = max altitude, bit 3 = min altitude
            FenceAltMax = fence && (fenceType & 1) != 0 ? Opt("FENCE_ALT_MAX") : null,
            FloorAlt = fence && (fenceType & 8) != 0 ? Opt("FENCE_ALT_MIN") : null,
            DefaultTargetSpeed = wp, FromParams = fromParams,
            MapRangeSteps = new double[] { 25, 50, 75, 100, 150, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000, 7500, 10000, 15000, 20000 },
            DefaultMapRange = 500, DefaultOrientation = "NORTH UP", TrackVectorSeconds = 30
        };
    }

    public static VehicleProfile Default(string key) => Build(key, _ => null, false);
}
