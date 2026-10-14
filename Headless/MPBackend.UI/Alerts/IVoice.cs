namespace MPBackend.UI.Alerts;

/// <summary>Text-to-speech for the voice messages; the host app supplies the platform implementation.</summary>
public interface IVoice
{
    Task SpeakAsync(string text, CancellationToken ct = default);
}

/// <summary>No speech (tones only).</summary>
public sealed class NullVoice : IVoice
{
    public Task SpeakAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
}
