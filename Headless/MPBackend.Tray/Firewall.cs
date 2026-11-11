using System.ComponentModel;
using System.Diagnostics;

namespace MPBackend.Tray;

/// <summary>
/// Adds an inbound Windows Firewall rule for this executable and the API port (elevated, one UAC prompt).
/// Binding to all interfaces otherwise relies on the firewall prompt Windows shows on first listen.
/// </summary>
public static class Firewall
{
    public const string RuleName = "MPBackend Tray (headless GCS API)";

    public static void AddRule(int port)
    {
        var exe = Environment.ProcessPath ?? Application.ExecutablePath;
        // delete a stale rule first so repeated use does not pile up duplicates; one elevation for both
        var script =
            $"netsh advfirewall firewall delete rule name=\"{RuleName}\" >nul & " +
            $"netsh advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP " +
            $"localport={port} program=\"{exe}\"";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c {script}")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit(30000);
            if (process is { HasExited: true, ExitCode: 0 })
                LogBuffer.Add("INFO", "Tray", $"firewall rule '{RuleName}' added for TCP {port}");
            else
                LogBuffer.Add("WARN", "Tray", $"netsh exited with {process?.ExitCode}; firewall rule not added");
        }
        catch (Win32Exception)
        {
            // UAC prompt declined
            LogBuffer.Add("WARN", "Tray", "firewall rule not added (elevation declined)");
        }
        catch (Exception ex)
        {
            LogBuffer.Add("WARN", "Tray", $"firewall rule not added: {ex.Message}");
        }
    }
}
