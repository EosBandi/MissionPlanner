using Microsoft.AspNetCore.Diagnostics;
using MPBackend.Core;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;
using MPBackend.Server;
using MPBackend.Server.Api;
using MPBackend.Server.Ws;

var builder = WebApplication.CreateBuilder(args);

var options = new BackendOptions();
builder.Configuration.GetSection("MPBackend").Bind(options);
if (string.IsNullOrWhiteSpace(options.DataDirectory))
    options.DataDirectory = new BackendOptions().DataDirectory;
options.DataDirectory = Path.GetFullPath(options.DataDirectory);

builder.Services.AddSingleton(_ => new Backend(options));
builder.Services.AddSingleton<WsHub>();
builder.Services.ConfigureHttpJsonOptions(o => Json.Configure(o.SerializerOptions));
// Swashbuckle reads the MVC JSON options for schema generation (enums as strings, camelCase)
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o => Json.Configure(o.JsonSerializerOptions));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Mission Planner Headless Backend", Version = "v1" });
    // nested DTO types share short names (e.g. Status); keep schema ids unique
    c.CustomSchemaIds(t => t.FullName?.Replace("MPBackend.", "").Replace("+", ".").Replace("`1", "")
        .Replace("[[", "_").Split(',')[0] ?? t.Name);
    var xml = Path.Combine(AppContext.BaseDirectory, "MPBackend.Server.xml");
    if (File.Exists(xml))
        c.IncludeXmlComments(xml);
});

var app = builder.Build();

// map library exceptions to HTTP status codes
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = ex switch
    {
        VehicleNotFoundException or FileNotFoundException or KeyNotFoundException => StatusCodes.Status404NotFound,
        InvalidOperationException => StatusCodes.Status409Conflict,
        MPBackend.Core.Files.FtpException or TimeoutException or IOException => StatusCodes.Status502BadGateway,
        FormatException or ArgumentException or BadHttpRequestException => StatusCodes.Status400BadRequest,
        OperationCanceledException => StatusCodes.Status499ClientClosedRequest,
        _ => StatusCodes.Status500InternalServerError
    };
    await Results.Problem(detail: ex?.Message, statusCode: ctx.Response.StatusCode).ExecuteAsync(ctx);
}));

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
app.UseSwagger();
app.UseSwaggerUI();

app.Map("/ws", async (HttpContext ctx, WsHub hub) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
        return Results.BadRequest("WebSocket expected");
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    await hub.HandleAsync(socket, ctx.RequestAborted);
    return Results.Empty;
});

app.MapBackendApi();

// create the backend eagerly so hooks/settings are ready, then open configured links
var backend = app.Services.GetRequiredService<Backend>();
app.Services.GetRequiredService<WsHub>();
foreach (var connection in builder.Configuration.GetSection("MPBackend:AutoConnect").Get<string[]>() ?? [])
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
