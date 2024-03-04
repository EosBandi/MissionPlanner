using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GMap.NET.WindowsForms;
using MissionPlanner.Controls;

namespace MissionPlanner.SprayGrid
{
    public class GridPlugin : MissionPlanner.Plugin.Plugin
    {
        public static MissionPlanner.Plugin.PluginHost Host2;

        ToolStripMenuItem but;
        MyButton customButton;

        public override string Name
        {
            get { return "SprayGrid-Soleon"; }
        }

        public override string Version
        {
            get { return "1.0"; }
        }

        public override string Author
        {
            get { return "Andras /EOSBANDI/ Schaffer"; }
        }

        public override bool Init()
        {
            return true;
        }

        public override bool Loaded()
        {
            Host2 = Host;

            but = new ToolStripMenuItem("SprayGrid");
            but.Click += but_Click;

            bool hit = false;
            ToolStripItemCollection col = Host.FPMenuMap.Items;
            int index = col.Count;
            foreach (ToolStripItem item in col)
            {
                if (item.Name.Equals("autoWPToolStripMenuItem"))
                {
                    index = col.IndexOf(item);
                    ((ToolStripMenuItem)item).DropDownItems.Add(but);
                    hit = true;
                    break;
                }
            }

            if (hit == false)
                col.Add(but);
            MainV2.instance.Invoke((Action)
                delegate
                {
                    customButton = Host.MainForm.FlightPlanner.Controls.Find("customButton", true).FirstOrDefault() as MyButton;
                    if (customButton != null)
                    {
                        customButton.Click += but_Click;
                        customButton.Text = "SprayGrid";
                        customButton.Visible = true;
                    }

                 });


            return true;
        }

        void but_Click(object sender, EventArgs e)
        {
            if (Host.FPDrawnPolygon != null && Host.FPDrawnPolygon.Points.Count > 2)
            {
                using (Form SprayGridUI = new SprayGridUI(this))
                {
                    MissionPlanner.Utilities.ThemeManager.ApplyThemeTo(SprayGridUI);
                    SprayGridUI.ShowDialog();
                }
            }
            else
            {
                CustomMessageBox.Show("Please define a polygon.", "Error");
            }
        }

        public override bool Exit()
        {
            return true;
        }
    }
}
