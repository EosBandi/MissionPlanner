namespace MPBackend.UI.Alerts;

/// <summary>CAS levels (spec cas.levels), highest first.</summary>
public enum AlertLevel
{
    Warning,
    Caution,
    Advisory,
    Status
}

/// <summary>One row of the spec's alert table (ardupilot.alerts).</summary>
public sealed record AlertDef(string Id, AlertLevel Level, string Text, string? Trigger, string? Action, string? AvionicsAnalog);

public static partial class AlertTable
{
    private static readonly Lazy<Dictionary<string, AlertDef>> byId =
        new(() => All.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase));

    public static AlertDef? Get(string id) => byId.Value.TryGetValue(id, out var d) ? d : null;
}

/// <summary>Flight phases (spec flightPhases) used for CAS inhibits and the status strip chip.</summary>
public enum FlightPhase
{
    Preflight,
    Armed,
    Takeoff,
    Enroute,
    Return,
    Landing,
    Postflight
}

/// <summary>An alert that is (or was) active.</summary>
public sealed class ActiveAlert
{
    public required string Id { get; init; }
    public required AlertLevel Level { get; set; }
    /// <summary>CAS text, max 18 characters, upper case (spec cas.window.format).</summary>
    public required string Text { get; set; }
    public string? Description { get; set; }
    public string? Action { get; set; }
    public DateTime Since { get; init; } = DateTime.UtcNow;
    public DateTime? ClearedAt { get; set; }
    public bool Acknowledged { get; set; }
    /// <summary>Suppressed by a flight-phase inhibit; stays in the list but is not shown.</summary>
    public bool Inhibited { get; set; }
    /// <summary>Event-type alerts (STATUSTEXT) have no persisting condition and expire on their own.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive => ClearedAt == null;
}
