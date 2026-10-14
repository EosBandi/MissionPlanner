using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Calibration;
using MPBackend.Core.Control;
using MPBackend.Core.Missions;
using MPBackend.Core.Params;

namespace MPBackend.Tests
{
    /// <summary>
    /// End-to-end tests against ArduCopter SITL (the binary Mission Planner's Simulation screen downloads).
    /// Inconclusive when SITL is not installed. Uses instance 3 (tcp:5790) so a dev SITL on 5760 can keep running.
    /// </summary>
    [TestClass]
    [TestCategory("SITL")]
    public class SitlIntegrationTests
    {
        private const int Instance = 3;
        private static readonly string SitlDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mission Planner", "sitl");

        private static Process sitl;
        private static Backend backend;

        [ClassInitialize]
        public static void Start(TestContext _)
        {
            var exe = Path.Combine(SitlDir, "ArduCopter.exe");
            if (!File.Exists(exe))
                return;

            var work = Path.Combine(SitlDir, "+");
            Directory.CreateDirectory(work);
            var psi = new ProcessStartInfo(exe,
                $"-M+ -O-35.363261,149.165230,584,353 -s1 --serial0 tcp:0 -I {Instance} --wipe " +
                "--defaults ../default_params/copter.parm")
            {
                WorkingDirectory = work,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["PATH"] = SitlDir + ";" + Environment.GetEnvironmentVariable("PATH");
            psi.Environment["HOME"] = work;
            sitl = Process.Start(psi);
            sitl.BeginOutputReadLine();
            sitl.BeginErrorReadLine();

            backend = new Backend(new BackendOptions
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), "mpbackend-tests"),
                RecordTlogs = true
            });
        }

        [ClassCleanup]
        public static void Stop()
        {
            backend?.Dispose();
            try
            {
                sitl?.Kill();
            }
            catch
            {
            }
        }

        [TestMethod]
        [Timeout(240_000)]
        public async Task ConnectDownloadParamsAndFly()
        {
            if (backend == null)
                Assert.Inconclusive("SITL not installed");

            var (link, connectJob) = backend.Links.Connect(ConnectionRequest.Parse($"tcp:127.0.0.1:{5760 + 10 * Instance}"));
            await backend.Jobs.WaitAsync(connectJob, TimeSpan.FromSeconds(60));
            Assert.AreEqual(JobState.Succeeded, connectJob.State, connectJob.Error);
            Assert.AreEqual(LinkState.Connected, link.State);
            Assert.IsTrue(File.Exists(link.TlogPath), "tlog not created");

            // the connect job queues the parameter download
            var paramJob = await WaitFor(() => backend.Jobs.List().FirstOrDefault(j => j.Kind == "params.download"),
                TimeSpan.FromSeconds(10));
            await backend.Jobs.WaitAsync(paramJob, TimeSpan.FromSeconds(60));
            Assert.AreEqual(JobState.Succeeded, paramJob.State, paramJob.Error);

            var vehicle = backend.Vehicles.List().Single();
            Assert.AreEqual("QUADROTOR", vehicle.Type);
            Assert.IsTrue(vehicle.ParamsTotal > 500 && vehicle.ParamsReceived == vehicle.ParamsTotal);

            // parameters: write + read back, .param export round trip, compare
            var set = await backend.Params.SetAsync(1, 1, "RTL_ALT_M", 22);
            Assert.IsTrue(set.Accepted, set.Error);
            Assert.AreEqual(22, backend.Params.Get(1, 1, "RTL_ALT_M").Value, 1e-4);
            var file = ParamService.ParseParamFile(backend.Params.ExportParamFile(1, 1));
            Assert.AreEqual(0, backend.Params.Compare(1, 1, file).Count, "export differs from vehicle");
            file["RTL_ALT_M"] = 33;
            Assert.AreEqual("RTL_ALT_M", backend.Params.Compare(1, 1, file).Single().Name);
            Assert.IsNotNull(backend.Params.GetMetadata(1, 1, new[] { "FRAME_CLASS" }));

            // MAVFTP: repeated operations must keep working (sequence numbers carry across calls)
            for (var i = 0; i < 3; i++)
            {
                var entries = await backend.Ftp.ListAsync(1, 1, "@SYS");
                Assert.IsTrue(entries.Any(e => e.Name == "uarts.txt"), $"listing {i} failed");
            }

            using (var uarts = await backend.Ftp.DownloadAsync(1, 1, "@SYS/uarts.txt"))
                StringAssert.StartsWith(new StreamReader(uarts).ReadToEnd(), "UARTV1");

            // onboard log list (contents depend on earlier SITL runs)
            Assert.IsNotNull(await backend.Logs.ListAsync(1, 1));

            // mission / fence / rally round trips (home is split out of the mission)
            var mission = new MissionPlan
            {
                Items =
                {
                    new MissionItem { CommandName = "TAKEOFF", Alt = 15 },
                    new MissionItem { CommandName = "WAYPOINT", Lat = -35.3620, Lng = 149.1650, Alt = 30 },
                    // absolute altitude: checks per-item frames survive (a terrain-relative item would make
                    // ArduPilot 4.7 SITL refuse to arm with "terrain data expired")
                    new MissionItem
                    {
                        CommandName = "WAYPOINT", Lat = -35.3625, Lng = 149.1660, Alt = 620,
                        Frame = MAVLink.MAV_FRAME.GLOBAL
                    },
                    new MissionItem { CommandName = "RETURN_TO_LAUNCH" }
                }
            };
            Assert.AreEqual(4, await backend.Missions.UploadAsync(1, 1, mission));
            var downloaded = await backend.Missions.DownloadAsync(1, 1, MAVLink.MAV_MISSION_TYPE.MISSION);
            Assert.IsNotNull(downloaded.Home);
            CollectionAssert.AreEqual(new[] { "TAKEOFF", "WAYPOINT", "WAYPOINT", "RETURN_TO_LAUNCH" },
                downloaded.Items.Select(i => i.CommandName).ToArray());
            Assert.AreEqual(MAVLink.MAV_FRAME.GLOBAL, downloaded.Items[2].Frame);
            Assert.AreEqual(-35.3625, downloaded.Items[2].Lat, 1e-6);

            var fence = MissionFiles.Parse("f.plan", MissionFiles.Write(MissionFileFormat.Plan, null,
                new MissionPlan
                {
                    Type = MAVLink.MAV_MISSION_TYPE.FENCE,
                    Items =
                    {
                        new MissionItem { CommandName = "FENCE_CIRCLE_INCLUSION", Param1 = 500, Lat = -35.3632, Lng = 149.1652 }
                    }
                })).Fence;
            Assert.AreEqual(1, await backend.Missions.UploadAsync(1, 1, fence));
            Assert.AreEqual(500, (await backend.Missions.DownloadAsync(1, 1, MAVLink.MAV_MISSION_TYPE.FENCE))
                .Items.Single().Param1);

            await backend.Missions.ClearAsync(1, 1, MAVLink.MAV_MISSION_TYPE.FENCE);
            Assert.AreEqual(0, (await backend.Missions.DownloadAsync(1, 1, MAVLink.MAV_MISSION_TYPE.FENCE)).Items.Count);

            var mode = await backend.Vehicles.SetModeAsync(1, 1, "Guided");
            Assert.IsTrue(mode.Accepted, mode.Message);

            // arming is refused until the EKF has a position, which takes a while after a wipe
            var armDeadline = DateTime.UtcNow.AddSeconds(90);
            while (!(await backend.Vehicles.ArmAsync(1, 1, true, false)).Accepted)
            {
                if (DateTime.UtcNow > armDeadline)
                    Assert.Fail("arming refused: " + string.Join(" | ",
                        backend.Vehicles.GetMessages(1, 1).TakeLast(6).Select(m => m.Text)));
                await Task.Delay(3000);
            }

            Assert.IsTrue((await backend.Vehicles.TakeoffAsync(1, 1, 5)).Accepted);
            await WaitFor(() => Task.FromResult(
                    (float)backend.Vehicles.GetState(1, 1, new[] { "alt" })["alt"] > 4 ? (bool?)true : null),
                TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(250));

            Assert.IsTrue((await backend.Vehicles.SetModeAsync(1, 1, "Land")).Accepted);

            // manual control + radio calibration: RC overrides show up in RC_CHANNELS, so they can drive the cal
            string[] rcNames = Enumerable.Range(1, 2).SelectMany(c => new[] { $"RC{c}_MIN", $"RC{c}_MAX", $"RC{c}_TRIM" })
                .ToArray();
            var rcOriginal = rcNames.ToDictionary(n => n, n => backend.Params.Get(1, 1, n).Value);
            backend.Calibration.StartRadio(1, 1);
            foreach (var pwm in new[] { 1100, 1900, 1500 })
                await Feed(new int?[] { pwm, pwm }, TimeSpan.FromSeconds(1.5));
            using (var feeding = new CancellationTokenSource())
            {
                var feeder = Feed(new int?[] { 1500, 1500 }, TimeSpan.FromMinutes(1), feeding.Token);
                var radio = await backend.Calibration.RadioFinishAsync(1, 1);
                feeding.Cancel();
                await feeder;
                Assert.AreEqual(CalibrationState.Succeeded, radio.State, radio.Message);
            }

            Assert.AreEqual(1100, backend.Params.Get(1, 1, "RC1_MIN").Value);
            Assert.AreEqual(1900, backend.Params.Get(1, 1, "RC2_MAX").Value);
            Assert.AreEqual(1500, backend.Params.Get(1, 1, "RC1_TRIM").Value);

            // stop releases the overrides
            backend.ManualControl.Stop(1, 1);
            var restore = backend.Params.StartBulkSet(1, 1, rcOriginal);
            await backend.Jobs.WaitAsync(restore, TimeSpan.FromSeconds(30));
            Assert.AreEqual(rcOriginal["RC1_MIN"], backend.Params.Get(1, 1, "RC1_MIN").Value);

            Assert.IsTrue(await backend.Links.DisconnectAsync(link.Id));
        }

        /// <summary>Send RC overrides at 10 Hz (the backend repeats them to the vehicle at 20 Hz).</summary>
        private static async Task Feed(int?[] channels, TimeSpan duration, CancellationToken ct = default)
        {
            var end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end && !ct.IsCancellationRequested)
            {
                backend.ManualControl.Update(1, 1, new ManualControlInput { Channels = channels });
                try
                {
                    await Task.Delay(100, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static Task<T> WaitFor<T>(Func<T> probe, TimeSpan timeout) where T : class =>
            WaitFor(() => Task.FromResult(probe()), timeout, TimeSpan.FromMilliseconds(250));

        private static async Task<T> WaitFor<T>(Func<Task<T>> probe, TimeSpan timeout, TimeSpan interval)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                var value = await probe();
                if (value != null)
                    return value;
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"condition not met within {timeout}");
                await Task.Delay(interval);
            }
        }
    }
}
