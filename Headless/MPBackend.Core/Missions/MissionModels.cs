using System;
using System.Collections.Generic;
using System.Linq;
using MissionPlanner.Utilities;

namespace MPBackend.Core.Missions
{
    /// <summary>
    /// One MISSION_ITEM(_INT): a row in MP's Flight Plan grid. Units are raw MAVLink (degrees, metres).
    /// </summary>
    public sealed class MissionItem
    {
        /// <summary>MAV_CMD id. When 0 and <see cref="CommandName"/> is set, the name is used.</summary>
        public ushort Command { get; set; }

        /// <summary>MAV_CMD name as MP names it (e.g. WAYPOINT, TAKEOFF, FENCE_POLYGON_VERTEX_INCLUSION).</summary>
        public string CommandName { get; set; }

        public MAVLink.MAV_FRAME Frame { get; set; } = MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT;
        public float Param1 { get; set; }
        public float Param2 { get; set; }
        public float Param3 { get; set; }
        public float Param4 { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public float Alt { get; set; }

        internal static MissionItem From(Locationwp wp) => new MissionItem
        {
            Command = wp.id,
            CommandName = Enum.IsDefined(typeof(MAVLink.MAV_CMD), wp.id) ? ((MAVLink.MAV_CMD)wp.id).ToString() : null,
            Frame = (MAVLink.MAV_FRAME)wp.frame,
            Param1 = wp.p1,
            Param2 = wp.p2,
            Param3 = wp.p3,
            Param4 = wp.p4,
            Lat = wp.lat,
            Lng = wp.lng,
            Alt = wp.alt
        };

        internal Locationwp ToLocationwp()
        {
            var id = Command;
            if (id == 0 && !string.IsNullOrWhiteSpace(CommandName))
            {
                var name = CommandName.Trim();
                foreach (var prefix in new[] { "MAV_CMD_NAV_", "MAV_CMD_", "NAV_" })
                    if (!Enum.IsDefined(typeof(MAVLink.MAV_CMD), name.ToUpperInvariant()) &&
                        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(prefix.Length);
                if (!Enum.TryParse<MAVLink.MAV_CMD>(name, true, out var cmd))
                    throw new ArgumentException($"unknown command '{CommandName}'");
                id = (ushort)cmd;
            }

            return new Locationwp
            {
                id = id,
                frame = (byte)Frame,
                p1 = Param1,
                p2 = Param2,
                p3 = Param3,
                p4 = Param4,
                lat = Lat,
                lng = Lng,
                alt = Alt
            };
        }
    }

    /// <summary>Home position (absolute altitude, metres AMSL).</summary>
    public sealed record GeoPoint(double Lat, double Lng, double Alt);

    /// <summary>
    /// A mission, fence or rally list. For ArduPilot missions, item 0 on the vehicle is home; it is split out
    /// into <see cref="Home"/> so <see cref="Items"/> holds only the user's commands.
    /// </summary>
    public sealed class MissionPlan
    {
        public MAVLink.MAV_MISSION_TYPE Type { get; set; } = MAVLink.MAV_MISSION_TYPE.MISSION;
        public GeoPoint Home { get; set; }
        public List<MissionItem> Items { get; set; } = new List<MissionItem>();
    }

    /// <summary>
    /// Result of parsing a file; a QGC .plan can carry all three lists.
    /// </summary>
    public sealed class MissionFileContent
    {
        public string Format { get; set; }
        public MissionPlan Mission { get; set; }
        public MissionPlan Fence { get; set; }
        public MissionPlan Rally { get; set; }

        public IEnumerable<MissionPlan> Plans => new[] { Mission, Fence, Rally }.Where(p => p != null);
    }

    public static class MissionCommands
    {
        public static bool IsFence(ushort id) => id >= (ushort)MAVLink.MAV_CMD.FENCE_RETURN_POINT &&
                                                 id <= (ushort)MAVLink.MAV_CMD.FENCE_HOME_CIRCLE_INCLUSION;

        public static bool IsRally(ushort id) => id == (ushort)MAVLink.MAV_CMD.RALLY_POINT;

        /// <summary>Guess the list type from its commands (MP stores all three in .waypoints files).</summary>
        public static MAVLink.MAV_MISSION_TYPE Classify(IReadOnlyCollection<MissionItem> items)
        {
            if (items.Count > 0 && items.All(i => IsFence(i.Command)))
                return MAVLink.MAV_MISSION_TYPE.FENCE;
            if (items.Count > 0 && items.All(i => IsRally(i.Command)))
                return MAVLink.MAV_MISSION_TYPE.RALLY;
            return MAVLink.MAV_MISSION_TYPE.MISSION;
        }
    }
}
