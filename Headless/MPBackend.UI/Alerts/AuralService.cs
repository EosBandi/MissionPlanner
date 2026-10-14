using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MPBackend.UI.Alerts;

/// <summary>
/// Aural alerting (spec cas.levels aural, interaction.aural): master warning = triple high tone + voice, repeated
/// every 5 s until acknowledged; master caution = double tone + voice once; advisories and status are silent.
/// Altitude alerter: single tone on approach, double tone on deviation; floor: "Minimum, minimum".
/// Tones are synthesised in the WebView (audio.js); voice goes through <see cref="IVoice"/>.
/// </summary>
public sealed class AuralService : IDisposable
{
    private static readonly TimeSpan WarningRepeat = TimeSpan.FromSeconds(5);

    // spec interaction.aural.voiceMessages, keyed by alert id; everything else speaks its CAS text
    private static readonly Dictionary<string, string> Voice = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TERRAIN_CAUT"] = "Caution, terrain, terrain",
        ["TERRAIN_WARN"] = "Warning, terrain, terrain, pull up",
        ["TRAFFIC_TA"] = "Traffic, traffic",
        ["TRAFFIC_RA"] = "Traffic, traffic",
        ["BATT_LOW"] = "Battery low",
        ["BATT_CRIT"] = "Battery critical",
        ["LINK_LOST"] = "Telemetry lost",
        ["FENCE_BREACH"] = "Fence",
        ["FENCE_NEAR"] = "Fence",
        ["EKF_FS"] = "E K F failsafe",
        ["RC_FS"] = "Radio failsafe",
        ["GPS_LOST"] = "G P S fail"
    };

    private static readonly object[][] WarningTone = { new object[] { 1000, 150, 80 }, new object[] { 1000, 150, 80 }, new object[] { 1000, 150, 0 } };
    private static readonly object[][] CautionTone = { new object[] { 700, 200, 100 }, new object[] { 700, 200, 0 } };
    private static readonly object[][] SingleTone = { new object[] { 550, 300, 0 } };
    private static readonly object[][] DoubleTone = { new object[] { 550, 200, 120 }, new object[] { 550, 200, 0 } };

    private readonly AlertService alerts;
    private readonly IVoice voice;
    private readonly ILogger<AuralService> log;
    private readonly HashSet<string> announced = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim speaking = new(1, 1);
    private readonly Queue<string> speechQueue = new();
    private IJSObjectReference? audio;
    private Timer? repeat;
    private DateTime lastWarningRepeat;

    public AuralService(AlertService alerts, IVoice voice, ILogger<AuralService> log)
    {
        this.alerts = alerts;
        this.voice = voice;
        this.log = log;
        alerts.Changed += OnAlerts;
        repeat = new Timer(_ => RepeatWarnings(), null, 1000, 1000);
    }

    public event Action? Changed;

    public bool Muted { get; private set; }
    public bool Ready => audio != null;

    public void SetMuted(bool muted)
    {
        Muted = muted;
        Changed?.Invoke();
    }

    /// <summary>Called by the shell once the WebView is up: loads audio.js and arms the gesture-based resume.</summary>
    public async Task AttachAsync(IJSRuntime js)
    {
        if (audio != null)
            return;
        try
        {
            audio = await js.InvokeAsync<IJSObjectReference>("import", "./_content/MPBackend.UI/js/audio.js");
            await audio.InvokeAsync<bool>("prime");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "audio unavailable");
        }
    }

    // ---- altitude / floor alerters (called by the altitude tape on state changes) ---------------------------
    public void AltitudeApproach() => Play("altitude approach", SingleTone, null);
    public void AltitudeDeviation() => Play("altitude deviation", DoubleTone, null);
    public void Minimums() => Play("minimums", null, "Minimum, minimum");

    // ---- CAS ----------------------------------------------------------------------------------------------
    private void OnAlerts()
    {
        foreach (var a in alerts.Active)
        {
            if (a.Acknowledged || a.Level > AlertLevel.Caution)
                continue;
            var key = a.Id + "@" + a.Since.Ticks;
            if (!announced.Add(key))
                continue;
            // escalation re-arms the acknowledgement; a new key per raise keeps re-announcing correct
            var phrase = Phrase(a);
            if (a.Level == AlertLevel.Warning)
            {
                Play("warning " + a.Id, WarningTone, phrase);
                lastWarningRepeat = DateTime.UtcNow;
            }
            else
                Play("caution " + a.Id, CautionTone, phrase);
        }

        // forget keys of alerts that are gone so the set does not grow forever
        if (announced.Count > 200)
        {
            var live = alerts.Active.Select(a => a.Id + "@" + a.Since.Ticks).ToHashSet();
            announced.RemoveWhere(k => !live.Contains(k));
        }
    }

    /// <summary>Spec: warning aural repeats every 5 s until acknowledged.</summary>
    private void RepeatWarnings()
    {
        var now = DateTime.UtcNow;
        if (now - lastWarningRepeat < WarningRepeat)
            return;
        var top = alerts.Active.FirstOrDefault(a => !a.Acknowledged && a.Level == AlertLevel.Warning);
        if (top == null)
            return;
        lastWarningRepeat = now;
        Play("warning repeat " + top.Id, WarningTone, Phrase(top));
    }

    /// <summary>Spec voice message when there is one; STATUSTEXT alerts speak the full message, others their CAS text.</summary>
    private static string Phrase(ActiveAlert a) =>
        Voice.TryGetValue(a.Id, out var v) ? v
        : a.Id.StartsWith("MSG:", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(a.Description) ? a.Description.ToLowerInvariant()
        : a.Text.ToLowerInvariant();

    private void Play(string what, object[][]? tone, string? phrase)
    {
        if (Muted)
            return;
        log.LogInformation("aural: {What}{Phrase}", what, phrase == null ? "" : " \"" + phrase + "\"");
        _ = PlayAsync(tone, phrase);
    }

    private async Task PlayAsync(object[][]? tone, string? phrase)
    {
        try
        {
            if (tone != null && audio != null)
            {
                var state = await audio.InvokeAsync<string>("play", tone, 1.0);
                if (state != "running")
                    log.LogWarning("aural: audio context {State} (needs a user gesture first)", state);
            }

            if (phrase != null)
            {
                lock (speechQueue)
                {
                    if (speechQueue.Contains(phrase))
                        return;
                    speechQueue.Enqueue(phrase);
                }

                await speaking.WaitAsync();
                try
                {
                    string next;
                    lock (speechQueue)
                        next = speechQueue.Dequeue();
                    await voice.SpeakAsync(next);
                }
                finally
                {
                    speaking.Release();
                }
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "aural playback failed");
        }
    }

    public void Dispose()
    {
        alerts.Changed -= OnAlerts;
        repeat?.Dispose();
    }
}
