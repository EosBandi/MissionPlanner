using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class myTabControl : TabControl
    {
        public myTabControl()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        public Color tabBGColorTop { get; set; } = Color.Gray;
        public Color tabBGColorBot { get; set; } = Color.SlateGray;
        public Color tabSelectedBGColorTop { get; set; } = Color.LightGreen;
        public Color tabSelectedBGColorBot { get; set; } = Color.YellowGreen;


        protected override void OnPaint(PaintEventArgs e)
        {

            if (!this.DesignMode) e.Graphics.Clear(FindForm().BackColor);

            Pen p = new Pen(Color.White, 4);

            //e.Graphics.DrawRectangle(p, 0, ItemSize.Height + 1, ClientRectangle.Right - 1, ClientRectangle.Height - ItemSize.Height - 2);


            base.OnPaint(e);
            for (int id = 0; id < this.TabCount; id++)
                DrawTabContent(e.Graphics, id);

        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            base.OnPaintBackground(pevent);

            if (!this.DesignMode)
            {
                SolidBrush b = new SolidBrush(Color.Yellow); // FindForm().BackColor);

                for (int id = 0; id < this.TabCount; id++)
                    pevent.Graphics.FillRectangle(b, GetTabRect(id)); ;
            }
        }

        private void DrawTabContent(Graphics graphics, int id)
        {

            Rectangle tabRect = GetTabRect(id);

            Rectangle contentRect = new Rectangle(ClientRectangle.X, ClientRectangle.Y, ItemSize.Width, ItemSize.Height);
            Rectangle textrect = contentRect;

            using (Bitmap bm = new Bitmap(contentRect.Width, contentRect.Height))
            {
                using (Graphics bmGraphics = Graphics.FromImage(bm))
                {

                    GraphicsPath outline = new GraphicsPath();
                    int width = contentRect.Width - 2;
                    int height = contentRect.Height - 4;

                    float wid = contentRect.Height / 3f;

                    wid = 8;

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

                    LinearGradientBrush linear;
                    Brush textColor;

                    if (id == SelectedIndex)
                    {
                        linear = new LinearGradientBrush(contentRect, tabSelectedBGColorTop, tabSelectedBGColorBot, LinearGradientMode.Vertical);
                        textColor = Brushes.Black;
                    }
                    else
                    {
                        linear = new LinearGradientBrush(contentRect, tabBGColorTop, tabBGColorBot, LinearGradientMode.Vertical);
                        textColor = Brushes.White;

                    }
                    bmGraphics.FillPath(linear, outline);
                    bmGraphics.Clip = new Region(outline);

                    if (id == SelectedIndex)
                    {
                        bmGraphics.DrawImage(Properties.Resources.button_shadow_inv, 0, 0, width, height);
                        bmGraphics.DrawPath(Pens.Green, outline);
                    }
                    else
                    {
                        bmGraphics.DrawImage(Properties.Resources.button_shadow, 0, 0, width, height);
                        bmGraphics.DrawPath(Pens.Gray, outline);
                    }

                    using (StringFormat string_format = new StringFormat())
                    {
                        using (Font big_font = new Font(this.Font.FontFamily, this.Font.Size + 1, FontStyle.Bold))
                        {

                            //SizeF extent = bmGraphics.MeasureString(this.TabPages[id].Text, big_font, new PointF(0,0) , string_format);
                            //var midx = extent.Width / 2;
                            //var midy = extent.Height / 2 - 2;

                            //bmGraphics.DrawString(this.TabPages[id].Text, big_font, textColor,
                            //    contentRect.Width / 2f - midx, contentRect.Height / 2f - midy, string_format);

                            string_format.Alignment = StringAlignment.Center;
                            string_format.LineAlignment = StringAlignment.Center;
                            bmGraphics.DrawString(this.TabPages[id].Text, big_font, textColor, contentRect, string_format);

                        }
                    }

                }
                graphics.DrawImage(bm, tabRect.X + 1, tabRect.Y + 2, tabRect.Width - 2, tabRect.Height - 2);

            }
        }

    }
}
