using GMap.NET;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;


namespace MissionPlanner.Maps
{
    public class GMapMarkerNFZCircle : GMapMarkerAirport
    {
        public Pen Pen = new Pen(Brushes.White, 2);

        public Color FillColor { get; set; }

        public Color Color
        {
            get { return Pen.Color; }
            set
            {
                if (!initcolor.HasValue) initcolor = value;
                Pen.Color = value;
            }
        }

        Color? initcolor = null;


        public GMapMarkerNFZCircle(PointLatLng p)
            : base(p)
        {
        }

        public override void OnRender(IGraphics g)
        {
            base.OnRender(g);

            if (wprad == 0 || Overlay.Control == null)
                return;

            // if we have drawn it, then keep that color
            if (!initcolor.HasValue)
                Color = Color.White;

            //wprad = 300;

            // undo autochange in mouse over
            //if (Pen.Color == Color.Blue)
            //  Pen.Color = Color.White;

            double width =
                (Overlay.Control.MapProvider.Projection.GetDistance(Overlay.Control.FromLocalToLatLng(0, 0),
                    Overlay.Control.FromLocalToLatLng(Overlay.Control.Width, 0)) * 1000.0);
            double height =
                (Overlay.Control.MapProvider.Projection.GetDistance(Overlay.Control.FromLocalToLatLng(0, 0),
                    Overlay.Control.FromLocalToLatLng(Overlay.Control.Height, 0)) * 1000.0);
            double m2pixelwidth = Overlay.Control.Width / width;
            double m2pixelheight = Overlay.Control.Height / height;

            GPoint loc = new GPoint((int)(LocalPosition.X - (m2pixelwidth * wprad * 2)), LocalPosition.Y);
            // MainMap.FromLatLngToLocal(wpradposition);


            int x = LocalPosition.X - Offset.X - (int)(Math.Abs(loc.X - LocalPosition.X) / 2);
            int y = LocalPosition.Y - Offset.Y - (int)Math.Abs(loc.X - LocalPosition.X) / 2;
            int widtharc = (int)Math.Abs(loc.X - LocalPosition.X);
            int heightarc = (int)Math.Abs(loc.X - LocalPosition.X);

            if (widtharc > 0 && widtharc < 200000000 && Overlay.Control.Zoom > 3)
            {
                g.DrawArc(Pen, new System.Drawing.Rectangle(x, y, widtharc, heightarc), 0, 360);

                g.FillPie(new SolidBrush(FillColor), x, y, widtharc, heightarc, 0, 360);
            }
        }

        public bool isActive()
        {
            DateTime time = DateTime.UtcNow;
            int dayOfWeek = ((int)time.DayOfWeek + 6) % 7 + 1;

            //get the 
            if (time < startDateTime || time > endDateTime)
                return false;

            foreach (var s in Schedules)
            {
                if (s.day == 0)
                    continue;

                if ((s.day & (1 << dayOfWeek)) != 0)
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
        public List<nfzSchedule> Schedules { get; set; }




    }
}
