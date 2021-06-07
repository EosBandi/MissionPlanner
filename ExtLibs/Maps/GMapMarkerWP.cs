using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;

namespace MissionPlanner.Maps
{
    [Serializable]
    public class GMapMarkerWP : GMarkerGoogle
    {
        string wpno = "";
        public bool selected = false;
        SizeF txtsize = SizeF.Empty;
        static Dictionary<string, Bitmap> fontBitmaps = new Dictionary<string, Bitmap>();
        static Font font;

        public GMapMarkerWP(PointLatLng p, string wpno, GMarkerGoogleType marker = GMarkerGoogleType.wp)
            : base(p, marker)
        {
            this.wpno = wpno;
            if (font == null)
                font = SystemFonts.DefaultFont;

            if (!fontBitmaps.ContainsKey(wpno))
            {
                Bitmap temp = new Bitmap(this.Size.Width, this.Size.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(temp))
                {
                    StringFormat stringFormat = new StringFormat();
                    stringFormat.Alignment = StringAlignment.Center;
                    stringFormat.LineAlignment = StringAlignment.Center;
                    g.DrawString(wpno, font, Brushes.Black, new Rectangle(0, 0, this.Size.Width, this.Size.Height-7), stringFormat);
                }
                fontBitmaps[wpno] = temp;
            }
        }

        //public GMapMarkerWP(PointLatLng p, string wpno, bool payloadAction)
        //    : base(p, GMarkerGoogleType.orange)
        //{
        //    this.wpno = wpno;
        //    if (font == null)
        //        font = SystemFonts.DefaultFont;

        //    if (!fontBitmaps.ContainsKey(wpno))
        //    {
        //        Bitmap temp = new Bitmap(this.Size.Width , this.Size.Height, PixelFormat.Format32bppArgb);
        //        using (Graphics g = Graphics.FromImage(temp))
        //        {
        //            StringFormat stringFormat = new StringFormat();
        //            stringFormat.Alignment = StringAlignment.Center;
        //            stringFormat.LineAlignment = StringAlignment.Center;
        //            g.DrawString(wpno, font, Brushes.Black, new Rectangle(0, 0, this.Size.Width, this.Size.Height), stringFormat);
        //        }
        //        fontBitmaps[wpno] = temp;
        //    }
        //}

        public override void OnRender(IGraphics g)
        {
            if (selected)
            {
                g.FillEllipse(Brushes.Red, new Rectangle(this.LocalPosition, this.Size));
                g.DrawArc(Pens.Red, new Rectangle(this.LocalPosition, this.Size), 0, 360);
            }

            base.OnRender(g);
            if (selected)
            {
                g.DrawImageUnscaled(Resources.wp_selected, this.LocalPosition);
            }

            if (Overlay.Control.Zoom> 11 || IsMouseOver)
            {
                g.DrawImageUnscaled(fontBitmaps[wpno], this.LocalPosition);
            }



        }
    }
}