namespace MPBackend.UI.Pfd;

/// <summary>One row of spec <c>ardupilot.fma</c>: lateral / vertical annunciations for an ArduPilot mode.</summary>
public sealed record FmaMode(string Lat, string Vert, string? ArmedLat, string? ArmedVert, string Authority, string? StatusColor);

/// <summary>What the FMA shows: active (green) and armed (white) lateral / vertical fields plus control authority.</summary>
public sealed record FmaFields(string LateralActive, string LateralArmed, string VerticalActive, string VerticalArmed,
    string Authority, bool StatusCaution);

public static partial class FmaTable
{
    // Modes lives in the generated partial; static initialiser order across partials is unspecified, so resolve lazily
    private static readonly Lazy<Dictionary<string, Dictionary<string, FmaMode>>> normalised = new(BuildNormalised);

    private static Dictionary<string, Dictionary<string, FmaMode>> BuildNormalised()
    {
        var result = new Dictionary<string, Dictionary<string, FmaMode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (veh, modes) in Modes)
        {
            var map = new Dictionary<string, FmaMode>();
            foreach (var (mode, m) in modes)
                map[Norm(mode)] = m;
            result[veh] = map;
        }

        return result;
    }

    /// <summary>MP spells modes "AltHold" / "SmartRTL"; the spec uses ArduPilot's ALT_HOLD / SMART_RTL.</summary>
    private static string Norm(string mode) => mode.Replace("_", "").Replace(" ", "").ToUpperInvariant();

    /// <summary>
    /// Resolve the FMA fields for a mode. "AUTO*" entries are derived from the mission: without the current
    /// mission item (later step) they show NAV / ALT with the landing phase armed, as the design guide's renderer does.
    /// </summary>
    public static FmaFields Resolve(bool plane, string? mode)
    {
        var veh = plane ? "plane" : "copter";
        var m = mode != null && normalised.Value[veh].TryGetValue(Norm(mode), out var found)
            ? found
            : new FmaMode((mode ?? "").ToUpperInvariant(), "", null, null, "RC", null);
        var auto = m.Lat == "AUTO*";
        var latActive = auto ? "NAV" : m.Lat;
        var vertActive = auto ? "ALT" : m.Vert.Split('/')[0];
        var latArmed = auto ? (plane ? "APR" : "") : m.ArmedLat ?? "";
        var vertArmed = auto ? "LAND" : m.ArmedVert?.Split(' ')[0] ?? "";
        return new FmaFields(latActive, latArmed, vertActive, vertArmed, m.Authority, m.StatusColor != null);
    }
}
