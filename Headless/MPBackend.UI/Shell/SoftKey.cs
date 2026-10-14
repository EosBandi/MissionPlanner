namespace MPBackend.UI.Shell;

/// <summary>
/// One slot of a 12-key soft-key bar (pfd.softkeys rules). <see cref="Critical"/> keys need a 1 s hold with a
/// fill bar; ordinary keys act on click. <see cref="Alert"/> is "w" / "c" for the flashing ALERTS key.
/// </summary>
public sealed record SoftKey(
    string Label,
    Func<Task>? Action = null,
    bool Selected = false,
    bool Disabled = false,
    bool Critical = false,
    string? Alert = null)
{
    public const int Slots = 12;

    public static readonly SoftKey Empty = new("");

    public bool IsEmpty => Label.Length == 0;

    public static SoftKey Simple(string label, Action action, bool selected = false) =>
        new(label, () =>
        {
            action();
            return Task.CompletedTask;
        }, selected);

    public static SoftKey Off(string label) => new(label, Disabled: true);

    /// <summary>
    /// Build a fixed 12-slot bar: missing slots are blank keys so positions never move (muscle memory rule).
    /// ALERTS is always the right-most key; pass it as the last element or leave it to be added.
    /// </summary>
    public static IReadOnlyList<SoftKey> Bar(params SoftKey?[] keys)
    {
        var bar = new SoftKey[Slots];
        for (var i = 0; i < Slots; i++)
            bar[i] = i < keys.Length && keys[i] != null ? keys[i]! : Empty;
        if (bar[Slots - 1].IsEmpty)
            bar[Slots - 1] = Alerts;
        return bar;
    }

    /// <summary>Placeholder until the alerts window exists (step 3).</summary>
    public static SoftKey Alerts { get; set; } = new("ALERTS", Disabled: true);
}
