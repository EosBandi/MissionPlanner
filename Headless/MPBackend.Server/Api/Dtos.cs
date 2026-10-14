using MPBackend.Core.Jobs;
using MPBackend.Core.Links;

namespace MPBackend.Server.Api;

/// <summary>
/// Open a link either with a compact <see cref="Connection"/> string (tcp:127.0.0.1:5760, udp:14550,
/// udpcl:192.168.1.10:14550, COM3:115200, ws:ws://host/path) or with the explicit fields.
/// </summary>
public sealed class ConnectDto
{
    public string? Connection { get; set; }
    public LinkType? Type { get; set; }
    public string? Port { get; set; }
    public string? Host { get; set; }
    public int? Baud { get; set; }
    public string? Url { get; set; }
    public bool FetchParams { get; set; } = true;

    public ConnectionRequest ToRequest()
    {
        var req = !string.IsNullOrWhiteSpace(Connection)
            ? ConnectionRequest.Parse(Connection)
            : new ConnectionRequest
            {
                Type = Type ?? throw new ArgumentException("either connection or type is required"),
                Port = Port,
                Host = Host,
                Url = Url,
                Baud = Baud ?? 115200
            };
        req.FetchParams = FetchParams;
        return req;
    }
}

public sealed record ConnectResponse(LinkInfo Link, Job Job);

public sealed record ModeDto(string Mode);

public sealed record ArmDto(bool Arm = true, bool Force = false);

/// <param name="Command">MAV_CMD name as in the MAVLink spec ("MAV_CMD_NAV_TAKEOFF") or MP's short form ("TAKEOFF"), or number.</param>
/// <param name="Params">Up to 7 params; missing ones are 0. For COMMAND_INT params 5/6 are lat/lon in degE7.</param>
public sealed record CommandDto(
    string Command,
    float[]? Params,
    bool UseInt = false,
    MAVLink.MAV_FRAME Frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
    bool RequireAck = true);

public sealed record TakeoffDto(float Altitude);

/// <param name="Alt">Metres relative to home.</param>
public sealed record GotoDto(double Lat, double Lng, float Alt);

public sealed record RebootDto(bool Bootloader = false);

public sealed record BackendInfo(string Version, string DataDirectory, string LogDirectory, IReadOnlyList<string> Topics);
