using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using MissionPlanner.Utilities;

namespace MPBackend.Core.Planning
{
    /// <summary>
    /// Port of <c>Grid.CreateSprayGrid</c> from the SprayGrid-Soleon Mission Planner plugin (the old
    /// ExtLibs/Utilities/Grid.cs): parallel strips across a polygon, cut around obstacles, ordered from a start
    /// position with an optional minimum lane separation. The private Grid helpers it used (closest line/point,
    /// intersections, point-in-polygon) are copied here unchanged so the output matches the plugin.
    /// Returned points carry Tag "S" (strip start) or "E" (strip end) and the requested altitude.
    /// </summary>
    public static class SprayGridGenerator
    {
        private const double rad2deg = 180 / Math.PI;
        private const double deg2rad = 1.0 / rad2deg;

        private struct Line
        {
            public utmpos p1;
            public utmpos p2;
            public utmpos basepnt;
        }

        public static List<PointLatLngAlt> Create(List<PointLatLngAlt> polygon,
            double altitude, double distance, double angle,
            SprayStartPosition startpos, float minLaneSeparation, PointLatLngAlt homeLocation,
            List<List<PointLatLngAlt>> obstacles, double shift, double barwidth,
            bool useextendedendpoint = true, PointLatLngAlt startPoint = null)
        {
            obstacles ??= new List<List<PointLatLngAlt>>();
            var obstacleVertexListUTM = new List<utmpos>();

            if (distance < 0.1)
                distance = 0.1;

            if (polygon.Count == 0)
                return new List<PointLatLngAlt>();

            // Make a non round number in case of corner cases
            if (minLaneSeparation != 0)
                minLaneSeparation += 0.5F;
            // Lane Separation in meters
            double minLaneSeparationINMeters = minLaneSeparation * distance;

            var ans = new List<PointLatLngAlt>();

            // utm zone distance calcs will be done in
            int utmzone = polygon[0].GetUTMZone();

            // utm position list
            var vertexListUTM = utmpos.ToList(PointLatLngAlt.ToUTM(utmzone, polygon), utmzone);
            // close the loop if its not already
            if (vertexListUTM[0] != vertexListUTM[vertexListUTM.Count - 1])
                vertexListUTM.Add(vertexListUTM[0]); // make a full loop

            // get mins/maxs of coverage area
            Rect area = getPolyMinMax(vertexListUTM);

            // used to determine the size of the outer grid area
            double areaDiagonalSize = area.DiagDistance();

            // somewhere to store out generated lines
            var grid = new List<Line>();
            // number of lines we need
            int lines = 0;

            // get start point middle
            double x = area.MidWidth;
            double y = area.MidHeight;

            // get left extent
            double xb1 = x;
            double yb1 = y;
            // to the left
            newPointAngleDistance(ref xb1, ref yb1, angle - 90, areaDiagonalSize / 2 + distance);
            // backwards
            newPointAngleDistance(ref xb1, ref yb1, angle + 180, areaDiagonalSize / 2 + distance);

            // set start point to left hand side
            x = xb1;
            y = yb1;

            // Shift the start point by "shift" meters
            newPointAngleDistance(ref x, ref y, angle + 90, shift);

            // draw the outergrid, this is a grid that cover the entire area of the rectangle plus more.
            while (lines < ((areaDiagonalSize + distance * 2) / distance))
            {
                // copy the start point to generate the end point
                double nx = x;
                double ny = y;
                newPointAngleDistance(ref nx, ref ny, angle, areaDiagonalSize + distance * 2);

                var line = new Line
                {
                    p1 = new utmpos(x, y, utmzone),
                    p2 = new utmpos(nx, ny, utmzone),
                    basepnt = new utmpos(x, y, utmzone)
                };
                grid.Add(line);

                newPointAngleDistance(ref x, ref y, angle + 90, distance);
                lines++;
            }

            // find intersections with our polygon

            // store lines that dont have any intersections
            var linesToRemove = new List<Line>();

            int gridLinesCount = grid.Count;

            // cycle through our grid, every grindline
            for (int gridLineNo = 0; gridLineNo < gridLinesCount; gridLineNo++)
            {
                double closestdistance = double.MaxValue;
                double farestdistance = double.MinValue;

                utmpos closestpoint = utmpos.Zero;
                utmpos farestpoint = utmpos.Zero;

                // somewhere to store our intersections
                var intersections = new List<utmpos>();

                int vertexNo = -1;
                int crosses = 0;
                utmpos intersectionPointUTM;
                // Go through every vertex in the polygon
                foreach (utmpos pnt in vertexListUTM)
                {
                    vertexNo++;
                    // Don't check the first vertex
                    if (vertexNo == 0)
                        continue;

                    // Find and intersection of two lines
                    intersectionPointUTM = FindLineIntersection(vertexListUTM[vertexNo - 1], vertexListUTM[vertexNo],
                        grid[gridLineNo].p1, grid[gridLineNo].p2);

                    if (!intersectionPointUTM.IsZero)
                    {
                        crosses++;
                        intersections.Add(intersectionPointUTM);
                        if (closestdistance > grid[gridLineNo].p1.GetDistance(intersectionPointUTM))
                        {
                            closestpoint.y = intersectionPointUTM.y;
                            closestpoint.x = intersectionPointUTM.x;
                            closestpoint.zone = intersectionPointUTM.zone;
                            closestdistance = grid[gridLineNo].p1.GetDistance(intersectionPointUTM);
                        }

                        if (farestdistance < grid[gridLineNo].p1.GetDistance(intersectionPointUTM))
                        {
                            farestpoint.y = intersectionPointUTM.y;
                            farestpoint.x = intersectionPointUTM.x;
                            farestpoint.zone = intersectionPointUTM.zone;
                            farestdistance = grid[gridLineNo].p1.GetDistance(intersectionPointUTM);
                        }
                    }
                }

                // start obstacles check------------------------------------------
                foreach (var obstacleVertexList in obstacles)
                {
                    obstacleVertexListUTM.Clear();
                    if (obstacleVertexList.Count >= 3)
                    {
                        obstacleVertexListUTM = utmpos.ToList(PointLatLngAlt.ToUTM(utmzone, obstacleVertexList), utmzone);

                        if (obstacleVertexListUTM[0] != obstacleVertexListUTM[obstacleVertexListUTM.Count - 1])
                            obstacleVertexListUTM.Add(obstacleVertexListUTM[0]); // make a full loop

                        if (barwidth > 0)
                            InflateVertex(obstacleVertexListUTM, barwidth / 2);
                    }
                    else
                    {
                        // Ignore non polygons and empty polygons
                        continue;
                    }

                    // Check if obstacle is inside our polygon
                    bool obstacleinside = true;
                    foreach (var p in obstacleVertexListUTM)
                    {
                        if (!PointInPolygon(p, vertexListUTM))
                        {
                            obstacleinside = false;
                            break;
                        }
                    }

                    if (!obstacleinside) continue;

                    vertexNo = -1;
                    foreach (utmpos pnt in obstacleVertexListUTM)
                    {
                        vertexNo++;
                        // Don't check the first vertex
                        if (vertexNo == 0)
                            continue;

                        intersectionPointUTM = FindLineIntersection(obstacleVertexListUTM[vertexNo - 1],
                            obstacleVertexListUTM[vertexNo], grid[gridLineNo].p1, grid[gridLineNo].p2);

                        if (!intersectionPointUTM.IsZero)
                        {
                            crosses++;
                            intersections.Add(intersectionPointUTM);
                            if (closestdistance > grid[gridLineNo].p1.GetDistance(intersectionPointUTM))
                            {
                                closestpoint.y = intersectionPointUTM.y;
                                closestpoint.x = intersectionPointUTM.x;
                                closestpoint.zone = intersectionPointUTM.zone;
                                closestdistance = grid[gridLineNo].p1.GetDistance(intersectionPointUTM);
                            }

                            if (farestdistance < grid[gridLineNo].p1.GetDistance(intersectionPointUTM))
                            {
                                farestpoint.y = intersectionPointUTM.y;
                                farestpoint.x = intersectionPointUTM.x;
                                farestpoint.zone = intersectionPointUTM.zone;
                                farestdistance = grid[gridLineNo].p1.GetDistance(intersectionPointUTM);
                            }
                        }
                    }
                }

                if (crosses == 0) // outside our polygon
                {
                    if (!PointInPolygon(grid[gridLineNo].p1, vertexListUTM) &&
                        !PointInPolygon(grid[gridLineNo].p2, vertexListUTM))
                        linesToRemove.Add(grid[gridLineNo]);
                }
                else if (crosses == 1) // bad - shouldnt happen
                {
                }
                else if (crosses == 2) // simple start and finish
                {
                    Line line = grid[gridLineNo];
                    line.p1 = closestpoint;
                    line.p2 = farestpoint;
                    grid[gridLineNo] = line;
                }
                else // multiple intersections
                {
                    // Remove the full line and add then neccessary lines separately
                    Line line = grid[gridLineNo];
                    linesToRemove.Add(line);

                    while (intersections.Count > 1)
                    {
                        var newline = new Line();

                        closestpoint = findClosestPoint(closestpoint, intersections);
                        newline.p1 = closestpoint;
                        intersections.Remove(closestpoint);

                        closestpoint = findClosestPoint(closestpoint, intersections);
                        newline.p2 = closestpoint;
                        intersections.Remove(closestpoint);

                        newline.basepnt = line.basepnt;

                        grid.Add(newline);
                    }
                }
            }
            // End obstacles check

            // cleanup and keep only lines that pass though our polygon
            foreach (Line line in linesToRemove)
                grid.Remove(line);

            if (grid.Count == 0)
                return ans;

            // pick start positon based on initial point rectangle
            utmpos startposutm;

            switch (startpos)
            {
                default:
                case SprayStartPosition.Home:
                    startposutm = new utmpos(homeLocation);
                    break;
                case SprayStartPosition.BottomLeft:
                    startposutm = new utmpos(area.Left, area.Bottom, utmzone);
                    break;
                case SprayStartPosition.BottomRight:
                    startposutm = new utmpos(area.Right, area.Bottom, utmzone);
                    break;
                case SprayStartPosition.TopLeft:
                    startposutm = new utmpos(area.Left, area.Top, utmzone);
                    break;
                case SprayStartPosition.TopRight:
                    startposutm = new utmpos(area.Right, area.Top, utmzone);
                    break;
                case SprayStartPosition.Point:
                    startposutm = new utmpos(startPoint ?? homeLocation);
                    break;
            }

            Line closest;
            utmpos lastpnt;

            if (startpos == SprayStartPosition.Point)
            {
                // Always start from this point whatever it takes
                closest = grid[0];
                lastpnt = new utmpos(startPoint ?? homeLocation);
                // find line in the grid which has the point lastpnt
                foreach (Line line in grid)
                {
                    if (line.p1.GetDistance(lastpnt) < 0.1)
                    {
                        closest = line;
                        break;
                    }

                    if (line.p2.GetDistance(lastpnt) < 0.1)
                    {
                        closest = line;
                        break;
                    }
                }
            }
            else
            {
                // find the closes polygon point based from our startpos selection
                startposutm = findClosestPoint(startposutm, vertexListUTM);
                // find closest line point to startpos
                closest = findClosestLine(startposutm, grid, 0 /*Lane separation does not apply to starting point*/, angle);
                // get the closes point from the line we picked
                if (closest.p1.GetDistance(startposutm) < closest.p2.GetDistance(startposutm))
                    lastpnt = closest.p1;
                else
                    lastpnt = closest.p2;
            }

            // S = start, E = end
            while (grid.Count > 0)
            {
                // for each line, check which end of the line is the next closest
                if (closest.p1.GetDistance(lastpnt) < closest.p2.GetDistance(lastpnt))
                {
                    utmpos newstart = closest.p1;
                    newstart.Tag = "S";
                    ans.Add(newstart);

                    utmpos newend = closest.p2;
                    newend.Tag = "E";
                    ans.Add(newend);

                    lastpnt = closest.p2;

                    grid.Remove(closest);
                    if (grid.Count == 0)
                        break;

                    if (useextendedendpoint)
                        closest = findClosestLine(newend, grid, minLaneSeparationINMeters, angle);
                    else
                        closest = findClosestLine(closest.p2, grid, minLaneSeparationINMeters, angle);
                }
                else
                {
                    utmpos newstart = closest.p2;
                    newstart.Tag = "S";
                    ans.Add(newstart);

                    utmpos newend = closest.p1;
                    newend.Tag = "E";
                    ans.Add(newend);

                    lastpnt = closest.p1;

                    grid.Remove(closest);
                    if (grid.Count == 0)
                        break;

                    if (useextendedendpoint)
                        closest = findClosestLine(newend, grid, minLaneSeparationINMeters, angle);
                    else
                        closest = findClosestLine(closest.p1, grid, minLaneSeparationINMeters, angle);
                }
            }

            // set the altitude on all points
            ans.ForEach(plla => { plla.Alt = altitude; });
            return ans;
        }

        /// <summary>Grow a closed UTM polygon by <paramref name="meters"/> (Clipper2 mitre offset).</summary>
        public static void InflateVertex(List<utmpos> vertex, double meters)
        {
            var input = new PathsD();
            var inputPath = new PathD();

            int zone = vertex[0].zone;

            for (int i = 0; i < vertex.Count; i++)
                inputPath.Add(new PointD(vertex[i].x, vertex[i].y));
            input.Add(inputPath);
            var output = Clipper.InflatePaths(input, meters, JoinType.Miter, EndType.Polygon);
            vertex.Clear();
            if (output.Count == 0)
                return;
            foreach (var p in output[0])
                vertex.Add(new utmpos(p.x, p.y, zone));
            if (vertex[0] != vertex[vertex.Count - 1])
                vertex.Add(vertex[0]); // make a full loop
        }

        /// <summary>Area of a lat/lng polygon in m² (UTM shoelace, as the plugin's calcpolygonarea).</summary>
        public static double PolygonArea(List<PointLatLngAlt> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return 0;
            int utmzone = polygon[0].GetUTMZone();
            var utm = utmpos.ToList(PointLatLngAlt.ToUTM(utmzone, polygon), utmzone);
            if (utm[0] != utm[utm.Count - 1])
                utm.Add(utm[0]);
            double prod1 = 0, prod2 = 0;
            for (int a = 0; a < utm.Count - 1; a++)
            {
                prod1 += utm[a].x * utm[a + 1].y;
                prod2 += utm[a].y * utm[a + 1].x;
            }

            return Math.Abs((prod1 - prod2) / 2);
        }

        // ---- helpers copied from MissionPlanner.Utilities.Grid (private there) ----

        private static Rect getPolyMinMax(List<utmpos> utmpos)
        {
            if (utmpos.Count == 0)
                return new Rect();

            double minx, miny, maxx, maxy;

            minx = maxx = utmpos[0].x;
            miny = maxy = utmpos[0].y;

            foreach (utmpos pnt in utmpos)
            {
                minx = Math.Min(minx, pnt.x);
                maxx = Math.Max(maxx, pnt.x);

                miny = Math.Min(miny, pnt.y);
                maxy = Math.Max(maxy, pnt.y);
            }

            return new Rect(minx, maxy, maxx - minx, miny - maxy);
        }

        private static void newPointAngleDistance(ref double x, ref double y, double bearing, double distance)
        {
            double degN = 90 - bearing;
            if (degN < 0)
                degN += 360;
            x = x + distance * Math.Cos(degN * deg2rad);
            y = y + distance * Math.Sin(degN * deg2rad);
        }

        // polar to rectangular
        private static utmpos newpos(utmpos input, double bearing, double distance)
        {
            double degN = 90 - bearing;
            if (degN < 0)
                degN += 360;
            double x = input.x + distance * Math.Cos(degN * deg2rad);
            double y = input.y + distance * Math.Sin(degN * deg2rad);

            return new utmpos(x, y, input.zone);
        }

        /// <summary>Segment/segment intersection, Zero when none.</summary>
        public static utmpos FindLineIntersection(utmpos start1, utmpos end1, utmpos start2, utmpos end2)
        {
            double denom = ((end1.x - start1.x) * (end2.y - start2.y)) - ((end1.y - start1.y) * (end2.x - start2.x));
            //  AB & CD are parallel
            if (denom == 0)
                return utmpos.Zero;
            double numer = ((start1.y - start2.y) * (end2.x - start2.x)) - ((start1.x - start2.x) * (end2.y - start2.y));
            double r = numer / denom;
            double numer2 = ((start1.y - start2.y) * (end1.x - start1.x)) - ((start1.x - start2.x) * (end1.y - start1.y));
            double s = numer2 / denom;
            if ((r < 0 || r > 1) || (s < 0 || s > 1))
                return utmpos.Zero;
            // Find intersection point
            var result = new utmpos();
            result.x = start1.x + (r * (end1.x - start1.x));
            result.y = start1.y + (r * (end1.y - start1.y));
            result.zone = start1.zone;
            return result;
        }

        /// <summary>Line/line intersection of the extended lines.</summary>
        private static utmpos FindLineIntersectionExtension(utmpos start1, utmpos end1, utmpos start2, utmpos end2)
        {
            double denom = ((end1.x - start1.x) * (end2.y - start2.y)) - ((end1.y - start1.y) * (end2.x - start2.x));
            //  AB & CD are parallel
            if (denom == 0)
                return utmpos.Zero;
            double numer = ((start1.y - start2.y) * (end2.x - start2.x)) -
                           ((start1.x - start2.x) * (end2.y - start2.y));
            double r = numer / denom;
            // Find intersection point
            var result = new utmpos();
            result.x = start1.x + (r * (end1.x - start1.x));
            result.y = start1.y + (r * (end1.y - start1.y));
            result.zone = start1.zone;
            return result;
        }

        private static utmpos findClosestPoint(utmpos start, List<utmpos> list)
        {
            utmpos answer = utmpos.Zero;
            double currentbest = double.MaxValue;

            foreach (utmpos pnt in list)
            {
                double dist1 = start.GetDistance(pnt);

                if (dist1 < currentbest)
                {
                    answer = pnt;
                    currentbest = dist1;
                }
            }

            return answer;
        }

        // Add an angle while normalizing output in the range 0...360
        private static double AddAngle(double angle, double degrees)
        {
            angle += degrees;
            angle = angle % 360;
            while (angle < 0)
                angle += 360;
            return angle;
        }

        private static Line findClosestLine(utmpos start, List<Line> list, double minDistance, double angle)
        {
            if (minDistance == 0)
            {
                Line answer = list[0];
                double shortest = double.MaxValue;

                foreach (Line line in list)
                {
                    double ans1 = start.GetDistance(line.p1);
                    double ans2 = start.GetDistance(line.p2);
                    utmpos shorterpnt = ans1 < ans2 ? line.p1 : line.p2;

                    if (shortest > start.GetDistance(shorterpnt))
                    {
                        answer = line;
                        shortest = start.GetDistance(shorterpnt);
                    }
                }

                return answer;
            }

            // By now, just add 5.000 km to our lines so they are long enough to allow intersection
            double METERS_TO_EXTEND = 5000;

            double perperndicularOrientation = AddAngle(angle, 90);

            // Calculation of a perpendicular line to the grid lines containing the "start" point
            utmpos start_perpendicular_line = newpos(start, perperndicularOrientation, -METERS_TO_EXTEND);
            utmpos stop_perpendicular_line = newpos(start, perperndicularOrientation, METERS_TO_EXTEND);

            // Store one intersection point per grid line
            var intersectedPoints = new Dictionary<utmpos, Line>();
            // lets order distances from every intersected point per line with the "start" point
            var ordered_min_to_max = new Dictionary<double, utmpos>();

            foreach (Line line in list)
            {
                // Calculate intersection point
                utmpos p = FindLineIntersectionExtension(line.p1, line.p2, start_perpendicular_line, stop_perpendicular_line);

                // Store it
                intersectedPoints[p] = line;

                // Calculate distances between interesected point and "start" (i.e. line and start)
                double distance_p = start.GetDistance(p);

                if (!ordered_min_to_max.ContainsKey(distance_p))
                    ordered_min_to_max.Add(distance_p, p);
            }

            // Acquire keys and sort them.
            var ordered_keys = ordered_min_to_max.Keys.ToList();
            ordered_keys.Sort();

            // Lets select a line that is the closest to "start" point but "mindistance" away at least.
            // If we have only one line, return that line whatever the minDistance says
            double key = double.MaxValue;
            int i = 0;
            while (key == double.MaxValue && i < ordered_keys.Count)
            {
                if (ordered_keys[i] >= minDistance)
                    key = ordered_keys[i];
                i++;
            }

            // If no line is selected (because all of them are closer than minDistance, then get the farest one
            if (key == double.MaxValue)
                key = ordered_keys[ordered_keys.Count - 1];

            var filteredlist = intersectedPoints.Where(a => a.Key.GetDistance(start) >= key);

            return findClosestLine(start, filteredlist.Select(a => a.Value).ToList(), 0, angle);
        }

        private static bool PointInPolygon(utmpos p, List<utmpos> poly)
        {
            utmpos p1, p2;
            bool inside = false;

            if (poly.Count < 3)
                return inside;

            var oldPoint = new utmpos(poly[poly.Count - 1]);

            for (int i = 0; i < poly.Count; i++)
            {
                var newPoint = new utmpos(poly[i]);

                if (newPoint.y > oldPoint.y)
                {
                    p1 = oldPoint;
                    p2 = newPoint;
                }
                else
                {
                    p1 = newPoint;
                    p2 = oldPoint;
                }

                if ((newPoint.y < p.y) == (p.y <= oldPoint.y)
                    && (p.x - p1.x) * (p2.y - p1.y) < (p2.x - p1.x) * (p.y - p1.y))
                {
                    inside = !inside;
                }

                oldPoint = newPoint;
            }

            return inside;
        }
    }
}
