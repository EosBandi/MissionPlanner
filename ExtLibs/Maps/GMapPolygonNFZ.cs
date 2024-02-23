using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.WindowsForms;


namespace MissionPlanner.Maps
{

    public enum nfzDay
    {
        MON = 0,
        TUE = 1,
        WED = 2,
        THU = 3,
        FRI = 4,
        SAT = 5,
        SUN = 6,
        ANY = 7
    }

    public class nfzSchedule
    {
        public byte day; // Bitfield BIT0-BIT6, 0 = Monday, BIT7 = everyday
        public DateTime startTime;
        public DateTime stopTime;
    }


    public class GMapPolygonNFZ : GMapPolygon
    {


        public RectLatLng ExtendedBounds { get; private set; }


        public GMapPolygonNFZ(List<PointLatLng> points, string name)
            : base(points, name)
        {

            //Update Bounds if it not yet calculated
            if (Bounds.Lat == 0 && Bounds.Lng == 0)
            {
                var minx = Points.Min(a => a.Lng);
                var maxx = Points.Max(a => a.Lng);
                var miny = Points.Min(a => a.Lat);
                var maxy = Points.Max(a => a.Lat);
                ExtendedBounds = RectLatLng.Inflate(RectLatLng.FromLTRB(minx, maxy, maxx, miny), 0.1, 0.1);
            }
            else
            {
                ExtendedBounds = RectLatLng.Inflate(Bounds, 0.1, 0.1);
            }

        }

        /// <summary>
        /// checks if point is inside the polygon,
        /// info.: http://greatmaps.codeplex.com/discussions/279437#post700449
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool IsInside(PointLatLng p)
        {
            int count = Points.Count;

            if (count < 3)
            {
                return false;
            }

            bool result = false;

            for (int i = 0, j = count - 1; i < count; i++)
            {
                var p1 = Points[i];
                var p2 = Points[j];

                if (p1.Lat < p.Lat && p2.Lat >= p.Lat || p2.Lat < p.Lat && p1.Lat >= p.Lat)
                {
                    if (p1.Lng + (p.Lat - p1.Lat) / (p2.Lat - p1.Lat) * (p2.Lng - p1.Lng) < p.Lng)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        /// <summary>
        /// Get the distance of a point from the polygon boudary,
        /// </summary>
        /// <param name = "p" ></ param >
        /// < returns > The distance of p in meters, zero if point inside the polygon,99999 if distance not calculable</returns>

        public double GetDistance(PointLatLng p)
        {

            double minDistance  = 99999;

            //We need at least three points to have a 2 dimensional polygon
            if (Points.Count < 3)
                return  minDistance;

            if (IsInside(p))
            {
                return 0;
            }


            if (!(ExtendedBounds.Contains(p)))
            {
                return minDistance;
            }


            List<PointLatLng> polygon = new List<PointLatLng>(Points.CloseTheLoop());
            for (int i = 0; i < Points.Count; i++)
            {
                //(double lat1 = , double lon1) = polygon[i];
                double lat1 = polygon[i].Lat;
                double lon1 = polygon[i].Lng;


                double lat2 = polygon[(i + 1) % polygon.Count].Lat;
                double lon2 = polygon[(i + 1) % polygon.Count].Lng; 
                // Get the next point, or the first point if we're at the end



                double distance = DistanceFromPointToLineSegment((p.Lat, p.Lng), (lat1, lon1), (lat2, lon2));

                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
        }



        public double DistanceFromPointToLineSegment((double lat, double lon) point, (double lat, double lon) point1, (double lat, double lon) point2)
        {
            double distance1 = HaversineDistance(point, point1);
            double distance2 = HaversineDistance(point, point2);

            return Math.Min(distance1, distance2) * 1000; //convert to meters
        }

        public double HaversineDistance((double lat, double lon) point1, (double lat, double lon) point2)
        {
            double R = 6371; // Radius of the Earth in kilometers
            double dLat = ToRadians(point2.lat - point1.lat);
            double dLon = ToRadians(point2.lon - point1.lon);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(point1.lat)) * Math.Cos(ToRadians(point2.lat)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        public double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }



        public bool isActive()
        {
            DateTime time = DateTime.UtcNow;
            int dayOfWeek = ((int)time.DayOfWeek + 6) % 7 + 1;


            if (startDateTime == DateTime.MinValue || endDateTime == DateTime.MinValue)
                return true;

            //get the 
            if (time < startDateTime || time > endDateTime)
                return false;

            foreach (var s in Schedules)
            {
                if (s.day == 0)
                    continue;

                if ((s.day & (1 << (dayOfWeek-1))) != 0)
                {
                    if (time.TimeOfDay >= s.startTime.TimeOfDay && time.TimeOfDay <= s.stopTime.TimeOfDay)
                        return true;
                }
                if (s.day == 0x80) //bit seven is all of days
                {
                    if (time.TimeOfDay >= s.startTime.TimeOfDay && time.TimeOfDay <= s.stopTime.TimeOfDay)
                        return true;
                }
            }

            return false;
        }




        //All date and tim is in UTC (converted when read in from file)
        public DateTime startDateTime { get; set; }
        public DateTime endDateTime { get; set; }
        public List<nfzSchedule> Schedules { get; set;}

    }
}
