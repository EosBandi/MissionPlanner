using System;
using System.Windows;
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



namespace MissionPlanner.Elistair
{

    public class EliPlugin : MissionPlanner.Plugin.Plugin
    {


        private WebClient browser = new WebClient();
        private Uri _url = new Uri("http://127.0.0.1/");
        private ElistairClass eli = new ElistairClass();
        private int _eli_wait_cycles = 0;


        SplitContainer sc;
        Label lab;
     
        MenuStrip mainmenu;
        SplitContainer SubMainLeft;
        SplitContainer MainH;
        TableLayoutPanel tblMap;

        Player.ucPlayerControl ucPlayerControl1;

        int a = 0;


        private System.Windows.Forms.Panel EliStatPanel;
        private System.Windows.Forms.GroupBox groupBoxWinch;
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
        private System.Windows.Forms.Button button1;


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
            HUD hud = SubMainLeft.Panel1.Controls["hud1"] as HUD;
            hud.displayAOASSA = false;
            hud.displayxtrack = false;
            hud.displayspeed = false;
            hud.displayconninfo = false;

            hud.ContextMenuStrip.Items["swapWithMapToolStripMenuItem"].Enabled = false;
            hud.ContextMenuStrip.Items["videoToolStripMenuItem"].Enabled = false;
            
            


            //Create and Add StreamPlayerControl to the place of tblMap
            ucPlayerControl1 = new Player.ucPlayerControl();
            ucPlayerControl1.AutoRecconect = true;
            ucPlayerControl1.ffmegParams = "";
            ucPlayerControl1.ffmegPath = "";
            ucPlayerControl1.Location = new System.Drawing.Point(0, 0);
            ucPlayerControl1.MediaUrl = "rtsp://192.168.0.33:554/user=admin&password=titok&channel=1&stream=1.sdp?real_stream--rtp-caching=1";// rtsp://localhost:8554/";
            ucPlayerControl1.Name = "ucPlayerControl1";
            ucPlayerControl1.RecordPath = "";
            ucPlayerControl1.Size = MainH.Panel2.Size;
            ucPlayerControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                       | System.Windows.Forms.AnchorStyles.Left)
                                       | System.Windows.Forms.AnchorStyles.Right)));
            ucPlayerControl1.TabIndex = 0;
            ucPlayerControl1.VideoRate = Player.ucPlayerControl.ratelist.WideScreen;
            ucPlayerControl1.VisiblePlayerMenu = true;
            ucPlayerControl1.VisibleStatus = true;
            MainH.Panel2.Controls.Add(ucPlayerControl1);
            ucPlayerControl1.Play();

            this.AddControls();
            SubMainLeft.Panel2.Controls.Add(EliStatPanel);
            EliStatPanel.Width = SubMainLeft.Panel2.Width;

            Image imageLand = (Image)(resources.GetObject("landing_512"));
            this.button1.BackgroundImage = imageLand;
            this.button1.BackgroundImageLayout = ImageLayout.Stretch;
                        


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

            this.button1.ForeColor = Color.Transparent;
            this.button1.BackColor = Color.Transparent;
            //Check if browser is busy
            if (!browser.IsBusy)
            {
                // not busy, lets start a download
                browser.DownloadStringAsync(_url);
                // and clear wait cycles
                _eli_wait_cycles = 0;
            } else
            {
                // browser is busy, if it was busy for more than 2 cycles then cancel current download
                if (_eli_wait_cycles<2) { _eli_wait_cycles++; }
                else
                {
                    browser.CancelAsync();
                }
            }

            MainV2.instance.Invoke((Action)
              delegate
              {
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
                  if (eli.Temperature < 40) lTemp.ForeColor = Color.White;
                  else if (eli.Temperature >= 40 && eli.Temperature < 45) lTemp.ForeColor = Color.Orange;
                  else lTemp.ForeColor = Color.OrangeRed;
                  //Power
                  // 
                  if (eli.Power < 1400) lPower.ForeColor = Color.White;
                  else if (eli.Power >= 1400 && eli.Power <= 1800) lPower.ForeColor = Color.Orange;
                  else lPower.ForeColor = Color.OrangeRed;



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
                return;
            }
            eli.Message = e.Result;
        }


        private bool AddControls()
        {

            this.EliStatPanel = new System.Windows.Forms.Panel();
            this.groupBoxWinch = new System.Windows.Forms.GroupBox();
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
            this.button1 = new System.Windows.Forms.Button();
            // 
            // EliStatPanel
            // 
            this.EliStatPanel.Controls.Add(this.groupBoxWinch);
            this.EliStatPanel.Controls.Add(this.button1);
            this.EliStatPanel.Location = new System.Drawing.Point(0, 0);
            this.EliStatPanel.Name = "EliStatPanel";
            this.EliStatPanel.Size = new System.Drawing.Size(340, 271);
            this.EliStatPanel.TabIndex = 0;
            // 
            // groupBoxWinch
            // 
            this.groupBoxWinch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
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
            this.groupBoxWinch.Location = new System.Drawing.Point(0, 0);
            this.groupBoxWinch.Name = "groupBoxWinch";
            this.groupBoxWinch.Size = new System.Drawing.Size(220, 163);
            this.groupBoxWinch.AutoSize = true;
            this.groupBoxWinch.TabIndex = 0;
            this.groupBoxWinch.TabStop = false;
            this.groupBoxWinch.Text = "Winch";
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
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Location = new System.Drawing.Point(229, 8);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(90,90 );
            this.button1.TabIndex = 1;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;

            return true;
        }


    }
}
