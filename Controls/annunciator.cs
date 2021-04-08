using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace MissionPlanner.Controls
{
    public enum Stat { NOMINAL, WARNING, ALERT, DISABLED, NOTEXIST }


    //[PreventTheming]
    public partial class annunciator : UserControl
    {
        private List<panelItem> panelItems = new List<panelItem>();

        public event EventHandler buttonClicked;
        public event EventHandler undock;

        private string _clickedButtonName;

        public string clickedButtonName
        {
            get { return _clickedButtonName; }

        }

        private bool _blinkStat = true;

        //Add 500ms timer
        private System.Timers.Timer timer1 = new System.Timers.Timer(500);

        public annunciator()
        {
            InitializeComponent();
        }

        public annunciator(int btnCount, Size btnSize)
        {
            InitializeComponent();

            for (int i = 0; i < btnCount; i++)
            {
                panelItem item = new panelItem();

                item.btn.Size = btnSize;
                item.btn.Name = "button" + i.ToString();
                item.btn.Location = new Point(i * (btnSize.Width), 0);
                item.btn.Margin = new Padding(1, 1, 1, 1);
                item.btn.Click += new System.EventHandler(this.panel_Click);
                item.btn.FlatAppearance.BorderSize = 1;
                item.btn.FlatAppearance.BorderColor = Color.Black;
                item.btn.FlatStyle = FlatStyle.Flat;
                item.btn.Font = new System.Drawing.Font("Arial Narrow", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                item.btn.buttonName = "EMPTY";
                item.btn.ContextMenu = contextMenu1;
                item.btn.BackColor = Color.Black;
                item.Status = Stat.ALERT;

                item.name = "EMPTY";
                item._disabled = true;

                layoutPanel.Controls.Add(item.btn);
                panelItems.Add(item);
            }

            timer1.Interval = 500;
            timer1.Elapsed += Timer_Tick;
            timer1.Enabled = true;

            if (!this.DesignMode)
            {
                timer1.Enabled = true;
            }
        }

        public void setPanels(string[] panelNames, string[] panelLabels)
        {

            if (panelLabels.Count() != panelNames.Count()) return;
            if (panelLabels.Count() == 0) return;

            if (panelLabels.Count() > panelItems.Count) Array.Resize(ref panelLabels, panelItems.Count);

            int index = 0;
            foreach (panelItem i in panelItems)
            {
                i.name = panelNames[index];
                i.btn.Text = panelLabels[index++];
                i.btn.BackColor = this.BackColor;
            }

        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _blinkStat = !_blinkStat;

            foreach (panelItem i in panelItems)
            {
                i.btn.blink(_blinkStat);
            }
            this.Invalidate();
        }

        public Stat getStatus(string panelName)
        {

            panelName = panelName.ToUpper();
            panelItem p = panelItems.Find(x => x.name == panelName);

            if (p is null)
            {
                throw new ArgumentException("Cannot find panel name ", panelName);
            }

            else
            {
                return p.Status;
            }
        }


        public void setStatus(string panelName, Stat c)
        {
            panelItem p = panelItems.Find(x => x.name == panelName);
            if (p != null)
            {
                p.setStatus(c);
            }
        }

        private void panel_Click(object sender, EventArgs e)
        {

            var b = (panelButton)sender;

            //find the panel by the button
            panelItem p = panelItems.Find(x => x.name == b.buttonName);

            if ((p.Status == Stat.DISABLED) && (b.BackColor == Color.DarkSlateGray)) return;


            if (b.Selected)
            {
                p.deselect();
            }
            else
            {
                _clickedButtonName = p.name;

                foreach (panelItem i in panelItems)
                    i.deselect();

                p.select();
            }


            EventHandler handler = this.buttonClicked;
            if (handler != null)
            {
                handler(this, e);
            }

        }

        private void annunciator_EnabledChanged(object sender, EventArgs e)
        {
            if (this.Enabled)
            {
                foreach (panelItem i in panelItems)
                {
                    i.enable();
                }
            }
            else
            {
                foreach (panelItem i in panelItems)
                {
                    i.disable();
                }
            }

        }

        private void menuUndockDock_Click(object sender, EventArgs e)
        {
            EventHandler handler = this.undock;
            if (handler != null)
            {
                handler(this, e);
            }
        }


    }

    public class panelItem : IEquatable<panelItem>
    {

        public panelButton btn { get; set; }

        private string _name;

        public string name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                btn.buttonName = _name;
            }
        }

        private Stat _status;

        public Stat Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                btn.Status = _status;
            }
        }

        public bool _disabled { get; set; }

        public panelItem()
        {
            btn = new panelButton();
        }


        public override string ToString()
        {
            return name;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            panelItem objAsPart = obj as panelItem;
            if (objAsPart == null) return false;
            else return Equals(objAsPart);
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }
        public bool Equals(panelItem other)
        {
            if (other == null) return false;
            return (this.name.Equals(other.name));
        }


        public void disable()
        {
            this.btn.Status = Stat.DISABLED;
            this._disabled = true;
            this.btn.Invalidate();

        }

        public void enable()
        {
            this.btn.Status = this.Status;
            this._disabled = false;
            this.btn.Invalidate();

        }

        public void select()
        {
            this.btn.Selected = true;
            this.btn.Invalidate();
        }

        public void deselect()
        {
            this.btn.Selected = false;
            this.btn.Invalidate();
        }

        public void setStatus(Stat s)
        {
            this.btn.Status = s;
            Status = s;
        }

    }


    public class panelButton : Button
    {
        bool _mousedown = false;

        internal Color _BGGradTop;
        internal Color _BGGradBot;
        internal Color _TextColor;
        internal Color _Outline;
        internal Color _ColorNotEnabled;
        internal Color _ColorMouseDown;

        internal Stat _status;
        internal bool _selected;

        public string buttonName { get; set; }

        //Testing rounded corners
        private int radius = 10;
        [DefaultValue(10)]
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
        //End - Counded corners.


        public Stat Status
        {
            get { return _status; }
            set
            {
                _status = value;
                this.setStatus(_status);

            }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
            }
        }

        bool inOnPaint = false;

        public Color BGGradTop { get { return _BGGradTop; } set { _BGGradTop = value; this.Invalidate(); } }
        public Color BGGradBot { get { return _BGGradBot; } set { _BGGradBot = value; this.Invalidate(); } }
        public Color ColorNotEnabled { get { return _ColorNotEnabled; } set { _ColorNotEnabled = value; this.Invalidate(); } }
        public Color ColorMouseDown { get { return _ColorMouseDown; } set { _ColorMouseDown = value; this.Invalidate(); } }

        // i want to ignore forecolor
        public Color TextColor { get { return _TextColor; } set { _TextColor = value; this.Invalidate(); } }
        public Color Outline { get { return _Outline; } set { _Outline = value; this.Invalidate(); } }

        protected override Size DefaultSize => base.DefaultSize;

        public panelButton()
        {

            _ColorMouseDown = Color.FromArgb(150, 0x2b, 0x3a, 0x03);


            Status = Stat.NOMINAL;
            Selected = false;

        }


        private void setStatus(Stat status)
        {
            switch (status)
            {
                case Stat.NOMINAL:
                    _BGGradTop = Color.LightGreen;
                    _BGGradBot = Color.YellowGreen;
                    _TextColor = Color.Black;
                    _Outline = Color.ForestGreen;
                    break;
                case Stat.WARNING:
                    _BGGradTop = Color.Yellow;
                    _BGGradBot = Color.Gold;
                    _TextColor = Color.Black;
                    _Outline = Color.Goldenrod;
                    break;
                case Stat.ALERT:
                    _BGGradTop = Color.Red;
                    _BGGradBot = Color.DarkRed;
                    _TextColor = Color.White;
                    _Outline = Color.Maroon;
                    break;
                case Stat.DISABLED:
                    _BGGradTop = Color.Gray;
                    _BGGradBot = Color.DarkSlateGray;
                    _TextColor = Color.Black;
                    _Outline = Color.Black;
                    break;
                case Stat.NOTEXIST:
                    break;
                default:
                    break;
            }

            this.Invalidate();
        }


        public void blink(bool b)
        {
            if (b)
            {
                switch (Status)
                {
                    case Stat.WARNING:
                        _BGGradTop = Color.Khaki;
                        _BGGradBot = Color.Yellow;
                        _TextColor = Color.Black;
                        _Outline = Color.Goldenrod;
                        break;
                    case Stat.ALERT:
                        _BGGradTop = Color.Tomato;
                        _BGGradBot = Color.Red;
                        _TextColor = Color.White;
                        _Outline = Color.Maroon;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (Status)
                {
                    case Stat.WARNING:
                        _BGGradTop = Color.Yellow;
                        _BGGradBot = Color.Gold;
                        _TextColor = Color.Black;
                        _Outline = Color.Goldenrod;
                        break;
                    case Stat.ALERT:
                        _BGGradTop = Color.Red;
                        _BGGradBot = Color.DarkRed;
                        _TextColor = Color.White;
                        _Outline = Color.Maroon;
                        break;
                    default:
                        break;
                }
            }

            this.Invalidate();

        }


        protected override void OnPaint(PaintEventArgs pevent)
        {
            //base.OnPaint(pevent);

            if (inOnPaint)
                return;

            inOnPaint = true;

            try
            {
                Graphics gr = pevent.Graphics;

                gr.Clear(Color.Black);
                gr.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle outside = new Rectangle(0, 0, this.Width, this.Height);

                LinearGradientBrush linear = new LinearGradientBrush(outside, BGGradTop, BGGradBot, LinearGradientMode.Vertical);

                Pen mypen;

                if (_selected)
                {
                    mypen = new Pen(Color.White, 2);
                }
                else
                {
                    mypen = new Pen(Outline, 1);
                }

                GraphicsPath outline = new GraphicsPath();

                float wid = this.Height / 3f;

                wid = 12;

                int width = this.Width - 1;
                int height = this.Height - 1;

                // tl
                outline.AddArc(0, 0, wid, wid, 180, 90);
                // top line
                outline.AddLine(wid, 0, width - wid, 0);
                // tr
                outline.AddArc(width - wid, 0, wid, wid, 270, 90);
                // br
                outline.AddArc(width - wid, height - wid, wid, wid, 0, 90);
                // bottom line
                outline.AddLine(wid, height, width - wid, height);
                // bl
                outline.AddArc(0, height - wid, wid, wid, 90, 90);
                // left line
                outline.AddLine(0, height - wid, 0, wid - wid / 2);


                gr.FillPath(linear, outline);


                if (_selected)
                {
                    gr.DrawImage(Properties.Resources.button_shadow_inv, 0, 0, this.Width, this.Height);
                    gr.DrawPath(mypen, outline);
                }
                else
                {
                    gr.DrawImage(Properties.Resources.button_shadow, 0, 0, this.Width, this.Height);

                }

                SolidBrush mybrush = new SolidBrush(TextColor);




                //if (_mouseover)
                //{
                //    SolidBrush brush = new SolidBrush(ColorMouseOver);

                //    gr.FillPath(brush, outline);
                //}
                if (_mousedown)
                {
                    SolidBrush brush = new SolidBrush(ColorMouseDown);

                    gr.FillPath(brush, outline);
                }

                if (!this.Enabled)
                {
                    SolidBrush brush = new SolidBrush(_ColorNotEnabled);

                    gr.FillPath(brush, outline);
                }


                StringFormat stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Center;
                stringFormat.LineAlignment = StringAlignment.Center;

                string display = this.Text;

                int amppos = display.IndexOf('&');
                if (amppos != -1)
                    display = display.Remove(amppos, 1);

                gr.DrawString(display, this.Font, mybrush, outside, stringFormat);
            }
            catch { }

            inOnPaint = false;
        }

        protected override void OnClick(EventArgs e)
        {
            if (_status != Stat.DISABLED)
                base.OnClick(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            //base.OnPaintBackground(pevent);
        }

        //protected override void OnMouseEnter(EventArgs e)
        //{
        //    _mouseover = true;
        //    base.OnMouseEnter(e);
        //}

        //protected override void OnMouseLeave(EventArgs e)
        //{
        //    _mouseover = false;
        //    base.OnMouseLeave(e);
        //}

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _mousedown = true;
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _mousedown = false;
            base.OnMouseUp(mevent);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }
    }

}