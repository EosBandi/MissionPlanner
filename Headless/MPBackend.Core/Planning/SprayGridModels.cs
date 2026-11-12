using System.Collections.Generic;
using MPBackend.Core.Missions;
using MPBackend.Core.Terrain;

namespace MPBackend.Core.Planning
{
    /// <summary>Where the first strip starts (same meaning as Mission Planner's Grid.StartPosition).</summary>
    public enum SprayStartPosition
    {
        Home = 0,
        BottomLeft = 1,
        TopLeft = 2,
        BottomRight = 3,
        TopRight = 4,
        /// <summary>Start at <see cref="SprayGridRequest.StartPoint"/> (a grid S/E point from a previous preview).</summary>
        Point = 5
    }

    /// <summary>Altitude frame of the waypoints (Mission Planner's Flight Planner "altmode").</summary>
    public enum SprayAltitudeReference
    {
        Relative,
        Absolute,
        Terrain
    }

    public enum SpraySplitMode
    {
        None,
        /// <summary><see cref="SprayGridRequest.SplitValue"/> is minutes per segment (including the flight home).</summary>
        Time,
        /// <summary><see cref="SprayGridRequest.SplitValue"/> is the number of segments.</summary>
        Segments
    }

    /// <summary>Where terrain heights come from for altitude tracking and relative-altitude correction.</summary>
    public enum SprayElevationSource
    {
        /// <summary>No terrain: every waypoint gets the requested altitude unchanged.</summary>
        None,
        /// <summary>The backend's srtm store (GeoTIFF/DTED files, SRTM tiles downloaded on demand).</summary>
        Backend,
        /// <summary>A height grid supplied by the client in <see cref="SprayGridRequest.Elevation"/>.</summary>
        Raster
    }

    /// <summary>
    /// A regular lat/lng grid of heights (metres AMSL) covering the field, sampled by the client (e.g. from Cesium
    /// terrain). Row 0 is the north edge, column 0 the west edge; <see cref="Heights"/> is row-major with
    /// <c>Rows * Cols</c> entries, null where unknown.
    /// </summary>
    public sealed class ElevationRaster
    {
        public double North { get; set; }
        public double South { get; set; }
        public double West { get; set; }
        public double East { get; set; }
        public int Cols { get; set; }
        public int Rows { get; set; }
        public double?[] Heights { get; set; }
    }

    /// <summary>Home / launch position; <see cref="Alt"/> (metres AMSL) is sampled from terrain when null.</summary>
    public sealed class SprayHome
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double? Alt { get; set; }
    }

    /// <summary>
    /// All inputs of the SprayGrid planner: the field, obstacles, home and the parameters of the old
    /// SprayGrid-Soleon plugin dialog. Units are metres, m/s, degrees, litres.
    /// </summary>
    public sealed class SprayGridRequest
    {
        /// <summary>Field boundary, at least 3 vertices, not necessarily closed.</summary>
        public List<LatLng> Polygon { get; set; } = new List<LatLng>();

        /// <summary>Exclusion polygons inside the field (circles should be sent as polygons).</summary>
        public List<List<LatLng>> Obstacles { get; set; } = new List<List<LatLng>>();

        public SprayHome Home { get; set; }

        // ---- grid ----

        /// <summary>Distance between strips (metres).</summary>
        public double LineDistance { get; set; } = 5;

        /// <summary>Strip bearing (degrees, 0 = north).</summary>
        public double Angle { get; set; }

        /// <summary>Shift of the whole grid perpendicular to the strips (metres).</summary>
        public double Shift { get; set; }

        /// <summary>Minimum number of strips to skip between consecutive strips (0 = adjacent).</summary>
        public int LaneSeparation { get; set; }

        public SprayStartPosition StartFrom { get; set; } = SprayStartPosition.Home;

        /// <summary>Used when <see cref="StartFrom"/> is <see cref="SprayStartPosition.Point"/>.</summary>
        public LatLng StartPoint { get; set; }

        /// <summary>Pick the next strip from the end of the current one (true) or from its start (false).</summary>
        public bool ExtendedEndpoint { get; set; } = true;

        // ---- spray ----

        public double LitersPerHa { get; set; }

        /// <summary>Spray bar width (metres); also the vehicle footprint for terrain sampling.</summary>
        public double SprayBarWidth { get; set; } = 4;

        /// <summary>Speed along the strips (m/s).</summary>
        public double FlySpeed { get; set; } = 5;

        /// <summary>Grow obstacles by half the bar width before routing around them.</summary>
        public bool ExpandObstacles { get; set; } = true;

        /// <summary>Plan as if there were no obstacles.</summary>
        public bool IgnoreObstacles { get; set; }

        // ---- altitude ----

        /// <summary>Spray height above ground (metres).</summary>
        public double Altitude { get; set; } = 3;

        /// <summary>Added to <see cref="Altitude"/> (GPS antenna above the spray bar, metres).</summary>
        public double GpsOffset { get; set; }

        public SprayAltitudeReference AltitudeReference { get; set; } = SprayAltitudeReference.Relative;

        /// <summary>
        /// Sample terrain under the spray-bar footprint and insert extra waypoints where the straight leg would
        /// deviate more than <see cref="TrackingAltitudeError"/> from the target height.
        /// </summary>
        public bool AltitudeTracking { get; set; }

        public double TrackingAltitudeError { get; set; } = 0.5;

        public double TakeoffAltitude { get; set; } = 5;

        // ---- mission ----

        public bool AddTakeoffAndRtl { get; set; } = true;

        /// <summary>Add CONDITION_YAW with the strip angle so the vehicle keeps its heading.</summary>
        public bool HeadLock { get; set; }

        /// <summary>Hold time at every waypoint (seconds, WAYPOINT param1).</summary>
        public double DelayAtWaypoint { get; set; }

        public SpraySplitMode SplitBy { get; set; } = SpraySplitMode.None;

        /// <summary>Minutes per segment (Time) or number of segments (Segments).</summary>
        public int SplitValue { get; set; }

        /// <summary>
        /// Vehicle WPNAV_SPEED in m/s; when set a DO_CHANGE_SPEED back to it is added after the last strip (the
        /// old plugin read it from the connected vehicle).
        /// </summary>
        public double? WpnavSpeed { get; set; }

        // ---- terrain ----

        public SprayElevationSource ElevationSource { get; set; } = SprayElevationSource.Backend;

        public ElevationRaster Elevation { get; set; }

        /// <summary>Seconds to wait for backend SRTM tiles to download (Backend source only).</summary>
        public double TerrainWait { get; set; } = 10;
    }

    /// <summary>
    /// One grid point. <see cref="Tag"/> is S (strip start), E (strip end), I (obstacle routing) or empty for an
    /// altitude-tracking point; <see cref="Split"/> marks the first point of a new time segment.
    /// </summary>
    public sealed record SprayGridPoint(double Lat, double Lng, double Alt, string Tag, bool Split);

    public sealed class SprayGridStats
    {
        /// <summary>Field area (m²), obstacles not subtracted.</summary>
        public double AreaM2 { get; set; }

        public double AreaHa => AreaM2 / 10000.0;

        /// <summary>Number of spray strips.</summary>
        public int Strips { get; set; }

        /// <summary>Length of the strips (metres).</summary>
        public double SprayDistanceM { get; set; }

        /// <summary>Time on the strips at <see cref="SprayGridRequest.FlySpeed"/> (seconds).</summary>
        public double SprayTimeS { get; set; }

        /// <summary>Home → grid → home path length (metres).</summary>
        public double TotalDistanceM { get; set; }

        public double FlightTimeS { get; set; }

        /// <summary>Estimated duration of each segment when splitting by time (seconds); empty otherwise.</summary>
        public List<double> SplitTimesS { get; set; } = new List<double>();

        /// <summary>Litres needed for the field at <see cref="SprayGridRequest.LitersPerHa"/>.</summary>
        public double LitersTotal { get; set; }
    }

    public sealed class SprayGridPreview
    {
        public List<SprayGridPoint> Points { get; set; } = new List<SprayGridPoint>();
        public SprayGridStats Stats { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class SprayMissionResult
    {
        public MissionPlan Mission { get; set; }

        /// <summary>Grid points after altitude tracking (what the waypoints were built from).</summary>
        public List<SprayGridPoint> Points { get; set; } = new List<SprayGridPoint>();

        public SprayGridStats Stats { get; set; }

        /// <summary>Mission sequence numbers (home = 0) of each segment's first item, for DO_JUMP / "start at".</summary>
        public List<int> SegmentStartSeq { get; set; } = new List<int>();

        public List<string> Warnings { get; set; } = new List<string>();
    }
}
