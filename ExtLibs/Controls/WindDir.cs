using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace MissionPlanner.Controls
{
    public partial class WindDir : MyUserControl
    {
        public WindDir()
        {
            InitializeComponent();
            this.BackColor = Color.Transparent;
            this.DoubleBuffered = true;

        }

        const double rad2deg = (180 / Math.PI);
        const double deg2rad = (1.0 / rad2deg);
        double _direction = 0;
        double _speed = 0;

        double maxspeed = 10;

        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Options")]
        public double Direction { get { return _direction; } set { if (_direction == (value + 180)) return; _direction = (value + 180); this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Options")]
        public double Speed { get { return _speed; } set { if (_speed == value) return; _speed = value; this.Invalidate(); } }

        Pen blackpen = new Pen(Color.Black,2);
        Pen redpen = new Pen(Color.Red, 2);

        protected override void OnPaint(PaintEventArgs e)
        {
           // e.Graphics.Clear(Color.Transparent);

            try
            {

               // Bitmap bg = new Bitmap(this.Width, this.Height);

                //this.Visible = false;

               // this.Parent.DrawToBitmap(bg, this.ClientRectangle);

               // this.BackgroundImage = bg;

                //this.Visible = true;
            }
            catch { }

            if (_direction > 360)
                _direction = _direction % 360;

            base.OnPaint(e);

            //Rectangle outside = new Rectangle(1,1,this.Width - 3, this.Height -3);

            //e.Graphics.DrawArc(blackpen, outside, 0, 360);

            //Rectangle inside = new Rectangle(this.Width / 4,this.Height / 4, (this.Width/4) * 2,(this.Height / 4) * 2);

            //e.Graphics.DrawArc(blackpen, inside, 0, 360);

            double x = (this.Width / 2) * Math.Cos((_direction - 90) * deg2rad);

            double y = (this.Height / 2) * Math.Sin((_direction-90) * deg2rad);

            // full scale is 10ms

            double scale = Math.Max(maxspeed, Speed);

            e.Graphics.DrawString(Speed.ToString("0"), this.Font, Brushes.Red, (float)5, (float)5);



            x = x / scale * Speed;
            y = y / scale * Speed;

            if (x != 0 || y != 0)
            {
                float outx =  (float)(this.Width / 2 + x);
                float outy =  (float)(this.Height / 2 + y);

                //line
                e.Graphics.DrawLine(redpen, this.Width / 2, this.Height / 2,outx,outy);

                // arrow

                float x1 = (this.Width / 7) * (float)Math.Cos((_direction - 60) * deg2rad);
                float y1 = (this.Height / 7) * (float)Math.Sin((_direction - 60) * deg2rad);

                e.Graphics.DrawLine(redpen, outx, outy, outx - x1, outy - y1);

                x1 = (this.Width / 7) * (float)Math.Cos((_direction + 60 + 180) * deg2rad);
                y1 = (this.Height / 7) * (float)Math.Sin((_direction + 60 + 180) * deg2rad);

                e.Graphics.DrawLine(redpen, outx, outy, outx - x1, outy - y1);
            }

        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {

            base.OnPaintBackground(e);
            //e.Graphics.Clear(Color.Transparent);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }


        // Rounded corners
        private int radius = 10;
        [DefaultValue(20)]
        public int Radius
        {
            get { return radius; }
            set
            {
                radius = value;
                this.RecreateRegion();
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private GraphicsPath GetRoundRectagle(Rectangle bounds, int radius)
        {
            float r = radius;
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(bounds.Left, bounds.Top, r, r, 180, 90);
            path.AddArc(bounds.Right - r, bounds.Top, r, r, 270, 90);
            path.AddArc(bounds.Right - r, bounds.Bottom - r, r, r, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void RecreateRegion()
        {
            var bounds = ClientRectangle;

            this.Region = Region.FromHrgn(CreateRoundRectRgn(bounds.Left, bounds.Top,
                bounds.Right, bounds.Bottom, Radius, radius));
            this.Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            this.RecreateRegion();
        }
        // End rounded rectangle




    }
}
