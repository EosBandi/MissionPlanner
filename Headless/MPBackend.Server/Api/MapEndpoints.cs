using System.Text;
using GMap.NET;
using MPBackend.Core;
using MPBackend.Core.Missions;
using MPBackend.Core.Terrain;

namespace MPBackend.Server.Api;

public sealed record SetCurrentDto(ushort Seq);

/// <summary>Export body: any of the three lists; .waypoints uses the first one present.</summary>
public sealed record MissionExportDto(MissionPlan? Mission, MissionPlan? Fence, MissionPlan? Rally);

public sealed record ProfileDto(List<LatLng> Path, double Spacing = 30, double Wait = 10);

public sealed record PrefetchDto(string Provider, double North, double West, double South, double East, int MinZoom,
    int MaxZoom);

public sealed record MapModeDto(AccessMode Mode);

public sealed record UploadResult(int Uploaded);

public sealed record AcceptedResult(bool Accepted);

public sealed record GeoidResult(double Lat, double Lng, double Undulation);

/// <summary>
/// Missions/fence/rally, mission files, terrain and map tiles (Milestone 3).
/// </summary>
public static class MapEndpoints
{
    public static void MapMissionApi(this RouteGroupBuilder api)
    {
        MapMissions(api);
        MapTerrain(api.MapGroup("/terrain").WithTags("Terrain"));
        MapTiles(api.MapGroup("/maps").WithTags("Maps"));
    }

    private static void MapMissions(RouteGroupBuilder api)
    {
        var m = api.MapGroup("/vehicles/{sysid}/{compid}/missions/{type}").WithTags("Missions");

        m.MapGet("/", async (Backend b, uint sysid, byte compid, string type, string? format,
                CancellationToken ct) =>
            {
                var plan = await b.Missions.DownloadAsync(sysid, compid, ParseType(type), ct);
                if (string.IsNullOrEmpty(format))
                    return Results.Ok(plan);
                return FileResult(ParseFormat(format), plan.Type == MAVLink.MAV_MISSION_TYPE.MISSION ? plan : null,
                    plan.Type == MAVLink.MAV_MISSION_TYPE.FENCE ? plan : null,
                    plan.Type == MAVLink.MAV_MISSION_TYPE.RALLY ? plan : null, $"{type}-{sysid}");
            })
            .Produces<MissionPlan>()
            .WithSummary("Download mission|fence|rally from the vehicle (JSON, or a file with ?format=waypoints|plan|kml)");

        m.MapPut("/", async (Backend b, uint sysid, byte compid, string type, MissionPlan plan, CancellationToken ct) =>
            {
                plan.Type = ParseType(type);
                return new UploadResult(await b.Missions.UploadAsync(sysid, compid, plan, ct));
            })
            .WithSummary("Upload a list; for missions 'home' is optional (the vehicle's home is used)");

        m.MapPost("/file", async (Backend b, uint sysid, byte compid, string type, string? filename,
                HttpRequest request, CancellationToken ct) =>
            {
                var t = ParseType(type);
                var content = MissionFiles.Parse(filename ?? FormFileName(request), await FileEndpoints.ReadTextAsync(request));
                var plan = content.Plans.FirstOrDefault(p => p.Type == t)
                           ?? throw new ArgumentException($"file contains no {type} items");
                return new UploadResult(await b.Missions.UploadAsync(sysid, compid, plan, ct));
            })
            .Accepts<string>("text/plain", "multipart/form-data")
            .WithSummary("Upload a .waypoints/.plan/.kml file (raw body or multipart 'file') to the vehicle");

        m.MapDelete("/", async (Backend b, uint sysid, byte compid, string type, CancellationToken ct) =>
            {
                await b.Missions.ClearAsync(sysid, compid, ParseType(type), ct);
                return Results.NoContent();
            })
            .Produces(204)
            .WithSummary("Clear the list on the vehicle (missions keep home)");

        api.MapPost("/vehicles/{sysid}/{compid}/missions/current", async (Backend b, uint sysid, byte compid,
                SetCurrentDto dto, CancellationToken ct) =>
            new AcceptedResult(await b.Missions.SetCurrentAsync(sysid, compid, dto.Seq, ct)))
            .WithTags("Missions")
            .WithSummary("Set the active mission item (MISSION_SET_CURRENT)");

        var files = api.MapGroup("/missions").WithTags("Missions");

        files.MapPost("/parse", async (string? filename, HttpRequest request) =>
                MissionFiles.Parse(filename ?? FormFileName(request), await FileEndpoints.ReadTextAsync(request)))
            .Accepts<string>("text/plain", "multipart/form-data")
            .WithSummary("Parse a .waypoints (QGC WPL 110), .plan (QGC JSON) or .kml file");

        files.MapPost("/export", (string format, MissionExportDto dto) =>
                FileResult(ParseFormat(format), dto.Mission, dto.Fence, dto.Rally, "mission"))
            .WithSummary("Write lists as a .waypoints, .plan or .kml file");
    }

    private static void MapTerrain(RouteGroupBuilder t)
    {
        t.MapGet("/elevation", (Backend b, double lat, double lng, double? wait, CancellationToken ct) =>
                b.Terrain.GetElevationAsync(lat, lng, TimeSpan.FromSeconds(wait ?? 5), ct))
            .WithSummary("Terrain height AMSL; waits up to 'wait' seconds (default 5) for the SRTM tile to download");

        t.MapPost("/profile", (Backend b, ProfileDto dto, CancellationToken ct) =>
                b.Terrain.GetProfileAsync(dto.Path, dto.Spacing, TimeSpan.FromSeconds(dto.Wait), ct))
            .WithSummary("Terrain heights sampled along a path every 'spacing' metres");

        t.MapGet("/geoid", (double lat, double lng) =>
                new GeoidResult(lat, lng, TerrainService.GeoidUndulation(lat, lng)))
            .WithSummary("EGM96 geoid height above the WGS84 ellipsoid (metres)");

        t.MapGet("/heightmap/{level}/{x}/{y}", async (Backend b, int level, int x, int y, int? size, double? wait,
                HttpResponse response, CancellationToken ct) =>
            {
                var n = size ?? 65;
                if (level < 0 || level > TerrainService.MaxHeightmapLevel || x < 0 || x >= (2 << level) ||
                    y < 0 || y >= (1 << level))
                    return Results.Problem("level/x/y outside the geographic tiling scheme", statusCode: 400);
                if (n < TerrainService.MinHeightmapSize || n > TerrainService.MaxHeightmapSize)
                    return Results.Problem(
                        $"size must be {TerrainService.MinHeightmapSize}..{TerrainService.MaxHeightmapSize}",
                        statusCode: 400);
                var heights = await b.Terrain.GetHeightmapAsync(level, x, y, n, TimeSpan.FromSeconds(wait ?? 30), ct);
                var bytes = new byte[heights.Length * sizeof(float)];
                Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
                response.Headers.CacheControl = "no-store";
                return Results.Bytes(bytes, "application/octet-stream");
            })
            .Produces(200, contentType: "application/octet-stream")
            .ProducesProblem(400)
            .WithSummary("Heightmap tile for a 3D map: size*size little-endian float32 heights in metres above the " +
                         "WGS84 ellipsoid (SRTM + EGM96 undulation), row 0 north, column 0 west, edges inclusive. " +
                         "Tile grid = Cesium GeographicTilingScheme (2x1 at level 0). Waits up to 'wait' seconds " +
                         "(default 30) for SRTM downloads; unknown and ocean heights are 0, and tiles coarser than level " +
                         $"{TerrainService.MinSampledLevel} are flat.");
    }

    private static void MapTiles(RouteGroupBuilder maps)
    {
        maps.MapGet("/providers", (Backend b) => b.Tiles.Providers());

        maps.MapGet("/tiles/{provider}/{z:int}/{x:long}/{y:long}", async (Backend b, HttpContext ctx, string provider,
                int z, long x, long y) =>
            {
                var tile = await Task.Run(() => b.Tiles.GetTile(provider, z, x, y), ctx.RequestAborted);
                if (tile == null)
                    return Results.NotFound();
                ctx.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Bytes(tile.Data, tile.ContentType);
            })
            .Produces(200, typeof(byte[]), "image/png", "image/jpeg").Produces(404)
            .WithSummary("XYZ tile (e.g. Leaflet: /api/v1/maps/tiles/GoogleSatelliteMap/{z}/{x}/{y})");

        maps.MapPost("/prefetch", (Backend b, PrefetchDto dto) =>
                Results.Accepted(null, b.Tiles.StartPrefetch(dto.Provider, dto.North, dto.West, dto.South, dto.East,
                    dto.MinZoom, dto.MaxZoom)))
            .Produces<MPBackend.Core.Jobs.Job>(202)
            .WithSummary("Download all tiles in an area/zoom range into the cache (for offline use)");

        maps.MapGet("/mode", (Backend b) => new MapModeDto(b.Tiles.Mode));

        maps.MapPut("/mode", (Backend b, MapModeDto dto) =>
            {
                b.Tiles.Mode = dto.Mode;
                return new MapModeDto(b.Tiles.Mode);
            })
            .WithSummary("ServerAndCache, CacheOnly (offline) or ServerOnly");
    }

    private static MAVLink.MAV_MISSION_TYPE ParseType(string type) => type?.ToLowerInvariant() switch
    {
        "mission" => MAVLink.MAV_MISSION_TYPE.MISSION,
        "fence" => MAVLink.MAV_MISSION_TYPE.FENCE,
        "rally" => MAVLink.MAV_MISSION_TYPE.RALLY,
        _ => throw new ArgumentException($"mission type must be mission, fence or rally (got '{type}')")
    };

    private static MissionFileFormat ParseFormat(string format) =>
        Enum.TryParse<MissionFileFormat>(format, true, out var f)
            ? f
            : throw new ArgumentException($"format must be waypoints, plan or kml (got '{format}')");

    private static IResult FileResult(MissionFileFormat format, MissionPlan? mission, MissionPlan? fence,
        MissionPlan? rally, string name)
    {
        var text = MissionFiles.Write(format, mission, fence, rally);
        var mime = format switch
        {
            MissionFileFormat.Plan => "application/json",
            MissionFileFormat.Kml => "application/vnd.google-earth.kml+xml",
            _ => "text/plain"
        };
        return Results.File(Encoding.UTF8.GetBytes(text), mime, name + MissionFiles.Extension(format));
    }

    private static string? FormFileName(HttpRequest request) =>
        request.HasFormContentType ? request.Form.Files.FirstOrDefault()?.FileName : null;
}
