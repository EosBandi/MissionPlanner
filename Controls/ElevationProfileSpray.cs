using Accord.Imaging.Filters;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZedGraph; // GE xml alt reader

namespace MissionPlanner.Controls
{
    public partial class ElevationProfileSpray : Form
    {
        //List<PointLatLngAlt> gelocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> srtmlocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> planlocs = new List<PointLatLngAlt>();
        PointPairList flightPlanPoints = new PointPairList();
        PointPairList flightPlanCheckedPoints = new PointPairList();
        PointPairList relativeToHomeElevation = new PointPairList();
        PointPairList differenceList = new PointPairList();

        List<PointLatLngAlt> problemlocs = new List<PointLatLngAlt>();

        //PointPairList list4terrain = new PointPairList();
        int distance = 0;
        double homealt = 0;
        int altindex = 0; // index of the altitude in the command list
        FlightPlanner.altmode altmode = FlightPlanner.altmode.Relative;


        public void initGraph(List<PointLatLngAlt> locs, double homealt, FlightPlanner.altmode altmode, int altindex)
        {

            this.altindex = altindex;
            //this.altmode = altmode;
            planlocs = locs;

            for (int a = 0; a < planlocs.Count; a++)
            {
                if (planlocs[a] == null || planlocs[a].Tag != null && planlocs[a].Tag.Contains("ROI"))
                {
                    planlocs.RemoveAt(a);
                    a--;
                }
            }

            if (planlocs.Count <= 1)
            {
                CustomMessageBox.Show("Please plan something first", Strings.ERROR);
                return;
            }


            if (locs.Count <= 1)
            {
                CustomMessageBox.Show("There is only a home point. Nothing to show!", Strings.ERROR);
                return;
            }


            srtm.altresponce alt = srtm.getAltitude(locs[0].Lat, locs[0].Lng);

            if (alt.currenttype == srtm.tiletype.valid)
            {
                this.homealt = alt.alt;
            }
            else
            {
                CustomMessageBox.Show("Home altitude not found", Strings.ERROR);
                return;
            }


            // get total distance
            distance = 0;
            PointLatLngAlt lastloc = null;
            foreach (PointLatLngAlt loc in planlocs)
            {
                if (loc == null)
                    continue;

                if (lastloc != null)
                {
                    distance += (int)loc.GetDistance(lastloc);
                }
                lastloc = loc;
            }

            relativeToHomeElevation.Clear();
            flightPlanCheckedPoints.Clear();
            differenceList.Clear();
            flightPlanPoints.Clear();
            zg1.GraphPane.CurveList.Clear();
            zg1.GraphPane.GraphObjList.Clear();
            zg1.RestoreScale(zg1.GraphPane);
            zg1.AxisChange();


            Form frm = Common.LoadingBox("Loading", "using alt data");

            srtmlocs = getSRTMAltPathArea(planlocs);

            frm.Close();




        }




        public ElevationProfileSpray(List<PointLatLngAlt> locs, double homealt, FlightPlanner.altmode altmode, int altindex)
        {
            InitializeComponent();
            initGraph(locs, homealt, altmode, altindex);

        }


        private void ElevationProfile_Load(object sender, EventArgs e)
        {
            if (planlocs.Count <= 1)
            {
                this.Close();
                return;
            }

            // Planner Plot
            double a = 0;
            int count = 0;
            PointLatLngAlt lastloc = null;
            foreach (PointLatLngAlt planloc in planlocs)
            {
                if (planloc == null)
                    continue;

                if (lastloc != null)
                {
                    a += planloc.GetDistance(lastloc);
                }

                if (altmode == FlightPlanner.altmode.Relative)
                {
                    flightPlanPoints.Add(a * CurrentState.multiplierdist, ((planloc.Alt-homealt) * CurrentState.multiplieralt), 0, planloc.Tag);
                }

                lastloc = planloc;
                count++;
            }
            // draw graph
            CreateChart(zg1);
        }


        // Center point lat and lng
        // boxsize is the size of the side of box with a center point
        // resolution is in cm, it impacts running time

        // Returns the maximum altitude in the area from srtm

        // To get lngOnecm
        //lngonecm = 111.320 * math.cos(math.radians(lattitude)) / 1000 / 100;
        //latonecm = 111.320 / 1000 / 100;
        //heading is the heading of the plane in degrees




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

                PointLatLngAlt pointToCheck = pnt.gps_offset(point.X*0.2, -point.Y*0.2);
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


        //Create a list of points that cover the size of the drone, point distance is 20cm, length and width of the drone is in 20cm units
        //Center is zero, units is 20cm.
        List<PointF> getVehicleAreaDisplacementPoints(double heading)
        {
            double width = 20; // 20 units of 0.2m = 4m

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

            double steps = 20; // 20 units of 0.2m = 4m

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


        List<PointLatLngAlt> getSRTMAltPathArea(List<PointLatLngAlt> list)
        {
            List<PointLatLngAlt> answer = new List<PointLatLngAlt>();
            List<PointF> displacement = new List<PointF>();

            PointLatLngAlt last = null;

            double disttotal = 0;

            foreach (PointLatLngAlt loc in list)
            {
                if (loc == null)
                    continue;

                //Removed terrain following, we use relative altitudes
                //Ignore the first point We don't have a heading
                if (last == null)
                {
                    last = loc;
                    continue;
                }

                double heading = last.GetBearing(loc);
                //heading = (heading + 90) % 360;
                //generate the displacement points
                displacement = getVehicleAreaDisplacementPoints(heading);


                double dist = last.GetDistance(loc);


                int points = (int)(dist * 2.5) + 1;

                double deltalat = (last.Lat - loc.Lat);
                double deltalng = (last.Lng - loc.Lng);
                double deltaalt = last.Alt - loc.Alt;

                double steplat = deltalat / points;
                double steplng = deltalng / points;
                double stepalt = deltaalt / points;

                PointLatLngAlt lastpnt = last;

                //Go through between the twp points in distance/4+1 steps which is 25cm
                for (int a = 0; a <= points; a++)
                {
                    double lat = last.Lat - steplat * a;        //location new position
                    double lng = last.Lng - steplng * a;
                    double alt = last.Alt - stepalt * a;        //vehicle center estimated altitude of a given point, extrapolated from the two points



                    //var newpoint = new PointLatLngAlt(lat, lng, srtm.getAltitude(lat, lng).alt, "");
                    var newpoint = new PointLatLngAlt(lat, lng, getMaxAltinArea(lat, lng, displacement).alt, "");

                    double subdist = lastpnt.GetDistance(newpoint);

                    disttotal += subdist;

                    // relative to home alt
                    relativeToHomeElevation.Add(disttotal, newpoint.Alt-homealt);

                    // Flight plane points
                    flightPlanCheckedPoints.Add(disttotal, alt - homealt);

                    double difference = (alt - homealt) - (newpoint.Alt - homealt);
                    //difference between the two
                    differenceList.Add(disttotal, difference);

                    lastpnt = newpoint;
                }

                //answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, srtm.getAltitude(loc.Lat, loc.Lng).alt, ""));
                //answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, getMaxAltinArea(loc.Lat, loc.Lng, displacement).alt, ""));

                last = loc;
            }
            return answer;
        }


        //List<PointLatLngAlt> getSRTMAltPath(List<PointLatLngAlt> list)
        //{
        //    List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

        //    PointLatLngAlt last = null;

        //    double disttotal = 0;

        //    foreach (PointLatLngAlt loc in list)
        //    {
        //        if (loc == null)
        //            continue;

        //        if (last == null)
        //        {
        //            last = loc;
        //            if (altmode == FlightPlanner.altmode.Terrain)
        //                loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;
        //            continue;
        //        }

        //        double dist = last.GetDistance(loc);

        //        if (altmode == FlightPlanner.altmode.Terrain)
        //            loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;

        //        int points = (int)(dist * 4) + 1;

        //        double deltalat = (last.Lat - loc.Lat);
        //        double deltalng = (last.Lng - loc.Lng);
        //        double deltaalt = last.Alt - loc.Alt;

        //        double steplat = deltalat / points;
        //        double steplng = deltalng / points;
        //        double stepalt = deltaalt / points;

        //        PointLatLngAlt lastpnt = last;

        //        for (int a = 0; a <= points; a++)
        //        {
        //            double lat = last.Lat - steplat * a;
        //            double lng = last.Lng - steplng * a;
        //            double alt = last.Alt - stepalt * a;

        //            var newpoint = new PointLatLngAlt(lat, lng, srtm.getAltitude(lat, lng).alt, "");

        //            double subdist = lastpnt.GetDistance(newpoint);

        //            disttotal += subdist;

        //            // srtm alts
        //            replativeToHomeElevation.Add(disttotal * CurrentState.multiplierdist, newpoint.Alt * CurrentState.multiplieralt);

        //            // terrain alt
        //            list4terrain.Add(disttotal * CurrentState.multiplierdist, (newpoint.Alt + alt) * CurrentState.multiplieralt);

        //            lastpnt = newpoint;
        //        }

        //        answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, srtm.getAltitude(loc.Lat, loc.Lng).alt, ""));

        //        last = loc;
        //    }
        //    return answer;
        //}

        //List<PointLatLngAlt> getGEAltPath(List<PointLatLngAlt> list)
        //{
        //    double alt = 0;
        //    double lat = 0;
        //    double lng = 0;

        //    int pos = 0;

        //    List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

        //    //http://code.google.com/apis/maps/documentation/elevation/
        //    //http://maps.google.com/maps/api/elevation/xml
        //    string coords = "";

        //    foreach (PointLatLngAlt loc in list)
        //    {
        //        if (loc == null)
        //            continue;

        //        coords = coords + loc.Lat.ToString(new System.Globalization.CultureInfo("en-US")) + "," +
        //                 loc.Lng.ToString(new System.Globalization.CultureInfo("en-US")) + "|";
        //    }
        //    coords = coords.Remove(coords.Length - 1);

        //    if (list.Count < 2 || coords.Length > (2048 - 256))
        //    {
        //        CustomMessageBox.Show("Too many/few WP's or to Big a Distance " + (distance / 1000) + "km", Strings.ERROR);
        //        return answer;
        //    }

        //    try
        //    {
        //        using (
        //            XmlTextReader xmlreader =
        //                new XmlTextReader("https://maps.google.com/maps/api/elevation/xml?path=" + coords + "&samples=" +
        //                                  (distance / 100).ToString(new System.Globalization.CultureInfo("en-US")) +
        //                                  "&sensor=false&key=" + GoogleMapProvider.APIKey))
        //        {
        //            while (xmlreader.Read())
        //            {
        //                xmlreader.MoveToElement();
        //                switch (xmlreader.Name)
        //                {
        //                    case "elevation":
        //                        alt = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
        //                        Console.WriteLine("DO it " + lat + " " + lng + " " + alt);
        //                        PointLatLngAlt loc = new PointLatLngAlt(lat, lng, alt, "");
        //                        answer.Add(loc);
        //                        pos++;
        //                        break;
        //                    case "lat":
        //                        lat = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
        //                        break;
        //                    case "lng":
        //                        lng = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
        //                        break;
        //                    default:
        //                        break;
        //                }
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        CustomMessageBox.Show("Error getting GE data", Strings.ERROR);
        //    }

        //    return answer;
        //}

        public void CreateChart(ZedGraphControl zgc)
        {
            zgc.IsShowCursorValues = true;


            GraphPane myPane = zgc.GraphPane;

            // Set the titles and axis labels
            myPane.Title.Text = "Elevation above ground";
            myPane.XAxis.Title.Text = "Distance (" + CurrentState.DistanceUnit + ")";
            myPane.YAxis.Title.Text = "Elevation (" + CurrentState.AltUnit + ")";

            LineItem myCurve;

            myCurve = myPane.AddCurve("Planned Path", flightPlanCheckedPoints, Color.Red, SymbolType.None);
            myCurve = myPane.AddCurve("Difference", differenceList, Color.Green, SymbolType.None);
            myCurve = myPane.AddCurve("DEM", relativeToHomeElevation, Color.Blue, SymbolType.None);

            foreach (PointPair pp in flightPlanPoints)
            {
                // Add a another text item to to point out a graph feature
                TextObj text = new TextObj((string)pp.Tag, pp.X, pp.Y);
                // rotate the text 90 degrees
                text.FontSpec.Angle = 90;
                text.FontSpec.FontColor = Color.White;
                // Align the text such that the Right-Center is at (700, 50) in user scale coordinates
                text.Location.AlignH = AlignH.Right;
                text.Location.AlignV = AlignV.Center;
                // Disable the border and background fill options for the text
                text.FontSpec.Fill.IsVisible = false;
                text.FontSpec.Border.IsVisible = false;
                myPane.GraphObjList.Add(text);
            }

            // Show the x axis grid
            myPane.XAxis.MajorGrid.IsVisible = true;

            myPane.XAxis.Scale.Min = 0;
            myPane.XAxis.Scale.Max = distance * CurrentState.multiplierdist;

            // Make the Y axis scale red
            myPane.YAxis.Scale.FontSpec.FontColor = Color.Red;
            myPane.YAxis.Title.FontSpec.FontColor = Color.Red;
            // turn off the opposite tics so the Y tics don't show up on the Y2 axis
            myPane.YAxis.MajorTic.IsOpposite = false;
            myPane.YAxis.MinorTic.IsOpposite = false;
            // Don't display the Y zero line
            myPane.YAxis.MajorGrid.IsZeroLine = true;
            // Align the Y axis labels so they are flush to the axis
            myPane.YAxis.Scale.Align = AlignP.Inside;
            // Manually set the axis range
            //myPane.YAxis.Scale.Min = -1;
            //myPane.YAxis.Scale.Max = 1;

            // Fill the axis background with a gradient
            //myPane.Chart.Fill = new Fill(Color.White, Color.LightGray, 45.0f);

            // Calculate the Axis Scale Ranges
            try
            {
                zg1.AxisChange();
            }
            catch
            {
            }
        }

        private void zg1_Load(object sender, EventArgs e)
        {

        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
            else
                SetWindowPos(this.Handle, MainV2.instance.Handle, 0, 0, 0, 0, TOPMOST_FLAGS);
        }

        private void myButton1_Click(object sender, EventArgs e)
        {

            MainV2.instance.FlightPlanner.writeKML();

            List<PointLatLngAlt> points = new List<PointLatLngAlt>();

            foreach (var item in MainV2.instance.FlightPlanner.pointlist)
            {
                if (item is null)
                    continue;

                double relAlt = 0;
                if (item.Tag != "H")
                {
                    int index = int.Parse(item.Tag.ToString()) - 1;
                    relAlt = MainV2.instance.FlightPlanner.Commands.Rows[index].Cells[altindex].Value == null
                        ? item.Alt
                        : double.Parse(MainV2.instance.FlightPlanner.Commands.Rows[index].Cells[altindex].Value.ToString());
                }
                points.Add(new PointLatLngAlt(item.Lat, item.Lng, relAlt));
            }

            //double homealt = MainV2.comPort.MAV.cs.HomeAlt;
            var altmode = FlightPlanner.altmode.Relative;
            // altmode should not change in sprayplanner mode.
            initGraph(MainV2.instance.FlightPlanner.pointlist, homealt, FlightPlanner.altmode.Relative, altindex);
            ElevationProfile_Load(sender, e);
            zg1.Invalidate();
        }
    }
}