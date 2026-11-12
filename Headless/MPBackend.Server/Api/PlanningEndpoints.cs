using System.Text;
using MPBackend.Core;
using MPBackend.Core.Missions;
using MPBackend.Core.Planning;

namespace MPBackend.Server.Api;

/// <summary>
/// Mission planning that needs no vehicle: the spray grid generator ported from the SprayGrid-Soleon plugin.
/// </summary>
public static class PlanningEndpoints
{
    public static void MapPlanningApi(this RouteGroupBuilder api)
    {
        var spray = api.MapGroup("/planning/spraygrid").WithTags("Planning");

        spray.MapPost("/preview", (Backend b, SprayGridRequest dto) => b.Planning.Preview(dto))
            .Produces<SprayGridPreview>().Produces(400)
            .WithSummary("Spray strips for a polygon (S/E points) with area, distance and time statistics; no terrain")
            .WithDescription("Fast enough to call on every parameter change. Points are tagged S (strip start) and E " +
                             "(strip end); split marks the first point of a new time segment. Errors in the request " +
                             "(polygon < 3 vertices, missing home, non-positive distance/speed) return 400.");

        spray.MapPost("/mission", async (Backend b, SprayGridRequest dto, string? format, CancellationToken ct) =>
            {
                var result = await b.Planning.BuildMissionAsync(dto, ct);
                if (string.IsNullOrEmpty(format))
                    return Results.Ok(result);
                var fmt = Enum.TryParse<MissionFileFormat>(format, true, out var f)
                    ? f
                    : throw new ArgumentException($"format must be waypoints, plan or kml (got '{format}')");
                var text = MissionFiles.Write(fmt, result.Mission);
                var mime = fmt switch
                {
                    MissionFileFormat.Plan => "application/json",
                    MissionFileFormat.Kml => "application/vnd.google-earth.kml+xml",
                    _ => "text/plain"
                };
                return Results.File(Encoding.UTF8.GetBytes(text), mime, "spraygrid" + MissionFiles.Extension(fmt));
            })
            .Produces<SprayMissionResult>().Produces(400)
            .WithSummary("Build the spray mission (sprayer script messages, takeoff/RTL, speed, splits, altitude tracking)")
            .WithDescription("Without ?format the MissionPlan is returned as JSON together with the grid points, " +
                             "statistics, segment start sequence numbers and warnings; with ?format=waypoints|plan|kml " +
                             "the mission file is returned as an attachment. elevationSource Backend waits up to " +
                             "terrainWait seconds for SRTM tiles; Raster uses the heights in the request; None applies " +
                             "no terrain correction.");
    }
}
