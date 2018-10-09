using System;
using System.Windows;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
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
using racPlayerControl;


namespace MissionPlanner.Elistair
{

    public class EliPlugin : MissionPlanner.Plugin.Plugin
    {


        private WebClient browser = new WebClient();
        private Uri _elistairUrl = new Uri("http://192.168.4.1/");
        //private String _videoUrl = new string("udp://224.10.10.10.:15004");
        private ElistairClass eli = new ElistairClass();
        private bool _eli_connected = false;
        private int _eli_wait_cycles = 0;

        private int _target_altitude = 15;


        SplitContainer sc;
        Label lab;
     
        MenuStrip mainmenu;
        SplitContainer SubMainLeft;
        SplitContainer MainH;
        TableLayoutPanel tblMap;
        HUD hud;

        racPlayerControl.racPlayerControl ucPlayerControl1;

        int a = 0;

        private Queue<String> messageQueue;

        private System.Windows.Forms.Panel EliStatPanel;
        private System.Windows.Forms.GroupBox groupBoxWinch;
        private System.Windows.Forms.GroupBox groupBoxButton;
        private System.Windows.Forms.Label lTorque;
        private System.Windows.Forms.Label lCOut;
        private System.Windows.Forms.Label lCSpeed;
        private System.Windows.Forms.Label lPower;
        private System.Windows.Forms.Label lTemp;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox btnDoLand;
        private System.Windows.Forms.PictureBox btnDoTakeoff;
        private System.Windows.Forms.PictureBox btnAltPlus;
        private System.Windows.Forms.PictureBox btnAltMinus;
        private System.Windows.Forms.Label lSafetyBatt;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lTargetAlt;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button bDoChangeAlt;
        private System.Windows.Forms.Label lMessage;

        public override string Name
        {
            get { return "Elistair Control Panel"; }
        }

        public override string Version
        {
            get { return "1.0"; }
        }

        public override string Author
        {
            get { return "Rotors and Cams"; }
        }

        //[DebuggerHidden]
        public override bool Init()
        {
            loopratehz = 1;

            //Init message queue
            messageQueue = new Queue<string>();


            //Use resources
            ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EliPlugin));
            
            // Change splash screen at plugin load

            Bitmap splash_logo = (Bitmap)(resources.GetObject("EasyPlanner_splash"));
            Program.Splash.BackgroundImage = splash_logo;
            Application.DoEvents();
            Program.Splash.MinimizeBox = false;
            Program.Splash.MaximizeBox = false;
            Program.Splash.ShowIcon = false;
            Program.Splash.FormBorderStyle = FormBorderStyle.None;
            Program.Splash.Controls["TXT_version"].Visible = false;
            Program.Splash.Controls["label1"].Visible = false;
            Program.Splash.Controls["pictureBox1"].Visible = false;

            //Remove unneccessary menu items from the bar 
            mainmenu = Host.MainForm.MainMenu;
            mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuHelp"));
            mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuTerminal"));
            mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuDonate"));
            mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuSimulation"));
            mainmenu.Items.RemoveAt(mainmenu.Items.IndexOfKey("MenuArduPilot"));
            ToolStripMenuItem a = mainmenu.ContextMenuStrip.Items["autoHideToolStripMenuItem"] as ToolStripMenuItem;
            a.PerformClick();
            //a.Checked = true;
            

            //Add Rotors logo to the menubar
            Bitmap lg = (Bitmap)(resources.GetObject("logo2"));
            ToolStripButton mi = new ToolStripButton();
            mi.Size = lg.Size;
            mi.Alignment = ToolStripItemAlignment.Right;
            mi.BackColor = Color.Transparent;
            mi.DisplayStyle = ToolStripItemDisplayStyle.Image;
            mi.Image = lg;
            mi.Text = "MenuLOGO";
            mi.Margin = new Padding(0);
            mi.Name = "MenuLOGO";
            mi.Overflow = ToolStripItemOverflow.Never;
            mi.ImageScaling = ToolStripItemImageScaling.None;
            Host.MainForm.MainMenu.Items.Add(mi);

            // hide map and tabControlActions
            SubMainLeft = Host.MainForm.FlightData.Controls.Find("SubMainLeft", true).FirstOrDefault() as SplitContainer;
            MainH = Host.MainForm.FlightData.Controls.Find("MAINH", true).FirstOrDefault() as SplitContainer;
            tblMap = Host.MainForm.FlightData.Controls.Find("tableMap", true).FirstOrDefault() as TableLayoutPanel;

            SubMainLeft.Panel2.Controls["tabControlActions"].Visible = false;
            tblMap.Visible = false;

            // Customize HUD
            hud = SubMainLeft.Panel1.Controls["hud1"] as HUD;
            hud.displayAOASSA = false;
            hud.displayxtrack = false;
            hud.displayspeed = false;
            hud.displayconninfo = false;

            hud.ContextMenuStrip.Items["swapWithMapToolStripMenuItem"].Enabled = false;
            hud.ContextMenuStrip.Items["videoToolStripMenuItem"].Enabled = false;


            this.lMessage = new System.Windows.Forms.Label();
            this.lMessage.Anchor =  ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top)
                                       | System.Windows.Forms.AnchorStyles.Left)
                                       | System.Windows.Forms.AnchorStyles.Right)));

            this.lMessage.AutoSize = false;
            this.lMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lMessage.Location = new System.Drawing.Point(0, 0);
            this.lMessage.Name = "lMessage";
            this.lMessage.Size = new System.Drawing.Size(MainH.Panel2.Size.Width, 40);
            this.lMessage.Text = "";
            this.lMessage.TextAlign = ContentAlignment.MiddleCenter;
            this.lMessage.ForeColor = Color.DarkOrange;
            MainH.Panel2.Controls.Add(lMessage);

            

            //Create and Add StreamPlayerControl to the place of tblMap
            ucPlayerControl1 = new racPlayerControl.racPlayerControl();
            ucPlayerControl1.AutoRecconect = true;
            ucPlayerControl1.ffmegParams = "";
            ucPlayerControl1.ffmegPath = "";
            ucPlayerControl1.Location = new System.Drawing.Point(0, 40);
            //ucPlayerControl1.MediaUrl = "rtsp://192.168.0.33:554/user=admin&password=titok&channel=&stream=.sdp";// rtsp://localhost:8554/";
            ucPlayerControl1.MediaUrl = "udp://224.10.10.10.:15004";
            ucPlayerControl1.Name = "ucPlayerControl1";
            ucPlayerControl1.RecordPath = "";
            ucPlayerControl1.Size = new System.Drawing.Size(MainH.Panel2.Size.Width, MainH.Panel2.Size.Height - 40);
            ucPlayerControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                       | System.Windows.Forms.AnchorStyles.Left)
                                       | System.Windows.Forms.AnchorStyles.Right)));
            ucPlayerControl1.TabIndex = 0;
            ucPlayerControl1.VideoRate = racPlayerControl.racPlayerControl.ratelist.WideScreen;
            ucPlayerControl1.VisiblePlayerMenu = true;
            ucPlayerControl1.VisibleStatus = true;
            MainH.Panel2.Controls.Add(ucPlayerControl1);
            ucPlayerControl1.Play();

            this.AddControls();
            SubMainLeft.Panel2.Controls.Add(EliStatPanel);
            EliStatPanel.Width = SubMainLeft.Panel2.Width;

            Image imageLand = (Image)(resources.GetObject("landing_512"));
            this.btnDoLand.BackgroundImage = imageLand;
            this.btnDoLand.BackgroundImageLayout = ImageLayout.Stretch;

            Image imageTakeoff = (Image)(resources.GetObject("start_512"));
            this.btnDoTakeoff.BackgroundImage = imageTakeoff;
            this.btnDoTakeoff.BackgroundImageLayout = ImageLayout.Stretch;

            Image imageAltPlus = (Image)(resources.GetObject("plus"));
            this.btnAltPlus.BackgroundImage = imageAltPlus;
            this.btnAltPlus.BackgroundImageLayout = ImageLayout.Stretch;

            Image imageAltMinus = (Image)(resources.GetObject("minus"));
            this.btnAltMinus.BackgroundImage = imageAltMinus;
            this.btnAltMinus.BackgroundImageLayout = ImageLayout.Stretch;
            // this.btnDoTakeoff.Location = new Point(229, EliStatPanel.Height - 100);


            MainV2.instance.Invoke((Action)
                delegate
                {

                    sc = Host.MainForm.FlightData.Controls.Find("splitContainer1", true).FirstOrDefault() as SplitContainer;
                    TrackBar tb = Host.MainForm.FlightData.Controls.Find("TRK_zoom", true).FirstOrDefault() as TrackBar;
                    Panel pn1 = Host.MainForm.FlightData.Controls.Find("panel1", true).FirstOrDefault() as Panel;

                    //System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PtestPlugin));



                    lab = new System.Windows.Forms.Label();
                    lab.Location = new System.Drawing.Point(66, 15);
                    lab.Text = "Ez itt ?";
                    sc.Panel2.Controls.Add(lab);
                    sc.Panel2.Controls.SetChildIndex(lab, 1);

                    Image fd = (Image)(resources.GetObject("rac_flightdata_icon"));
                    mainmenu.Items["menuFlightData"].Image = fd;

                    /*
                    videoPanel = new System.Windows.Forms.Panel() { Name = "videoPanel" };

                    int x = Host.MainForm.FlightData.Width;
                    int y = Host.MainForm.FlightData.Height;

                    videoPanel.Location = new System.Drawing.Point(x-300-tb.Width-5 ,y-200-33-5);
                    videoPanel.Height = 200;
                    videoPanel.Width = 300;
                    videoPanel.Anchor = (AnchorStyles.Bottom | AnchorStyles.Right);

                    Host.MainForm.FlightData.Controls.Add(videoPanel);
                    Host.MainForm.FlightData.Controls.SetChildIndex(videoPanel, 1);
                    */
                    
                    //System.Windows.Forms.ToolStripMenuItem men = new System.Windows.Forms.ToolStripMenuItem() { Text = "VideoSwitch" };
                    //men.Click += men_Click;
                    //Host.FDMenuMap.Items.Add(men);

                });



     

            return true;
        }

        public override bool Loaded()
        {
            browser.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadStringCompleted);
            return true;
        }
        public override bool Loop()
        {


 
            //Check if browser is busy
            if (!browser.IsBusy)
            {
                // not busy, lets start a download
                browser.DownloadStringAsync(_elistairUrl);
                // and clear wait cycles
                _eli_wait_cycles = 0;
            } else
            {
                // browser is busy, if it was busy for more than 2 cycles then cancel current download
                if (_eli_wait_cycles<2) { _eli_wait_cycles++; }
                else
                {
                    browser.CancelAsync();
                    _eli_connected = false;

                }
            }
   


            MainV2.instance.Invoke((Action)
              delegate
              {

                  

                  this.btnAltPlus.Location = new System.Drawing.Point(btnDoLand.Location.X, EliStatPanel.Height / 2 - 100);
                  this.btnAltMinus.Location = new System.Drawing.Point(btnDoLand.Location.X, EliStatPanel.Height / 2 + 20);
                  //Host.MainForm.Menu
                  // Update winch data
                  lTemp.Text = eli.Temperature.ToString();
                  lPower.Text = eli.Power.ToString();
                  lCSpeed.Text = eli.CableSpeed.ToString();
                  lCOut.Text = eli.CableLength.ToString();
                  lTorque.Text = eli.Torque.ToString();
                  // check safety boundaries and color winch data accordingly
                  // 
                  //Temp 
                  // 
                  if (eli.Temperature < 40) lTemp.BackColor = Color.Transparent;
                  else if (eli.Temperature >= 40 && eli.Temperature < 45) lTemp.BackColor = Color.Orange;
                  else
                  { 
                      lTemp.BackColor = Color.OrangeRed;
                      messageQueue.Enqueue("Winch overtemp! Please land ASAP!");
                  }
                  //Power
                  // 
                  if (eli.Power < 1400) lPower.BackColor = Color.Transparent;
                  else if (eli.Power >= 1400 && eli.Power <= 1800) lPower.BackColor = Color.Orange;
                  else
                  {
                      lPower.BackColor = Color.OrangeRed;
                      messageQueue.Enqueue("Tether power overload! Please land ASAP!");
                  }


                  if (Host.cs.battery_voltage2 > 47.2) lSafetyBatt.BackColor = Color.Transparent;
                  else if (Host.cs.battery_voltage2 > 46.8) lSafetyBatt.BackColor = Color.Orange;
                  else lSafetyBatt.BackColor = Color.Red;



                  lSafetyBatt.Text = Math.Round(Host.cs.battery_voltage2,2).ToString("F2") + " V";

                  if (Host.cs.mode.ToUpper() != "LAND")
                  {
                      if (Host.cs.battery_voltage2 <= 46.8 )
                      {
                          messageQueue.Enqueue("Safety Battery lov voltage! Please Land ASAP.");

                      }

                      if ((Math.Abs(Host.cs.roll) > 15) || (Math.Abs(Host.cs.pitch) > 15))
                      {
                          messageQueue.Enqueue("Strong wind detected, please consider landing!");
                      }
                  }

                  if (!_eli_connected)
                  {
                      groupBoxWinch.Text = "Winch - disconnected";
                      groupBoxWinch.ForeColor = Color.Red;
                  }
                  else
                  {
                      groupBoxWinch.Text = "Winch - connected";
                      groupBoxWinch.ForeColor = Color.Green;
                  }


                  if ( ((Host.cs.mode.ToUpper() == "GUIDED") || (Host.cs.mode.ToUpper() == "LOITER") && (Host.cs.armed) && (Host.cs.alt > 14)) )
                  {
                      btnAltMinus.Enabled = true;
                      btnAltPlus.Enabled = true;
                      lTargetAlt.Text = _target_altitude.ToString() + " m";
                  }
                  else
                  {
                      btnAltMinus.Enabled = false;
                      btnAltPlus.Enabled = false;
                      lTargetAlt.Text = "---";
                      _target_altitude = 15;

                  }


                  if (messageQueue.Count >= 1)
                  {
                      lMessage.ForeColor = Color.Red;
                      lMessage.Text = messageQueue.Dequeue();
                  }
                  

              });


            return true;

        }
        public override bool Exit()
        {
            if (browser.IsBusy)
            {
                browser.CancelAsync();
            }
            return true;
        }
        private void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            { eli.Message = "";
                _eli_connected = false;
                return;
            }
            eli.Message = e.Result;
            _eli_connected = true;
        }

        private void btnAltPlus_Click(object sender, EventArgs e)
        {
            if (_target_altitude < 80)
            {
                _target_altitude += 5;
                lTargetAlt.Text = _target_altitude.ToString() + " m";
                bDoChangeAlt.Enabled = true;
            }
        }

        private void btnAltMinus_Click(object sender, EventArgs e)
        {
            if (_target_altitude > 20)
            {
                _target_altitude -= 5;
                lTargetAlt.Text = _target_altitude.ToString() + " m";
                bDoChangeAlt.Enabled = true;
            }
        }

        private void btnDoChangeAlt_Click(object sender, EventArgs e)
        {
            Locationwp loc = new Locationwp();
            loc.lat = Host.cs.HomeLocation.Lat;
            loc.lng = Host.cs.HomeLocation.Lng;
            loc.alt = _target_altitude;
            Host.comPort.setGuidedModeWP(loc,true);
            bDoChangeAlt.Enabled = false;

        }
        private void btnDoLand_Click(object sender, EventArgs e)
        {
            DoLand();
        }
        private void btnDoTakeoff_Click(object sender, EventArgs e)
        {
            DoTakeOff();
        }

        private void DoTakeOff()
        {
            //Do takeoff only if 
            if (   !Host.cs.armed 
                && (Host.cs.battery_voltage2 > 46.8)
                && (Host.cs.battery_voltage > Host.cs.battery_voltage2) )
            {
                Host.comPort.setMode("Loiter");

                if (Host.comPort.doARM(true))
                {
                    Host.comPort.setMode("GUIDED");
                    Host.comPort.doCommand(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 15);
                }

                messageQueue.Enqueue("Takeoff is in progress!");
            }
            else
            {
                //Takeoff failed add neccessary message
            }
        }

        private void DoLand()
        {
            if (Host.cs.armed && (Host.cs.alt > 0))
            {
                Host.comPort.setMode("LAND");
            }
            messageQueue.Enqueue("Landing is in progress! You can abort via manual control only.");
        }

        private bool AddControls()
        {

            this.EliStatPanel = new System.Windows.Forms.Panel();
            this.groupBoxWinch = new System.Windows.Forms.GroupBox();
            this.groupBoxButton = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lTemp = new System.Windows.Forms.Label();
            this.lPower = new System.Windows.Forms.Label();
            this.lCSpeed = new System.Windows.Forms.Label();
            this.lCOut = new System.Windows.Forms.Label();
            this.lTorque = new System.Windows.Forms.Label();
            this.btnDoLand = new System.Windows.Forms.PictureBox();
            this.btnDoTakeoff = new System.Windows.Forms.PictureBox();
            this.btnAltPlus = new System.Windows.Forms.PictureBox();
            this.btnAltMinus = new System.Windows.Forms.PictureBox();
            this.label6 = new System.Windows.Forms.Label();
            this.lSafetyBatt = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.lTargetAlt = new System.Windows.Forms.Label();
            this.bDoChangeAlt = new System.Windows.Forms.Button();
            // 
            // EliStatPanel
            // 
            this.EliStatPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.EliStatPanel.Controls.Add(this.groupBoxWinch);
            this.EliStatPanel.Controls.Add(this.btnDoLand);
            this.EliStatPanel.Controls.Add(this.btnDoTakeoff);
            this.EliStatPanel.Controls.Add(this.btnAltMinus);
            this.EliStatPanel.Controls.Add(this.btnAltPlus);
            this.EliStatPanel.Controls.Add(this.lSafetyBatt);
            this.EliStatPanel.Controls.Add(this.label6);
            this.EliStatPanel.Controls.Add(this.lTargetAlt);
            this.EliStatPanel.Controls.Add(this.label7);
            this.EliStatPanel.Controls.Add(this.groupBoxButton);
            this.EliStatPanel.Controls.Add(this.bDoChangeAlt);
            this.EliStatPanel.Location = new System.Drawing.Point(0, 0);
            this.EliStatPanel.Name = "EliStatPanel";
            this.EliStatPanel.Size = new System.Drawing.Size(340, SubMainLeft.Panel2.Height-2);
            this.EliStatPanel.TabIndex = 0;
            // 
            // groupBoxWinch
            // 
            this.groupBoxWinch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top)
            | System.Windows.Forms.AnchorStyles.Left))));
            this.groupBoxWinch.Controls.Add(this.lTorque);
            this.groupBoxWinch.Controls.Add(this.lCOut);
            this.groupBoxWinch.Controls.Add(this.lCSpeed);
            this.groupBoxWinch.Controls.Add(this.lPower);
            this.groupBoxWinch.Controls.Add(this.lTemp);
            this.groupBoxWinch.Controls.Add(this.label5);
            this.groupBoxWinch.Controls.Add(this.label4);
            this.groupBoxWinch.Controls.Add(this.label3);
            this.groupBoxWinch.Controls.Add(this.label2);
            this.groupBoxWinch.Controls.Add(this.label1);

            this.groupBoxWinch.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBoxWinch.Location = new System.Drawing.Point(2, 0);
            this.groupBoxWinch.Name = "groupBoxWinch";
            this.groupBoxWinch.Size = new System.Drawing.Size(220, 163);
            this.groupBoxWinch.AutoSize = true;
            this.groupBoxWinch.TabIndex = 0;
            this.groupBoxWinch.TabStop = false;
            this.groupBoxWinch.Text = "Winch";

            this.groupBoxButton.Anchor = (System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right);
            this.groupBoxButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBoxButton.Location = new System.Drawing.Point(EliStatPanel.Width - 122, 1);
            this.groupBoxButton.Name = "groupBoxButton";
            this.groupBoxButton.Size = new System.Drawing.Size(110, EliStatPanel.Height-2);
            this.groupBoxButton.AutoSize = true;
            this.groupBoxButton.TabIndex = 0;
            this.groupBoxButton.TabStop = false;
            this.groupBoxButton.Text = "";

            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(18, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "Temp C`";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(18, 51);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 20);
            this.label2.TabIndex = 1;
            this.label2.Text = "Power W";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(18, 72);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(112, 20);
            this.label3.TabIndex = 2;
            this.label3.Text = "Cable Speed";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(18, 93);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(86, 20);
            this.label4.TabIndex = 3;
            this.label4.Text = "Cable out";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(18, 114);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(65, 20);
            this.label5.TabIndex = 4;
            this.label5.Text = "Torque";
            // 
            // lTemp
            // 
            this.lTemp.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.lTemp.AutoSize = true;
            this.lTemp.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lTemp.Location = new System.Drawing.Point(154, 32);
            this.lTemp.Name = "lTemp";
            this.lTemp.Size = new System.Drawing.Size(19, 20);
            this.lTemp.TabIndex = 5;
            this.lTemp.Text = "0";
            // 
            // lPower
            // 
            this.lPower.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.lPower.AutoSize = true;
            this.lPower.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lPower.Location = new System.Drawing.Point(154, 51);
            this.lPower.Name = "lPower";
            this.lPower.Size = new System.Drawing.Size(19, 20);
            this.lPower.TabIndex = 6;
            this.lPower.Text = "0";
            // 
            // lCSpeed
            // 
            this.lCSpeed.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.lCSpeed.AutoSize = true;
            this.lCSpeed.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lCSpeed.Location = new System.Drawing.Point(154, 72);
            this.lCSpeed.Name = "lCSpeed";
            this.lCSpeed.Size = new System.Drawing.Size(19, 20);
            this.lCSpeed.TabIndex = 7;
            this.lCSpeed.Text = "0";
            // 
            // lCOut
            // 
            this.lCOut.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.lCOut.AutoSize = true;
            this.lCOut.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lCOut.Location = new System.Drawing.Point(154, 93);
            this.lCOut.Name = "lCOut";
            this.lCOut.Size = new System.Drawing.Size(19, 20);
            this.lCOut.TabIndex = 8;
            this.lCOut.Text = "0";
            // 
            // lTorque
            // 
            this.lTorque.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.lTorque.AutoSize = true;
            this.lTorque.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lTorque.Location = new System.Drawing.Point(154, 114);
            this.lTorque.Name = "lTorque";
            this.lTorque.Size = new System.Drawing.Size(19, 20);
            this.lTorque.TabIndex = 9;
            this.lTorque.Text = "0";

            int h = EliStatPanel.Height;
            // 
            // btnDoLand
            // 
            this.btnDoLand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDoLand.Location = new System.Drawing.Point(229, 10);
            this.btnDoLand.Name = "btnDoLand";
            this.btnDoLand.Size = new System.Drawing.Size(90,90 );
            this.btnDoLand.TabIndex = 1;
            this.btnDoLand.Text = "";
            this.btnDoLand.DoubleClick += new System.EventHandler(this.btnDoLand_Click);

            // 
            // btnDoTakeoff
            // 
            this.btnDoTakeoff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDoTakeoff.Location = new System.Drawing.Point(229, (int)(h - 100));
            this.btnDoTakeoff.Name = "btnDoTakeoff";
            this.btnDoTakeoff.Size = new System.Drawing.Size(90, 90);
            this.btnDoTakeoff.TabIndex = 1;
            this.btnDoTakeoff.Text = "aaaa";
            this.btnDoTakeoff.DoubleClick += new System.EventHandler(this.btnDoTakeoff_Click);

            // 
            // btnAltPlus
            // 
            this.btnAltPlus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top| System.Windows.Forms.AnchorStyles.Right)));
            this.btnAltPlus.Location = new System.Drawing.Point(229, EliStatPanel.Height/2 - 40);
            this.btnAltPlus.Name = "btnAltPlus";
            this.btnAltPlus.Size = new System.Drawing.Size(80, 80);
            this.btnAltPlus.TabIndex = 1;
            this.btnAltPlus.Text = "";
            this.btnAltPlus.Click += new System.EventHandler(this.btnAltPlus_Click);

            // 
            // btnAltMinus
            // 
            this.btnAltMinus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAltMinus.Location = new System.Drawing.Point(229, EliStatPanel.Height / 2 + 40);
            this.btnAltMinus.Name = "btnAltMinus";
            this.btnAltMinus.Size = new System.Drawing.Size(80, 80);
            this.btnAltMinus.TabIndex = 1;
            this.btnAltMinus.Text = "aaaa";
            this.btnAltMinus.Click += new System.EventHandler(this.btnAltMinus_Click);


            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(18, 180);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(110, 20);
            this.label6.TabIndex = 10;
            this.label6.Text = "Safety BATT";
            // 
            // lSafetyBatt
            // 
            this.lSafetyBatt.AutoSize = true;
            this.lSafetyBatt.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lSafetyBatt.Location = new System.Drawing.Point(152, 180);
            this.lSafetyBatt.Name = "lSafetyBatt";
            this.lSafetyBatt.Size = new System.Drawing.Size(19, 20);
            this.lSafetyBatt.TabIndex = 10;
            this.lSafetyBatt.Text = "0 V";


            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(20, 223);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(110, 20);
            this.label7.TabIndex = 12;
            this.label7.Text = "Target ALT";
            // 
            // lTargetAlt
            // 
            this.lTargetAlt.AutoSize = true;
            this.lTargetAlt.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lTargetAlt.Location = new System.Drawing.Point(58, 253);
            this.lTargetAlt.Name = "lTargetAlt";
            this.lTargetAlt.Size = new System.Drawing.Size(38, 20);
            this.lTargetAlt.TabIndex = 13;
            this.lTargetAlt.Text = "0 m";

            this.bDoChangeAlt.Location = new System.Drawing.Point(24, 278);
            this.bDoChangeAlt.Name = "bDoChangeAlt";
            this.bDoChangeAlt.Size = new System.Drawing.Size(104, 23);
            this.bDoChangeAlt.TabIndex = 14;
            this.bDoChangeAlt.Text = "Execute";
            this.bDoChangeAlt.UseVisualStyleBackColor = true;
            this.bDoChangeAlt.Click += new System.EventHandler(this.btnDoChangeAlt_Click);

            return true;
        }


    }
}
