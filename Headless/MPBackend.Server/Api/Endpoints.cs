using MissionPlanner.Utilities;
using MPBackend.Core;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Telemetry;
using MPBackend.Server.Ws;

namespace MPBackend.Server.Api;

public static class Endpoints
{
    public static void MapBackendApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        MapSystem(api);
        MapLinks(api.MapGroup("/links").WithTags("Links"));
        MapVehicles(api.MapGroup("/vehicles/{sysid}/{compid}").WithTags("Vehicles"));
        MapJobs(api.MapGroup("/jobs").WithTags("Jobs"));
        api.MapFileApi();
        api.MapMissionApi();
        api.MapControlApi();
        api.MapPlanningApi();
        api.MapSystemApi();
    }

    private static void MapSystem(RouteGroupBuilder api)
    {
        api.MapGet("/info", () => new BackendInfo(
                typeof(Endpoints).Assembly.GetName().Version?.ToString() ?? "",
                Settings.GetUserDataDirectory(),
                Settings.Instance.LogDir,
                WsHub.Topics))
            .WithTags("System");

        api.MapGet("/serialports", () => LinkManager.GetSerialPorts()).WithTags("System");

        api.MapGet("/telemetry/fields", () => TelemetryFields.All)
            .WithTags("Telemetry")
            .WithSummary("All CurrentState fields that can be read via /state or the telemetry WebSocket topic");

        api.MapGet("/vehicles", (Backend b) => b.Vehicles.List()).WithTags("Vehicles");

        api.MapPost("/params/metadata/update", (Backend b, bool? force) =>
                Results.Accepted(null, b.Params.StartMetadataUpdate(null, force == true)))
            .Produces<Job>(202)
            .WithTags("Parameters")
            .WithSummary("Refresh apm.pdef.xml for all vehicle types (cached 7 days unless force=true)");
    }

    private static void MapLinks(RouteGroupBuilder links)
    {
        links.MapGet("/", (Backend b) => b.Links.Links.Select(l => l.ToInfo()));

        links.MapGet("/{id:int}", (Backend b, int id) =>
                b.Links.Get(id) is { } link ? Results.Ok(link.ToInfo()) : Results.NotFound())
            .Produces<LinkInfo>().Produces(404);

        links.MapPost("/", (Backend b, ConnectDto dto) =>
            {
                var (link, job) = b.Links.Connect(dto.ToRequest());
                return Results.Accepted($"/api/v1/links/{link.Id}", new ConnectResponse(link.ToInfo(), job));
            })
            .Produces<ConnectResponse>(202)
            .WithSummary("Open a link; progress is reported through the returned job and the 'links' topic");

        links.MapDelete("/{id:int}", async (Backend b, int id) =>
                await b.Links.DisconnectAsync(id) ? Results.NoContent() : Results.NotFound())
            .Produces(204).Produces(404);
    }

    private static void MapVehicles(RouteGroupBuilder v)
    {
        v.MapGet("/", (Backend b, uint sysid, byte compid) => b.Vehicles.Get(sysid, compid));

        v.MapGet("/state", (Backend b, uint sysid, byte compid, string? fields) =>
                b.Vehicles.GetState(sysid, compid, SplitFields(fields)))
            .WithSummary("Snapshot of CurrentState; 'fields' is a comma separated list (all when omitted)");

        v.MapGet("/messages", (Backend b, uint sysid, byte compid) => b.Vehicles.GetMessages(sysid, compid));

        v.MapGet("/modes", (Backend b, uint sysid, byte compid) => b.Vehicles.GetModes(sysid, compid));

        v.MapPost("/mode", (Backend b, uint sysid, byte compid, ModeDto dto, CancellationToken ct) =>
            b.Vehicles.SetModeAsync(sysid, compid, dto.Mode, ct));

        v.MapPost("/arm", (Backend b, uint sysid, byte compid, ArmDto dto, CancellationToken ct) =>
            b.Vehicles.ArmAsync(sysid, compid, dto.Arm, dto.Force, ct));

        v.MapPost("/command", (Backend b, uint sysid, byte compid, CommandDto dto, CancellationToken ct) =>
            b.Vehicles.CommandAsync(sysid, compid, ParseCommand(dto.Command), dto.Params, dto.UseInt, dto.Frame,
                dto.RequireAck, ct));

        v.MapPost("/takeoff", (Backend b, uint sysid, byte compid, TakeoffDto dto, CancellationToken ct) =>
            b.Vehicles.TakeoffAsync(sysid, compid, dto.Altitude, ct));

        v.MapPost("/goto", (Backend b, uint sysid, byte compid, GotoDto dto, CancellationToken ct) =>
            b.Vehicles.GotoAsync(sysid, compid, dto.Lat, dto.Lng, dto.Alt, ct));

        v.MapPost("/reboot", (Backend b, uint sysid, byte compid, RebootDto dto, CancellationToken ct) =>
            b.Vehicles.RebootAsync(sysid, compid, dto.Bootloader, ct));
    }

    private static void MapJobs(RouteGroupBuilder jobs)
    {
        jobs.MapGet("/", (Backend b) => b.Jobs.List());

        jobs.MapGet("/{id}", (Backend b, string id) =>
                b.Jobs.Get(id) is { } job ? Results.Ok(job) : Results.NotFound())
            .Produces<Job>().Produces(404);

        jobs.MapDelete("/{id}", (Backend b, string id) =>
                b.Jobs.Cancel(id) ? Results.Accepted() : Results.NotFound())
            .Produces(202).Produces(404)
            .WithSummary("Cancel a running job");
    }

    internal static string[]? SplitFields(string? fields) =>
        string.IsNullOrWhiteSpace(fields)
            ? null
            : fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static MAVLink.MAV_CMD ParseCommand(string command)
    {
        var name = command?.Trim() ?? "";
        if (name.StartsWith("MAV_CMD_", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(8);
        if (Enum.TryParse<MAVLink.MAV_CMD>(name, true, out var cmd))
            return cmd;
        // MP's generator also drops the NAV_ prefix (MAV_CMD_NAV_TAKEOFF -> TAKEOFF)
        if (name.StartsWith("NAV_", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(name.Substring(4), true, out cmd))
            return cmd;
        throw new ArgumentException($"unknown MAV_CMD '{command}'");
    }
}
