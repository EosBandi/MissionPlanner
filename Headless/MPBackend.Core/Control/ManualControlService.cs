using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Control
{
    /// <summary>
    /// One manual control frame from a client (e.g. the browser Gamepad API). Use exactly one of:
    /// <list type="bullet">
    /// <item><see cref="Channels"/>: RC PWM per channel (index 0 = CH1); null = not overridden, 0 = release.</item>
    /// <item><see cref="Normalized"/>: channel number → -1..1, mapped with the vehicle's RCx_MIN/TRIM/MAX
    /// (the throttle channel, RCMAP_THROTTLE, maps -1..1 linearly from min to max).</item>
    /// <item><see cref="Manual"/>: MANUAL_CONTROL x/y/z/r (-1000..1000; z 0..1000) and buttons.</item>
    /// </list>
    /// </summary>
    public sealed class ManualControlInput
    {
        public int?[] Channels { get; set; }
        public Dictionary<int, double> Normalized { get; set; }
        public ManualAxes Manual { get; set; }
    }

    public sealed class ManualAxes
    {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public short R { get; set; }
        public ushort Buttons { get; set; }
    }

    public sealed record ManualControlStatus(uint SysId, byte CompId, bool Active, string Mode, DateTime LastInput,
        IReadOnlyList<int> Channels);

    /// <summary>
    /// Port of MainV2.joysticksend: sends RC_CHANNELS_OVERRIDE (or MANUAL_CONTROL) at 20 Hz from the latest client
    /// input. If no input arrives for <see cref="InputTimeout"/>, the overrides are released (failsafe), so a
    /// closed browser tab cannot leave sticks stuck.
    /// </summary>
    public sealed class ManualControlService : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ManualControlService));
        private static readonly TimeSpan SendInterval = TimeSpan.FromMilliseconds(50);
        public static readonly TimeSpan InputTimeout = TimeSpan.FromMilliseconds(500);

        private readonly VehicleService vehicles;
        private readonly ConcurrentDictionary<(uint, byte), Controller> controllers = new();

        public ManualControlService(VehicleService vehicles)
        {
            this.vehicles = vehicles;
        }

        /// <summary>Feed the latest input; starts sending if not already.</summary>
        public ManualControlStatus Update(uint sysid, byte compid, ManualControlInput input)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            var controller = controllers.AddOrUpdate((sysid, compid),
                _ => new Controller(link, sysid, compid),
                (_, existing) =>
                {
                    // the vehicle is now reached through another link (reconnect, redundant link failover)
                    if (existing.Link == link)
                        return existing;
                    existing.Dispose();
                    return new Controller(link, sysid, compid);
                });
            controller.Apply(input, mav);
            return controller.ToStatus();
        }

        /// <summary>Stop sending and release all overrides.</summary>
        public bool Stop(uint sysid, byte compid)
        {
            if (!controllers.TryRemove((sysid, compid), out var c))
                return false;
            c.Dispose();
            return true;
        }

        public ManualControlStatus GetStatus(uint sysid, byte compid) =>
            controllers.TryGetValue((sysid, compid), out var c) ? c.ToStatus() : null;

        public void Dispose()
        {
            foreach (var key in controllers.Keys.ToList())
                Stop(key.Item1, key.Item2);
        }

        private sealed class Controller : IDisposable
        {
            private const int MaxChannels = 18;
            private readonly Link link;

            public Link Link => link;
            private readonly uint sysid;
            private readonly byte compid;
            private readonly CancellationTokenSource cts = new();
            private readonly object sync = new();
            private ushort[] channels;
            private MAVLink.mavlink_manual_control_t? manual;
            private DateTime lastInput;
            private bool released = true;

            public Controller(Link link, uint sysid, byte compid)
            {
                this.link = link;
                this.sysid = sysid;
                this.compid = compid;
                _ = Task.Run(() => LoopAsync(cts.Token));
            }

            public void Apply(ManualControlInput input, MAVState mav)
            {
                lock (sync)
                {
                    lastInput = DateTime.UtcNow;
                    if (input.Manual != null)
                    {
                        channels = null;
                        manual = new MAVLink.mavlink_manual_control_t
                        {
                            target = (byte)sysid,
                            x = input.Manual.X,
                            y = input.Manual.Y,
                            z = input.Manual.Z,
                            r = input.Manual.R,
                            buttons = input.Manual.Buttons
                        };
                        return;
                    }

                    manual = null;
                    // UINT16_MAX means "ignore this channel" in RC_CHANNELS_OVERRIDE
                    channels ??= Enumerable.Repeat(ushort.MaxValue, MaxChannels).ToArray();
                    if (input.Channels != null)
                    {
                        for (var i = 0; i < Math.Min(MaxChannels, input.Channels.Length); i++)
                            channels[i] = input.Channels[i] is int v ? (ushort)Math.Clamp(v, 0, 2200) : ushort.MaxValue;
                    }

                    if (input.Normalized != null)
                    {
                        var throttle = mav.param.ContainsKey("RCMAP_THROTTLE") ? (int)mav.param["RCMAP_THROTTLE"].Value : 3;
                        foreach (var (ch, v) in input.Normalized)
                        {
                            if (ch < 1 || ch > MaxChannels)
                                continue;
                            channels[ch - 1] = Map(mav, ch, Math.Clamp(v, -1, 1), ch == throttle);
                        }
                    }
                }
            }

            /// <summary>-1..1 to PWM using the vehicle's RC calibration (MP's Joystick does the same).</summary>
            private static ushort Map(MAVState mav, int ch, double v, bool linear)
            {
                double P(string name, double fallback) =>
                    mav.param.ContainsKey($"RC{ch}_{name}") ? mav.param[$"RC{ch}_{name}"].Value : fallback;

                var min = P("MIN", 1100);
                var max = P("MAX", 1900);
                var trim = linear ? (min + max) / 2 : P("TRIM", 1500);
                var pwm = v >= 0 ? trim + v * (max - trim) : trim + v * (trim - min);
                return (ushort)Math.Round(pwm);
            }

            private async Task LoopAsync(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(SendInterval, token).ConfigureAwait(false);
                        if (!link.Mav.BaseStream.IsOpen || link.Mav.BaseStream.BytesToWrite >= 50)
                            continue;

                        object packet;
                        lock (sync)
                        {
                            if (DateTime.UtcNow - lastInput > InputTimeout)
                            {
                                if (!released)
                                {
                                    log.Warn($"manual control input timeout for {sysid}/{compid}, releasing");
                                    Release();
                                }

                                continue;
                            }

                            released = false;
                            packet = manual.HasValue ? manual.Value : (object)Override(channels);
                        }

                        link.Mav.sendPacket(packet, sysid, compid);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        log.Debug(ex);
                    }
                }
            }

            private MAVLink.mavlink_rc_channels_override_t Override(ushort[] ch)
            {
                ch ??= Enumerable.Repeat(ushort.MaxValue, MaxChannels).ToArray();
                return new MAVLink.mavlink_rc_channels_override_t
                {
                    target_system = (byte)sysid,
                    target_component = compid,
                    chan1_raw = ch[0], chan2_raw = ch[1], chan3_raw = ch[2], chan4_raw = ch[3],
                    chan5_raw = ch[4], chan6_raw = ch[5], chan7_raw = ch[6], chan8_raw = ch[7],
                    chan9_raw = ch[8], chan10_raw = ch[9], chan11_raw = ch[10], chan12_raw = ch[11],
                    chan13_raw = ch[12], chan14_raw = ch[13], chan15_raw = ch[14], chan16_raw = ch[15],
                    chan17_raw = ch[16], chan18_raw = ch[17]
                };
            }

            /// <summary>All-zero override hands every channel back to the RC receiver.</summary>
            private void Release()
            {
                released = true;
                channels = null;
                manual = null;
                try
                {
                    var zero = Override(new ushort[MaxChannels]);
                    for (var i = 0; i < 3; i++)
                        link.Mav.sendPacket(zero, sysid, compid);
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }

            public ManualControlStatus ToStatus()
            {
                lock (sync)
                    return new ManualControlStatus(sysid, compid, !released || DateTime.UtcNow - lastInput <= InputTimeout,
                        manual.HasValue ? "manual_control" : "rc_override", lastInput,
                        channels?.Select(c => c == ushort.MaxValue ? -1 : (int)c).ToList());
            }

            public void Dispose()
            {
                cts.Cancel();
                lock (sync)
                    Release();
            }
        }
    }
}
