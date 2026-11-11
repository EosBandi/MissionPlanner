using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MPBackend.Core;
using MPBackend.Server.Hosting;

namespace MPBackend.Tray;

public enum ServerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Failed
}

/// <summary>
/// Owns the backend (one per process, lives until exit) and the Kestrel host serving the API, which can be
/// stopped and started again, e.g. after the listen address or port changed.
/// </summary>
public sealed class ServerHost : IDisposable
{
    private WebApplication? app;
    private readonly SemaphoreSlim gate = new(1, 1);

    public ServerHost(BackendOptions options)
    {
        Backend = new Backend(options);
        BufferAppender.Attach();
        LogBuffer.Add("INFO", "Tray", $"backend ready, data directory {options.DataDirectory}");
    }

    public Backend Backend { get; }

    public ServerState State { get; private set; } = ServerState.Stopped;

    public string? Error { get; private set; }

    public DateTime? StartedAt { get; private set; }

    /// <summary>Address the host is bound to while running (e.g. 0.0.0.0).</summary>
    public string Address { get; private set; } = "";

    public int Port { get; private set; }

    /// <summary>Raised on backend / host threads whenever <see cref="State"/> changes.</summary>
    public event Action? Changed;

    public async Task StartAsync(string address, int port)
    {
        await gate.WaitAsync();
        try
        {
            if (app != null)
                return;

            SetState(ServerState.Starting);
            var url = $"http://{address}:{port}";
            try
            {
                var host = BackendWebApp.Create(Backend,
                    // content root = install folder so wwwroot (dev console) is found whatever the current directory
                    new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory },
                    builder =>
                    {
                        builder.WebHost.UseUrls(url);
                        builder.Logging.ClearProviders();
                        builder.Logging.AddProvider(new BufferLoggerProvider());
                        // library exceptions are mapped to 4xx/5xx by BackendWebApp; the middleware's own
                        // "unhandled exception" error line would only confuse in the status window
                        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
                    });
                host.Urls.Clear();
                host.Urls.Add(url);
                await host.StartAsync();

                app = host;
                Address = address;
                Port = port;
                StartedAt = DateTime.Now;
                Error = null;
                SetState(ServerState.Running);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                LogBuffer.Add("ERROR", "Tray", $"could not start the API on {url}: {ex.Message}");
                SetState(ServerState.Failed);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (app == null)
                return;

            SetState(ServerState.Stopping);
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await app.StopAsync(timeout.Token);
                await app.DisposeAsync();
            }
            catch (Exception ex)
            {
                LogBuffer.Add("WARN", "Tray", $"stopping the API: {ex.Message}");
            }
            app = null;
            StartedAt = null;
            SetState(ServerState.Stopped);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>URL for opening pages from this machine (loopback when bound to all interfaces).</summary>
    public string LocalUrl(int port) =>
        $"http://{(IsAnyAddress(Address) || Address == "" ? "127.0.0.1" : Address)}:{port}";

    /// <summary>
    /// URLs clients can use: the bound address, or when bound to all interfaces, loopback plus every local
    /// IPv4 address of an interface that is up.
    /// </summary>
    public IReadOnlyList<string> ClientUrls(string address, int port)
    {
        if (!IsAnyAddress(address))
            return [$"http://{address}:{port}"];

        var urls = new List<string> { $"http://127.0.0.1:{port}" };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        urls.Add($"http://{ip.Address}:{port}");
            }
        }
        catch (Exception ex)
        {
            LogBuffer.Add("WARN", "Tray", $"listing network interfaces: {ex.Message}");
        }
        return urls;
    }

    public static bool IsAnyAddress(string address) =>
        address == "0.0.0.0" || address == "*" || address == "+" ||
        (IPAddress.TryParse(address, out var ip) && (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)));

    private void SetState(ServerState state)
    {
        State = state;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            LogBuffer.Add("WARN", "Tray", $"stop on exit: {ex.Message}");
        }
        // closes the links and saves the MP settings
        Backend.Dispose();
    }
}
