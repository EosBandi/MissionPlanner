using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using GMap.NET.MapProviders;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;
using NetTopologySuite.Algorithm;
using OpenTK.Graphics.OpenGL;
using Org.BouncyCastle.Crypto.Engines;
using ZedGraph; // GE xml alt reader

namespace MissionPlanner.Controls
{
    public partial class ElevationProfile : Form
    {
        List<PointLatLngAlt> gelocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> srtmlocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> planlocs = new List<PointLatLngAlt>();
        PointPairList list1 = new PointPairList();
        PointPairList list2 = new PointPairList();
        PointPairList list3 = new PointPairList();

        PointPairList list4terrain = new PointPairList();
        int distance = 0;
        double homealt = 0;
        FlightPlanner.altmode altmode = FlightPlanner.altmode.Relative;

        public ElevationProfile(List<PointLatLngAlt> locs, double homealt, FlightPlanner.altmode altmode)
        {
            InitializeComponent();

            this.altmode = altmode;

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

            this.homealt = homealt;

            Form frm = Common.LoadingBox("Loading", "using alt data");

            //gelocs = getGEAltPath(planlocs);

            srtmlocs = getSRTMAltPathArea(planlocs);

            frm.Close();

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        private void ElevationProfile_Load(object sender, EventArgs e)
        {
            if (planlocs.Count <= 1)
            {
                this.Close();
                return;
            }
            // GE plot
            /*
            double a = 0;
            double increment = (distance / (float)(gelocs.Count - 1));

            foreach (PointLatLngAlt geloc in gelocs)
            {
                if (geloc == null)
                    continue;

                list2.Add(a * CurrentState.multiplierdist, Convert.ToInt32(geloc.Alt * CurrentState.multiplieralt));

                Console.WriteLine("GE " + geloc.Lng + "," + geloc.Lat + "," + geloc.Alt);

                a += increment;
            }
            */
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

                // deal with at mode
                if (altmode == FlightPlanner.altmode.Terrain)
                {
                    list1 = list4terrain;
                    break;
                }
                else if (altmode == FlightPlanner.altmode.Relative)
                {
                    // already includes the home alt
                    list1.Add(a * CurrentState.multiplierdist, (planloc.Alt * CurrentState.multiplieralt), 0, planloc.Tag);
                }
                else
                {
                    // abs
                    // already absolute
                    list1.Add(a * CurrentState.multiplierdist, (planloc.Alt * CurrentState.multiplieralt), 0, planloc.Tag);
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

            double km1 = 0.008983; // One km in degrees
            //Perhaps it sould go outside
            double lngstep = km1 * Math.Cos(MathHelper.deg2rad * lat) / 1000 / 100 * 20;  //20cm in longitude degrees
            double latstep = km1 / 1000 / 100 * 20; // 20 cm in latitude degrees


            foreach (PointF point in displacement_points)
            {
                double latpoint = lat + point.Y * latstep;
                double lngpoint = lng + point.X * lngstep;

                srtm.altresponce alt = srtm.getAltitude(latpoint, lngpoint);

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
            return result;
        }


        //Create a list of points that cover the size of the drone, point distance is 20cm, length and width of the drone is in 20cm units
        //Center is zero, units is 20cm.
        List<PointF> getVehicleAreaDisplacementPoints(double heading)
        {

            double width = 20; // 20 units of 0.2m = 4m

            //Sanity check, legth and width should be even numbers
            //The vehicle width is perpendicular to the heading
            double alpha = (heading + 90) % 360;

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

                double heading = last.GetBearing(loc); // Neds to add 90 degree 
                heading = (heading + 90) % 360;
                //generate the displacement points
                displacement = getVehicleAreaDisplacementPoints(heading);


                double dist = last.GetDistance(loc);


                int points = (int)(dist * 4) + 1;

                double deltalat = (last.Lat - loc.Lat);
                double deltalng = (last.Lng - loc.Lng);
                double deltaalt = last.Alt - loc.Alt;

                double steplat = deltalat / points;
                double steplng = deltalng / points;
                double stepalt = deltaalt / points;

                PointLatLngAlt lastpnt = last;

                for (int a = 0; a <= points; a++)
                {
                    double lat = last.Lat - steplat * a;
                    double lng = last.Lng - steplng * a;
                    double alt = last.Alt - stepalt * a;

                    //var newpoint = new PointLatLngAlt(lat, lng, srtm.getAltitude(lat, lng).alt, "");
                    var newpoint = new PointLatLngAlt(lat, lng, getMaxAltinArea(lat, lng, displacement).alt, "");

                    double subdist = lastpnt.GetDistance(newpoint);

                    disttotal += subdist;

                    // srtm alts
                    list3.Add(disttotal * CurrentState.multiplierdist, newpoint.Alt * CurrentState.multiplieralt);

                    // terrain alt
                    list4terrain.Add(disttotal * CurrentState.multiplierdist, (newpoint.Alt + alt) * CurrentState.multiplieralt);

                    lastpnt = newpoint;
                }

                //answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, srtm.getAltitude(loc.Lat, loc.Lng).alt, ""));
                answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, getMaxAltinArea(loc.Lat, loc.Lng, displacement).alt, ""));

                last = loc;
            }
            return answer;
        }


        List<PointLatLngAlt> getSRTMAltPath(List<PointLatLngAlt> list)
        {
            List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

            PointLatLngAlt last = null;

            double disttotal = 0;

            foreach (PointLatLngAlt loc in list)
            {
                if (loc == null)
                    continue;

                if (last == null)
                {
                    last = loc;
                    if (altmode == FlightPlanner.altmode.Terrain)
                        loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;
                    continue;
                }

                double dist = last.GetDistance(loc);

                if (altmode == FlightPlanner.altmode.Terrain)
                    loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;

                int points = (int)(dist * 4) + 1;

                double deltalat = (last.Lat - loc.Lat);
                double deltalng = (last.Lng - loc.Lng);
                double deltaalt = last.Alt - loc.Alt;

                double steplat = deltalat / points;
                double steplng = deltalng / points;
                double stepalt = deltaalt / points;

                PointLatLngAlt lastpnt = last;

                for (int a = 0; a <= points; a++)
                {
                    double lat = last.Lat - steplat * a;
                    double lng = last.Lng - steplng * a;
                    double alt = last.Alt - stepalt * a;

                    var newpoint = new PointLatLngAlt(lat, lng, srtm.getAltitude(lat, lng).alt, "");

                    double subdist = lastpnt.GetDistance(newpoint);

                    disttotal += subdist;

                    // srtm alts
                    list3.Add(disttotal * CurrentState.multiplierdist, newpoint.Alt * CurrentState.multiplieralt);

                    // terrain alt
                    list4terrain.Add(disttotal * CurrentState.multiplierdist, (newpoint.Alt + alt) * CurrentState.multiplieralt);

                    lastpnt = newpoint;
                }

                answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, srtm.getAltitude(loc.Lat, loc.Lng).alt, ""));

                last = loc;
            }
            return answer;
        }

        List<PointLatLngAlt> getGEAltPath(List<PointLatLngAlt> list)
        {
            double alt = 0;
            double lat = 0;
            double lng = 0;

            int pos = 0;

            List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

            //http://code.google.com/apis/maps/documentation/elevation/
            //http://maps.google.com/maps/api/elevation/xml
            string coords = "";

            foreach (PointLatLngAlt loc in list)
            {
                if (loc == null)
                    continue;

                coords = coords + loc.Lat.ToString(new System.Globalization.CultureInfo("en-US")) + "," +
                         loc.Lng.ToString(new System.Globalization.CultureInfo("en-US")) + "|";
            }
            coords = coords.Remove(coords.Length - 1);

            if (list.Count < 2 || coords.Length > (2048 - 256))
            {
                CustomMessageBox.Show("Too many/few WP's or to Big a Distance " + (distance / 1000) + "km", Strings.ERROR);
                return answer;
            }

            try
            {
                using (
                    XmlTextReader xmlreader =
                        new XmlTextReader("https://maps.google.com/maps/api/elevation/xml?path=" + coords + "&samples=" +
                                          (distance / 100).ToString(new System.Globalization.CultureInfo("en-US")) +
                                          "&sensor=false&key=" + GoogleMapProvider.APIKey))
                {
                    while (xmlreader.Read())
                    {
                        xmlreader.MoveToElement();
                        switch (xmlreader.Name)
                        {
                            case "elevation":
                                alt = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                Console.WriteLine("DO it " + lat + " " + lng + " " + alt);
                                PointLatLngAlt loc = new PointLatLngAlt(lat, lng, alt, "");
                                answer.Add(loc);
                                pos++;
                                break;
                            case "lat":
                                lat = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                break;
                            case "lng":
                                lng = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch
            {
                CustomMessageBox.Show("Error getting GE data", Strings.ERROR);
            }

            return answer;
        }

        public void CreateChart(ZedGraphControl zgc)
        {
            GraphPane myPane = zgc.GraphPane;

            // Set the titles and axis labels
            myPane.Title.Text = "Elevation above ground";
            myPane.XAxis.Title.Text = "Distance (" + CurrentState.DistanceUnit + ")";
            myPane.YAxis.Title.Text = "Elevation (" + CurrentState.AltUnit + ")";

            LineItem myCurve;

            myCurve = myPane.AddCurve("Planned Path", list1, Color.Red, SymbolType.None);
            //myCurve = myPane.AddCurve("Google", list2, Color.Green, SymbolType.None);
            myCurve = myPane.AddCurve("DEM", list3, Color.Blue, SymbolType.None);

            foreach (PointPair pp in list1)
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
    }
}