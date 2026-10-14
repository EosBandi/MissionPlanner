using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MPBackend.Core.Links;
using MPBackend.Core.Params;
using MPBackend.Core.Telemetry;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Calibration
{
    public enum CalibrationKind
    {
        Accel,
        Compass,
        Radio
    }

    public enum CalibrationState
    {
        Running,
        /// <summary>Accel: the vehicle asked for a position; call next once it is in place.</summary>
        WaitingForUser,
        Succeeded,
        Failed,
        Cancelled
    }

    public sealed record CompassProgress(byte Id, int Percent, string Status, int Attempt);

    public sealed record CompassReport(byte Id, string Status, float Fitness, float OfsX, float OfsY, float OfsZ,
        bool Autosaved);

    public sealed record RadioChannel(int Channel, int Value, int Min, int Max);

    /// <summary>
    /// Live state of a calibration (also published on the "calibration" topic).
    /// </summary>
    public sealed record CalibrationStatus(
        CalibrationKind Kind,
        uint SysId,
        byte CompId,
        CalibrationState State,
        string Message,
        string Position,
        IReadOnlyList<CompassProgress> Compasses,
        IReadOnlyList<CompassReport> Reports,
        IReadOnlyList<RadioChannel> Channels,
        DateTime Updated);

    /// <summary>
    /// Sensor/RC calibration flows from MP's setup screens (ConfigAccelerometerCalibration, ConfigHWCompass2,
    /// ConfigRadioInput), driven through the API instead of buttons and message boxes.
    /// One calibration per vehicle at a time.
    /// </summary>
    public sealed class CalibrationService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(CalibrationService));

        private readonly VehicleService vehicles;
        private readonly ParamService parameters;
        private readonly EventHub events;
        private readonly ConcurrentDictionary<(uint, byte), Session> sessions = new();

        public CalibrationService(VehicleService vehicles, ParamService parameters, EventHub events)
        {
            this.vehicles = vehicles;
            this.parameters = parameters;
            this.events = events;
        }

        public CalibrationStatus GetStatus(uint sysid, byte compid) =>
            sessions.TryGetValue((sysid, compid), out var s) ? s.ToStatus() : null;

        // ---------------- accelerometer ----------------

        /// <summary>
        /// Start the 6-position accel calibration (PREFLIGHT_CALIBRATION param5=1). The vehicle then asks for
        /// each position via COMMAND_LONG ACCELCAL_VEHICLE_POS; answer with <see cref="AccelNextAsync"/>.
        /// </summary>
        public async Task<CalibrationStatus> StartAccelAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var (link, _) = vehicles.Resolve(sysid, compid);
            var session = Begin(new AccelSession(this, link, sysid, compid));
            try
            {
                session.Subscribe(MAVLink.MAVLINK_MSG_ID.STATUSTEXT, MAVLink.MAVLINK_MSG_ID.COMMAND_LONG);
                var ok = await link.RunExclusiveAsync(m => m.doCommandAsync(sysid, compid,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 1, 0, 0), ct).ConfigureAwait(false);
                if (!ok)
                    session.Finish(CalibrationState.Failed, "vehicle rejected PREFLIGHT_CALIBRATION");
                return session.ToStatus();
            }
            catch
            {
                session.Finish(CalibrationState.Failed, "failed to start");
                throw;
            }
        }

        /// <summary>Confirm the vehicle is in the requested position.</summary>
        public CalibrationStatus AccelNext(uint sysid, byte compid)
        {
            if (!(Current(sysid, compid) is AccelSession s) || s.Pos == 0)
                throw new InvalidOperationException("no accel calibration is waiting for a position");
            s.Confirm();
            return s.ToStatus();
        }

        /// <summary>
        /// One-shot calibrations: level (accel trims), simple accel, gyro, baro, airspeed.
        /// </summary>
        public async Task<CommandResult> PreflightAsync(uint sysid, byte compid, string type,
            CancellationToken ct = default)
        {
            // PREFLIGHT_CALIBRATION: p1 gyro, p3 baro/ground pressure, p5 accel (2 = level, 4 = simple), p6 airspeed
            var p = new float[7];
            switch (type?.ToLowerInvariant())
            {
                case "level": p[4] = 2; break;
                case "simpleaccel": p[4] = 4; break;
                case "gyro": p[0] = 1; break;
                case "baro": p[2] = 1; break;
                case "airspeed": p[5] = 2; break;
                default: throw new ArgumentException("type must be level, simpleaccel, gyro, baro or airspeed");
            }

            return await vehicles.CommandAsync(sysid, compid, MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, p, ct: ct)
                .ConfigureAwait(false);
        }

        // ---------------- compass ----------------

        /// <summary>
        /// Onboard compass calibration (DO_START_MAG_CAL); progress via MAG_CAL_PROGRESS / MAG_CAL_REPORT.
        /// </summary>
        public async Task<CalibrationStatus> StartCompassAsync(uint sysid, byte compid, byte mask = 0,
            bool retry = true, bool autosave = true, float delay = 0, bool autoreboot = false,
            CancellationToken ct = default)
        {
            var (link, _) = vehicles.Resolve(sysid, compid);
            var session = Begin(new CompassSession(this, link, sysid, compid, retry));
            try
            {
                session.Subscribe(MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS, MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT);
                var ok = await link.RunExclusiveAsync(m => m.doCommandAsync(sysid, compid,
                        MAVLink.MAV_CMD.DO_START_MAG_CAL, mask, retry ? 1 : 0, autosave ? 1 : 0, delay,
                        autoreboot ? 1 : 0, 0, 0), ct)
                    .ConfigureAwait(false);
                if (!ok)
                    session.Finish(CalibrationState.Failed, "vehicle rejected DO_START_MAG_CAL");
                return session.ToStatus();
            }
            catch
            {
                session.Finish(CalibrationState.Failed, "failed to start");
                throw;
            }
        }

        /// <summary>Accept the results (needed when autosave is off).</summary>
        public async Task<CalibrationStatus> CompassAcceptAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var s = Current(sysid, compid) as CompassSession
                    ?? throw new InvalidOperationException("no compass calibration in progress");
            await vehicles.CommandAsync(sysid, compid, MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, new[] { 0f, 0, 1 }, ct: ct)
                .ConfigureAwait(false);
            s.Finish(CalibrationState.Succeeded, "accepted; reboot the autopilot");
            return s.ToStatus();
        }

        /// <summary>
        /// Large vehicle mag cal: the compass offsets are computed from a known true heading (needs GPS lock).
        /// </summary>
        public Task<CommandResult> CompassFixedYawAsync(uint sysid, byte compid, float yawDeg,
            CancellationToken ct = default) =>
            vehicles.CommandAsync(sysid, compid, MAVLink.MAV_CMD.FIXED_MAG_CAL_YAW, new[] { yawDeg }, ct: ct);

        // ---------------- radio ----------------

        /// <summary>
        /// Start capturing RC min/max: move all sticks and switches to their extremes, then
        /// <see cref="RadioFinishAsync"/> with sticks centred and throttle down.
        /// </summary>
        public CalibrationStatus StartRadio(uint sysid, byte compid)
        {
            var (link, _) = vehicles.Resolve(sysid, compid);
            var session = Begin(new RadioSession(this, link, sysid, compid));
            return session.ToStatus();
        }

        /// <summary>
        /// Take the current positions as trims and write RCx_MIN/MAX/TRIM for every channel that moved.
        /// </summary>
        public async Task<CalibrationStatus> RadioFinishAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var s = Current(sysid, compid) as RadioSession
                    ?? throw new InvalidOperationException("no radio calibration in progress");
            await s.FinishAsync(parameters, ct).ConfigureAwait(false);
            return s.ToStatus();
        }

        // ---------------- common ----------------

        public async Task<CalibrationStatus> CancelAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var s = Current(sysid, compid) ?? throw new InvalidOperationException("no calibration in progress");
            if (s is CompassSession)
                await vehicles.CommandAsync(sysid, compid, MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, new[] { 0f, 0, 1 },
                    ct: ct).ConfigureAwait(false);
            s.Finish(CalibrationState.Cancelled, "cancelled");
            return s.ToStatus();
        }

        private Session Current(uint sysid, byte compid) =>
            sessions.TryGetValue((sysid, compid), out var s) && !s.Done ? s : null;

        private T Begin<T>(T session) where T : Session
        {
            var key = (session.SysId, session.CompId);
            if (sessions.TryGetValue(key, out var existing) && !existing.Done)
                throw new InvalidOperationException($"{existing.Kind} calibration already in progress");
            sessions[key] = session;
            session.Start();
            session.Publish();
            return session;
        }

        // ---------------- sessions ----------------

        private abstract class Session
        {
            private readonly List<int> subscriptions = new List<int>();
            protected readonly CalibrationService Owner;
            protected readonly Link Link;

            protected Session(CalibrationService owner, Link link, uint sysid, byte compid, CalibrationKind kind)
            {
                Owner = owner;
                Link = link;
                SysId = sysid;
                CompId = compid;
                Kind = kind;
            }

            public uint SysId { get; }
            public byte CompId { get; }
            public CalibrationKind Kind { get; }
            public CalibrationState State { get; protected set; } = CalibrationState.Running;
            public string Message { get; protected set; }
            public DateTime Updated { get; private set; } = DateTime.UtcNow;
            public bool Done => State is CalibrationState.Succeeded or CalibrationState.Failed or
                CalibrationState.Cancelled;

            public virtual void Start()
            {
            }

            public void Subscribe(params MAVLink.MAVLINK_MSG_ID[] ids)
            {
                foreach (var id in ids)
                    subscriptions.Add(Link.Mav.SubscribeToPacketType(id, msg =>
                    {
                        try
                        {
                            if (!Done)
                                OnPacket(msg);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"{Kind} calibration packet", ex);
                        }

                        return true;
                    }, SysId, CompId));
            }

            protected virtual void OnPacket(MAVLink.MAVLinkMessage msg)
            {
            }

            public void Finish(CalibrationState state, string message)
            {
                if (Done)
                    return;
                State = state;
                Message = message;
                foreach (var id in subscriptions)
                    Link.Mav.UnSubscribeToPacketType(id);
                subscriptions.Clear();
                OnFinished();
                Publish();
            }

            protected virtual void OnFinished()
            {
            }

            public void Publish()
            {
                Updated = DateTime.UtcNow;
                Owner.events.Publish(Topics.Calibration, ToStatus());
            }

            public virtual CalibrationStatus ToStatus() => new CalibrationStatus(Kind, SysId, CompId, State, Message,
                null, null, null, null, Updated);
        }

        private sealed class AccelSession : Session
        {
            public AccelSession(CalibrationService owner, Link link, uint sysid, byte compid)
                : base(owner, link, sysid, compid, CalibrationKind.Accel)
            {
                Message = "starting";
            }

            public MAVLink.ACCELCAL_VEHICLE_POS Pos { get; private set; }

            public void Confirm()
            {
                // MP answers the vehicle's position request by echoing it back as a COMMAND_LONG
                Link.Mav.sendPacket(new MAVLink.mavlink_command_long_t
                {
                    target_system = (byte)SysId,
                    target_component = CompId,
                    command = (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS,
                    param1 = (float)Pos
                }, SysId, CompId);
                State = CalibrationState.Running;
                Message = $"confirmed {Pos}";
                Publish();
            }

            protected override void OnPacket(MAVLink.MAVLinkMessage msg)
            {
                if (msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.STATUSTEXT)
                {
                    var text = Decode(msg.ToStructure<MAVLink.mavlink_statustext_t>().text);
                    var lower = text.ToLowerInvariant();
                    if (lower.Contains("calibration successful"))
                        Finish(CalibrationState.Succeeded, text);
                    else if (lower.Contains("calibration failed") || lower.Contains("calibration cancelled"))
                        Finish(CalibrationState.Failed, text);
                    else if (lower.Contains("place vehicle") || lower.Contains("calibration"))
                    {
                        Message = text;
                        Publish();
                    }
                }
                else if (msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG)
                {
                    var cmd = msg.ToStructure<MAVLink.mavlink_command_long_t>();
                    if (cmd.command != (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS)
                        return;
                    var pos = (MAVLink.ACCELCAL_VEHICLE_POS)(int)cmd.param1;
                    if (pos == MAVLink.ACCELCAL_VEHICLE_POS.SUCCESS)
                        Finish(CalibrationState.Succeeded, "calibration successful");
                    else if ((int)cmd.param1 > (int)MAVLink.ACCELCAL_VEHICLE_POS.SUCCESS)
                        Finish(CalibrationState.Failed, "calibration failed");
                    else
                    {
                        Pos = pos;
                        State = CalibrationState.WaitingForUser;
                        Message = "Please place vehicle " + pos;
                        Publish();
                    }
                }
            }

            public override CalibrationStatus ToStatus() => new CalibrationStatus(Kind, SysId, CompId, State, Message,
                Pos == 0 ? null : Pos.ToString(), null, null, null, Updated);
        }

        private sealed class CompassSession : Session
        {
            private readonly bool retry;
            private readonly ConcurrentDictionary<byte, CompassProgress> progress = new();
            private readonly ConcurrentDictionary<byte, CompassReport> reports = new();
            private DateTime lastPublish = DateTime.MinValue;

            public CompassSession(CalibrationService owner, Link link, uint sysid, byte compid, bool retry)
                : base(owner, link, sysid, compid, CalibrationKind.Compass)
            {
                this.retry = retry;
                Message = "rotate the vehicle around all axes";
            }

            protected override void OnPacket(MAVLink.MAVLinkMessage msg)
            {
                if (msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS)
                {
                    var p = msg.ToStructure<MAVLink.mavlink_mag_cal_progress_t>();
                    progress[p.compass_id] = new CompassProgress(p.compass_id, p.completion_pct,
                        ((MAVLink.MAG_CAL_STATUS)p.cal_status).ToString(), p.attempt);
                    // progress arrives fast; publish at most 5 Hz
                    if (DateTime.UtcNow - lastPublish > TimeSpan.FromMilliseconds(200))
                    {
                        lastPublish = DateTime.UtcNow;
                        Publish();
                    }
                }
                else if (msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT)
                {
                    var r = msg.ToStructure<MAVLink.mavlink_mag_cal_report_t>();
                    var status = (MAVLink.MAG_CAL_STATUS)r.cal_status;
                    reports[r.compass_id] = new CompassReport(r.compass_id, status.ToString(), r.fitness, r.ofs_x,
                        r.ofs_y, r.ofs_z, r.autosaved == 1);

                    // all compasses that reported progress have a final report
                    var all = progress.Keys.All(reports.ContainsKey);
                    if (all && reports.Values.All(x => x.Status == nameof(MAVLink.MAG_CAL_STATUS.MAG_CAL_SUCCESS)))
                        Finish(CalibrationState.Succeeded,
                            reports.Values.All(x => x.Autosaved)
                                ? "calibration saved; reboot the autopilot"
                                : "calibration complete; accept to save");
                    else if (all && !retry)
                        Finish(CalibrationState.Failed, "calibration failed: " +
                                                        string.Join(", ", reports.Values.Select(x => $"{x.Id} {x.Status}")));
                    else
                        Publish();
                }
            }

            public override CalibrationStatus ToStatus() => new CalibrationStatus(Kind, SysId, CompId, State, Message,
                null, progress.Values.OrderBy(p => p.Id).ToList(), reports.Values.OrderBy(r => r.Id).ToList(), null,
                Updated);
        }

        private sealed class RadioSession : Session
        {
            private const int Channels = 16;
            private readonly int[] min = Enumerable.Repeat(3000, Channels).ToArray();
            private readonly int[] max = new int[Channels];
            private readonly int[] value = new int[Channels];
            private Timer timer;
            private int ticks;
            private byte oldRate;

            public RadioSession(CalibrationService owner, Link link, uint sysid, byte compid)
                : base(owner, link, sysid, compid, CalibrationKind.Radio)
            {
                Message = "move all sticks and switches to their extremes";
            }

            public override void Start()
            {
                var cs = Link.FindMav(SysId, CompId)?.cs;
                oldRate = (byte)(cs?.raterc ?? 2);
                try
                {
                    Link.Mav.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 10, SysId, CompId);
                }
                catch (Exception ex)
                {
                    log.Warn("RC stream request", ex);
                }

                timer = new Timer(_ => Sample(), null, 100, 100);
            }

            private void Sample()
            {
                var cs = Link.FindMav(SysId, CompId)?.cs;
                if (cs == null || Done)
                    return;
                var snap = TelemetryFields.Snapshot(cs, Enumerable.Range(1, Channels).Select(c => $"ch{c}in"));
                lock (min)
                {
                    for (var c = 0; c < Channels; c++)
                        value[c] = snap.TryGetValue($"ch{c + 1}in", out var v) ? Convert.ToInt32(v) : 0;

                    // like MP, only track while channel 1 reports a plausible PWM
                    if (value[0] > 800 && value[0] < 2200)
                    {
                        for (var c = 0; c < Channels; c++)
                        {
                            min[c] = Math.Min(min[c], value[c]);
                            max[c] = Math.Max(max[c], value[c]);
                        }
                    }
                }

                if (++ticks % 2 == 0)
                    Publish();
            }

            public async Task FinishAsync(ParamService parameters, CancellationToken ct)
            {
                timer?.Dispose();
                Sample();
                int[] mins, maxs, trims;
                lock (min)
                {
                    if (!(min[0] > 800 && min[0] < 2200))
                    {
                        Finish(CalibrationState.Failed, "bad channel 1 input");
                        return;
                    }

                    mins = min.ToArray();
                    maxs = max.ToArray();
                    trims = value.Select((v, c) => Math.Min(Math.Max(v, min[c]), max[c])).ToArray();
                }

                var written = new List<string>();
                for (var c = 0; c < Channels; c++)
                {
                    // MP's rule: min < max, all non-zero, trim within range
                    if (!(mins[c] < maxs[c] && mins[c] != 0 && maxs[c] != 0 && trims[c] <= maxs[c] &&
                          trims[c] >= mins[c] && trims[c] != 0))
                        continue;

                    var ch = $"RC{c + 1}";
                    foreach (var (name, v) in new[] { (ch + "_MIN", mins[c]), (ch + "_MAX", maxs[c]), (ch + "_TRIM", trims[c]) })
                    {
                        var r = await parameters.SetAsync(SysId, CompId, name, v, true, ct).ConfigureAwait(false);
                        if (!r.Accepted)
                            log.Warn($"radio cal: {name} {r.Error}");
                    }

                    written.Add($"CH{c + 1} {mins[c]}-{trims[c]}-{maxs[c]}");
                }

                Finish(written.Count > 0 ? CalibrationState.Succeeded : CalibrationState.Failed,
                    written.Count > 0 ? "saved " + string.Join(", ", written) : "no channel moved");
            }

            protected override void OnFinished()
            {
                timer?.Dispose();
                try
                {
                    Link.Mav.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, oldRate, SysId, CompId);
                }
                catch
                {
                }
            }

            public override CalibrationStatus ToStatus()
            {
                lock (min)
                    return new CalibrationStatus(Kind, SysId, CompId, State, Message, null, null, null,
                        Enumerable.Range(0, Channels)
                            .Select(c => new RadioChannel(c + 1, value[c], min[c] == 3000 ? 0 : min[c], max[c]))
                            .ToList(), Updated);
            }
        }

        private static string Decode(byte[] text)
        {
            var s = Encoding.ASCII.GetString(text);
            var nul = s.IndexOf('\0');
            return nul >= 0 ? s.Substring(0, nul) : s;
        }
    }
}
