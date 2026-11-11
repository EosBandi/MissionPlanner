using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.Server.Hosting;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var options = new BackendOptions();
configuration.GetSection("MPBackend").Bind(options);
if (string.IsNullOrWhiteSpace(options.DataDirectory))
    options.DataDirectory = new BackendOptions().DataDirectory;
options.DataDirectory = Path.GetFullPath(options.DataDirectory);

// create the backend eagerly so hooks/settings are ready before the host starts
var backend = new Backend(options);
// content root = install folder so wwwroot and appsettings.json are found whatever the current directory
var app = BackendWebApp.Create(backend, new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

foreach (var connection in configuration.GetSection("MPBackend:AutoConnect").Get<string[]>() ?? [])
{
    try
    {
        backend.Links.Connect(ConnectionRequest.Parse(connection));
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "auto-connect {Connection} failed", connection);
    }
}

app.Lifetime.ApplicationStopping.Register(backend.Dispose);

app.Run();

public partial class Program;
