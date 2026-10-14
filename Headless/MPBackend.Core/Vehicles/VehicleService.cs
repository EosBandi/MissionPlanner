using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Utilities;
using MPBackend.Core.Links;
using MPBackend.Core.Telemetry;

namespace MPBackend.Core.Vehicles
{
    public sealed class VehicleNotFoundException : Exception
    {
        public VehicleNotFoundException(uint sysid, byte compid)
            : base($"vehicle {sysid}/{compid} not found")
        {
        }
    }

    public sealed record VehicleInfo(
        int LinkId,
        uint SysId,
        byte CompId,
        string Type,
        string Autopilot,
        string Firmware,
        string Version,
        string Mode,
        bool Armed,
        int LinkQuality,
        DateTime LastPacket,
        int ParamsReceived,
        int ParamsTotal);

    public sealed record FlightMode(int Number, string Name);

    public sealed record StatusTextEntry(DateTime Time, string Text);

    public sealed record CommandResult(bool Accepted, string Message = null);

    /// <summary>
    /// Vehicle-level actions (MP's FlightData action buttons, mode/arm controls, guided "fly to here").
    /// </summary>
    public sealed class VehicleService
    {
        private readonly LinkManager links;

        public VehicleService(LinkManager links)
        {
            this.links = links;
        }

        public IReadOnlyList<VehicleInfo> List() =>
            links.Links.Where(l => l.State == LinkState.Connected)
                .SelectMany(l => l.Vehicles.Select(m => ToInfo(l, m)))
                .ToList();

        public VehicleInfo Get(uint sysid, byte compid)
        {
            var (link, mav) = Resolve(sysid, compid);
            return ToInfo(link, mav);
        }

        public Dictionary<string, object> GetState(uint sysid, byte compid, IEnumerable<string> fields = null) =>
            TelemetryFields.Snapshot(Resolve(sysid, compid).mav.cs, fields);

        public IReadOnlyList<StatusTextEntry> GetMessages(uint sysid, byte compid)
        {
            var cs = Resolve(sysid, compid).mav.cs;
            // MP appends to this list from the reader thread without locking; retry if it changes under us
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return cs.messages.ToArray()
                        .Select(m => new StatusTextEntry(m.time.ToUniversalTime(), m.message)).ToList();
                }
                catch (ArgumentException) when (attempt < 5)
                {
                }
                catch (InvalidOperationException) when (attempt < 5)
                {
                }
            }
        }

        public IReadOnlyList<FlightMode> GetModes(uint sysid, byte compid) =>
            Common.getModesList(Resolve(sysid, compid).mav.cs.firmware)
                .Select(kv => new FlightMode(kv.Key, kv.Value)).ToList();

        /// <summary>
        /// Change flight mode and wait (up to 3 s) for the heartbeat to confirm it.
        /// </summary>
        public async Task<CommandResult> SetModeAsync(uint sysid, byte compid, string mode,
            CancellationToken ct = default)
        {
            var (link, mav) = Resolve(sysid, compid);
            var target = GetModes(sysid, compid)
                .FirstOrDefault(m => string.Equals(m.Name, mode, StringComparison.OrdinalIgnoreCase));
            if (target == null)
                return new CommandResult(false, $"unknown mode '{mode}'");

            await link.RunExclusiveAsync(m =>
            {
                m.setMode(sysid, compid, target.Name);
                return true;
            }, ct).ConfigureAwait(false);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (string.Equals(mav.cs.mode, target.Name, StringComparison.OrdinalIgnoreCase))
                    return new CommandResult(true);
                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            return new CommandResult(false, $"mode is still {mav.cs.mode}");
        }

        public async Task<CommandResult> ArmAsync(uint sysid, byte compid, bool arm, bool force,
            CancellationToken ct = default)
        {
            var (link, _) = Resolve(sysid, compid);
            var ok = await link.RunExclusiveAsync(m => m.doARMAsync(sysid, compid, arm, force), ct)
                .ConfigureAwait(false);
            return new CommandResult(ok, ok ? null : "rejected (check status messages for the reason)");
        }

        /// <summary>
        /// Send COMMAND_LONG (or COMMAND_INT when <paramref name="useInt"/>, with x/y as degE7) and wait for the ack.
        /// </summary>
        public async Task<CommandResult> CommandAsync(uint sysid, byte compid, MAVLink.MAV_CMD command,
            float[] p, bool useInt = false, MAVLink.MAV_FRAME frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
            bool requireAck = true, CancellationToken ct = default)
        {
            var (link, _) = Resolve(sysid, compid);
            p = (p ?? Array.Empty<float>()).Concat(Enumerable.Repeat(0f, 7)).Take(7).ToArray();

            var ok = await link.RunExclusiveAsync(m => useInt
                    ? m.doCommandIntAsync(sysid, compid, command, p[0], p[1], p[2], p[3], (int)p[4], (int)p[5],
                        p[6], requireAck, null, frame)
                    : m.doCommandAsync(sysid, compid, command, p[0], p[1], p[2], p[3], p[4], p[5], p[6],
                        requireAck), ct)
                .ConfigureAwait(false);
            return new CommandResult(ok);
        }

        public Task<CommandResult> TakeoffAsync(uint sysid, byte compid, float altitude,
            CancellationToken ct = default) =>
            CommandAsync(sysid, compid, MAVLink.MAV_CMD.TAKEOFF, new[] { 0f, 0, 0, 0, 0, 0, altitude }, ct: ct);

        public Task<CommandResult> RebootAsync(uint sysid, byte compid, bool bootloader,
            CancellationToken ct = default) =>
            CommandAsync(sysid, compid, MAVLink.MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN, new[] { bootloader ? 3f : 1f },
                requireAck: false, ct: ct);

        /// <summary>
        /// Guided "fly to here" (switches to GUIDED if needed). Altitude in metres relative to home.
        /// </summary>
        public async Task<CommandResult> GotoAsync(uint sysid, byte compid, double lat, double lng, float alt,
            CancellationToken ct = default)
        {
            var (link, _) = Resolve(sysid, compid);
            var wp = new Locationwp
            {
                lat = lat,
                lng = lng,
                alt = alt,
                frame = (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT
            };

            await link.RunExclusiveAsync(m =>
            {
                m.setGuidedModeWP(sysid, compid, wp);
                return true;
            }, ct).ConfigureAwait(false);
            return new CommandResult(true);
        }

        public (Link link, MAVState mav) Resolve(uint sysid, byte compid)
        {
            var (link, mav) = links.FindVehicle(sysid, compid);
            if (link == null)
                throw new VehicleNotFoundException(sysid, compid);
            return (link, mav);
        }

        private static VehicleInfo ToInfo(Link link, MAVState m) => new VehicleInfo(
            link.Id,
            m.sysid,
            m.compid,
            m.aptype.ToString(),
            m.apname.ToString(),
            m.cs.firmware.ToString(),
            m.VersionString ?? m.cs.version?.ToString(),
            m.cs.mode,
            m.cs.armed,
            m.cs.linkqualitygcs,
            m.lastvalidpacket.ToUniversalTime(),
            m.param.TotalReceived,
            m.param.TotalReported);
    }
}
