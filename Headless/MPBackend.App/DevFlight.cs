using Microsoft.Extensions.Logging;
using MPBackend.Core;
using MPBackend.Core.Missions;

namespace MPBackend.App;

/// <summary>
/// Development aid (MPGCS_DEV_FLY=&lt;alt&gt;): waits for the first vehicle's parameters, uploads a small mission around
/// home (takeoff, four waypoints, RTL) and flies it in AUTO so the displays show a moving vehicle.
/// MPGCS_DEV_GPSLOSS=1 additionally switches the simulated GPS off for 45 s after take-off (CAS exercise).
/// Only meant for a simulator.
/// </summary>
internal static class DevFlight
{
    public static async Task RunAsync(Backend backend, float altitude, ILogger log)
    {
        try
        {
            Core.Vehicles.VehicleInfo? v = null;
            for (var i = 0; i < 120 && v == null; i++)
            {
                await Task.Delay(1000);
                v = backend.Vehicles.List().FirstOrDefault(x => x.ParamsTotal > 0 && x.ParamsReceived >= x.ParamsTotal);
            }

            if (v == null)
            {
                log.LogWarning("dev flight: no vehicle with parameters");
                return;
            }

            // SITL needs a few seconds of EKF convergence after the first position fix
            await Task.Delay(8000);
            var state = backend.Vehicles.GetState(v.SysId, v.CompId, new[] { "lat", "lng" });
            var lat = Convert.ToDouble(state["lat"]);
            var lng = Convert.ToDouble(state["lng"]);
            if (lat == 0)
            {
                log.LogWarning("dev flight: no position");
                return;
            }

            // a rectangle ~250 m across, north-east of home
            MissionItem Wp(ushort cmd, double dlat, double dlng, float alt, float p1 = 0) => new()
                { Command = cmd, Lat = lat + dlat, Lng = lng + dlng, Alt = alt, Param1 = p1, Frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT };
            var plan = new MissionPlan
            {
                Items =
                {
                    new MissionItem { Command = 22, Alt = altitude, Frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT }, // NAV_TAKEOFF
                    Wp(16, 0.0010, 0.0012, altitude),
                    Wp(16, 0.0022, 0.0012, altitude + 10),
                    Wp(16, 0.0022, -0.0008, altitude + 10),
                    Wp(16, 0.0005, -0.0010, altitude),
                    new MissionItem { Command = 20 } // NAV_RETURN_TO_LAUNCH
                }
            };
            log.LogInformation("dev flight: uploaded {Count} items", await backend.Missions.UploadAsync(v.SysId, v.CompId, plan));

            // AUTO_OPTIONS bit 0 = allow arming in AUTO, bit 1 = allow takeoff without raising the throttle
            await backend.Params.SetAsync(v.SysId, v.CompId, "AUTO_OPTIONS", 3);
            log.LogInformation("dev flight: {Result}", await backend.Vehicles.SetModeAsync(v.SysId, v.CompId, "Auto"));
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var arm = await backend.Vehicles.ArmAsync(v.SysId, v.CompId, true, false);
                log.LogInformation("dev flight: arm {Result}", arm);
                if (arm.Accepted)
                    break;
                await Task.Delay(5000);
            }

            if (Environment.GetEnvironmentVariable("MPGCS_DEV_GPSLOSS") == "1")
            {
                // (SIM_GPS1_ENABLE on ArduPilot 4.5+, SIM_GPS_DISABLE before)
                await Task.Delay(40000);
                var gpsParam = backend.Params.Get(v.SysId, v.CompId, "SIM_GPS1_ENABLE") != null ? "SIM_GPS1_ENABLE" : "SIM_GPS_DISABLE";
                var off = gpsParam == "SIM_GPS1_ENABLE" ? 0 : 1;
                log.LogInformation("dev flight: GPS off {Result}", await backend.Params.SetAsync(v.SysId, v.CompId, gpsParam, off));
                await Task.Delay(45000);
                log.LogInformation("dev flight: GPS on {Result}", await backend.Params.SetAsync(v.SysId, v.CompId, gpsParam, 1 - off));
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "dev flight failed");
        }
    }
}
