using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using log4net;
using MissionPlanner.ArduPilot;
using MissionPlanner.GCSViews;
using MissionPlanner.Plugin;
using MissionPlanner.Utilities;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SharpKml.Dom;
using static IronPython.Modules._ast;
using static MissionPlanner.GCSViews.FlightPlanner;
using static MissionPlanner.Utilities.Pelco;

namespace MissionPlanner.SprayGrid
{

    public struct GridData
    {
        public double distance;
        public double litersperha;
        public double flyspeed;
        public double altitude;
        public double angle;
        public altmode altreference;
        public Utilities.Grid.StartPosition startfrom;
        public double barsize;
        public double waitatwp;
        public SprayGridUI.splitby splitby;
        public double segments;
        public bool alttrackingenabled;
        public double trackingalterror;
        public double gridshift;
        public bool expandobstacles;
        public bool addtakeoff;
        public bool headlock;
        public int lanesep;
        public bool extendedpoint;
        public double takeoffalt;

        public List<PointLatLngAlt> polygon;
        public List<List<PointLatLngAlt>> obstaclesmarks;

        public List<Locationwp> fences;

    }




    public partial class SprayGridUI : Form
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        const double rad2deg = (180 / Math.PI);
        const double deg2rad = (1.0 / rad2deg);

        GMapOverlay layerpolygons;
        GMapOverlay layerFences;
        GMapPolygon wppoly;

        GMapOverlay colorRoutes;

        private GridPlugin plugin;
        List<PointLatLngAlt> grid;
        List<double> splitstime = new List<double>();
        bool firstDataLoad = true;
        public enum splitby
        {
            None = 0,
            Time = 1,
            Segments = 2
        }

        List<PointLatLngAlt> list = new List<PointLatLngAlt>();
        List<List<PointLatLngAlt>> obstacles = new List<List<PointLatLngAlt>>();

        internal PointLatLng MouseDownStart = new PointLatLng();
        internal PointLatLng MouseDownEnd;
        internal PointLatLngAlt CurrentGMapMarkerStartPos;
        PointLatLng currentMousePosition;
        GMapMarker marker;
        GMapMarker CurrentGMapMarker = null;
        int CurrentGMapMarkerIndex = 0;
        bool isMouseDown = false;
        bool isMouseDraging = false;
        static public Object thisLock = new Object();

        bool loading = false;
        bool verifyHeightState = false;

        public PluginHost Host2 { get; private set; }

        public SprayGridUI(GridPlugin plugin)
        {
            loading = true;
            this.plugin = plugin;

            InitializeComponent();

            map.MapProvider = plugin.Host.FDMapType;

            layerpolygons = new GMapOverlay("polygons");
            map.Overlays.Add(layerpolygons);

            colorRoutes = new GMapOverlay("colorRoutes");
            map.Overlays.Add(colorRoutes);

            layerFences = new GMapOverlay("obstacles");
            map.Overlays.Add(layerFences);


            CMB_startfrom.DataSource = Enum.GetNames(typeof(Utilities.Grid.StartPosition));
            CMB_startfrom.SelectedIndex = (int)Utilities.Grid.StartPosition.Home;


            CMB_AltReference.DisplayMember = "Value";
            CMB_AltReference.ValueMember = "Key";
            CMB_AltReference.DataSource = EnumTranslator.EnumToList<altmode>();
            CMB_AltReference.SelectedIndex = MainV2.instance.FlightPlanner.CMB_altmode.SelectedIndex;


            CMB_split.DataSource = Enum.GetNames(typeof(splitby));
            CMB_split.SelectedIndex = (int)splitby.None;



            // set and angle that is good
            list = new List<PointLatLngAlt>();
            plugin.Host.FPDrawnPolygon.Points.ForEach(x => { list.Add(x); });
            var area = calcpolygonarea(plugin.Host.FPDrawnPolygon.Points);
            if (area > 10000)
            {
                NUM_Distance.Value = (int)Math.Sqrt(area) / 10;
            }
            // Angle is the angle of the longest side
            //NUM_angle.Value = (decimal)((getAngleOfLongestSide(list) + 360) % 360);

            // Angle is the angle of the first side
            int utmzone = list[0].GetUTMZone();
            utmpos p0 = new utmpos(list[0]);
            utmpos p1 = new utmpos(list[1]);
            var a = (p1.GetBearing(p0) + 360.0) % 360.0;
            NUM_angle.Value = (decimal)a;



            // Map Events
            map.OnMarkerEnter += new MarkerEnter(map_OnMarkerEnter);
            map.OnMarkerLeave += new MarkerLeave(map_OnMarkerLeave);
            map.MouseUp += new MouseEventHandler(map_MouseUp);
            map.MouseDown += new System.Windows.Forms.MouseEventHandler(this.map_MouseDown);
            map.MouseMove += new System.Windows.Forms.MouseEventHandler(this.map_MouseMove);


            //obstacle = new List<PointLatLngAlt>();
            getObstaclesFromFences();
            loading = false;
        }

        void loadsettings()
        {

            loadsetting("SprayGrid_linedistance", NUM_Distance);
            loadsetting("SprayGrid_litersperha", NUM_LitPerHa);
            loadsetting("SprayGrid_flyspeed", NUM_UpDownFlySpeed);
            loadsetting("SprayGrid_alt", NUM_altitude);
            loadsetting("SprayGrid_barsize", NUM_SprayBarWidth);
            loadsetting("SprayGrid_lanesep", NUM_LaneSeparation);
            loadsetting("SprayGrid_addtakeoff", CHK_addTakeoffAndLanding);
            loadsetting("SprayGrid_headlock", CHK_Headlock);
            loadsetting("SprayGrid_alttrackingenabled", CHK_enableAltTracking);
            loadsetting("SprayGrid_alttrackerror", NUM_trackingAltError);
            loadsetting("SprayGrid_expandobstacles", CHK_expandObstacles);
            loadsetting("SprayGrid_takeoffalt", NUM_TakeoffAlt);

        }
        void loadsetting(string key, Control item)
        {
            if (plugin.Host.config.ContainsKey(key))
            {
                if (item is NumericUpDown)
                {
                    ((NumericUpDown)item).Value = decimal.Parse(plugin.Host.config[key].ToString());
                }
                else if (item is ComboBox)
                {
                    ((ComboBox)item).Text = plugin.Host.config[key].ToString();
                }
                else if (item is CheckBox)
                {
                    ((CheckBox)item).Checked = bool.Parse(plugin.Host.config[key].ToString());
                }
                else if (item is RadioButton)
                {
                    ((RadioButton)item).Checked = bool.Parse(plugin.Host.config[key].ToString());
                }
            }
        }
        void savesettings()
        {
            plugin.Host.config["SprayGrid_alt"] = NUM_altitude.Value.ToString();
            plugin.Host.config["SprayGrid_linedistance"] = NUM_Distance.Value.ToString();
            plugin.Host.config["SprayGrid_litersperha"] = NUM_LitPerHa.Value.ToString();
            plugin.Host.config["SprayGrid_flyspeed"] = NUM_UpDownFlySpeed.Value.ToString();
            plugin.Host.config["SprayGrid_barsize"] = NUM_SprayBarWidth.Value.ToString();
            plugin.Host.config["SprayGrid_lanesep"] = NUM_LaneSeparation.Value.ToString();
            plugin.Host.config["SprayGrid_addtakeoff"] = CHK_addTakeoffAndLanding.Checked.ToString();
            plugin.Host.config["SprayGrid_headlock"] = CHK_Headlock.Checked.ToString();
            plugin.Host.config["SprayGrid_alttrackingenabled"] = CHK_enableAltTracking.Checked.ToString();
            plugin.Host.config["SprayGrid_alttrackerror"] = NUM_trackingAltError.Value.ToString();
            plugin.Host.config["SprayGrid_expandobstacles"] = CHK_expandObstacles.Checked.ToString();
            plugin.Host.config["SprayGrid_takeoffalt"] = NUM_TakeoffAlt.Value.ToString();
        }
        private void map_OnMarkerLeave(GMapMarker item)
        {
            if (!isMouseDown)
            {
                if (item is GMapMarker)
                {
                    // when you click the context menu this triggers and causes problems
                    CurrentGMapMarker = null;
                }

            }
        }
        private void map_OnMarkerEnter(GMapMarker item)
        {
            if (!isMouseDown)
            {
                if (item is GMapMarker)
                {
                    CurrentGMapMarker = item as GMapMarker;
                    CurrentGMapMarkerStartPos = CurrentGMapMarker.Position;
                }
            }
        }
        private void map_MouseUp(object sender, MouseEventArgs e)
        {
            MouseDownEnd = map.FromLocalToLatLng(e.X, e.Y);

            // Console.WriteLine("MainMap MU");

            if (e.Button == MouseButtons.Right) // ignore right clicks
            {
                return;
            }

            if (isMouseDown) // mouse down on some other object and dragged to here.
            {
                if (e.Button == MouseButtons.Left)
                {
                    isMouseDown = false;
                }
                if (!isMouseDraging)
                {
                    if (CurrentGMapMarker != null)
                    {
                        // Redraw polygon
                        //AddDrawPolygon();
                    }
                }
            }
            isMouseDraging = false;
            CurrentGMapMarker = null;
            CurrentGMapMarkerIndex = 0;
            CurrentGMapMarkerStartPos = null;
        }
        private void map_MouseDown(object sender, MouseEventArgs e)
        {
            MouseDownStart = map.FromLocalToLatLng(e.X, e.Y);

            if (e.Button == MouseButtons.Left && Control.ModifierKeys != Keys.Alt)
            {
                isMouseDown = true;
                isMouseDraging = false;

                if (CurrentGMapMarkerStartPos != null)
                    CurrentGMapMarkerIndex = list.FindIndex(c => c.ToString() == CurrentGMapMarkerStartPos.ToString());
            }
        }
        private void map_MouseMove(object sender, MouseEventArgs e)
        {
            PointLatLng point = map.FromLocalToLatLng(e.X, e.Y);
            currentMousePosition = point;

            if (MouseDownStart == point)
                return;

            if (!isMouseDown)
            {
                // update mouse pos display
                //SetMouseDisplay(point.Lat, point.Lng, 0);
            }

            //draging
            if (e.Button == MouseButtons.Left && isMouseDown)
            {
                isMouseDraging = true;

                if (CurrentGMapMarker != null)
                {
                    if (CurrentGMapMarkerIndex == -1)
                    {
                        isMouseDraging = false;
                        return;
                    }

                    PointLatLng pnew = map.FromLocalToLatLng(e.X, e.Y);

                    CurrentGMapMarker.Position = pnew;

                    list[CurrentGMapMarkerIndex] = new PointLatLngAlt(pnew);
                    recalculateGrid(sender, e);
                }
                else // left click pan
                {
                    double latdif = MouseDownStart.Lat - point.Lat;
                    double lngdif = MouseDownStart.Lng - point.Lng;

                    try
                    {
                        lock (thisLock)
                        {
                            map.Position = new PointLatLng(map.Position.Lat + latdif, map.Position.Lng + lngdif);
                        }
                    }
                    catch { }
                }
            }
        }
        void AddDrawPolygon()
        {
            List<PointLatLng> list2 = new List<PointLatLng>();

            list.ForEach(x => { list2.Add(x); });

            var poly = new GMapPolygon(list2, "poly");
            poly.Stroke = new Pen(Color.Red, 2);
            poly.Fill = Brushes.Transparent;

            layerpolygons.Polygons.Add(poly);

            foreach (var item in list)
            {
                layerpolygons.Markers.Add(new GMarkerGoogle(item, GMarkerGoogleType.red));
            }
        }
        double getAngleOfLongestSide(List<PointLatLngAlt> list)
        {
            double angle = 0;
            double maxdist = 0;
            PointLatLngAlt last = list[list.Count - 1];
            foreach (var item in list)
            {
                if (item.GetDistance(last) > maxdist)
                {
                    angle = item.GetBearing(last);
                    maxdist = item.GetDistance(last);
                }
                last = item;
            }

            return (angle + 360) % 360;
        }

        private void getObstaclesFromFences()
        {
            // find the Overlay with Id equal to "fence" from MainV2.instance.FlightPlanner.MainMap.Overlays
            GMapOverlay fenceOverlay = null;
            layerFences.Polygons.Clear();
            obstacles.Clear();

            foreach (var overlay in MainV2.instance.FlightPlanner.MainMap.Overlays)
            {
                if (overlay.Id == "fence")
                {
                    fenceOverlay = overlay;
                    break;
                }
            }
            if (fenceOverlay != null)
            {
                //process all polygons from the overlay into a list of polygons
                foreach (GMapPolygon poly in fenceOverlay.Polygons)
                {
                    GMapPolygon pAdd = new GMapPolygon(poly.Points, "poly");
                    pAdd.Stroke = new Pen(Color.Red, 2);
                    pAdd.Fill = new SolidBrush(Color.FromArgb(50, Color.Red));
                    pAdd.IsVisible = true;
                    layerFences.Polygons.Add(pAdd);

                    //process all points from the polygon into a list of points
                    List<PointLatLngAlt> o = new List<PointLatLngAlt>();
                    foreach (PointLatLng point in poly.Points)
                    {
                        o.Add(new PointLatLngAlt(point.Lat, point.Lng, 0));
                    }
                    if (o.Count > 0)
                    {
                        obstacles.Add(o);
                    }
                }


                //Add circle points to the list
                int x = 0;

                foreach (var m in fenceOverlay.Markers)
                {
                    if (m is GMapMarkerRect)
                    {
                        if (((GMapMarkerRect)m).Color == Color.Red && ((GMapMarkerRect)m).wprad > 0)
                        {
                            List<PointLatLngAlt> o = new List<PointLatLngAlt>();
                            List<PointLatLng> oPoly = new List<PointLatLng>();

                            PointLatLngAlt center = new PointLatLngAlt(((GMapMarkerRect)m).Position.Lat, ((GMapMarkerRect)m).Position.Lng, 0);

                            //create a hexagonal with the center in the marker position and with the idameter of the circle
                            for (int i = 0; i < 8; i++)
                            {
                                PointLatLngAlt p = center.newpos(i * 45, (double)((GMapMarkerRect)m).wprad);
                                o.Add(p);
                                oPoly.Add(new PointLatLng(p.Lat, p.Lng));
                            }

                            if (o.Count > 0)
                            {
                                obstacles.Add(o);

                                GMapPolygon pAdd = new GMapPolygon(oPoly, "poly");
                                pAdd.Stroke = new Pen(Color.Red, 2);
                                pAdd.Fill = new SolidBrush(Color.FromArgb(50, Color.Red));
                                pAdd.IsVisible = true;
                                layerFences.Polygons.Add(pAdd);
                            }
                        }
                    }
                }
            }
        }

        private void recalculateGrid(object sender, EventArgs e)
        {
            if (list.Count < 3)
                return;

            double barwidth = (double)NUM_SprayBarWidth.Value;
            if (!CHK_expandObstacles.Checked)
            {
                barwidth = 0;
            }

            grid = Utilities.Grid.CreateSprayGrid(list, (double)NUM_altitude.Value, (double)NUM_Distance.Value,
                (double)NUM_angle.Value,
                (Utilities.Grid.StartPosition)Enum.Parse(typeof(Utilities.Grid.StartPosition), CMB_startfrom.Text), (float)NUM_LaneSeparation.Value,
                plugin.Host.cs.PlannedHomeLocation, obstacles, (double)NUM_Shift.Value, barwidth, CHK_extendedpoint.Checked);

            List<PointLatLng> list2 = new List<PointLatLng>();

            grid.ForEach(x => { list2.Add(x); });

            map.HoldInvalidation = true;

            layerpolygons.Polygons.Clear();
            layerpolygons.Markers.Clear();

            if (chk_boundary.Checked)
                AddDrawPolygon();

            if (grid.Count == 0)
            {
                map.ZoomAndCenterMarkers("polygons");
                return;
            }

            //*** Time check
            PointLatLngAlt yet_another_prevpoint = grid[0];
            double flyspeedms = CurrentState.fromSpeedDisplayUnit((double)NUM_UpDownFlySpeed.Value);
            double split_time = 0;

            if (CMB_split.SelectedIndex == (int)splitby.Time)
            {
                split_time = (int)NUM_Segments.Value * 60;
            }
            double chunk_dist = grid.First().GetDistance(MainV2.comPort.MAV.cs.PlannedHomeLocation) / 1000.0;
            double chunk_time = (chunk_dist * 1000 + grid.First().GetDistance(MainV2.comPort.MAV.cs.PlannedHomeLocation)) / flyspeedms;

            if (0 < split_time && split_time < chunk_time)
            {
                split_time = 0;
            }

            splitstime.Clear();

            foreach (var item in grid)
            {
                // Handle splitting by time
                if (split_time > 0)
                {
                    chunk_dist += item.GetDistance(yet_another_prevpoint) / 1000;
                    double dist_to_home = item.GetDistance(MainV2.comPort.MAV.cs.PlannedHomeLocation) / 1000;


                    chunk_time = (chunk_dist + dist_to_home) * 1000 / flyspeedms;
                    if (chunk_time > split_time)
                    {
                        double chunk_till_end = chunk_time;
                        // Split the mission here
                        chunk_dist = item.GetDistance(MainV2.comPort.MAV.cs.PlannedHomeLocation) / 1000.0;
                        chunk_time = 2 * (chunk_dist * 1000) / flyspeedms; // round trip

                        // If the round-trip time to/from this waypoint is more than the split time, then we cannot
                        // possibly split the mission into the requested number of segments. So, we won't split the mission.
                        if (split_time < chunk_time)
                        {
                            split_time = 0;
                        }
                        else
                        {
                            item.Tag2 = "SplitTime";
                            splitstime.Add(chunk_till_end);
                        }
                    }
                    yet_another_prevpoint = item;
                }
            }
            //One last chunk
            if (split_time > 0)
            {
                splitstime.Add(chunk_time);
            }


            int strips = 0;
            int a = 1;
            PointLatLngAlt prevpoint = grid[0];

            foreach (var item in grid)
            {
                if (item.Tag == "S" || item.Tag == "E")
                {
                    strips++;
                    if (chk_markers.Checked)
                    {
                        if (item.Tag == "S")
                        {
                            layerpolygons.Markers.Add(new GMarkerGoogle(item, GMarkerGoogleType.green_small)
                            {
                                ToolTipText = a.ToString() + item.Tag,
                                ToolTipMode = MarkerTooltipMode.OnMouseOver
                            });
                        }
                        if (item.Tag == "E")
                        {
                            layerpolygons.Markers.Add(new GMarkerGoogle(item, GMarkerGoogleType.red_small)
                            {
                                ToolTipText = a.ToString() + item.Tag,
                                ToolTipMode = MarkerTooltipMode.OnMouseOver
                            });
                        }
                        a++;
                    }
                }
                if (item.Tag == "I")
                {
                    layerpolygons.Markers.Add(new GMarkerGoogle(item, GMarkerGoogleType.purple_small));
                }
                prevpoint = item;
            }

            // add wp polygon
            wppoly = new GMapPolygon(list2, "Grid");
            wppoly.Stroke.Color = Color.Yellow;
            wppoly.Fill = Brushes.Transparent;
            wppoly.Stroke.Width = 2;
            if (chk_grid.Checked)
                layerpolygons.Polygons.Add(wppoly);

            colorRoutes.Clear();

            //First point must be "S" and last point must be "E"
            int color = 0;
            int itemnumber = -1;
            PointLatLng firstpoint = grid[0];
            foreach (var p in grid)
            {
                itemnumber++;
                if (itemnumber == 0)
                    continue;

                PointLatLng secondpoint = p;
                if (p.Tag == "E")
                {
                    GMapPolygon poly = new GMapPolygon(new List<PointLatLng> { firstpoint, secondpoint }, "line" + itemnumber.ToString())
                    {
                        Fill = Brushes.Transparent,
                        IsVisible = true,
                        IsHitTestVisible = false,
                        Stroke = new Pen(Color.Yellow, 4)
                    };
                    colorRoutes.Polygons.Add(poly);
                }
                else
                {
                    GMapPolygon poly = new GMapPolygon(new List<PointLatLng> { firstpoint, secondpoint }, "line" + itemnumber.ToString())
                    {
                        Fill = Brushes.Transparent,
                        IsVisible = true,
                        IsHitTestVisible = false,
                        Stroke = new Pen(Color.LightBlue, 4)
                    };
                    colorRoutes.Polygons.Add(poly);
                }

                firstpoint = secondpoint;
            }
            if (chk_grid.Checked)
                colorRoutes.IsVisibile = true;
            else
                colorRoutes.IsVisibile = false;

            Console.WriteLine("Poly Dist " + wppoly.Distance);

            double fullArea = calcpolygonarea(plugin.Host.FPDrawnPolygon.Points);
            //double obstacleArea = 0;
            //foreach (var o in obstacles)
            //{
            //    obstacleArea += calcpolygonarea(o);
            //}

            //fullArea = fullArea - obstacleArea;

            double distance = wppoly.Distance;
            lbl_area.Text = fullArea.ToString("#") + " m^2";
            lbl_distance.Text = distance.ToString("0.##") + " km";
            lbl_strips.Text = ((int)(strips / 2)).ToString();
            lbl_distbetweenlines.Text = NUM_Distance.Value.ToString("0.##") + " m";
            lbl_flighttime.Text = secondsToNice(distance * 1000 / (double)NUM_UpDownFlySpeed.Value);
            lbl_litersPerHA.Text = NUM_LitPerHa.Value.ToString("0.##") + " L";
            lbl_plannedSpeed.Text = NUM_UpDownFlySpeed.Value.ToString("0.##") + " m/s";

            tb_TimeSegments.Text = "";
            if (splitstime.Count > 0)
            {
                foreach (var t in splitstime)
                {
                    tb_TimeSegments.Text += secondsToNice(t) + " \r\n";
                }
            }
            else
            {
                tb_TimeSegments.Text = "No Split";

            }


            map.HoldInvalidation = false;
            map.Refresh();
            if (firstDataLoad)
            {
                map.ZoomAndCenterMarkers("polygons");
                firstDataLoad = false;
            }


        }
        string secondsToNice(double seconds)
        {
            if (seconds < 0)
                return "Infinity Seconds";

            double secs = seconds % 60;
            int mins = (int)(seconds / 60) % 60;
            int hours = (int)(seconds / 3600);// % 24;

            if (hours > 0)
            {
                return hours + ":" + mins.ToString("00") + ":" + secs.ToString("00") + " Hours";
            }
            else if (mins > 0)
            {
                return mins + ":" + secs.ToString("00") + " Minutes";
            }
            else
            {
                return secs.ToString("0.00") + " Seconds";
            }
        }
        double calcpolygonarea(List<PointLatLng> polygon)
        {
            // should be a closed polygon
            // coords are in lat long
            // need utm to calc area

            if (polygon.Count == 0)
            {
                CustomMessageBox.Show("Please define a polygon!");
                return 0;
            }

            // close the polygon
            if (polygon[0] != polygon[polygon.Count - 1])
                polygon.Add(polygon[0]); // make a full loop

            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

            IGeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

            int utmzone = (int)((polygon[0].Lng - -186.0) / 6.0);

            IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone, polygon[0].Lat < 0 ? false : true);

            ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(wgs84, utm);

            double prod1 = 0;
            double prod2 = 0;

            for (int a = 0; a < (polygon.Count - 1); a++)
            {
                double[] pll1 = { polygon[a].Lng, polygon[a].Lat };
                double[] pll2 = { polygon[a + 1].Lng, polygon[a + 1].Lat };

                double[] p1 = trans.MathTransform.Transform(pll1);
                double[] p2 = trans.MathTransform.Transform(pll2);

                prod1 += p1[0] * p2[1];
                prod2 += p1[1] * p2[0];
            }

            double answer = (prod1 - prod2) / 2;

            if (polygon[0] == polygon[polygon.Count - 1])
                polygon.RemoveAt(polygon.Count - 1); // unmake a full loop

            return Math.Abs(answer);
        }
        private void No_split_process()
        {

        }
        private void SprayGridUI_Resize(object sender, EventArgs e)
        {
            map.ZoomAndCenterMarkers("polygons");
        }
        private void SprayGridUI_Load(object sender, EventArgs e)
        {
            loadsettings();
        }
        private void CMB_split_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch ((splitby)CMB_split.SelectedIndex)
            {
                case splitby.None:
                    NUM_Segments.Visible = false;
                    labelSegments.Visible = false;
                    break;
                case splitby.Time:
                    NUM_Segments.Visible = true;
                    labelSegments.Text = "Time(min)/ Segment";
                    labelSegments.Visible = true;
                    CHK_addTakeoffAndLanding.Checked = true;
                    break;
                case splitby.Segments:
                    NUM_Segments.Visible = true;
                    labelSegments.Text = "# of Segments";
                    labelSegments.Visible = true;
                    CHK_addTakeoffAndLanding.Checked = true;
                    break;
            }

            //call domainUpDown1_ValueChanged
            recalculateGrid(sender, e);


        }

        private srtm.altresponce getVehicleSRTMAlt(double lat, double lng, List<PointF> displacementMap)
        {
            srtm.altresponce altsrtm = new srtm.altresponce();
            altsrtm = getMaxAltinArea(lat, lng, displacementMap);
            return altsrtm;

        }


        private void BUT_Accept_Click2(object sender, EventArgs e)
        {

            if (CustomMessageBox.Show(
                        "Do you want to save the grid before convert it to flight plan?",
                        "Save Grid", MessageBoxButtons.YesNo) == (int)DialogResult.Yes)
            {
                SaveGrid();
            }


            double homealt = 0;
            //verifyHeightState = MainV2.instance.FlightPlanner.CHK_verifyheight.Checked;
            verifyHeightState = true; //Force verify height
            MainV2.instance.FlightPlanner.TXT_DefaultAlt.Text = NUM_altitude.Value.ToString();
            List<PointF> displacementMap = new List<PointF>();
            displacementMap = getVehicleAreaDisplacementPoints((double)NUM_angle.Value, (double)NUM_SprayBarWidth.Value);


            //Do the ALT tracking calculation
            if (CHK_enableAltTracking.Checked == true)
            {
                //Do grid altitude based on srtm

                srtm.altresponce altsrtm = srtm.getAltitude(MainV2.comPort.MAV.cs.PlannedHomeLocation.Lat, MainV2.comPort.MAV.cs.PlannedHomeLocation.Lng);

                if (altsrtm.currenttype == srtm.tiletype.valid)
                {
                    homealt = altsrtm.alt;
                }
                else
                {
                    homealt = MainV2.comPort.MAV.cs.PlannedHomeLocation.Alt;
                }

                foreach (var p in grid)
                {
                    altsrtm = getVehicleSRTMAlt(p.Lat, p.Lng, displacementMap);
                    p.Alt = altsrtm.alt - homealt + (double)NUM_altitude.Value;
                }

                List<PointLatLngAlt> newGrid = new List<PointLatLngAlt>();

                //expand grid with altitude issue point
                PointLatLngAlt last = null;
                double targetRelativeAlt = (double)NUM_altitude.Value;

                foreach (PointLatLngAlt loc in grid)
                {
                    if (loc == null)
                        continue;

                    //Removed terrain following, we use relative altitudes
                    //Ignore the first point We don't have a heading
                    if (last == null)
                    {
                        newGrid.Add(loc);
                        last = loc;
                        continue;
                    }

                    double dist = last.GetDistance(loc);

                    int points = (int)(dist * 2.5) + 1;

                    double deltalat = (last.Lat - loc.Lat);
                    double deltalng = (last.Lng - loc.Lng);
                    double steplat = deltalat / points;
                    double steplng = deltalng / points;

                    double deltaalt = last.Alt - loc.Alt;
                    double stepalt = deltaalt / points;

                    double lastalt = last.Alt;
                    int lasta = 0;

                    //Go through between the two points in distance/4+1 steps which is 25cm
                    for (int a = 0; a <= points; a++)
                    {
                        double lat = last.Lat - steplat * a;        //location new position
                        double lng = last.Lng - steplng * a;

                        double extrapolatedRelativeAlt = lastalt - stepalt * (a - lasta);        //vehicle center estimated altitude of a given point, extrapolated from the two points 

                        double actualTerrainAlt = getMaxAltinArea(lat, lng, displacementMap).alt;

                        double terrainToHome = actualTerrainAlt - homealt;

                        double altAboveTerrain = extrapolatedRelativeAlt - terrainToHome;

                        PointLatLngAlt newpoint = new PointLatLngAlt(lat, lng, terrainToHome + targetRelativeAlt, "");

                        if (Math.Abs(altAboveTerrain - targetRelativeAlt) > (double)NUM_trackingAltError.Value)
                        {
                            newGrid.Add(newpoint);
                            lastalt = newpoint.Alt;
                            deltaalt = lastalt - loc.Alt;
                            int remaining = points - a;
                            stepalt = deltaalt / (remaining);
                            lasta = a;
                        }
                    }
                    newGrid.Add(loc);
                    last = loc;
                }

                //Check interim alt points to see if they really needed
                //Go throght the grid and get prev, current and next point

                List<PointLatLngAlt> pointstoremove = new List<PointLatLngAlt>();

                bool notDoneYet = true;
                while (notDoneYet)
                {
                    notDoneYet = false;
                    PointLatLngAlt pointToRemove = new PointLatLngAlt();

                    foreach (var p in newGrid)
                    {
                        int index = newGrid.IndexOf(p);
                        if (index > 0 && index < newGrid.Count - 1)
                        {
                            PointLatLngAlt prev = newGrid[index - 1];
                            PointLatLngAlt next = newGrid[index + 1];

                            if (p.Tag == "S" || p.Tag == "E")
                                continue;

                            // Assume that x1 and y1 is the zero point
                            double x2 = prev.GetDistance(next);
                            double xc = prev.GetDistance(p);
                            double y2 = prev.Alt - next.Alt;
                            double yc = prev.Alt - p.Alt;

                            double m = y2 / x2;
                            double c = y2 - m * x2;
                            double y_prime = m * xc + c;
                            double d = Math.Abs(y_prime - yc);

                            if (d < (double)NUM_trackingAltError.Value)
                            {
                                pointToRemove = p;
                                notDoneYet = true;
                            }
                            if (notDoneYet)
                                break;
                        }
                    }
                    if (notDoneYet)
                    {
                        newGrid.Remove(pointToRemove);
                    }
                }

                grid = newGrid;

            }

            int split_segment = 0;
            int split_time = 0;
            int splitValue = 0;


            if (grid != null && grid.Count > 0)
            {


                //If no alt tracking points then the alt setting will be done by the flight planner
                MainV2.instance.FlightPlanner.CHK_verifyheight.Checked = !CHK_enableAltTracking.Checked;

                MainV2.instance.FlightPlanner.CMB_altmode.SelectedIndex = (int)CMB_AltReference.SelectedIndex;  //We are using the same source, so indexes will match
                MainV2.instance.FlightPlanner.quickadd = true;

                if (CMB_split.SelectedIndex == (int)splitby.None)
                {
                    //Start of No_Split process
                    if (grid != null && grid.Count > 0)
                    {
                        MainV2.instance.FlightPlanner.quickadd = true;

                        PointLatLngAlt lastpnt = PointLatLngAlt.Zero;

                        //Add first command to set sprayer

                        plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 1, (double)NUM_LitPerHa.Value, (double)NUM_Distance.Value, (double)NUM_UpDownFlySpeed.Value, 0, 0, 0);


                        if (CHK_addTakeoffAndLanding.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, (double)NUM_TakeoffAlt.Value);
                        }

                        //Add start point
                        plugin.Host.AddWPtoList(MAVLink.MAV_CMD.DO_CHANGE_SPEED, 1,
                            (int)((float)NUM_UpDownFlySpeed.Value / CurrentState.multiplierspeed), 0, 0, 0, 0, 0,
                            null);

                        if (CHK_Headlock.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.CONDITION_YAW, (double)NUM_angle.Value, 0, 0, 0, 0, 0, 0, null);
                        }
                        int index = 0;

                        grid.ForEach(plla =>
                        {
                            if (!(plla.Lat == lastpnt.Lat && plla.Lng == lastpnt.Lng && plla.Alt == lastpnt.Alt))

                            {
                                AddWP(plla.Lng, plla.Lat, plla.Alt, plla.Tag, homealt, srtm.getAltitude(plla.Lat, plla.Lng).alt);

                                double distanceToNext = 0;

                                if (index < grid.Count - 1)
                                {
                                    distanceToNext = Math.Round(plla.GetDistance(grid[index + 1]), 1);
                                }
                                if (plla.Tag == "S")
                                {
                                    plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 3, distanceToNext, 0, 0, 0, 0);
                                }

                                if (plla.Tag == "E")
                                {
                                    plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 0, 0, 0, 0, 0, 0);
                                }
                                index++;
                            }

                            lastpnt = plla;
                        });


                        //if speed is set, set it back to WPNAV
                        if (NUM_UpDownFlySpeed.Value != 0)
                        {
                            if (MainV2.comPort.MAV.param["WPNAV_SPEED"] != null)
                            {
                                double speed = MainV2.comPort.MAV.param["WPNAV_SPEED"].Value;
                                speed = speed / 100;
                                plugin.Host.AddWPtoList(MAVLink.MAV_CMD.DO_CHANGE_SPEED, 0, speed, 0, 0, 0, 0, 0);
                            }
                        }

                        if (CHK_addTakeoffAndLanding.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, 0, 0, 0, 0, 0, 0, 0);
                        }

                    }
                    else
                    {
                        CustomMessageBox.Show("Bad Grid", "Error");
                    }


                    //End of No_split_process
                }
                else
                {

                    splitValue = (int)NUM_Segments.Value;

                    if ((splitValue > 1) && (CMB_split.SelectedIndex == (int)splitby.None) && (CHK_addTakeoffAndLanding.Checked != true))
                    {
                        CustomMessageBox.Show("You must use Land/RTL to split a mission", Strings.ERROR);
                        return;
                    }

                    if (CMB_split.SelectedIndex == (int)splitby.Time) split_time = splitValue;
                    if (CMB_split.SelectedIndex == (int)splitby.Segments) split_segment = splitValue;

                    //Update values from UI
                    //var gridobject = savegriddata();

                    int wpsplit = (int)Math.Round((double)grid.Count / (double)splitValue, MidpointRounding.AwayFromZero);

                    // Tracks the start and end indices in grid
                    List<int> starts = new List<int>() { };
                    List<int> ends = new List<int>() { };

                    // Tracks the actual waypoint numbers for the starts and end
                    List<int> wpsplitstart = new List<int>() { };

                    if (split_time > 0)
                    {
                        starts.Add(0);
                        for (int i = 0; i < grid.Count; i++)
                        {
                            if (grid[i].Tag2 == "SplitTime")
                            {
                                starts.Add(i);
                                ends.Add(i);
                                split_segment++;
                            }
                        }
                        ends.Add(grid.Count);
                    }
                    else
                    {
                        for (int i = 0; i < split_segment; i++)
                        {
                            int start = wpsplit * i;
                            int end = wpsplit * (i + 1);

                            while (start != 0 && start < grid.Count && grid[start].Tag != "S")
                            {
                                start--;
                            }

                            while (end > 0 && end < grid.Count && grid[end].Tag != "S")
                            {
                                end--;
                            }
                            starts.Add(start);
                            ends.Add(end);
                        }
                    }

                    for (int splitno = 0; splitno < starts.Count; splitno++)
                    {
                        int start = starts[splitno];
                        int end = ends[splitno];

                        //Add first command to set sprayer

                        var wpno = plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 1, (double)NUM_LitPerHa.Value, (double)NUM_Distance.Value, (double)NUM_UpDownFlySpeed.Value, 0, 0, 0);
                        wpsplitstart.Add(wpno);

                        if (CHK_addTakeoffAndLanding.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, (double)NUM_TakeoffAlt.Value);

                        }

                        plugin.Host.AddWPtoList(MAVLink.MAV_CMD.DO_CHANGE_SPEED, 0, ((float)NUM_UpDownFlySpeed.Value / CurrentState.multiplierspeed), 0, 0, 0, 0, 0);

                        if (CHK_Headlock.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.CONDITION_YAW, (double)NUM_angle.Value, 0, 0, 0, 0, 0, 0, null);
                        }

                        int i = 0;
                        bool startedtrigdist = false;
                        PointLatLngAlt lastplla = PointLatLngAlt.Zero;

                        foreach (var plla in grid)
                        {
                            // skip before start point
                            if (i < start)
                            {
                                i++;
                                continue;
                            }
                            // skip after endpoint
                            if (i >= end)
                                break;
                            if (i > start)
                            {
                                // internal point check
                                if (plla.Tag == "M")
                                {
                                    //Do nothing
                                }
                                else
                                {
                                    // only add points that are ends
                                    if (plla.Tag == "S" || plla.Tag == "E" || plla.Tag == "I")
                                    {
                                        if (plla.Lat != lastplla.Lat || plla.Lng != lastplla.Lng ||
                                            plla.Alt != lastplla.Alt)
                                            AddWP(plla.Lng, plla.Lat, plla.Alt, plla.Tag, homealt, srtm.getAltitude(plla.Lat, plla.Lng).alt);
                                    }

                                    double distanceToNext = 0;
                                    if (i < grid.Count - 1)
                                    {
                                        distanceToNext = Math.Round(plla.GetDistance(grid[i + 1]), 1);
                                    }
                                    if (plla.Tag == "S")
                                    {
                                        plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 3, distanceToNext, 0, 0, 0, 0);
                                    }

                                    if (plla.Tag == "E")
                                    {
                                        plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 0, 0, 0, 0, 0, 0);
                                    }
                                }
                            }
                            else
                            {
                                AddWP(plla.Lng, plla.Lat, plla.Alt, plla.Tag, homealt, srtm.getAltitude(plla.Lat, plla.Lng).alt);
                                double distanceToNext = 0;
                                if (i < grid.Count - 1)
                                {
                                    distanceToNext = Math.Round(plla.GetDistance(grid[i + 1]), 1);
                                }
                                if (plla.Tag == "S")
                                {
                                    plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 3, distanceToNext, 0, 0, 0, 0);
                                }

                                if (plla.Tag == "E")
                                {
                                    plugin.Host.AddWPtoList((MAVLink.MAV_CMD)217, 2, 0, 0, 0, 0, 0, 0);
                                }
                            }
                            lastplla = plla;
                            ++i;
                        }

                        // end

                        //if speed is set, set it back to WPNAV
                        if (NUM_UpDownFlySpeed.Value != 0)
                        {
                            if (MainV2.comPort.MAV.param["WPNAV_SPEED"] != null)
                            {
                                double speed = MainV2.comPort.MAV.param["WPNAV_SPEED"].Value;
                                speed = speed / 100;
                                plugin.Host.AddWPtoList(MAVLink.MAV_CMD.DO_CHANGE_SPEED, 0, speed, 0, 0, 0, 0, 0);
                            }
                        }

                        if (CHK_addTakeoffAndLanding.Checked)
                        {
                            plugin.Host.AddWPtoList(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, 0, 0, 0, 0, 0, 0, 0);
                        }
                    }

                    if (split_segment > 1)
                    {
                        int index = 0;
                        foreach (var i in wpsplitstart)
                        {
                            // add do jump
                            plugin.Host.InsertWP(index, MAVLink.MAV_CMD.DO_JUMP, i + wpsplitstart.Count + 1, 1, 0, 0, 0, 0, 0);
                            index++;
                        }

                    }
                }
                // Redraw the polygon in FP
                plugin.Host.RedrawFPPolygon(list);

                savesettings();

                MainV2.instance.FlightPlanner.quickadd = false;
                MainV2.instance.FlightPlanner.writeKML();
                MainV2.instance.FlightPlanner.CHK_verifyheight.Checked = verifyHeightState;

                this.Close();
            }
            else
            {
                CustomMessageBox.Show("Bad Grid", "Error");
            }
        }
        private void AddWP(double Lng, double Lat, double Alt, string tag, double HomeAlt, double pointAlt)
        {
            //if (CHK_copter_headinghold.Checked)
            //{
            //    plugin.Host.AddWPtoList(MAVLink.MAV_CMD.CONDITION_YAW, Convert.ToInt32(TXT_headinghold.Text), 0, 0, 0, 0, 0, 0, gridobject);
            //}

            double p4 = 0;
            double p3 = 0;

            if (tag == "S") p4 = 1;
            if (tag == "E") p4 = 2;

            if (HomeAlt is double.NaN)
            {
                p3 = Alt;
            }
            else
            {
                //  p.Alt = altsrtm.alt - homealt + (double)NUM_altitude.Value;
                //We have a valid home altitude, so calculate back the relative altitude at the given point (Alt tracking)
                p3 = Alt - (pointAlt - HomeAlt);

            }



            if (NUM_DelayAtWP.Value > 0)
            {
                plugin.Host.AddWPtoList(MAVLink.MAV_CMD.WAYPOINT, (double)NUM_DelayAtWP.Value, 0, p3, p4, Lng, Lat, Alt * CurrentState.multiplierdist, null);
            }
            else
            {
                plugin.Host.AddWPtoList(MAVLink.MAV_CMD.WAYPOINT, 0, 0, p3, p4, Lng, Lat, Alt * CurrentState.multiplierdist, null);
            }
        }
        private void NUM_Segments_ValueChanged(object sender, EventArgs e)
        {
            recalculateGrid(sender, e);
        }
        private void myButton1_Click(object sender, EventArgs e)
        {
            map.HoldInvalidation = false;
            map.ZoomAndCenterMarkers("polygons");
        }

        private void CMB_startfrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            if (CMB_startfrom.Text == Utilities.Grid.StartPosition.Point.ToString())
            {
                int pnt = 1;
                MissionPlanner.Controls.InputBox.Show("Enter point #", "Please enter a boundary point number", ref pnt);

                if (list.Count > pnt)
                    Utilities.Grid.StartPointLatLngAlt = list[pnt - 1];
            }

            recalculateGrid(sender, e);
        }

        private void NUM_LaneSeparation_ValueChanged(object sender, EventArgs e)
        {
            recalculateGrid(sender, e);
        }

        private void CHK_extendedpoint_CheckedChanged(object sender, EventArgs e)
        {
            recalculateGrid(sender, e);
        }



        List<PointF> getVehicleAreaDisplacementPoints(double heading, double barwidth)
        {
            double width = barwidth / 0.2; // 20 units of 0.2m = 4m

            //Sanity check, legth and width should be even numbers
            //The vehicle width is perpendicular to the heading
            double alpha = (heading) % 360;

            double alpha_radians = alpha * Math.PI / 180;
            double half_length = width / 2;
            double cos_alpha = Math.Cos(alpha_radians); // dx
            double sin_alpha = Math.Sin(alpha_radians); // dy

            double startX = (-half_length * cos_alpha);
            double startY = (-half_length * sin_alpha);
            double endX = (half_length * cos_alpha);
            double endY = (half_length * sin_alpha);

            double shiftx = -sin_alpha;
            double shifty = cos_alpha;

            List<PointF> points = new List<PointF>();

            double dx = endX - startX;
            double dy = endY - startY;

            double steps = barwidth / 0.2; // 20 units of 0.2m = 4m

            // Calculate the increment in x and y for each step
            double xIncrement = dx / steps;
            double yIncrement = dy / steps;

            // Add the points on the line to the list
            // The shift is one step 20cm to forward and backward So 60cm length 4 meters width.

            for (int i = 0; i <= steps; i++)
            {
                double x = startX + i * xIncrement;
                double y = startY + i * yIncrement;
                points.Add(new PointF((float)x, (float)y));
                points.Add(new PointF((float)(x - shiftx), (float)(y - shifty)));
                points.Add(new PointF((float)(x + shiftx), (float)(y + shifty)));
            }
            return points;
        }

        srtm.altresponce getMaxAltinArea(double lat, double lng, List<PointF> displacement_points)
        {
            srtm.altresponce result = new srtm.altresponce();
            result = srtm.getAltitude(lat, lng);
            result.currenttype = srtm.tiletype.invalid;

            PointLatLngAlt pnt = new PointLatLngAlt(lat, lng, 0, "");
            //StreamWriter sw = new StreamWriter("e:\\points.csv",false);
            //sw.WriteLine("Lat, Lng");
            foreach (PointF point in displacement_points)
            {

                PointLatLngAlt pointToCheck = pnt.gps_offset(point.X * 0.2, -point.Y * 0.2);
                //sw.WriteLine(pointToCheck.Lat + "," + pointToCheck.Lng);

                srtm.altresponce alt = srtm.getAltitude(pointToCheck.Lat, pointToCheck.Lng);

                if (alt.currenttype == srtm.tiletype.valid)
                {
                    if (result.currenttype == srtm.tiletype.invalid)
                    {
                        result = alt;
                    }
                    else
                    {
                        if (alt.alt > result.alt)
                        {
                            result = alt;
                        }
                    }
                }
            }
            //sw.Close();
            return result;
        }

        //Saving and loading

        GridData saveGridData()
        {
            GridData answer = new GridData();

            answer.distance = (double)NUM_Distance.Value;
            answer.litersperha = (double)NUM_LitPerHa.Value;
            answer.flyspeed = (double)NUM_UpDownFlySpeed.Value;
            answer.altitude = (double)NUM_altitude.Value;
            answer.angle = (double)NUM_angle.Value;
            answer.altreference = (altmode)CMB_AltReference.SelectedIndex;
            answer.startfrom = (Utilities.Grid.StartPosition)CMB_startfrom.SelectedIndex;
            answer.barsize = (double)NUM_SprayBarWidth.Value;
            answer.waitatwp = (double)NUM_DelayAtWP.Value;
            answer.splitby = (splitby)CMB_split.SelectedIndex;
            answer.segments = (int)NUM_Segments.Value;
            answer.alttrackingenabled = CHK_enableAltTracking.Checked;
            answer.trackingalterror = (double)NUM_trackingAltError.Value;
            answer.gridshift = (double)NUM_Shift.Value;
            answer.expandobstacles = CHK_expandObstacles.Checked;
            answer.addtakeoff = CHK_addTakeoffAndLanding.Checked;
            answer.headlock = CHK_Headlock.Checked;
            answer.lanesep = (int)NUM_LaneSeparation.Value;
            answer.extendedpoint = CHK_extendedpoint.Checked;
            answer.takeoffalt = (double)NUM_TakeoffAlt.Value;

            answer.polygon = list;
            answer.obstaclesmarks = obstacles;
            answer.fences = MainV2.instance.FlightPlanner.editedFencePoints;

            return answer;

        }


        void loadGridData(GridData data)
        {
            NUM_Distance.Value = (decimal)data.distance;
            NUM_LitPerHa.Value = (decimal)data.litersperha;
            NUM_UpDownFlySpeed.Value = (decimal)data.flyspeed;
            NUM_altitude.Value = (decimal)data.altitude;
            NUM_angle.Value = (decimal)data.angle;
            CMB_AltReference.SelectedIndex = (int)data.altreference;
            CMB_startfrom.SelectedIndex = (int)data.startfrom;
            NUM_SprayBarWidth.Value = (decimal)data.barsize;
            NUM_DelayAtWP.Value = (decimal)data.waitatwp;
            CMB_split.SelectedIndex = (int)data.splitby;
            NUM_Segments.Value = (decimal)data.segments;
            CHK_enableAltTracking.Checked = data.alttrackingenabled;
            NUM_trackingAltError.Value = (decimal)data.trackingalterror;
            NUM_Shift.Value = (decimal)data.gridshift;
            CHK_expandObstacles.Checked = data.expandobstacles;
            CHK_addTakeoffAndLanding.Checked = data.addtakeoff;
            CHK_Headlock.Checked = data.headlock;
            NUM_LaneSeparation.Value = (decimal)data.lanesep;
            CHK_extendedpoint.Checked = data.extendedpoint;
            NUM_TakeoffAlt.Value = (decimal)data.takeoffalt;

            list = data.polygon;
            obstacles = data.obstaclesmarks;
            MainV2.instance.FlightPlanner.editedFencePoints = data.fences;
        }



        public void LoadGrid()
        {
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(GridData));

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "*.grid|*.grid";
                ofd.ShowDialog();

                if (File.Exists(ofd.FileName))
                {
                    using (StreamReader sr = new StreamReader(ofd.FileName))
                    {
                        var test = (GridData)reader.Deserialize(sr);

                        loading = true;
                        loadGridData(test);
                        loading = false;
                        recalculateGrid(null, null);
                    }
                }
            }
        }


        public void SaveGrid()
        {
            System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(GridData));

            var griddata = saveGridData();

            // Save config too
            savesettings();

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "*.grid|*.grid";
                var result = sfd.ShowDialog();

                if (sfd.FileName != "" && result == DialogResult.OK)
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        writer.Serialize(sw, griddata);
                    }
                }
            }
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.O))
            {
                LoadGrid();

                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveGrid();

                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }




    }
}

