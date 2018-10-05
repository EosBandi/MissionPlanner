using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MissionPlanner.Utilities;
using MissionPlanner.ArduPilot;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using System.Reflection;
using System.ComponentModel;
using System.Drawing;
using MissionPlanner.Controls;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;


namespace MissionPlanner.Ptest
{



    public class PtestPlugin : MissionPlanner.Plugin.Plugin
    {

        SplitContainer sc;
        Panel videoPanel;
        TableLayoutPanel tblMap;
        Label lab;
        Button b;

        Player.ucPlayerControl ucPlayerControl1;

        public override string Name
        {
            get { return "Ptest"; }
        }

        public override string Version
        {
            get { return "0.1"; }
        }

        public override string Author
        {
            get { return "Andras Schaffer"; }
        }

        //[DebuggerHidden]
        public override bool Init()
        {
            loopratehz = 1;

            MainV2.instance.Invoke((Action)
                delegate
                {


                    Program.Splash.MinimizeBox = false;
                    Program.Splash.MaximizeBox = false;
                    Program.Splash.ShowIcon = false;
                    Program.Splash.FormBorderStyle = FormBorderStyle.None;
                

                    sc = Host.MainForm.FlightData.Controls.Find("splitContainer1", true).FirstOrDefault() as SplitContainer;
                    TrackBar tb = Host.MainForm.FlightData.Controls.Find("TRK_zoom", true).FirstOrDefault() as TrackBar;
                    Panel pn1 = Host.MainForm.FlightData.Controls.Find("panel1", true).FirstOrDefault() as Panel;
                    tblMap = Host.MainForm.FlightData.Controls.Find("tableMap", true).FirstOrDefault() as TableLayoutPanel;
                    SplitContainer SubMainLeft = Host.MainForm.FlightData.Controls.Find("SubMainLeft", true).FirstOrDefault() as SplitContainer;
                    HUD hud = SubMainLeft.Panel1.Controls["hud1"] as HUD;
                    hud.skyColor1 = Color.Red;

                    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PtestPlugin));

                    SubMainLeft.Panel2.Controls["tabControlActions"].Visible = false;



                    lab = new System.Windows.Forms.Label();
                    lab.Location = new System.Drawing.Point(66, 15);
                    lab.Text = "Ez itt ?";
                    sc.Panel2.Controls.Add(lab);
                    sc.Panel2.Controls.SetChildIndex(lab, 1);

                    b = new Button();
                    b.Location = new Point(200, 30);
                    //b.Text = "Button";
                    sc.Panel2.Controls.Add(b);
                    sc.Panel2.Controls.SetChildIndex(b, 1);
                    Image i = (Image)(resources.GetObject("logo2"));
                    b.Image = i;
                    b.BackColor = Color.Transparent;
                    b.ForeColor = Color.Transparent;
                    b.Width = 300;
                    b.Height = 60;
                    
            


                    MenuStrip mainmenu = Host.MainForm.MainMenu;
                    //Remove menu items from Simulation (Sim, Terminal, Help, Donate and ArduPilot)
                    mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuHelp"));
                    mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuTerminal"));
                    mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuDonate"));
                    mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuSimulation"));
                    mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuArduPilot"));


                    Host.MainForm.MainMenu.SuspendLayout();
                    Bitmap lg = (Bitmap)(resources.GetObject("logo2"));
                    ToolStripButton mi = new ToolStripButton();

                    mi.Size = lg.Size;
                    mi.Alignment = ToolStripItemAlignment.Right;
                    mi.BackColor = Color.Transparent;
                    mi.DisplayStyle = ToolStripItemDisplayStyle.Image;
                    mi.Image = lg;
                    mi.ForeColor = Color.White;
                    mi.Text = "MenuLOGO";
                    mi.Margin = new Padding(0);
                    mi.Name = "MenuLogo";
                    mi.Overflow = ToolStripItemOverflow.Never;
                    mi.ImageScaling = ToolStripItemImageScaling.None;
                   
              
                    Host.MainForm.MainMenu.Items.Add(mi);

                    Host.MainForm.MainMenu.ResumeLayout(true);


                    Image fd = (Image)(resources.GetObject("rac_flightdata_icon"));
                    mainmenu.Items["menuFlightData"].Image = fd;


                    videoPanel = new System.Windows.Forms.Panel() { Name = "videoPanel" };

                    int x = Host.MainForm.FlightData.Width;
                    int y = Host.MainForm.FlightData.Height;

                    videoPanel.Location = new System.Drawing.Point(x-300-tb.Width-5 ,y-200-33-5);
                    videoPanel.Height = 200;
                    videoPanel.Width = 300;
                    videoPanel.Anchor = (AnchorStyles.Bottom | AnchorStyles.Right);

                    Host.MainForm.FlightData.Controls.Add(videoPanel);
                    Host.MainForm.FlightData.Controls.SetChildIndex(videoPanel, 1);

                    

                    videoPanel.BringToFront();
                    
                    ucPlayerControl1 = new Player.ucPlayerControl();
                    ucPlayerControl1.AutoRecconect = true;
                    ucPlayerControl1.ffmegParams = "";
                    ucPlayerControl1.ffmegPath = "";
                    ucPlayerControl1.Location = new System.Drawing.Point(0, 0);
                    ucPlayerControl1.MediaUrl = "";// rtsp://localhost:8554/";
                    ucPlayerControl1.Name = "ucPlayerControl1";
                    ucPlayerControl1.RecordPath = "";
                    ucPlayerControl1.Size = videoPanel.Size;
                    ucPlayerControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                               | System.Windows.Forms.AnchorStyles.Left)
                                               | System.Windows.Forms.AnchorStyles.Right)));
                    ucPlayerControl1.TabIndex = 0;
                    ucPlayerControl1.VideoRate = Player.ucPlayerControl.ratelist.OriginalRate;
                    ucPlayerControl1.VisiblePlayerMenu = false;
                    ucPlayerControl1.VisibleStatus = false;
                    ucPlayerControl1.Controls["panel1"].Controls["streamPlayerControl1"].DoubleClick += new EventHandler(men_Click);


                    videoPanel.Controls.Add(ucPlayerControl1);

                    ucPlayerControl1.BringToFront();

                    System.Windows.Forms.ToolStripMenuItem men = new System.Windows.Forms.ToolStripMenuItem() { Text = "VideoSwitch" };
                    men.Click += men_Click;
                    Host.FDMenuMap.Items.Add(men);




                });



     

            return true;
        }

        void men_Click(object sender, EventArgs e)
        {

            SplitContainer sc = Host.MainForm.FlightData.Controls.Find("splitContainer1", true).FirstOrDefault() as SplitContainer;

            if (videoPanel.Controls.Contains(ucPlayerControl1))
            {
                videoPanel.Controls.Add(Host.FDGMapControl);
                sc.Panel2.Controls.Add(ucPlayerControl1);
                ucPlayerControl1.Size = sc.Panel2.Size;
            }
            else
            {
                videoPanel.Controls.Add(ucPlayerControl1);
                sc.Panel2.Controls.Add(Host.FDGMapControl);
                ucPlayerControl1.Size = videoPanel.Size;
            }
        }

        public override bool Loaded()
        {
            return true;
        }
        public override bool Loop()
        {
            b.BackColor = Color.Transparent;
            b.ForeColor = Color.Transparent;

            MainV2.instance.Invoke((Action)
           delegate
           {

               if (Host.cs.connected)
               {
                   lab.Text = "connected";
                   ucPlayerControl1.Play();

               }
               else
               {
                   lab.Text = "Disconnected";
                   ucPlayerControl1.Stop();
               }

           });
           
            return true;
        }
        public override bool Exit()
        {
            return true;
        }
    }
}
