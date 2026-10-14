using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Utilities;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Missions
{
    public sealed record MissionEvent(string Event, uint SysId, byte CompId, MAVLink.MAV_MISSION_TYPE Type, int Count);

    /// <summary>
    /// Mission / fence / rally transfer using MP's mission protocol (mav_mission), with the home and frame
    /// handling FlightPlanner applies (getWPs / saveWPs).
    /// </summary>
    public sealed class MissionService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(MissionService));

        private readonly VehicleService vehicles;
        private readonly JobManager jobs;
        private readonly EventHub events;

        public MissionService(VehicleService vehicles, JobManager jobs, EventHub events)
        {
            this.vehicles = vehicles;
            this.jobs = jobs;
            this.events = events;
        }

        public Task<MissionPlan> DownloadAsync(uint sysid, byte compid, MAVLink.MAV_MISSION_TYPE type,
            CancellationToken ct = default)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            return jobs.RunAsync($"{Kind(type)}.download", $"Download {Kind(type)} {sysid}/{compid}", async ctx =>
            {
                var list = await link.RunExclusiveAsync(m => WithPort(m, () =>
                        mav_mission.download(m, sysid, compid, type, Progress(ctx))), ctx.Token)
                    .ConfigureAwait(false);

                var plan = ToPlan(type, list, mav);
                events.Publish(Topics.Missions, new MissionEvent("downloaded", sysid, compid, type, plan.Items.Count));
                return plan;
            }, link.Id, ct);
        }

        /// <summary>
        /// Upload a list. For ArduPilot missions the home (plan.Home, else the vehicle's current home) is sent as
        /// item 0; fence items are forced to GLOBAL frame. Afterwards home is re-read and terrain for every point
        /// is pre-loaded, as FlightPlanner does.
        /// </summary>
        public Task<int> UploadAsync(uint sysid, byte compid, MissionPlan plan, CancellationToken ct = default)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            var type = plan.Type;
            var list = BuildUploadList(plan, mav);

            return jobs.RunAsync($"{Kind(type)}.upload", $"Upload {Kind(type)} {sysid}/{compid}", async ctx =>
            {
                await link.RunExclusiveAsync(m => WithPort(m, async () =>
                {
                    await mav_mission.upload(m, sysid, compid, type, list, Progress(ctx)).ConfigureAwait(false);
                    if (type == MAVLink.MAV_MISSION_TYPE.MISSION)
                    {
                        try
                        {
                            await m.getHomePositionAsync(sysid, compid).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // FlightPlanner falls back to reading mission item 0 (home)
                            log.Info("HOME_POSITION not received after upload, reading item 0: " + ex.Message);
                            try
                            {
                                await m.getWPAsync(sysid, compid, 0).ConfigureAwait(false);
                            }
                            catch (Exception ex2)
                            {
                                log.Warn("home refresh after upload failed", ex2);
                            }
                        }
                    }

                    return true;
                }), ctx.Token).ConfigureAwait(false);

                PreloadTerrain(link.Mav, list);
                events.Publish(Topics.Missions, new MissionEvent("uploaded", sysid, compid, type, plan.Items.Count));
                return plan.Items.Count;
            }, link.Id, ct);
        }

        /// <summary>
        /// Remove all items: missions keep only home (as MP's "clear mission"), fence/rally become empty.
        /// </summary>
        public Task<int> ClearAsync(uint sysid, byte compid, MAVLink.MAV_MISSION_TYPE type,
            CancellationToken ct = default) =>
            UploadAsync(sysid, compid, new MissionPlan { Type = type }, ct);

        public async Task<bool> SetCurrentAsync(uint sysid, byte compid, ushort seq, CancellationToken ct = default)
        {
            var (link, _) = vehicles.Resolve(sysid, compid);
            return await link.RunExclusiveAsync(m => WithPort(m, () => m.setWPCurrentAsync(sysid, compid, seq)), ct)
                .ConfigureAwait(false);
        }

        private static List<Locationwp> BuildUploadList(MissionPlan plan, MAVState mav)
        {
            var list = plan.Items.Select(i => i.ToLocationwp()).ToList();

            if (plan.Type == MAVLink.MAV_MISSION_TYPE.FENCE)
            {
                // fence does not use alt, and needs to be global
                list = list.Select(wp =>
                {
                    wp.frame = (byte)MAVLink.MAV_FRAME.GLOBAL;
                    return wp;
                }).ToList();
            }

            if (plan.Type == MAVLink.MAV_MISSION_TYPE.MISSION && IsArduPilot(mav))
            {
                var home = plan.Home;
                if (home == null)
                {
                    var current = mav.cs.HomeLocation;
                    home = new GeoPoint(current.Lat, current.Lng, current.Alt);
                }

                list.Insert(0, new Locationwp
                {
                    id = (ushort)MAVLink.MAV_CMD.WAYPOINT,
                    frame = (byte)MAVLink.MAV_FRAME.GLOBAL,
                    lat = home.Lat,
                    lng = home.Lng,
                    alt = (float)home.Alt
                });
            }

            return list;
        }

        private static MissionPlan ToPlan(MAVLink.MAV_MISSION_TYPE type, List<Locationwp> list, MAVState mav)
        {
            var plan = new MissionPlan { Type = type };
            var items = list.AsEnumerable();
            if (type == MAVLink.MAV_MISSION_TYPE.MISSION && IsArduPilot(mav) && list.Count > 0)
            {
                plan.Home = new GeoPoint(list[0].lat, list[0].lng, list[0].alt);
                items = list.Skip(1);
            }

            plan.Items = items.Select(MissionItem.From).ToList();
            return plan;
        }

        private static bool IsArduPilot(MAVState mav) => mav.apname == MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;

        /// <summary>
        /// Bridge mav_mission's progress callback to the job; throwing there is how MP cancels a transfer.
        /// </summary>
        private static Action<int, string> Progress(JobContext ctx) => (percent, status) =>
        {
            ctx.Token.ThrowIfCancellationRequested();
            ctx.Report(percent, status);
        };

        /// <summary>
        /// MP's mission calls set giveComport and only clear it on success; make sure the reader resumes.
        /// </summary>
        private static async Task<T> WithPort<T>(MAVLinkInterface m, Func<Task<T>> op)
        {
            try
            {
                return await op().ConfigureAwait(false);
            }
            finally
            {
                m.giveComport = false;
            }
        }

        private static void PreloadTerrain(MAVLinkInterface m, IEnumerable<Locationwp> list)
        {
            foreach (var wp in list.Where(w => w.lat != 0 && w.lng != 0))
            {
                try
                {
                    m.Terrain?.checkTerrain(wp.lat, wp.lng);
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }
        }

        private static string Kind(MAVLink.MAV_MISSION_TYPE type) => type.ToString().ToLowerInvariant();
    }
}
