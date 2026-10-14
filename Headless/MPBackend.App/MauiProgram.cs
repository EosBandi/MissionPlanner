using System.Globalization;
using Microsoft.Extensions.Logging;
using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.UI.Services;

namespace MPBackend.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // instrument readouts use dot decimals and tabular digits whatever the device locale
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();

        // one backend per process; app-private data directory (Android: /data/data/<app>/files)
        var options = new BackendOptions
        {
            DataDirectory = Path.Combine(FileSystem.AppDataDirectory, "MPBackend"),
            // development aid: point at an existing Mission Planner / backend gmapcache to reuse its tiles
            MapCacheDirectory = Environment.GetEnvironmentVariable("MPGCS_MAPCACHE")
        };
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => new Backend(options));
        builder.Services.AddMpBackendUI();

        builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(options.DataDirectory, "logs", "app.log")));
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // development aids: MPGCS_AUTOCONNECT=tcp:127.0.0.1:5760 opens a link at startup;
        // MPGCS_DEV_FLY=<alt m> then flies a short GUIDED pattern on the SITL once its parameters are in
        var auto = Environment.GetEnvironmentVariable("MPGCS_AUTOCONNECT");
        if (!string.IsNullOrWhiteSpace(auto))
        {
            var backend = app.Services.GetRequiredService<Backend>();
            var log = app.Services.GetRequiredService<ILogger<App>>();
            try
            {
                backend.Links.Connect(ConnectionRequest.Parse(auto));
                if (double.TryParse(Environment.GetEnvironmentVariable("MPGCS_DEV_FLY"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var alt))
                    _ = DevFlight.RunAsync(backend, (float)alt, log);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "auto-connect {Connection} failed", auto);
            }
        }

        return app;
    }
}
