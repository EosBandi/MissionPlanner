using System.Text.Json;
using System.Text.Json.Serialization;
using MPBackend.Core;

namespace MPBackend.Tray;

/// <summary>
/// The tray app's own settings, persisted as JSON in the local app data folder (independent of the backend's
/// config.xml so the data directory itself can be configured here).
/// </summary>
public sealed class TrayOptions
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MPBackend", "tray.json");

    /// <summary>Interface to bind: 0.0.0.0 (all, LAN access), 127.0.0.1 or a specific local address.</summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 5880;

    /// <summary>Backend data directory; null = the backend default (%LOCALAPPDATA%\MPBackend).</summary>
    public string? DataDirectory { get; set; }

    public bool RecordTlogs { get; set; } = true;

    /// <summary>log4net level for mpbackend.log: DEBUG, INFO, WARN, ERROR.</summary>
    public string LogLevel { get; set; } = "INFO";

    public bool StartMinimized { get; set; }

    public bool AutoStartServer { get; set; } = true;

    [JsonIgnore]
    public string Url => $"http://{ListenAddress}:{Port}";

    [JsonIgnore]
    public string EffectiveDataDirectory => string.IsNullOrWhiteSpace(DataDirectory)
        ? new BackendOptions().DataDirectory
        : Path.GetFullPath(DataDirectory);

    public BackendOptions ToBackendOptions() => new()
    {
        DataDirectory = EffectiveDataDirectory,
        RecordTlogs = RecordTlogs,
        LogLevel = LogLevel
    };

    /// <summary>True when <paramref name="other"/> differs in a setting the backend only reads at start-up.</summary>
    public bool NeedsRestart(TrayOptions other) =>
        !string.Equals(EffectiveDataDirectory, other.EffectiveDataDirectory, StringComparison.OrdinalIgnoreCase) ||
        RecordTlogs != other.RecordTlogs ||
        !string.Equals(LogLevel, other.LogLevel, StringComparison.OrdinalIgnoreCase);

    public TrayOptions Clone() => (TrayOptions)MemberwiseClone();

    public void CopyFrom(TrayOptions other)
    {
        ListenAddress = other.ListenAddress;
        Port = other.Port;
        DataDirectory = other.DataDirectory;
        RecordTlogs = other.RecordTlogs;
        LogLevel = other.LogLevel;
        StartMinimized = other.StartMinimized;
        AutoStartServer = other.AutoStartServer;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static TrayOptions Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<TrayOptions>(File.ReadAllText(FilePath), JsonOptions) ?? new TrayOptions();
        }
        catch (Exception ex)
        {
            LogBuffer.Add(new LogLine(DateTime.Now, "WARN", "Tray", $"could not read {FilePath}: {ex.Message}"));
        }
        return new TrayOptions();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
