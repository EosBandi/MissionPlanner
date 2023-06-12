
namespace GMap.NET
{
   using System;
   using System.Globalization;

   /// <summary>
   /// the point of coordinates
   /// </summary>
   [Serializable]
   public struct PointLatLng
   {
      public static readonly PointLatLng Empty = new PointLatLng();
      private double lat;
      private double lng;

      bool NotEmpty;

      public PointLatLng(double lat, double lng)
      {
         this.lat = lat;
         this.lng = lng;
         NotEmpty = true;
      }

      /// <summary>
      /// returns true if coordinates wasn't assigned
      /// </summary>
      public bool IsEmpty
      {
         get
         {
            return !NotEmpty;
         }
      }

      public double Lat
      {
         get
         {
            return this.lat;
         }
         set
         {
            this.lat = value;
            NotEmpty = true;
         }
      }

      public double Lng
      {
         get
         {
            return this.lng;
         }
         set
         {
            this.lng = value;
            NotEmpty = true;
         }
      }

      public static PointLatLng operator +(PointLatLng pt, SizeLatLng sz)
      {
         return Add(pt, sz);
      }

      public static PointLatLng operator -(PointLatLng pt, SizeLatLng sz)
      {
         return Subtract(pt, sz);
      }

      public static bool operator ==(PointLatLng left, PointLatLng right)
      {
         return ((left.Lng == right.Lng) && (left.Lat == right.Lat));
      }

      public static bool operator !=(PointLatLng left, PointLatLng right)
      {
         return !(left == right);
      }

      public static PointLatLng Add(PointLatLng pt, SizeLatLng sz)
      {
         return new PointLatLng(pt.Lat - sz.HeightLat, pt.Lng + sz.WidthLng);
      }

      public static PointLatLng Subtract(PointLatLng pt, SizeLatLng sz)
      {
         return new PointLatLng(pt.Lat + sz.HeightLat, pt.Lng - sz.WidthLng);
      }

      public override bool Equals(object obj)
      {
         if(!(obj is PointLatLng))
         {
            return false;
         }
         PointLatLng tf = (PointLatLng)obj;
         return (((tf.Lng == this.Lng) && (tf.Lat == this.Lat)) && tf.GetType().Equals(base.GetType()));
      }

      public void Offset(PointLatLng pos)
      {
         this.Offset(pos.Lat, pos.Lng);
      }

      public void Offset(double lat, double lng)
      {
         this.Lng += lng;
         this.Lat -= lat;
      }

        public const double rad2deg = (180 / Math.PI);
        public const double deg2rad = (1.0 / rad2deg);

        public double GetBearing(PointLatLng p2)
        {
            var latitude1 = deg2rad * (this.Lat);
            var latitude2 = deg2rad * (p2.Lat);
            var longitudeDifference = deg2rad * (p2.Lng - this.Lng);

            var y = Math.Sin(longitudeDifference) * Math.Cos(latitude2);
            var x = Math.Cos(latitude1) * Math.Sin(latitude2) - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitudeDifference);

            return (rad2deg * (Math.Atan2(y, x)) + 360) % 360;
        }

        public double GetAngle(PointLatLng point, double heading)
        {
            double angle = GetBearing(point) - heading;
            if (angle < -180.0)
            {
                angle += 360.0;
            }
            if (angle > 180.0)
            {
                angle -= 360.0;
            }
            return angle;
        }

        public double GetDistance2(PointLatLng p2)
        {
            //http://www.movable-type.co.uk/scripts/latlong.html
            var R = 6371.0; // 6371 km
            var dLat = (p2.Lat - Lat) * deg2rad;
            var dLon = (p2.Lng - Lng) * deg2rad;
            var lat1 = Lat * deg2rad;
            var lat2 = p2.Lat * deg2rad;

            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            var d = R * c * 1000.0; // M

            return d;
        }

        public override int GetHashCode()
      {
         return (this.Lng.GetHashCode() ^ this.Lat.GetHashCode());
      }

      public override string ToString()
      {
         return string.Format(CultureInfo.CurrentCulture, "{{Lat={0}, Lng={1}}}", this.Lat, this.Lng);
      }
   }
}