using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core.Missions;

namespace MPBackend.Tests
{
    [TestClass]
    public class MissionFilesTests
    {
        private static MissionPlan SampleMission() => new MissionPlan
        {
            Home = new GeoPoint(-35.363261, 149.165230, 584),
            Items = new List<MissionItem>
            {
                new MissionItem { CommandName = "TAKEOFF", Alt = 20 },
                new MissionItem { CommandName = "MAV_CMD_NAV_WAYPOINT", Lat = -35.362, Lng = 149.165, Alt = 30 },
                new MissionItem
                {
                    CommandName = "WAYPOINT", Lat = -35.3625, Lng = 149.166, Alt = 40,
                    Frame = MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT
                },
                new MissionItem { Command = 178, Param1 = 1, Param2 = 8, Frame = MAVLink.MAV_FRAME.GLOBAL },
                new MissionItem { CommandName = "RETURN_TO_LAUNCH" }
            }
        };

        private static MissionPlan SampleFence() => new MissionPlan
        {
            Type = MAVLink.MAV_MISSION_TYPE.FENCE,
            Items = new[] { (-35.360, 149.162), (-35.360, 149.168), (-35.366, 149.168) }
                .Select(p => new MissionItem
                {
                    CommandName = "FENCE_POLYGON_VERTEX_INCLUSION", Param1 = 3, Lat = p.Item1, Lng = p.Item2,
                    Frame = MAVLink.MAV_FRAME.GLOBAL
                })
                .Append(new MissionItem
                {
                    CommandName = "FENCE_CIRCLE_EXCLUSION", Param1 = 30, Lat = -35.363, Lng = 149.1655,
                    Frame = MAVLink.MAV_FRAME.GLOBAL
                }).ToList()
        };

        [TestMethod]
        public void WaypointsRoundTripKeepsHomeFramesAndParams()
        {
            var text = MissionFiles.Write(MissionFileFormat.Waypoints, SampleMission());
            StringAssert.StartsWith(text, "QGC WPL 110");

            var parsed = MissionFiles.Parse("m.waypoints", text).Mission;
            Assert.AreEqual(-35.363261, parsed.Home.Lat, 1e-7);
            Assert.AreEqual(584, parsed.Home.Alt, 1e-3);
            CollectionAssert.AreEqual(new[] { "TAKEOFF", "WAYPOINT", "WAYPOINT", "DO_CHANGE_SPEED", "RETURN_TO_LAUNCH" },
                parsed.Items.Select(i => i.CommandName).ToArray());
            Assert.AreEqual(MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT, parsed.Items[2].Frame);
            Assert.AreEqual(8, parsed.Items[3].Param2);
            Assert.AreEqual(-35.3625, parsed.Items[2].Lat, 1e-7);
        }

        [TestMethod]
        public void WaypointsFileWithFenceItemsIsClassifiedAsFence()
        {
            var text = MissionFiles.Write(MissionFileFormat.Waypoints, SampleFence());
            var parsed = MissionFiles.Parse("f.waypoints", text);
            Assert.IsNull(parsed.Mission);
            Assert.AreEqual(4, parsed.Fence.Items.Count);
        }

        [TestMethod]
        public void PlanRoundTripCarriesMissionFenceAndRally()
        {
            var rally = new MissionPlan
            {
                Type = MAVLink.MAV_MISSION_TYPE.RALLY,
                Items = new List<MissionItem> { new MissionItem { CommandName = "RALLY_POINT", Lat = -35.36, Lng = 149.16, Alt = 25 } }
            };

            var text = MissionFiles.Write(MissionFileFormat.Plan, SampleMission(), SampleFence(), rally);
            var parsed = MissionFiles.Parse("all.plan", text);

            Assert.AreEqual(5, parsed.Mission.Items.Count);
            Assert.AreEqual(MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT, parsed.Mission.Items[2].Frame);
            Assert.AreEqual(584, parsed.Mission.Home.Alt, 1e-3);

            var fence = parsed.Fence.Items;
            Assert.AreEqual(4, fence.Count);
            Assert.IsTrue(fence.Take(3).All(f => f.CommandName == "FENCE_POLYGON_VERTEX_INCLUSION" && f.Param1 == 3));
            Assert.AreEqual("FENCE_CIRCLE_EXCLUSION", fence[3].CommandName);
            Assert.AreEqual(30, fence[3].Param1);

            Assert.AreEqual(25, parsed.Rally.Items.Single().Alt);
        }

        [TestMethod]
        public void KmlLineStringBecomesWaypoints()
        {
            var kml = MissionFiles.Write(MissionFileFormat.Kml, SampleMission());
            var parsed = MissionFiles.Parse("route.kml", kml).Mission;
            // only located items are exported as the route
            Assert.AreEqual(2, parsed.Items.Count);
            Assert.AreEqual(-35.3625, parsed.Items[1].Lat, 1e-6);
            Assert.AreEqual(40, parsed.Items[1].Alt, 1e-3);
        }

        [TestMethod]
        public void RejectsUnknownContent()
        {
            Assert.ThrowsException<System.FormatException>(() => MissionFiles.Parse("x.txt", "hello"));
        }
    }
}
