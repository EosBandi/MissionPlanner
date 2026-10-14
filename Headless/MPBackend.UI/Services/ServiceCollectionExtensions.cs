using Microsoft.Extensions.DependencyInjection;
using MPBackend.UI.Shell;

namespace MPBackend.UI.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the UI services on top of an already registered <c>MPBackend.Core.Backend</c> singleton.
    /// </summary>
    public static IServiceCollection AddMpBackendUI(this IServiceCollection services)
    {
        services.AddSingleton<IGcs>(sp => new InProcessGcs(sp.GetRequiredService<MPBackend.Core.Backend>()));
        services.AddSingleton<GcsSession>();
        services.AddSingleton<ShellState>();
        services.AddSingleton<Pfd.PfdFeed>();
        services.AddSingleton<Pfd.SelectedValues>();
        services.AddSingleton<Alerts.AlertService>();
        services.AddSingleton<Map.TileCache>();
        services.AddSingleton<Map.MissionStore>();
        return services;
    }
}
