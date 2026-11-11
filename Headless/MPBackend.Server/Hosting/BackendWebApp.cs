using Microsoft.AspNetCore.Diagnostics;
using MPBackend.Core;
using MPBackend.Core.Vehicles;
using MPBackend.Server.Api;
using MPBackend.Server.Ws;

namespace MPBackend.Server.Hosting;

/// <summary>
/// Builds the REST + WebSocket host around an existing <see cref="Backend"/>. Shared by the console server
/// (<c>Program.cs</c>) and any other process that wants to serve the API in-process (the tray app).
/// </summary>
public static class BackendWebApp
{
    /// <summary>
    /// Create a configured but not yet started <see cref="WebApplication"/>: JSON options, CORS, Swagger,
    /// exception mapping, static files from <c>wwwroot</c>, the <c>/ws</c> endpoint and the <c>/api/v1</c> API.
    /// </summary>
    /// <param name="backend">The backend to expose; its lifetime stays with the caller.</param>
    /// <param name="options">Host options (command line args, content root); defaults when null.</param>
    /// <param name="configure">Optional hook run before <c>Build()</c>, e.g. to set URLs or add logging.</param>
    public static WebApplication Create(Backend backend, WebApplicationOptions? options = null,
        Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options ?? new WebApplicationOptions());

        builder.Services.AddSingleton(backend);
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

        configure?.Invoke(builder);

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

        // wire the hub to backend events now, before the first link is opened
        app.Services.GetRequiredService<WsHub>();

        return app;
    }
}
