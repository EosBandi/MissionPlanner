using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;

namespace MPBackend.Core.Links
{
    public enum LinkState
    {
        Connecting,
        Connected,
        Disconnected,
        Failed
    }

    public sealed record VehicleId(uint SysId, byte CompId);

    public sealed record LinkInfo(
        int Id,
        string Connection,
        LinkType Type,
        LinkState State,
        string Error,
        DateTime? ConnectedAt,
        string TlogPath,
        IReadOnlyList<VehicleId> Vehicles);

    public sealed record StatusMessage(
        int LinkId,
        uint SysId,
        byte CompId,
        MAVLink.MAV_SEVERITY Severity,
        string Text,
        DateTime Time);

    /// <summary>
    /// One MAVLink connection (MP's MainV2.Comports entry) plus the background reader that keeps it alive.
    /// </summary>
    public sealed class Link : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Link));

        /// <summary>
        /// MP's blocking APIs take over the port via the single <see cref="MAVLinkInterface.giveComport"/> flag,
        /// so only one such operation may run per link at a time.
        /// </summary>
        private readonly SemaphoreSlim opLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// MAVFTP and LOG_DATA transfers are subscription driven (the pump keeps reading) but must not overlap
        /// each other, so they get their own lock instead of blocking params/commands.
        /// </summary>
        private readonly SemaphoreSlim transferLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// One MAVFtp per vehicle for the life of the link: it carries the FTP sequence number, and ArduPilot
        /// answers a request that reuses the previous sequence number with its cached (wrong) reply.
        /// </summary>
        private readonly ConcurrentDictionary<(uint, byte), MissionPlanner.ArduPilot.Mavlink.MAVFtp> ftpSessions =
            new ConcurrentDictionary<(uint, byte), MissionPlanner.ArduPilot.Mavlink.MAVFtp>();

        private CancellationTokenSource pumpCts;
        private Task pumpTask;

        internal Link(int id, ConnectionRequest request)
        {
            Id = id;
            Request = request;
            Mav = new MAVLinkInterface();
        }

        public int Id { get; }
        public ConnectionRequest Request { get; }
        public MAVLinkInterface Mav { get; }
        public LinkState State { get; internal set; } = LinkState.Connecting;
        public string Error { get; internal set; }
        public DateTime? ConnectedAt { get; internal set; }
        public string TlogPath { get; internal set; }

        internal event Action<Link> PumpStopped;

        /// <summary>
        /// Vehicles/components seen on this link (excludes MP's hidden placeholder entries).
        /// </summary>
        public IReadOnlyList<MAVState> Vehicles =>
            Mav.MAVlist.Where(m => m.sysid != 0).ToList();

        internal MissionPlanner.ArduPilot.Mavlink.MAVFtp GetFtp(uint sysid, byte compid) =>
            ftpSessions.GetOrAdd((sysid, compid), k => new MissionPlanner.ArduPilot.Mavlink.MAVFtp(Mav, k.Item1, k.Item2));

        public MAVState FindMav(uint sysid, byte compid) =>
            Vehicles.FirstOrDefault(m => m.sysid == sysid && m.compid == compid);

        public LinkInfo ToInfo() => new LinkInfo(Id, Request.ToString(), Request.Type, State, Error, ConnectedAt,
            TlogPath, Vehicles.Select(v => new VehicleId(v.sysid, v.compid)).ToList());

        /// <summary>
        /// Run an operation that uses MP's request/response APIs (params, missions, commands...) with exclusive
        /// use of the link.
        /// </summary>
        public async Task<T> RunExclusiveAsync<T>(Func<MAVLinkInterface, Task<T>> op,
            CancellationToken ct = default)
        {
            await opLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await op(Mav).ConfigureAwait(false);
            }
            finally
            {
                opLock.Release();
            }
        }

        public Task<T> RunExclusiveAsync<T>(Func<MAVLinkInterface, T> op, CancellationToken ct = default) =>
            RunExclusiveAsync(m => Task.Run(() => op(m), ct), ct);

        /// <summary>
        /// Run a bulk transfer (MAVFTP, log download). Waits for, but does not block, regular operations.
        /// </summary>
        public async Task<T> RunTransferAsync<T>(Func<MAVLinkInterface, Task<T>> op, CancellationToken ct = default)
        {
            await transferLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await op(Mav).ConfigureAwait(false);
            }
            finally
            {
                transferLock.Release();
            }
        }

        internal void StartPump()
        {
            pumpCts = new CancellationTokenSource();
            var token = pumpCts.Token;
            pumpTask = Task.Run(() => PumpAsync(token));
        }

        internal async Task StopPumpAsync()
        {
            if (pumpCts == null)
                return;
            pumpCts.Cancel();
            try
            {
                await pumpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Port of MainV2.SerialReader for a single link: GCS heartbeat, param/version polling,
        /// packet reading and the CurrentState update pump (which also re-requests streams).
        /// </summary>
        private async Task PumpAsync(CancellationToken token)
        {
            var lastHeartbeat = DateTime.MinValue;
            var mav = Mav;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!mav.BaseStream.IsOpen)
                    {
                        log.Warn($"link {Id} closed");
                        break;
                    }

                    if (DateTime.UtcNow - lastHeartbeat >= TimeSpan.FromSeconds(1))
                    {
                        lastHeartbeat = DateTime.UtcNow;
                        SendHeartbeats(mav);
                        Poll(mav);
                    }

                    if (mav.giveComport)
                    {
                        // a blocking operation is reading the port itself
                        await Task.Delay(10, token).ConfigureAwait(false);
                        continue;
                    }

                    var start = DateTime.UtcNow;
                    var read = false;
                    while (mav.BaseStream.IsOpen && mav.BaseStream.BytesToRead > 10 && !mav.giveComport &&
                           DateTime.UtcNow - start < TimeSpan.FromSeconds(1) && !token.IsCancellationRequested)
                    {
                        await mav.readPacketAsync().ConfigureAwait(false);
                        read = true;
                    }

                    foreach (var m in mav.MAVlist)
                    {
                        try
                        {
                            m.cs.UpdateCurrentSettings(null, false, mav, m);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }
                    }

                    if (!read)
                        await Task.Delay(1, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Error($"link {Id} reader", ex);
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            if (!token.IsCancellationRequested)
                PumpStopped?.Invoke(this);
        }

        private static void SendHeartbeats(MAVLinkInterface mav)
        {
            var hb = new MAVLink.mavlink_heartbeat_t
            {
                type = (byte)MAVLink.MAV_TYPE.GCS,
                autopilot = (byte)MAVLink.MAV_AUTOPILOT.INVALID,
                mavlink_version = 3
            };

            // one heartbeat per protocol flavour in use on this link: mavlink1, mavlink2, mavlink2 signed
            bool sentSigned = false, sentV2 = false, sentV1 = false;
            foreach (var m in mav.MAVlist)
            {
                if (m.mavlinkv2)
                {
                    if (m.signing)
                    {
                        if (sentSigned) continue;
                        sentSigned = true;
                    }
                    else
                    {
                        if (sentV2) continue;
                        sentV2 = true;
                    }
                }
                else
                {
                    if (sentV1) continue;
                    sentV1 = true;
                }

                mav.sendPacket(hb, m.sysid, m.compid);
            }
        }

        private void Poll(MAVLinkInterface mav)
        {
            // only poll when nobody else is using the link
            if (mav.giveComport || !opLock.Wait(0))
                return;
            try
            {
                // finish an incomplete param list in the background (MainV2 does this once connected 60s)
                if (!mav.MAV.cs.armed && ConnectedAt.HasValue &&
                    DateTime.UtcNow > ConnectedAt.Value.AddSeconds(60))
                {
                    mav.getParamPoll();
                    mav.getParamPoll();
                }

                foreach (var m in mav.MAVlist)
                {
                    if (m.sysid != 0 && m.cs.capabilities == 0 && DateTime.Now.Second % 20 == 0 &&
                        m.cs.version < new Version(0, 1))
                        mav.getVersion(m.sysid, m.compid, false);
                }
            }
            catch (Exception ex)
            {
                log.Debug(ex);
            }
            finally
            {
                opLock.Release();
            }
        }

        internal static StatusMessage ParseStatusText(int linkId, MAVLink.MAVLinkMessage message)
        {
            var msg = message.ToStructure<MAVLink.mavlink_statustext_t>();
            var text = Encoding.UTF8.GetString(msg.text);
            var nul = text.IndexOf('\0');
            if (nul >= 0)
                text = text.Substring(0, nul);
            return new StatusMessage(linkId, message.sysid, message.compid, (MAVLink.MAV_SEVERITY)msg.severity, text,
                DateTime.UtcNow);
        }

        public void Dispose()
        {
            try
            {
                StopPumpAsync().Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            try
            {
                Mav.Close();
            }
            catch (Exception ex)
            {
                log.Debug(ex);
            }

            try
            {
                Mav.Dispose();
            }
            catch
            {
            }
        }
    }
}
