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
            this.Width = 75;
            this.Height = 40;


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
            e.Graphics.Clear(Color.DarkSlateGray);


            if (_direction > 360)
                _direction = _direction % 360;

            base.OnPaint(e);


            var r = ClientRectangle;
            r.X += 1;
            r.Y += 1;
            r.Width -= 3;
            r.Height -= 3;
            var g = GetRoundRectagle(r, 10);
            e.Graphics.DrawPath(Pens.Gray, g);

            Font f = new Font("Arial", 12, FontStyle.Bold);

            e.Graphics.DrawString(((int)Speed).ToString("D2"), f, Brushes.White, this.Width - 30, 2);
            e.Graphics.DrawString(((int)Direction).ToString("D3") + "°", f, Brushes.White, this.Width -39, 18);
            e.Graphics.TranslateTransform(17.5f, 20);

            Point[] arrow = new Point[3];
            arrow[0] = new Point(0, -12);
            arrow[1] = new Point(-5, 8);
            arrow[2] = new Point(5, 8);
            e.Graphics.RotateTransform((float)_direction);
            e.Graphics.FillPolygon(Brushes.White, arrow);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {

            e.Graphics.Clear(Color.DarkSlateGray);
            base.OnPaintBackground(e);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }


        // Rounded corners
        private int radius = 5;


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
                bounds.Right, bounds.Bottom, radius, radius));
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
