using System;
using System.Drawing;
using GMap.NET;
using GMap.NET.WindowsForms;

namespace MissionPlanner.Maps
{
    [Serializable]
    public class GMapMarkerADSBPlane : GMapMarker
    {
        private static readonly Bitmap icong = new Bitmap(global::MissionPlanner.Maps.Resources.ADSB_green,
            new Size(40, 40));

        private static readonly Bitmap iconr = new Bitmap(global::MissionPlanner.Maps.Resources.ADSB_red,
            new Size(40, 40));

        private static readonly Bitmap icono = new Bitmap(global::MissionPlanner.Maps.Resources.ADSB_orange,
            new Size(40, 40));

        public float heading = 0;
        public AlertLevelOptions AlertLevel = AlertLevelOptions.Green;

        public enum AlertLevelOptions
        {
            Green,
            Orange,
            Red
        }

        public GMapMarkerADSBPlane(PointLatLng p, float heading, AlertLevelOptions alert = AlertLevelOptions.Green)
            : base(p)
        {
            this.AlertLevel = alert;
            this.heading = heading;
            Size = icong.Size;
            Offset = new Point(Size.Width / -2, Size.Height / -2);
        }

        public override void OnRender(IGraphics g)
        {
            var temp = g.Transform;
            g.TranslateTransform(LocalPosition.X - Offset.X, LocalPosition.Y - Offset.Y);

            g.RotateTransform(-Overlay.Control.Bearing);

            try
            {
                g.RotateTransform(heading);
            }
            catch
            {
            }

            switch (AlertLevel)
            {
                case AlertLevelOptions.Green:
                    g.DrawImageUnscaled(icong, icong.Width / -2, icong.Height / -2);
                    break;
                case AlertLevelOptions.Orange:
                    g.DrawImageUnscaled(icono, icono.Width / -2, icono.Height / -2);
                    break;
                case AlertLevelOptions.Red:
                    g.DrawImageUnscaled(iconr, iconr.Width / -2, iconr.Height / -2);
                    break;
            }

            g.Transform = temp;
        }
    }
}