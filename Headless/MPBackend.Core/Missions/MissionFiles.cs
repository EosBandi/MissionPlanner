using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MissionPlanner.Utilities;
using Newtonsoft.Json;

namespace MPBackend.Core.Missions
{
    public enum MissionFileFormat
    {
        /// <summary>QGC WPL 110 text (.waypoints/.txt) as written by Mission Planner.</summary>
        Waypoints,
        /// <summary>QGroundControl JSON plan (.plan); carries mission, fence and rally.</summary>
        Plan,
        /// <summary>KML LineString (export for Google Earth; import as waypoints like MP).</summary>
        Kml
    }

    /// <summary>
    /// Mission file import/export. Reading .waypoints uses MP's WaypointFile; writing follows FlightPlanner's
    /// savewaypoints format. .plan is read/written with MP's MissionFile model but keeps per-item frames and
    /// includes geofence and rally sections.
    /// </summary>
    public static class MissionFiles
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static MissionFileContent Parse(string fileName, string content)
        {
            content ??= "";
            var trimmed = content.TrimStart('﻿', ' ', '\r', '\n', '\t');
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();

            if (trimmed.StartsWith("QGC WPL"))
                return ParseWaypoints(content);
            if (trimmed.StartsWith("{"))
                return ParsePlan(content);
            if (trimmed.StartsWith("<") || ext == ".kml")
                return ParseKml(content);
            throw new FormatException("unrecognised mission file (expected QGC WPL, .plan JSON or KML)");
        }

        public static string Write(MissionFileFormat format, MissionPlan mission, MissionPlan fence = null,
            MissionPlan rally = null)
        {
            switch (format)
            {
                case MissionFileFormat.Waypoints:
                    return WriteWaypoints(mission ?? fence ?? rally ?? new MissionPlan());
                case MissionFileFormat.Plan:
                    return WritePlan(mission, fence, rally);
                case MissionFileFormat.Kml:
                    return WriteKml(mission ?? new MissionPlan());
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public static string Extension(MissionFileFormat format) => format switch
        {
            MissionFileFormat.Plan => ".plan",
            MissionFileFormat.Kml => ".kml",
            _ => ".waypoints"
        };

        // ---- .waypoints (QGC WPL 110) ----

        private static MissionFileContent ParseWaypoints(string content)
        {
            var temp = Path.GetTempFileName();
            List<Locationwp> list;
            try
            {
                File.WriteAllText(temp, content);
                list = WaypointFile.ReadWaypointFile(temp);
            }
            finally
            {
                File.Delete(temp);
            }

            // line 0 is always home in MP's format
            var home = list.Count > 0 && (list[0].lat != 0 || list[0].lng != 0)
                ? new GeoPoint(list[0].lat, list[0].lng, list[0].alt)
                : null;
            var items = list.Skip(1).Select(MissionItem.From).ToList();
            var plan = new MissionPlan { Type = MissionCommands.Classify(items), Home = home, Items = items };

            var result = new MissionFileContent { Format = "waypoints" };
            switch (plan.Type)
            {
                case MAVLink.MAV_MISSION_TYPE.FENCE:
                    result.Fence = plan;
                    break;
                case MAVLink.MAV_MISSION_TYPE.RALLY:
                    result.Rally = plan;
                    break;
                default:
                    result.Mission = plan;
                    break;
            }

            return result;
        }

        private static string WriteWaypoints(MissionPlan plan)
        {
            var sb = new StringBuilder();
            sb.Append("QGC WPL 110\n");
            var home = plan.Home ?? new GeoPoint(0, 0, 0);
            sb.Append(string.Join("\t", "0", "1", "0", "16", "0", "0", "0", "0",
                home.Lat.ToString("0.0000000", Inv), home.Lng.ToString("0.0000000", Inv),
                home.Alt.ToString("0.000000", Inv), "1")).Append('\n');

            var seq = 1;
            foreach (var item in plan.Items)
            {
                var wp = item.ToLocationwp();
                sb.Append(string.Join("\t",
                    (seq++).ToString(Inv), "0", wp.frame.ToString(Inv), wp.id.ToString(Inv),
                    F8(wp.p1), F8(wp.p2), F8(wp.p3), F8(wp.p4), F8(wp.lat), F8(wp.lng),
                    ((double)wp.alt).ToString("0.000000", Inv), "1")).Append('\n');
            }

            return sb.ToString();
        }

        private static string F8(double v) => v.ToString("0.00000000", Inv);

        // ---- .plan (QGroundControl JSON) ----

        private static MissionFileContent ParsePlan(string content)
        {
            var root = JsonConvert.DeserializeObject<MissionFile.RootObject>(content)
                       ?? throw new FormatException("empty plan file");
            var result = new MissionFileContent { Format = "plan" };

            if (root.mission != null)
            {
                var items = new List<MissionItem>();
                foreach (var item in root.mission.items ?? new List<MissionFile.Item>())
                {
                    if (item.type == "ComplexItem")
                        items.AddRange((item.TransectStyleComplexItem?.Items ?? new List<MissionFile.Item>())
                            .Select(i => MissionItem.From(i)));
                    else if (item.type == null || item.type == "SimpleItem")
                        items.Add(MissionItem.From(item));
                }

                var h = root.mission.plannedHomePosition;
                result.Mission = new MissionPlan
                {
                    Type = MAVLink.MAV_MISSION_TYPE.MISSION,
                    Home = h != null && h.Count >= 3 ? new GeoPoint(h[0], h[1], h[2]) : null,
                    Items = items
                };
            }

            if (root.geoFence != null)
            {
                var items = new List<MissionItem>();
                foreach (var poly in root.geoFence.polygons ?? new List<MissionFile.Polygon>())
                {
                    var cmd = poly.inclusion
                        ? MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION
                        : MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_EXCLUSION;
                    items.AddRange(poly.polygon.Select(p => FenceItem(cmd, poly.polygon.Count, p[0], p[1])));
                }

                foreach (var circle in root.geoFence.circles ?? new List<MissionFile.Circle>())
                {
                    var cmd = circle.inclusion
                        ? MAVLink.MAV_CMD.FENCE_CIRCLE_INCLUSION
                        : MAVLink.MAV_CMD.FENCE_CIRCLE_EXCLUSION;
                    items.Add(FenceItem(cmd, (float)circle.circle.radius, circle.circle.center[0],
                        circle.circle.center[1]));
                }

                if (items.Count > 0)
                    result.Fence = new MissionPlan { Type = MAVLink.MAV_MISSION_TYPE.FENCE, Items = items };
            }

            if (root.rallyPoints?.points?.Count > 0)
            {
                result.Rally = new MissionPlan
                {
                    Type = MAVLink.MAV_MISSION_TYPE.RALLY,
                    Items = root.rallyPoints.points.Select(p => new MissionItem
                    {
                        Command = (ushort)MAVLink.MAV_CMD.RALLY_POINT,
                        CommandName = nameof(MAVLink.MAV_CMD.RALLY_POINT),
                        Frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
                        Lat = p[0],
                        Lng = p[1],
                        Alt = p.Count > 2 ? (float)p[2] : 0
                    }).ToList()
                };
            }

            return result;
        }

        private static MissionItem FenceItem(MAVLink.MAV_CMD cmd, float p1, double lat, double lng) => new MissionItem
        {
            Command = (ushort)cmd,
            CommandName = cmd.ToString(),
            Frame = MAVLink.MAV_FRAME.GLOBAL,
            Param1 = p1,
            Lat = lat,
            Lng = lng
        };

        private static string WritePlan(MissionPlan mission, MissionPlan fence, MissionPlan rally)
        {
            var home = mission?.Home ?? new GeoPoint(0, 0, 0);
            var seq = 1;
            var root = new MissionFile.RootObject
            {
                fileType = "Plan",
                groundStation = "MissionPlanner",
                version = 1,
                mission = new MissionFile.Mission
                {
                    version = 2,
                    firmwareType = (int)MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA,
                    vehicleType = (int)MAVLink.MAV_TYPE.QUADROTOR,
                    cruiseSpeed = 15,
                    hoverSpeed = 5,
                    plannedHomePosition = new List<double> { home.Lat, home.Lng, home.Alt },
                    items = (mission?.Items ?? new List<MissionItem>()).Select(i =>
                    {
                        MissionFile.Item item = i.ToLocationwp();
                        item.type = "SimpleItem";
                        item.autoContinue = true;
                        item.doJumpId = seq++;
                        return item;
                    }).ToList()
                },
                geoFence = new MissionFile.GeoFence
                {
                    version = 2,
                    polygons = FencePolygons(fence),
                    circles = Resolved(fence)
                        .Where(i => i.Command == (ushort)MAVLink.MAV_CMD.FENCE_CIRCLE_INCLUSION ||
                                    i.Command == (ushort)MAVLink.MAV_CMD.FENCE_CIRCLE_EXCLUSION)
                        .Select(i => new MissionFile.Circle
                        {
                            version = 1,
                            inclusion = i.Command == (ushort)MAVLink.MAV_CMD.FENCE_CIRCLE_INCLUSION,
                            circle = new MissionFile.Circle2 { center = new List<double> { i.Lat, i.Lng }, radius = i.Param1 }
                        }).ToList()
                },
                rallyPoints = new MissionFile.RallyPoints
                {
                    version = 2,
                    points = (rally?.Items ?? new List<MissionItem>())
                        .Select(i => new List<double> { i.Lat, i.Lng, i.Alt }).ToList()
                }
            };

            return JsonConvert.SerializeObject(root, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        /// <summary>
        /// Group consecutive polygon vertices: param1 of each vertex is the vertex count of its polygon.
        /// </summary>
        private static List<MissionFile.Polygon> FencePolygons(MissionPlan fence)
        {
            var result = new List<MissionFile.Polygon>();
            var items = Resolved(fence);
            for (var i = 0; i < items.Count;)
            {
                var cmd = items[i].Command;
                if (cmd != (ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION &&
                    cmd != (ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_EXCLUSION)
                {
                    i++;
                    continue;
                }

                var count = Math.Max(1, (int)items[i].Param1);
                var vertices = items.Skip(i).Take(count).ToList();
                result.Add(new MissionFile.Polygon
                {
                    version = 1,
                    inclusion = cmd == (ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION,
                    polygon = vertices.Select(v => new List<double> { v.Lat, v.Lng }).ToList()
                });
                i += vertices.Count;
            }

            return result;
        }

        /// <summary>Items with <see cref="MissionItem.Command"/> filled in from CommandName where needed.</summary>
        private static List<MissionItem> Resolved(MissionPlan plan) =>
            (plan?.Items ?? new List<MissionItem>()).Select(i => MissionItem.From(i.ToLocationwp())).ToList();

        // ---- KML ----

        private static MissionFileContent ParseKml(string content)
        {
            var doc = XDocument.Parse(content);
            var items = new List<MissionItem>();
            // like FlightPlanner.processKMLMission: every LineString vertex becomes a waypoint
            foreach (var coords in doc.Descendants().Where(e => e.Name.LocalName == "LineString")
                         .SelectMany(ls => ls.Elements().Where(e => e.Name.LocalName == "coordinates")))
            {
                foreach (var tuple in coords.Value.Split(new[] { ' ', '\n', '\r', '\t' },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = tuple.Split(',');
                    if (parts.Length < 2)
                        continue;
                    items.Add(new MissionItem
                    {
                        Command = (ushort)MAVLink.MAV_CMD.WAYPOINT,
                        CommandName = nameof(MAVLink.MAV_CMD.WAYPOINT),
                        Frame = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
                        Lng = double.Parse(parts[0], Inv),
                        Lat = double.Parse(parts[1], Inv),
                        Alt = parts.Length > 2 ? float.Parse(parts[2], Inv) : 0
                    });
                }
            }

            if (items.Count == 0)
                throw new FormatException("KML contains no LineString");
            return new MissionFileContent
            {
                Format = "kml",
                Mission = new MissionPlan { Type = MAVLink.MAV_MISSION_TYPE.MISSION, Items = items }
            };
        }

        private static string WriteKml(MissionPlan plan)
        {
            XNamespace ns = "http://www.opengis.net/kml/2.2";
            var located = plan.Items.Where(i => i.Lat != 0 || i.Lng != 0).ToList();
            string Coord(double lat, double lng, double alt) =>
                $"{lng.ToString("0.0000000", Inv)},{lat.ToString("0.0000000", Inv)},{alt.ToString("0.###", Inv)}";

            var doc = new XDocument(new XElement(ns + "kml",
                new XElement(ns + "Document",
                    new XElement(ns + "name", "Mission"),
                    new XElement(ns + "Placemark",
                        new XElement(ns + "name", "Route"),
                        new XElement(ns + "LineString",
                            new XElement(ns + "altitudeMode", "relativeToGround"),
                            new XElement(ns + "coordinates",
                                string.Join(" ", located.Select(i => Coord(i.Lat, i.Lng, i.Alt)))))),
                    located.Select((i, n) => new XElement(ns + "Placemark",
                        new XElement(ns + "name", $"{n + 1} {i.CommandName}"),
                        new XElement(ns + "Point",
                            new XElement(ns + "altitudeMode", "relativeToGround"),
                            new XElement(ns + "coordinates", Coord(i.Lat, i.Lng, i.Alt))))))));
            return doc.Declaration + doc.ToString();
        }
    }
}
