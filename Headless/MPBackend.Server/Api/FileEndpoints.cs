using System.Text;
using MPBackend.Core;
using MPBackend.Core.Files;
using MPBackend.Core.Jobs;
using MPBackend.Core.Params;

namespace MPBackend.Server.Api;

public sealed record ParamSetDto(double Value, bool Force = false);

public sealed record ParamWriteDto(Dictionary<string, double> Values);

public sealed record ParamWithMetadata(string Name, double Value, string Type, double? Default, ParamMetadata? Metadata);

public sealed record LogDownloadDto(ushort[] Ids, bool AllowArmed = false);

public sealed record FtpPathDto(string Path);

public sealed record FtpRenameDto(string From, string To);

public sealed record FtpCrcResult(string Path, uint Crc32);

/// <summary>
/// Parameters, MAVFTP and log endpoints (Milestone 2).
/// </summary>
public static class FileEndpoints
{
    public static void MapFileApi(this RouteGroupBuilder api)
    {
        MapParams(api.MapGroup("/vehicles/{sysid}/{compid}/params").WithTags("Parameters"));
        MapFtp(api.MapGroup("/vehicles/{sysid}/{compid}/ftp").WithTags("MAVFTP"));
        MapLogs(api);
    }

    private static void MapParams(RouteGroupBuilder p)
    {
        p.MapGet("/", (Backend b, uint sysid, byte compid, bool? metadata) =>
            {
                var values = b.Params.List(sysid, compid);
                if (metadata != true)
                    return Results.Ok(values);
                var meta = b.Params.GetMetadata(sysid, compid, values.Select(v => v.Name));
                return Results.Ok(values.Select(v =>
                    new ParamWithMetadata(v.Name, v.Value, v.Type, v.Default, meta.GetValueOrDefault(v.Name))));
            })
            .Produces<ParamWithMetadata[]>()
            .WithSummary("All parameters; metadata=true adds the apm.pdef.xml documentation for each");

        p.MapGet("/{name}", (Backend b, uint sysid, byte compid, string name) =>
                b.Params.Get(sysid, compid, name) is { } v ? Results.Ok(v) : Results.NotFound())
            .Produces<ParamValue>().Produces(404);

        p.MapPut("/{name}", (Backend b, uint sysid, byte compid, string name, ParamSetDto dto, CancellationToken ct) =>
            b.Params.SetAsync(sysid, compid, name, dto.Value, dto.Force, ct));

        p.MapPost("/write", (Backend b, uint sysid, byte compid, ParamWriteDto dto) =>
                Results.Accepted(null, b.Params.StartBulkSet(sysid, compid, dto.Values)))
            .Produces<Job>(202)
            .WithSummary("Write many parameters as a job (result lists failures and reboot-required names)");

        p.MapPost("/refresh", (Backend b, uint sysid, byte compid, bool? cache) =>
                Results.Accepted(null, b.Params.StartDownload(sysid, compid, cache == true)))
            .Produces<Job>(202)
            .WithSummary("Re-download the parameter list (bypasses the local cache unless cache=true)");

        p.MapGet("/metadata", (Backend b, uint sysid, byte compid, string? names) =>
            b.Params.GetMetadata(sysid, compid, Endpoints.SplitFields(names)));

        p.MapGet("/file", (Backend b, uint sysid, byte compid) =>
                Results.File(Encoding.UTF8.GetBytes(b.Params.ExportParamFile(sysid, compid)), "text/plain",
                    $"vehicle-{sysid}-{compid}-{DateTime.Now:yyyyMMdd-HHmmss}.param"))
            .Produces(200, typeof(string), "text/plain")
            .WithSummary("Download the parameters as a Mission Planner .param file");

        p.MapPost("/compare", async (Backend b, uint sysid, byte compid, HttpRequest request) =>
                b.Params.Compare(sysid, compid, ParamService.ParseParamFile(await ReadTextAsync(request))))
            .Accepts<string>("text/plain", "multipart/form-data")
            .WithSummary("Diff a .param file (raw body or multipart 'file') against the vehicle");

        p.MapPost("/parse", async (HttpRequest request) =>
                ParamService.ParseParamFile(await ReadTextAsync(request)))
            .Accepts<string>("text/plain", "multipart/form-data")
            .WithSummary("Parse a .param/.parm file into name/value pairs");
    }

    private static void MapFtp(RouteGroupBuilder f)
    {
        f.MapGet("/list", (Backend b, uint sysid, byte compid, string? path, CancellationToken ct) =>
            b.Ftp.ListAsync(sysid, compid, path ?? "/", ct));

        f.MapGet("/file", async (Backend b, uint sysid, byte compid, string path, CancellationToken ct) =>
        {
            var data = await b.Ftp.DownloadAsync(sysid, compid, path, null, ct);
            return Results.File(data, "application/octet-stream", Path.GetFileName(path.TrimEnd('/')));
        });

        f.MapPut("/file", async (Backend b, uint sysid, byte compid, string path, HttpRequest request,
            CancellationToken ct) =>
        {
            // MAVFtp needs a seekable stream with a known length
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            await b.Ftp.UploadAsync(sysid, compid, path, buffer, null, ct);
            return Results.NoContent();
        }).Accepts<byte[]>("application/octet-stream");

        f.MapDelete("/file", async (Backend b, uint sysid, byte compid, string path, CancellationToken ct) =>
        {
            await b.Ftp.DeleteAsync(sysid, compid, path, false, false, ct);
            return Results.NoContent();
        });

        f.MapDelete("/dir", async (Backend b, uint sysid, byte compid, string path, bool? recursive,
            CancellationToken ct) =>
        {
            await b.Ftp.DeleteAsync(sysid, compid, path, true, recursive == true, ct);
            return Results.NoContent();
        });

        f.MapPost("/mkdir", async (Backend b, uint sysid, byte compid, FtpPathDto dto, CancellationToken ct) =>
        {
            await b.Ftp.CreateDirectoryAsync(sysid, compid, dto.Path, ct);
            return Results.NoContent();
        });

        f.MapPost("/rename", async (Backend b, uint sysid, byte compid, FtpRenameDto dto, CancellationToken ct) =>
        {
            await b.Ftp.RenameAsync(sysid, compid, dto.From, dto.To, ct);
            return Results.NoContent();
        });

        f.MapGet("/crc", async (Backend b, uint sysid, byte compid, string path, CancellationToken ct) =>
            new FtpCrcResult(path, await b.Ftp.Crc32Async(sysid, compid, path, ct)));
    }

    private static void MapLogs(RouteGroupBuilder api)
    {
        var onboard = api.MapGroup("/vehicles/{sysid}/{compid}/logs").WithTags("Logs");

        onboard.MapGet("/", (Backend b, uint sysid, byte compid, CancellationToken ct) =>
                b.Logs.ListAsync(sysid, compid, ct))
            .WithSummary("Onboard dataflash logs (LOG_ENTRY)");

        onboard.MapPost("/download", (Backend b, uint sysid, byte compid, LogDownloadDto dto) =>
                Results.Accepted(null, b.Logs.StartDownload(sysid, compid, dto.Ids, dto.AllowArmed)))
            .Produces<Job>(202)
            .WithSummary("Download logs into the local log directory as a job; result lists the saved files");

        onboard.MapDelete("/", async (Backend b, uint sysid, byte compid, CancellationToken ct) =>
            {
                await b.Logs.EraseAllAsync(sysid, compid, ct);
                return Results.NoContent();
            })
            .WithSummary("Erase all onboard logs");

        var local = api.MapGroup("/logs/local").WithTags("Logs");

        local.MapGet("/", (Backend b) => b.Logs.ListLocal())
            .WithSummary("Downloaded .bin logs and recorded .tlog files in the backend's log directory");

        local.MapGet("/file", (string path) =>
        {
            var full = LogService.ResolveLocal(path);
            return Results.File(full, "application/octet-stream", Path.GetFileName(full), enableRangeProcessing: true);
        });
    }

    internal static async Task<string> ReadTextAsync(HttpRequest request)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault()
                ?? throw new ArgumentException("multipart request without a file");
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }

        using var body = new StreamReader(request.Body);
        return await body.ReadToEndAsync();
    }
}
