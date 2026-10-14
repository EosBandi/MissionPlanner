using MPBackend.UI.Alerts;

namespace MPBackend.App;

/// <summary>Voice messages through the platform text-to-speech (WinRT on Windows, Android TTS).</summary>
internal sealed class MauiVoice : IVoice
{
    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        var options = new SpeechOptions { Pitch = 1.0f, Volume = 1.0f };
        await TextToSpeech.Default.SpeakAsync(text, options, ct);
    }
}
