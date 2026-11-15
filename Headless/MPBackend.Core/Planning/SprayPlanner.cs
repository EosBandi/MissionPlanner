using System;
using System.Collections.Generic;
using System.Linq;
using MissionPlanner.Utilities;
using MPBackend.Core.Missions;
using MPBackend.Core.Terrain;

namespace MPBackend.Core.Planning
{
    /// <summary>
    /// The SprayGrid-Soleon plugin's dialog logic without the dialog: grid + statistics (preview) and the mission
    /// built from it (sprayer script messages, takeoff/RTL, speed, head lock, time/segment splits with DO_JUMP,
    /// altitude tracking over the spray-bar footprint). Pure functions; terrain comes through
    /// <see cref="IElevationSampler"/>.
    /// </summary>
    public static class SprayPlanner
    {
        private const string SplitTag = "SplitTime";

        /// <summary>DO_SEND_SCRIPT_MESSAGE: the sprayer Lua script's command channel.</summary>
        private const MAVLink.MAV_CMD ScriptMessage = (MAVLink.MAV_CMD)217;

        private sealed class GridResult
        {
            public List<PointLatLngAlt> Polygon;
            public PointLatLngAlt Home;
            public List<PointLatLngAlt> Grid;
            public SprayGridStats Stats;
            public List<string> Warnings = new List<string>();
        }

        public static SprayGridPreview Preview(SprayGridRequest r)
        {
            var g = ComputeGrid(r);
            return new SprayGridPreview { Points = ToPoints(g.Grid), Stats = g.Stats, Warnings = g.Warnings };
        }

        // ---------------------------------------------------------------- grid + stats (recalculateGrid)

        private static GridResult ComputeGrid(SprayGridRequest r)
        {
            if (r == null)
                throw new ArgumentException("request body is required");
            if (r.Polygon == null || r.Polygon.Count < 3)
                throw new ArgumentException("polygon needs at least 3 vertices");
            if (r.Home == null)
                throw new ArgumentException("home is required");
            if (!(r.LineDistance > 0))
                throw new ArgumentException("lineDistance must be positive");
            if (!(r.FlySpeed > 0))
                throw new ArgumentException("flySpeed must be positive");
            if (r.SplitBy != SpraySplitMode.None && r.SplitValue < 1)
                throw new ArgumentException("splitValue must be at least 1 when splitting");
            if (r.StartFrom == SprayStartPosition.Point && r.StartPoint == null)
                throw new ArgumentException("startPoint is required when startFrom is Point");

            var res = new GridResult
            {
                Polygon = r.Polygon.Select(p => new PointLatLngAlt(p.Lat, p.Lng, 0)).ToList(),
                Home = new PointLatLngAlt(r.Home.Lat, r.Home.Lng, r.Home.Alt ?? 0)
            };

            var obstacles = new List<List<PointLatLngAlt>>();
            if (!r.IgnoreObstacles && r.Obstacles != null)
                foreach (var o in r.Obstacles.Where(o => o != null && o.Count >= 3))
                    obstacles.Add(o.Select(p => new PointLatLngAlt(p.Lat, p.Lng, 0)).ToList());

            double barwidth = r.ExpandObstacles ? r.SprayBarWidth : 0;
            var startPoint = r.StartPoint == null ? null : new PointLatLngAlt(r.StartPoint.Lat, r.StartPoint.Lng, 0);

            res.Grid = SprayGridGenerator.Create(res.Polygon, r.Altitude + r.GpsOffset, r.LineDistance, r.Angle,
                r.StartFrom, r.LaneSeparation, res.Home, obstacles, r.Shift, barwidth, r.ExtendedEndpoint,
                startPoint);

            res.Stats = new SprayGridStats
            {
                AreaM2 = SprayGridGenerator.PolygonArea(res.Polygon)
            };
            res.Stats.LitersTotal = res.Stats.AreaHa * r.LitersPerHa;

            if (res.Grid.Count == 0)
            {
                res.Warnings.Add("no strips fit in the polygon with this line distance and angle");
                return res;
            }

            res.Stats.SplitTimesS = MarkTimeSplits(res.Grid, r, res.Home, res.Warnings);

            int strips = 0;
            double sprayDistance = 0, total = 0;
            for (int i = 0; i < res.Grid.Count; i++)
            {
                var p = res.Grid[i];
                if (p.Tag == "S" || p.Tag == "E")
                    strips++;
                if (i == 0)
                    continue;
                double d = res.Grid[i - 1].GetDistance(p);
                total += d;
                if (p.Tag == "E")
                    sprayDistance += d;
            }

            total += res.Home.GetDistance(res.Grid[0]) + res.Home.GetDistance(res.Grid[res.Grid.Count - 1]);

            res.Stats.Strips = strips / 2;
            res.Stats.SprayDistanceM = sprayDistance;
            res.Stats.SprayTimeS = sprayDistance / r.FlySpeed;
            res.Stats.TotalDistanceM = total;
            res.Stats.FlightTimeS = total / r.FlySpeed;
            return res;
        }

        /// <summary>
        /// Split by time: walk the grid accumulating distance; where the time so far plus the flight home exceeds
        /// the segment time, mark the point as the start of a new segment (the plugin's Tag2 = "SplitTime").
        /// Returns the estimated duration of each segment.
        /// </summary>
        private static List<double> MarkTimeSplits(List<PointLatLngAlt> grid, SprayGridRequest r, PointLatLngAlt home,
            List<string> warnings)
        {
            var times = new List<double>();
            double v = r.FlySpeed;
            double split_time = r.SplitBy == SpraySplitMode.Time ? r.SplitValue * 60.0 : 0;
            if (split_time <= 0)
                return times;

            double chunk_dist = grid[0].GetDistance(home) / 1000.0;
            double chunk_time = (chunk_dist * 1000 + grid[0].GetDistance(home)) / v;

            if (split_time < chunk_time)
            {
                warnings.Add("segment time is shorter than the flight to the first strip and back; not split");
                return times;
            }

            var prev = grid[0];
            foreach (var item in grid)
            {
                chunk_dist += item.GetDistance(prev) / 1000;
                double dist_to_home = item.GetDistance(home) / 1000;
                chunk_time = (chunk_dist + dist_to_home) * 1000 / v;
                if (chunk_time > split_time)
                {
                    double chunk_till_end = chunk_time;
                    // Split the mission here
                    chunk_dist = item.GetDistance(home) / 1000.0;
                    chunk_time = 2 * (chunk_dist * 1000) / v; // round trip

                    // If the round-trip time to/from this waypoint is more than the split time, then we cannot
                    // possibly split the mission into the requested number of segments. So, we won't split the mission.
                    if (split_time < chunk_time)
                    {
                        warnings.Add("segment time is shorter than a round trip to the field; splitting stopped at " +
                                     $"segment {times.Count + 1}");
                        return times;
                    }

                    item.Tag2 = SplitTag;
                    times.Add(chunk_till_end);
                }

                prev = item;
            }

            // One last chunk
            times.Add(chunk_time);
            return times;
        }

        private static List<SprayGridPoint> ToPoints(List<PointLatLngAlt> grid) =>
            grid.Select(p => new SprayGridPoint(p.Lat, p.Lng, p.Alt, p.Tag ?? "", p.Tag2 == SplitTag)).ToList();

        // ---------------------------------------------------------------- mission (BUT_Accept_Click2)

        public static SprayMissionResult BuildMission(SprayGridRequest r, IElevationSampler sampler)
        {
            sampler ??= NoElevationSampler.Instance;
            var g = ComputeGrid(r);
            if (g.Grid.Count == 0)
                throw new ArgumentException("no strips fit in the polygon with this line distance and angle");

            var warnings = g.Warnings;
            var grid = g.Grid;
            var home = g.Home;
            double target = r.Altitude + r.GpsOffset;
            int missing = 0;

            double? Sample(double lat, double lng)
            {
                var h = sampler.Sample(lat, lng);
                if (h == null && sampler.Available)
                    missing++;
                return h;
            }

            var displacement = DisplacementPoints(r.Angle, r.SprayBarWidth);

            double MaxAltInArea(double lat, double lng)
            {
                // the plugin's getMaxAltinArea: the centre sample is the fallback, the first valid footprint
                // sample replaces it and later ones raise it
                double result = Sample(lat, lng) ?? 0;
                bool valid = false;
                var pnt = new PointLatLngAlt(lat, lng, 0);
                foreach (var (x, y) in displacement)
                {
                    var check = pnt.gps_offset(x * 0.2, -y * 0.2);
                    var alt = Sample(check.Lat, check.Lng);
                    if (alt == null)
                        continue;
                    if (!valid)
                    {
                        result = alt.Value;
                        valid = true;
                    }
                    else if (alt.Value > result)
                    {
                        result = alt.Value;
                    }
                }

                return result;
            }

            // terrain under home; feeds WAYPOINT param3 so a Relative waypoint carries its height above the terrain
            // under the point (the target) whether or not altitude tracking inserted points
            double homealt = 0;

            if (r.AltitudeTracking && sampler.Available)
            {
                if (r.AltitudeReference == SprayAltitudeReference.Terrain)
                {
                    // Terrain frame: altitude above the terrain under the point, raised by the footprint's highest spot
                    foreach (var p in grid)
                    {
                        double altsrtm = Sample(p.Lat, p.Lng) ?? 0;
                        double maxalt = MaxAltInArea(p.Lat, p.Lng);
                        p.Alt = maxalt - altsrtm + target;
                    }
                }
                else
                {
                    homealt = Sample(home.Lat, home.Lng) ?? (r.Home.Alt ?? 0);
                    if (r.AltitudeReference == SprayAltitudeReference.Absolute)
                        homealt = 0;

                    foreach (var p in grid)
                        p.Alt = MaxAltInArea(p.Lat, p.Lng) - homealt + target;

                    grid = InsertTrackingPoints(grid, homealt, target, r.TrackingAltitudeError, MaxAltInArea);
                    PruneTrackingPoints(grid, r.TrackingAltitudeError);
                }
            }
            else if (sampler.Available)
            {
                // The plugin forced the Flight Planner's "verify height" on, which rewrites every waypoint's
                // altitude from srtm when it is added: terrain under the point + target - terrain under home.
                double homeTerrain = Sample(home.Lat, home.Lng) ?? (r.Home.Alt ?? 0);
                if (r.AltitudeReference == SprayAltitudeReference.Relative)
                    homealt = homeTerrain;
                foreach (var p in grid)
                {
                    switch (r.AltitudeReference)
                    {
                        case SprayAltitudeReference.Absolute:
                            p.Alt = (Sample(p.Lat, p.Lng) ?? 0) + target;
                            break;
                        case SprayAltitudeReference.Terrain:
                            p.Alt = target;
                            break;
                        default:
                            p.Alt = (Sample(p.Lat, p.Lng) ?? 0) + target - homeTerrain;
                            break;
                    }
                }
            }
            else
            {
                if (r.AltitudeTracking)
                    warnings.Add("altitude tracking needs an elevation source; waypoints use the plain altitude");
                else if (r.AltitudeReference == SprayAltitudeReference.Absolute)
                    warnings.Add("absolute altitude without an elevation source: waypoints use the plain altitude");
            }

            // ---- mission items ----

            var items = new List<MissionItem>();
            var navFrame = r.AltitudeReference switch
            {
                SprayAltitudeReference.Absolute => MAVLink.MAV_FRAME.GLOBAL,
                SprayAltitudeReference.Terrain => MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT,
                _ => MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT
            };

            int Add(MAVLink.MAV_CMD cmd, double p1, double p2, double p3, double p4, double lat, double lng, double alt,
                MAVLink.MAV_FRAME frame)
            {
                items.Add(new MissionItem
                {
                    Command = (ushort)cmd,
                    CommandName = cmd.ToString(),
                    Frame = frame,
                    Param1 = (float)p1,
                    Param2 = (float)p2,
                    Param3 = (float)p3,
                    Param4 = (float)p4,
                    Lat = lat,
                    Lng = lng,
                    Alt = (float)alt
                });
                return items.Count - 1;
            }

            void AddWp(PointLatLngAlt p)
            {
                double p4 = p.Tag == "S" ? 1 : p.Tag == "E" ? 2 : 0;
                double p3 = 0;
                // We need reference value only if we are using Relative Altitude
                if (r.AltitudeReference == SprayAltitudeReference.Relative)
                {
                    double pointAlt = Sample(p.Lat, p.Lng) ?? 0;
                    // calculate back the relative altitude at the given point (Alt tracking)
                    p3 = Math.Round(p.Alt - (pointAlt - homealt), 2);
                }

                Add(MAVLink.MAV_CMD.WAYPOINT, r.DelayAtWaypoint > 0 ? r.DelayAtWaypoint : 0, 0, p3, p4, p.Lat, p.Lng, p.Alt,
                    navFrame);
            }

            int EmitSegment(int start, int end, double speedType)
            {
                // Add first command to set sprayer
                int setup = Add(ScriptMessage, 1, r.LitersPerHa, r.LineDistance, r.FlySpeed, 0, 0, 0, navFrame);

                if (r.AddTakeoffAndRtl)
                    Add(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, home.Lat, home.Lng, r.TakeoffAltitude,
                        MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT);

                Add(MAVLink.MAV_CMD.DO_CHANGE_SPEED, speedType, r.FlySpeed, 0, 0, 0, 0, 0, navFrame);

                if (r.HeadLock)
                    Add(MAVLink.MAV_CMD.CONDITION_YAW, r.Angle, 0, 0, 0, 0, 0, 0, navFrame);

                PointLatLngAlt last = null;
                for (int i = start; i < end && i < grid.Count; i++)
                {
                    var p = grid[i];
                    if (p.Tag == "M")
                        continue;
                    if (last != null && p.Lat == last.Lat && p.Lng == last.Lng && p.Alt == last.Alt)
                        continue;
                    AddWp(p);

                    double distanceToNext = i < grid.Count - 1 ? Math.Round(p.GetDistance(grid[i + 1]), 1) : 0;
                    if (p.Tag == "S")
                        Add(ScriptMessage, 2, 3, distanceToNext, 0, 0, 0, 0, navFrame);
                    if (p.Tag == "E")
                        Add(ScriptMessage, 2, 0, 0, 0, 0, 0, 0, navFrame);
                    last = p;
                }

                // if speed is set, set it back to WPNAV
                if (r.FlySpeed != 0 && r.WpnavSpeed is { } wpnav)
                    Add(MAVLink.MAV_CMD.DO_CHANGE_SPEED, 0, wpnav, 0, 0, 0, 0, 0, navFrame);

                if (r.AddTakeoffAndRtl)
                    Add(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, 0, 0, 0, 0, 0, 0, 0, navFrame);

                return setup;
            }

            var setupIndices = new List<int>();

            if (r.SplitBy == SpraySplitMode.None)
            {
                setupIndices.Add(EmitSegment(0, grid.Count, 1));
            }
            else
            {
                if (!r.AddTakeoffAndRtl)
                    warnings.Add("split missions normally need takeoff/RTL in every segment");

                var starts = new List<int>();
                var ends = new List<int>();

                if (r.SplitBy == SpraySplitMode.Time)
                {
                    starts.Add(0);
                    for (int i = 0; i < grid.Count; i++)
                    {
                        if (grid[i].Tag2 == SplitTag)
                        {
                            starts.Add(i);
                            ends.Add(i);
                        }
                    }

                    ends.Add(grid.Count);
                }
                else
                {
                    int segments = r.SplitValue;
                    int wpsplit = (int)Math.Round((double)grid.Count / segments, MidpointRounding.AwayFromZero);
                    for (int i = 0; i < segments; i++)
                    {
                        int start = wpsplit * i;
                        // the plugin cut the last segment at wpsplit * segments too, which could drop the tail
                        int end = i == segments - 1 ? grid.Count : wpsplit * (i + 1);

                        while (start != 0 && start < grid.Count && grid[start].Tag != "S")
                            start--;
                        while (end > 0 && end < grid.Count && grid[end].Tag != "S")
                            end--;
                        starts.Add(start);
                        ends.Add(end);
                    }
                }

                for (int s = 0; s < starts.Count; s++)
                {
                    if (ends[s] <= starts[s])
                    {
                        warnings.Add($"segment {s + 1} would be empty and was skipped");
                        continue;
                    }

                    setupIndices.Add(EmitSegment(starts[s], ends[s], 0));
                }

                if (setupIndices.Count > 1)
                {
                    // DO_JUMPs at the top, one per segment, so the vehicle can be started at any segment by setting
                    // the current item; targets are mission seq numbers (home = 0) after the jumps are inserted
                    int index = 0;
                    foreach (var setup in setupIndices)
                    {
                        items.Insert(index, new MissionItem
                        {
                            Command = (ushort)MAVLink.MAV_CMD.DO_JUMP,
                            CommandName = MAVLink.MAV_CMD.DO_JUMP.ToString(),
                            Frame = navFrame,
                            Param1 = setup + setupIndices.Count + 1,
                            Param2 = 1
                        });
                        index++;
                    }
                }
            }

            int jumps = setupIndices.Count > 1 ? setupIndices.Count : 0;
            var result = new SprayMissionResult
            {
                Mission = new MissionPlan
                {
                    Type = MAVLink.MAV_MISSION_TYPE.MISSION,
                    Home = new GeoPoint(home.Lat, home.Lng,
                        r.Home.Alt ?? (sampler.Available ? Sample(home.Lat, home.Lng) ?? 0 : 0)),
                    Items = items
                },
                Points = ToPoints(grid),
                Stats = g.Stats,
                SegmentStartSeq = setupIndices.Select(s => s + jumps + 1).ToList(),
                Warnings = warnings
            };

            if (missing > 0)
                warnings.Add($"{missing} terrain samples were unavailable and counted as 0 m AMSL");

            return result;
        }

        // ---------------------------------------------------------------- altitude tracking

        /// <summary>
        /// Points along the spray bar (perpendicular to <paramref name="heading"/>) in 0.2 m units, each with a
        /// copy 0.2 m ahead and behind: the vehicle footprint that is sampled for the highest terrain.
        /// </summary>
        internal static List<(double X, double Y)> DisplacementPoints(double heading, double barwidth)
        {
            double width = barwidth / 0.2; // 20 units of 0.2m = 4m

            double alpha = heading % 360;
            double alpha_radians = alpha * Math.PI / 180;
            double half_length = width / 2;
            double cos_alpha = Math.Cos(alpha_radians); // dx
            double sin_alpha = Math.Sin(alpha_radians); // dy

            double startX = -half_length * cos_alpha;
            double startY = -half_length * sin_alpha;
            double endX = half_length * cos_alpha;
            double endY = half_length * sin_alpha;

            double shiftx = -sin_alpha;
            double shifty = cos_alpha;

            var points = new List<(double, double)>();

            double dx = endX - startX;
            double dy = endY - startY;

            double steps = barwidth / 0.2;
            if (steps < 1)
                steps = 1;

            double xIncrement = dx / steps;
            double yIncrement = dy / steps;

            for (int i = 0; i <= steps; i++)
            {
                double x = startX + i * xIncrement;
                double y = startY + i * yIncrement;
                points.Add((x, y));
                points.Add((x - shiftx, y - shifty));
                points.Add((x + shiftx, y + shifty));
            }

            return points;
        }

        /// <summary>
        /// Walk every leg in 0.4 m steps; where the straight-line altitude would be more than
        /// <paramref name="maxError"/> off the target height above the footprint's terrain, insert a point.
        /// </summary>
        private static List<PointLatLngAlt> InsertTrackingPoints(List<PointLatLngAlt> grid, double homealt,
            double target, double maxError, Func<double, double, double> maxAltInArea)
        {
            var newGrid = new List<PointLatLngAlt>();
            PointLatLngAlt last = null;

            foreach (var loc in grid)
            {
                if (loc == null)
                    continue;

                // Ignore the first point, we don't have a heading
                if (last == null)
                {
                    newGrid.Add(loc);
                    last = loc;
                    continue;
                }

                double dist = last.GetDistance(loc);
                int points = (int)(dist * 2.5) + 1;

                double steplat = (last.Lat - loc.Lat) / points;
                double steplng = (last.Lng - loc.Lng) / points;
                double stepalt = (last.Alt - loc.Alt) / points;

                double lastalt = last.Alt;
                int lasta = 0;

                // a == points is loc itself, which is added below with its tag
                for (int a = 0; a < points; a++)
                {
                    double lat = last.Lat - steplat * a;
                    double lng = last.Lng - steplng * a;

                    // vehicle centre altitude at this point, extrapolated from the two ends
                    double extrapolatedRelativeAlt = lastalt - stepalt * (a - lasta);

                    double actualTerrainAlt = maxAltInArea(lat, lng);
                    double terrainToHome = actualTerrainAlt - homealt;
                    double altAboveTerrain = extrapolatedRelativeAlt - terrainToHome;

                    if (Math.Abs(altAboveTerrain - target) > maxError)
                    {
                        var newpoint = new PointLatLngAlt(lat, lng, terrainToHome + target, "");
                        newGrid.Add(newpoint);
                        lastalt = newpoint.Alt;
                        int remaining = points - a;
                        stepalt = remaining > 0 ? (lastalt - loc.Alt) / remaining : 0;
                        lasta = a;
                    }
                }

                newGrid.Add(loc);
                last = loc;
            }

            return newGrid;
        }

        /// <summary>Remove inserted points that lie within <paramref name="maxError"/> of the line between their neighbours.</summary>
        private static void PruneTrackingPoints(List<PointLatLngAlt> grid, double maxError)
        {
            bool notDoneYet = true;
            while (notDoneYet)
            {
                notDoneYet = false;
                for (int index = 1; index < grid.Count - 1; index++)
                {
                    var p = grid[index];
                    if (p.Tag == "S" || p.Tag == "E")
                        continue;

                    var prev = grid[index - 1];
                    var next = grid[index + 1];

                    double x2 = prev.GetDistance(next);
                    double xc = prev.GetDistance(p);
                    double y2 = prev.Alt - next.Alt;
                    double yc = prev.Alt - p.Alt;

                    double m = y2 / x2;
                    double c = y2 - m * x2;
                    double y_prime = m * xc + c;
                    double d = Math.Abs(y_prime - yc);

                    if (d < maxError)
                    {
                        grid.RemoveAt(index);
                        notDoneYet = true;
                        break;
                    }
                }
            }
        }
    }
}
