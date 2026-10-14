using MPBackend.Core.Vehicles;
using MPBackend.UI.Services;

namespace MPBackend.UI.Pfd;

/// <summary>Knob modes (spec behaviours.selectedValueEditing.modes).</summary>
public enum SelectedKind
{
    Hdg,
    Crs,
    Alt,
    Vs,
    Spd
}

/// <summary>
/// What the instruments draw for the operator-selected values: null = no selection (the bug follows the vehicle's
/// own target); Solid = the vehicle acknowledged it; otherwise the bug is hollow (reference / pending).
/// </summary>
public sealed record SelectedState(
    SelectedKind Mode,
    double? Hdg, bool HdgSolid,
    double? Crs,
    double? Alt, bool AltSolid,
    double? Vs,
    double? Spd, bool SpdSolid,
    bool Pending,
    bool Nack,
    string? Popup,
    string? Message)
{
    public static readonly SelectedState Empty = new(SelectedKind.Hdg, null, false, null, null, false, null, null, false, false, false, null, null);
}

/// <summary>
/// Selected-value editing (spec behaviours.selectedValueEditing, interaction.bezelMapping): one knob mode at a
/// time, turn = adjust, push = sync to the current value, commit with ENT / SEND or automatically after 1.5 s idle
/// in GUIDED. HDG / ALT / SPD go to the vehicle; CRS and V/S are local references. NACK flashes yellow and reverts.
/// </summary>
public sealed class SelectedValues : IDisposable
{
    private static readonly TimeSpan ModeRevert = TimeSpan.FromMilliseconds(10000);   // tokens.motion.knobModeRevertTimeout_ms
    private static readonly TimeSpan PopupLife = TimeSpan.FromMilliseconds(10000);    // tokens.motion.selectedValuePopupTimeout_ms
    private static readonly TimeSpan AutoCommit = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan NackFlash = TimeSpan.FromSeconds(3);

    private readonly GcsSession session;
    private readonly IGcs gcs;
    private readonly PfdFeed feed;
    private readonly object sync = new();
    private readonly Dictionary<SelectedKind, double> values = new();
    private readonly Dictionary<SelectedKind, double> committed = new();
    private readonly HashSet<SelectedKind> solid = new();
    private SelectedKind mode = SelectedKind.Hdg;
    private bool pending;
    private DateTime popupUntil, nackUntil, modeTouched;
    private string? message;
    private Timer? autoCommit, housekeeping;
    private bool committing;

    public SelectedValues(GcsSession session, IGcs gcs, PfdFeed feed)
    {
        this.session = session;
        this.gcs = gcs;
        this.feed = feed;
        session.Changed += OnSessionChanged;
        housekeeping = new Timer(_ => Tick(), null, 500, 500);
    }

    public event Action? Changed;

    public SelectedState State
    {
        get
        {
            lock (sync)
            {
                var now = DateTime.UtcNow;
                return new SelectedState(mode,
                    Get(SelectedKind.Hdg), solid.Contains(SelectedKind.Hdg),
                    Get(SelectedKind.Crs),
                    Get(SelectedKind.Alt), solid.Contains(SelectedKind.Alt),
                    Get(SelectedKind.Vs),
                    Get(SelectedKind.Spd), solid.Contains(SelectedKind.Spd),
                    pending, now < nackUntil,
                    now < popupUntil ? Popup() : null,
                    now < nackUntil ? message : null);
            }
        }
    }

    private double? Get(SelectedKind k) => values.TryGetValue(k, out var v) ? v : null;

    public SelectedKind Mode => mode;

    public void SetMode(SelectedKind kind)
    {
        lock (sync)
        {
            mode = kind;
            Touch();
        }

        Notify();
    }

    /// <summary>Knob turn: steps of one unit (heading °, metre, m/s, 0.1 m/s for V/S).</summary>
    public void Adjust(double steps)
    {
        lock (sync)
        {
            // a fresh edit starts from the bug the instrument shows (the vehicle's own target), not the live value
            var v = Get(mode) ?? Target(mode);
            v = mode switch
            {
                SelectedKind.Hdg or SelectedKind.Crs => Svg.Norm(Math.Round(v) + steps),
                SelectedKind.Alt => Math.Max(0, Math.Round(v) + steps),
                SelectedKind.Spd => Math.Max(0, Math.Round(v) + steps),
                SelectedKind.Vs => Math.Clamp(Math.Round(v * 10) / 10 + steps * 0.1, -10, 10),
                _ => v
            };
            Select(mode, v);
        }

        Notify();
    }

    /// <summary>Knob push: sync the selection to the current value (spec pushAction).</summary>
    public void Sync()
    {
        lock (sync)
            Select(mode, Current(mode));
        Notify();
    }

    /// <summary>ENT / SEND: send the pending selection to the vehicle (GUIDED only).</summary>
    public async Task CommitAsync()
    {
        SelectedKind kind;
        double value;
        lock (sync)
        {
            if (!pending || committing)
                return;
            kind = mode;
            if (!values.TryGetValue(kind, out value))
                return;
            committing = true;
        }

        try
        {
            var v = session.ActiveVehicle;
            var guided = (feed.State.Mode ?? "").StartsWith("Guided", StringComparison.OrdinalIgnoreCase);
            if (v == null || !guided)
            {
                Fail("GUIDED ONLY", kind, revert: false);
                return;
            }

            if (kind is SelectedKind.Crs or SelectedKind.Vs)
            {
                // local references (spec commands: CRS "local reference", V/S plane reference only)
                lock (sync)
                {
                    pending = false;
                    committed[kind] = value;
                    solid.Add(kind);
                }

                Notify();
                return;
            }

            var result = await gcs.SetGuidedTargetAsync(v.SysId, v.CompId, kind, value);
            if (result.Accepted)
            {
                lock (sync)
                {
                    pending = false;
                    committed[kind] = value;
                    solid.Add(kind);
                }

                Notify();
            }
            else
                Fail((result.Message ?? "REJECTED").ToUpperInvariant(), kind, revert: true);
        }
        catch (Exception ex)
        {
            Fail(ex.Message.ToUpperInvariant(), kind, revert: true);
        }
        finally
        {
            lock (sync)
                committing = false;
        }
    }

    /// <summary>Drop the selection of a kind so the bug follows the vehicle again.</summary>
    public void Clear(SelectedKind kind)
    {
        lock (sync)
        {
            values.Remove(kind);
            committed.Remove(kind);
            solid.Remove(kind);
            if (kind == mode)
                pending = false;
        }

        Notify();
    }

    // ---- internals -----------------------------------------------------------------------------------------

    private void Select(SelectedKind kind, double v)
    {
        values[kind] = v;
        solid.Remove(kind);
        pending = true;
        Touch();
        // auto-commit in GUIDED after 1.5 s idle (spec commit rule); otherwise the operator presses ENT / SEND
        autoCommit?.Dispose();
        autoCommit = (feed.State.Mode ?? "").StartsWith("Guided", StringComparison.OrdinalIgnoreCase)
            ? new Timer(_ => _ = CommitAsync(), null, AutoCommit, Timeout.InfiniteTimeSpan)
            : null;
    }

    private void Touch()
    {
        var now = DateTime.UtcNow;
        modeTouched = now;
        popupUntil = now + PopupLife;
    }

    private void Fail(string text, SelectedKind kind, bool revert)
    {
        lock (sync)
        {
            message = text;
            nackUntil = DateTime.UtcNow + NackFlash;
            if (revert)
            {
                if (committed.TryGetValue(kind, out var prev))
                    values[kind] = prev;
                else
                    values.Remove(kind);
                pending = false;
            }
        }

        Notify();
    }

    private double Current(SelectedKind kind)
    {
        var s = feed.State;
        return kind switch
        {
            SelectedKind.Hdg => s.Heading,
            SelectedKind.Crs => s.TargetBearing,
            SelectedKind.Alt => Math.Round(s.Alt),
            SelectedKind.Vs => Math.Round(s.ClimbRate * 10) / 10,
            SelectedKind.Spd => Math.Round(feed.Profile.FixedWing ? s.Airspeed : s.GroundSpeed),
            _ => 0
        };
    }

    private double Target(SelectedKind kind)
    {
        var s = feed.State;
        return kind switch
        {
            SelectedKind.Hdg => s.NavBearing != 0 ? s.NavBearing : s.Heading,
            SelectedKind.Alt => s.TargetAltValid ? Math.Round(s.TargetAlt) : Math.Round(s.Alt),
            SelectedKind.Spd => Math.Round(s.TargetSpeed),
            _ => Current(kind)
        };
    }

    private string Popup()
    {
        var v = Get(mode) ?? Target(mode);
        return mode switch
        {
            SelectedKind.Hdg => "HDG " + Svg.D3(v) + "°",
            SelectedKind.Crs => "CRS " + Svg.D3(v) + "°",
            SelectedKind.Alt => "ALT " + Svg.F(v, "0") + "m",
            SelectedKind.Vs => "V/S " + (v >= 0 ? "+" : "") + Svg.F(v, "0.0"),
            _ => "SPD " + Svg.F(v, "0") + "m/s"
        };
    }

    private void Tick()
    {
        var changed = false;
        lock (sync)
        {
            var now = DateTime.UtcNow;
            if (mode != SelectedKind.Hdg && !pending && now - modeTouched > ModeRevert)
            {
                mode = SelectedKind.Hdg;
                changed = true;
            }

            // popup and NACK flash expire on their own; the instruments poll State at 20 Hz anyway
            if (popupUntil != default && now > popupUntil)
            {
                popupUntil = default;
                changed = true;
            }
        }

        if (changed)
            Notify();
    }

    private void OnSessionChanged()
    {
        // new vehicle: drop everything
        var v = session.ActiveVehicle;
        if (v == null)
        {
            lock (sync)
            {
                values.Clear();
                committed.Clear();
                solid.Clear();
                pending = false;
            }

            Notify();
        }
    }

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        autoCommit?.Dispose();
        housekeeping?.Dispose();
    }
}
