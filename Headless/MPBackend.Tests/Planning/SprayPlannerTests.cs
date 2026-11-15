using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core.Planning;
using MPBackend.Core.Terrain;

namespace MPBackend.Tests.Planning
{
    /// <summary>
    /// The spray-grid planner (port of the SprayGrid-Soleon plugin): strips, ordering, obstacles, splits, the
    /// sprayer script messages and altitude tracking, on a 100 m x 60 m field near the SITL home.
    /// </summary>
    [TestClass]
    public class SprayPlannerTests
    {
        // ~100 m east-west (0.0011 deg at 35 S), ~60 m north-south (0.00054 deg)
        private const double Lat0 = -35.3632, Lng0 = 149.1652;
        private const double LatN = Lat0 + 0.00054, LngE = Lng0 + 0.0011;

        private static SprayGridRequest Field() => new SprayGridRequest
        {
            Polygon = new List<LatLng>
            {
                new LatLng(Lat0, Lng0), new LatLng(LatN, Lng0), new LatLng(LatN, LngE), new LatLng(Lat0, LngE)
            },
            Home = new SprayHome { Lat = Lat0 - 0.0002, Lng = Lng0 - 0.0002 },
            LineDistance = 5,
            Angle = 0,
            LitersPerHa = 20,
            SprayBarWidth = 4,
            FlySpeed = 5,
            Altitude = 3,
            GpsOffset = 0.5,
            TakeoffAltitude = 6,
            ElevationSource = SprayElevationSource.None
        };

        private static bool InField(SprayGridPoint p, double marginDeg = 0.00002) =>
            p.Lat >= Lat0 - marginDeg && p.Lat <= LatN + marginDeg && p.Lng >= Lng0 - marginDeg && p.Lng <= LngE + marginDeg;

        [TestMethod]
        public void PreviewMakesAlternatingStripsInsideThePolygon()
        {
            var r = Field();
            var preview = SprayPlanner.Preview(r);

            Assert.IsTrue(preview.Points.Count > 0, "no points");
            Assert.AreEqual(0, preview.Points.Count % 2, "S/E pairs");
            for (int i = 0; i < preview.Points.Count; i++)
                Assert.AreEqual(i % 2 == 0 ? "S" : "E", preview.Points[i].Tag, $"tag at {i}");
            Assert.IsTrue(preview.Points.All(p => InField(p)), "a point lies outside the field");
            Assert.IsTrue(preview.Points.All(p => Math.Abs(p.Alt - 3.5) < 1e-9), "altitude + gps offset");

            var s = preview.Stats;
            // 100 m wide field, 5 m strips -> about 20 strips of ~60 m
            Assert.IsTrue(s.Strips >= 18 && s.Strips <= 22, $"strips {s.Strips}");
            Assert.AreEqual(s.Strips, preview.Points.Count / 2);
            Assert.IsTrue(Math.Abs(s.AreaM2 - 6000) < 300, $"area {s.AreaM2}");
            Assert.IsTrue(s.SprayDistanceM > s.Strips * 55 && s.SprayDistanceM < s.Strips * 65, $"spray distance {s.SprayDistanceM}");
            Assert.IsTrue(s.TotalDistanceM > s.SprayDistanceM, "total includes transits and home legs");
            Assert.AreEqual(s.SprayDistanceM / 5, s.SprayTimeS, 1e-9);
            Assert.AreEqual(s.AreaHa * 20, s.LitersTotal, 1e-9);
            Assert.AreEqual(0, s.SplitTimesS.Count);
            Assert.AreEqual(0, preview.Warnings.Count, string.Join("; ", preview.Warnings));

            // strips run along UTM grid north (angle 0): the ends of a 60 m strip differ by the grid convergence only
            for (int i = 0; i < preview.Points.Count; i += 2)
                Assert.IsTrue(Math.Abs(preview.Points[i].Lng - preview.Points[i + 1].Lng) * 90800 < 2.5, $"strip {i / 2} not along the angle");
        }

        [TestMethod]
        public void StartsNearHomeAndRespectsLaneSeparation()
        {
            var r = Field();
            var first = SprayPlanner.Preview(r).Points[0];
            // home is south-west of the field: the first point is the south-west corner strip
            Assert.IsTrue(Math.Abs(first.Lng - Lng0) < 0.00008 && Math.Abs(first.Lat - Lat0) < 0.00003,
                $"first point {first.Lat},{first.Lng} should be near the SW corner");

            r.LaneSeparation = 2;
            var sep = SprayPlanner.Preview(r).Points;
            // consecutive strips are at least 2 lanes apart
            int close = 0;
            for (int i = 1; i < sep.Count / 2; i++)
            {
                double dLng = Math.Abs(sep[2 * i].Lng - sep[2 * (i - 1)].Lng) * 90800; // metres at this latitude
                if (dLng < 9)
                    close++;
            }

            Assert.IsTrue(close <= 1, $"{close} consecutive strips closer than 2 lanes");
            Assert.AreEqual(SprayPlanner.Preview(Field()).Points.Count, sep.Count, "same strips, different order");
        }

        [TestMethod]
        public void ObstacleSplitsStripsAndExpandsByBarWidth()
        {
            var r = Field();
            var plain = SprayPlanner.Preview(r).Stats.Strips;

            // 20 m square in the middle of the field
            double cLat = (Lat0 + LatN) / 2, cLng = (Lng0 + LngE) / 2;
            double dLat = 0.00009, dLng = 0.00011;
            r.Obstacles.Add(new List<LatLng>
            {
                new LatLng(cLat - dLat, cLng - dLng), new LatLng(cLat + dLat, cLng - dLng),
                new LatLng(cLat + dLat, cLng + dLng), new LatLng(cLat - dLat, cLng + dLng)
            });

            r.ExpandObstacles = false;
            var cut = SprayPlanner.Preview(r);
            Assert.IsTrue(cut.Stats.Strips > plain, $"strips crossing the obstacle should be cut in two ({cut.Stats.Strips} vs {plain})");
            Assert.IsTrue(cut.Points.All(p => !(Math.Abs(p.Lat - cLat) < dLat - 0.00001 && Math.Abs(p.Lng - cLng) < dLng - 0.00001)),
                "a point lies inside the obstacle");

            r.ExpandObstacles = true; // inflate by half the 4 m bar
            var inflated = SprayPlanner.Preview(r);
            Assert.IsTrue(inflated.Stats.Strips >= cut.Stats.Strips, "inflated obstacle cuts at least as many strips");
            Assert.IsTrue(inflated.Stats.SprayDistanceM < cut.Stats.SprayDistanceM, "inflated obstacle removes more strip length");

            r.IgnoreObstacles = true;
            Assert.AreEqual(plain, SprayPlanner.Preview(r).Stats.Strips, "ignoreObstacles plans the full field");
        }

        [TestMethod]
        public void MissionHasSprayerSetupStartStopAndTakeoffRtl()
        {
            var r = Field();
            r.HeadLock = true;
            r.DelayAtWaypoint = 1;
            r.WpnavSpeed = 8;
            var m = SprayPlanner.BuildMission(r, NoElevationSampler.Instance);
            var items = m.Mission.Items;

            Assert.AreEqual(217, items[0].Command, "sprayer setup first");
            Assert.AreEqual(1f, items[0].Param1);
            Assert.AreEqual(20f, items[0].Param2, "litres/ha");
            Assert.AreEqual(5f, items[0].Param3, "line distance");
            Assert.AreEqual(5f, items[0].Param4, "speed");

            Assert.AreEqual("TAKEOFF", items[1].CommandName);
            Assert.AreEqual(6f, items[1].Alt);
            Assert.AreEqual(MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT, items[1].Frame);
            Assert.AreEqual(r.Home.Lat, items[1].Lat);

            Assert.AreEqual("DO_CHANGE_SPEED", items[2].CommandName);
            Assert.AreEqual(5f, items[2].Param2);
            Assert.AreEqual("CONDITION_YAW", items[3].CommandName);
            Assert.AreEqual(0f, items[3].Param1);

            // every S waypoint is followed by "spray on" (2, 3, distance), every E by "spray off" (2, 0)
            var wps = items.Where(i => i.CommandName == "WAYPOINT").ToList();
            Assert.AreEqual(m.Points.Count, wps.Count);
            foreach (var wp in wps)
            {
                int idx = items.IndexOf(wp);
                var next = items[idx + 1];
                Assert.AreEqual(217, next.Command, "script message after each waypoint");
                Assert.AreEqual(2f, next.Param1);
                Assert.AreEqual(1f, wp.Param1, "delay at waypoint");
                Assert.AreEqual(3.5f, wp.Alt);
                Assert.AreEqual(MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT, wp.Frame);
                if (wp.Param4 == 1f) // S
                {
                    Assert.AreEqual(3f, next.Param2);
                    var end = items[idx + 2];
                    Assert.AreEqual("WAYPOINT", end.CommandName, "strip end follows");
                    var expected = new MissionPlanner.Utilities.PointLatLngAlt(wp.Lat, wp.Lng).GetDistance(
                        new MissionPlanner.Utilities.PointLatLngAlt(end.Lat, end.Lng));
                    Assert.AreEqual(Math.Round(expected, 1), next.Param3, 0.051, "distance to the strip end");
                }
                else
                {
                    Assert.AreEqual(2f, wp.Param4, "E marker");
                    Assert.AreEqual(0f, next.Param2);
                }
            }

            Assert.AreEqual("DO_CHANGE_SPEED", items[items.Count - 2].CommandName);
            Assert.AreEqual(8f, items[items.Count - 2].Param2, "back to WPNAV_SPEED");
            Assert.AreEqual("RETURN_TO_LAUNCH", items[items.Count - 1].CommandName);
            CollectionAssert.AreEqual(new[] { 1 }, m.SegmentStartSeq);
            Assert.AreEqual(r.Home.Lat, m.Mission.Home.Lat);
            Assert.AreEqual(0, m.Warnings.Count, string.Join("; ", m.Warnings));

            r.AddTakeoffAndRtl = false;
            r.HeadLock = false;
            r.WpnavSpeed = null;
            var bare = SprayPlanner.BuildMission(r, NoElevationSampler.Instance).Mission.Items;
            Assert.IsFalse(bare.Any(i => i.CommandName == "TAKEOFF" || i.CommandName == "RETURN_TO_LAUNCH" ||
                                         i.CommandName == "CONDITION_YAW"));
            Assert.AreEqual(1, bare.Count(i => i.CommandName == "DO_CHANGE_SPEED"));
        }

        [TestMethod]
        public void SplitBySegmentsAddsDoJumpsToEachSegment()
        {
            var r = Field();
            r.SplitBy = SpraySplitMode.Segments;
            r.SplitValue = 3;
            var m = SprayPlanner.BuildMission(r, NoElevationSampler.Instance);
            var items = m.Mission.Items;

            Assert.AreEqual(3, m.SegmentStartSeq.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("DO_JUMP", items[i].CommandName, $"jump {i} at the top");
                Assert.AreEqual(1f, items[i].Param2, "one repeat");
                var target = items[(int)items[i].Param1 - 1]; // seq counts home as 0
                Assert.AreEqual(217, target.Command, $"jump {i} lands on a sprayer setup");
                Assert.AreEqual(1f, target.Param1);
                Assert.AreEqual((int)items[i].Param1, m.SegmentStartSeq[i]);
            }

            Assert.AreEqual(3, items.Count(i => i.CommandName == "TAKEOFF"));
            Assert.AreEqual(3, items.Count(i => i.CommandName == "RETURN_TO_LAUNCH"));
            // every grid point is flown exactly once; segments start on an S point
            var wps = items.Where(i => i.CommandName == "WAYPOINT").ToList();
            Assert.AreEqual(m.Points.Count, wps.Count, "no grid point lost or duplicated by the split");
            foreach (var seq in m.SegmentStartSeq)
            {
                var firstWp = items.Skip(seq - 1).First(i => i.CommandName == "WAYPOINT");
                Assert.AreEqual(1f, firstWp.Param4, "segment starts on a strip start");
            }
        }

        [TestMethod]
        public void SplitByTimeMarksSegmentsThatFitTheTime()
        {
            var r = Field();
            r.SplitBy = SpraySplitMode.Time;
            r.SplitValue = 2; // minutes; the whole field takes ~5 minutes at 5 m/s
            var preview = SprayPlanner.Preview(r);
            Assert.IsTrue(preview.Stats.SplitTimesS.Count >= 2, $"segments {preview.Stats.SplitTimesS.Count}");
            Assert.AreEqual(preview.Stats.SplitTimesS.Count - 1, preview.Points.Count(p => p.Split), "one split mark per boundary");
            Assert.IsTrue(preview.Stats.SplitTimesS.Take(preview.Stats.SplitTimesS.Count - 1).All(t => t > 120 && t < 200),
                "a segment ends on the first point that pushes it past the limit: " + string.Join(", ", preview.Stats.SplitTimesS.Select(t => t.ToString("0"))));

            var m = SprayPlanner.BuildMission(r, NoElevationSampler.Instance);
            Assert.AreEqual(preview.Stats.SplitTimesS.Count, m.SegmentStartSeq.Count);
            Assert.AreEqual(m.SegmentStartSeq.Count, m.Mission.Items.Count(i => i.CommandName == "DO_JUMP"));

            r.SplitValue = 1; // one minute cannot cover the round trip from a home 1 km away
            r.Home = new SprayHome { Lat = Lat0 - 0.009, Lng = Lng0 };
            var tooShort = SprayPlanner.Preview(r);
            Assert.AreEqual(0, tooShort.Stats.SplitTimesS.Count);
            Assert.IsTrue(tooShort.Warnings.Any(w => w.Contains("segment time")), string.Join("; ", tooShort.Warnings));
        }

        private sealed class FuncSampler : IElevationSampler
        {
            private readonly Func<double, double, double?> f;
            public FuncSampler(Func<double, double, double?> f) => this.f = f;
            public bool Available => true;
            public double? Sample(double lat, double lng) => f(lat, lng);
        }

        [TestMethod]
        public void TerrainCorrectsRelativeAndAbsoluteAltitudesWithoutTracking()
        {
            var r = Field();
            // flat 100 m plateau, home 2 m lower
            var flat = new FuncSampler((lat, lng) => lat < Lat0 - 0.00005 ? 98 : 100);

            var rel = SprayPlanner.BuildMission(r, flat);
            Assert.IsTrue(rel.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => Math.Abs(i.Alt - 5.5f) < 1e-4),
                "relative = terrain + target - home terrain");
            Assert.AreEqual(98, rel.Mission.Home.Alt, "home altitude sampled");
            Assert.IsTrue(rel.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => Math.Abs(i.Param3 - 3.5f) < 1e-4),
                "param3 = height above the terrain under the point, also without tracking");

            r.AltitudeReference = SprayAltitudeReference.Absolute;
            var abs = SprayPlanner.BuildMission(r, flat);
            Assert.IsTrue(abs.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => Math.Abs(i.Alt - 103.5f) < 1e-4));
            Assert.IsTrue(abs.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => i.Frame == MAVLink.MAV_FRAME.GLOBAL));

            r.AltitudeReference = SprayAltitudeReference.Terrain;
            var ter = SprayPlanner.BuildMission(r, flat);
            Assert.IsTrue(ter.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => Math.Abs(i.Alt - 3.5f) < 1e-4));
            Assert.IsTrue(ter.Mission.Items.Where(i => i.CommandName == "WAYPOINT").All(i => i.Frame == MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT));

            r.AltitudeReference = SprayAltitudeReference.Absolute;
            var none = SprayPlanner.BuildMission(r, NoElevationSampler.Instance);
            Assert.IsTrue(none.Warnings.Any(w => w.Contains("absolute")), "absolute without terrain warns");
        }

        [TestMethod]
        public void AltitudeTrackingInsertsPointsOverABumpAndKeepsClearance()
        {
            var r = Field();
            r.AltitudeTracking = true;
            r.TrackingAltitudeError = 0.5;
            double cLat = (Lat0 + LatN) / 2, cLng = (Lng0 + LngE) / 2;
            // 6 m high gaussian bump in the middle of the field on a 100 m plain
            double Terrain(double lat, double lng)
            {
                double dn = (lat - cLat) * 111320, de = (lng - cLng) * 90800;
                return 100 + 6 * Math.Exp(-(dn * dn + de * de) / (2 * 12 * 12));
            }

            var bump = new FuncSampler((lat, lng) => Terrain(lat, lng));
            var m = SprayPlanner.BuildMission(r, bump);
            var wps = m.Mission.Items.Where(i => i.CommandName == "WAYPOINT").ToList();

            Assert.IsTrue(m.Points.Count > SprayPlanner.Preview(r).Points.Count, "tracking points were inserted");
            Assert.IsTrue(m.Points.Any(p => p.Tag == ""), "inserted points carry no S/E tag");
            Assert.AreEqual(m.Points.Count, wps.Count, "every tracking point becomes a waypoint");

            double target = 3.5;
            foreach (var wp in wps)
            {
                double aboveTerrain = wp.Alt + 100 - Terrain(wp.Lat, wp.Lng); // relative to home terrain (100)
                Assert.IsTrue(aboveTerrain >= target - r.TrackingAltitudeError - 1e-3,
                    $"waypoint {wp.Lat},{wp.Lng} only {aboveTerrain:0.00} m above terrain");
                Assert.IsTrue(aboveTerrain <= target + 6 + 1e-3, "never higher than the bump plus target");
            }

            // interim points lie on their strip
            foreach (var p in m.Points.Where(p => p.Tag == ""))
                Assert.IsTrue(InField(p), "tracking point outside the field");

            // the plain strips far from the bump stay two points
            var untouched = m.Points.Where(p => Math.Abs((p.Lng - cLng) * 90800) > 40).ToList();
            Assert.IsTrue(untouched.All(p => p.Tag != ""), "no tracking points on flat strips");
        }

        [TestMethod]
        public void RasterSamplerInterpolatesAndRejectsBadInput()
        {
            var raster = new ElevationRaster
            {
                North = 1, South = 0, West = 0, East = 1, Cols = 2, Rows = 2,
                Heights = new double?[] { 100, 200, 300, 400 } // NW, NE, SW, SE
            };
            var s = new RasterElevationSampler(raster);
            Assert.AreEqual(100, s.Sample(1, 0));
            Assert.AreEqual(200, s.Sample(1, 1));
            Assert.AreEqual(300, s.Sample(0, 0));
            Assert.AreEqual(250, s.Sample(0.5, 0.5));
            Assert.AreEqual(150, s.Sample(1, 0.5));
            Assert.IsNull(s.Sample(2, 0.5), "outside the raster");

            raster.Heights[3] = null;
            Assert.AreEqual(100, new RasterElevationSampler(raster).Sample(0.9, 0.1), "nearest known corner when a corner is missing");

            Assert.ThrowsException<ArgumentException>(() => new RasterElevationSampler(null));
            Assert.ThrowsException<ArgumentException>(() =>
                new RasterElevationSampler(new ElevationRaster { North = 1, South = 0, West = 0, East = 1, Cols = 2, Rows = 2, Heights = new double?[3] }));
        }

        [TestMethod]
        public void RejectsBadRequests()
        {
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(null));
            var r = Field();
            r.Polygon.RemoveAt(0);
            r.Polygon.RemoveAt(0);
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(r));
            r = Field();
            r.Home = null;
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(r));
            r = Field();
            r.LineDistance = 0;
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(r));
            r = Field();
            r.SplitBy = SpraySplitMode.Segments;
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(r));
            r = Field();
            r.StartFrom = SprayStartPosition.Point;
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.Preview(r));

            // a polygon too small for the line distance yields no strips: preview warns, mission is a 400
            r = Field();
            r.LineDistance = 500;
            var empty = SprayPlanner.Preview(r);
            Assert.AreEqual(0, empty.Points.Count);
            Assert.IsTrue(empty.Warnings.Any(w => w.Contains("no strips")));
            Assert.ThrowsException<ArgumentException>(() => SprayPlanner.BuildMission(r, NoElevationSampler.Instance));
        }

        [TestMethod]
        public void StartFromPointBeginsAtThatStrip()
        {
            var r = Field();
            var points = SprayPlanner.Preview(r).Points;
            var pick = points[8]; // some S point in the middle
            r.StartFrom = SprayStartPosition.Point;
            r.StartPoint = new LatLng(pick.Lat, pick.Lng);
            var again = SprayPlanner.Preview(r).Points;
            Assert.AreEqual(pick.Lat, again[0].Lat, 1e-9);
            Assert.AreEqual(pick.Lng, again[0].Lng, 1e-9);
            Assert.AreEqual(points.Count, again.Count);
        }
    }
}
