using MPBackend.Core;
using MPBackend.Core.Control;

namespace MPBackend.Server.Api;

public sealed record CompassStartDto(byte Mask = 0, bool Retry = true, bool Autosave = true, float Delay = 0,
    bool Autoreboot = false);

public sealed record FixedYawDto(float Yaw);

/// <summary>
/// Calibration and manual control (Milestone 4).
/// </summary>
public static class ControlEndpoints
{
    public static void MapControlApi(this RouteGroupBuilder api)
    {
        var cal = api.MapGroup("/vehicles/{sysid}/{compid}/calibration").WithTags("Calibration");

        cal.MapGet("/", (Backend b, uint sysid, byte compid) =>
                b.Calibration.GetStatus(sysid, compid) is { } s ? Results.Ok(s) : Results.NoContent())
            .Produces<MPBackend.Core.Calibration.CalibrationStatus>().Produces(204)
            .WithSummary("Current/last calibration of this vehicle (live updates on the 'calibration' topic)");

        cal.MapPost("/accel/start", (Backend b, uint sysid, byte compid, CancellationToken ct) =>
                b.Calibration.StartAccelAsync(sysid, compid, ct))
            .WithSummary("6-position accel calibration; follow 'position' and call accel/next when placed");

        cal.MapPost("/accel/next", (Backend b, uint sysid, byte compid) => b.Calibration.AccelNext(sysid, compid))
            .WithSummary("Confirm the vehicle is in the requested position");

        cal.MapPost("/preflight/{type}", (Backend b, uint sysid, byte compid, string type, CancellationToken ct) =>
                b.Calibration.PreflightAsync(sysid, compid, type, ct))
            .WithSummary("One-shot: level, simpleaccel, gyro, baro or airspeed");

        cal.MapPost("/compass/start", (Backend b, uint sysid, byte compid, CompassStartDto? dto, CancellationToken ct) =>
            {
                dto ??= new CompassStartDto();
                return b.Calibration.StartCompassAsync(sysid, compid, dto.Mask, dto.Retry, dto.Autosave, dto.Delay,
                    dto.Autoreboot, ct);
            })
            .WithSummary("Onboard compass calibration (rotate the vehicle); progress per compass");

        cal.MapPost("/compass/accept", (Backend b, uint sysid, byte compid, CancellationToken ct) =>
            b.Calibration.CompassAcceptAsync(sysid, compid, ct));

        cal.MapPost("/compass/fixed-yaw", (Backend b, uint sysid, byte compid, FixedYawDto dto, CancellationToken ct) =>
                b.Calibration.CompassFixedYawAsync(sysid, compid, dto.Yaw, ct))
            .WithSummary("Large vehicle mag cal from a known true heading (GPS lock required)");

        cal.MapPost("/radio/start", (Backend b, uint sysid, byte compid) => b.Calibration.StartRadio(sysid, compid))
            .WithSummary("Capture RC min/max; move all sticks and switches to their extremes");

        cal.MapPost("/radio/finish", (Backend b, uint sysid, byte compid, CancellationToken ct) =>
                b.Calibration.RadioFinishAsync(sysid, compid, ct))
            .WithSummary("With sticks centred and throttle down: save RCx_MIN/MAX/TRIM");

        cal.MapPost("/cancel", (Backend b, uint sysid, byte compid, CancellationToken ct) =>
            b.Calibration.CancelAsync(sysid, compid, ct));

        var mc = api.MapGroup("/vehicles/{sysid}/{compid}/manual-control").WithTags("Manual control");

        mc.MapPost("/", (Backend b, uint sysid, byte compid, ManualControlInput input) =>
                b.ManualControl.Update(sysid, compid, input))
            .WithSummary("Feed input (prefer the WebSocket 'manual_control' op for continuous streams); " +
                         "released automatically 500 ms after the last input");

        mc.MapGet("/", (Backend b, uint sysid, byte compid) =>
                b.ManualControl.GetStatus(sysid, compid) is { } s ? Results.Ok(s) : Results.NoContent())
            .Produces<ManualControlStatus>().Produces(204);

        mc.MapDelete("/", (Backend b, uint sysid, byte compid) =>
                b.ManualControl.Stop(sysid, compid) ? Results.NoContent() : Results.NotFound())
            .WithSummary("Stop and release all RC overrides");
    }
}
