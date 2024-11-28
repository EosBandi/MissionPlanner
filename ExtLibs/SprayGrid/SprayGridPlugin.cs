using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DotSpatial.Data.Properties;
using GMap.NET;
using GMap.NET.WindowsForms;
using MissionPlanner.Controls;
using MissionPlanner.Maps;

namespace MissionPlanner.SprayGrid
{
    public class GridPlugin : MissionPlanner.Plugin.Plugin
    {
        public static MissionPlanner.Plugin.PluginHost Host2;

        ToolStripMenuItem but;
        ToolStripMenuItem but2;
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

            but2 = new ToolStripMenuItem("Refresh map tiles");
            but2.Click += but2_Click;

            ToolStripItemCollection col2 = Host.FPMenuMap.Items;
            index = col2.Count;
            foreach (ToolStripItem item in col2)
            {
                if (item.Name.Equals("mapToolToolStripMenuItem"))
                {
                    index = col2.IndexOf(item);
                    ((ToolStripMenuItem)item).DropDownItems.Add(but2);
                    hit = true;
                    break;
                }
            }

            if (hit == false)
                col.Add(but);


            if (MissionPlanner.Utilities.Settings.Instance.GetBoolean("ShowSprayGridButton", true) == true)
            {

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

            }
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

        void but2_Click(object sender, EventArgs e)
        {

           GMapControl mapcontrol = Host2.FPGMapControl;
           var removed = ((PureImageCache)MyImageCache.Instance).DeleteOlderThan(DateTime.Now, mapcontrol.MapProvider.DbId);
           CustomMessageBox.Show("Removed " + removed + " images");
       }

        public override bool Exit()
        {
            return true;
        }
    }
}
