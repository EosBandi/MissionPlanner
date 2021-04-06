
namespace MissionPlanner
{
    partial class hudtest_tobedelete
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.hud1 = new MissionPlanner.Controls.HUD();
            this.SuspendLayout();
            // 
            // hud1
            // 
            this.hud1.airspeed = 35F;
            this.hud1.alt = 0F;
            this.hud1.altunit = "";
            this.hud1.AOA = 0F;
            this.hud1.BackColor = System.Drawing.Color.Black;
            this.hud1.batterycellcount = 0;
            this.hud1.batterylevel = 0F;
            this.hud1.batteryremaining = 0F;
            this.hud1.bgimage = null;
            this.hud1.connected = false;
            this.hud1.critAOA = 25F;
            this.hud1.criticalvoltagealert = false;
            this.hud1.critSSA = 30F;
            this.hud1.current = 0F;
            this.hud1.datetime = new System.DateTime(((long)(0)));
            this.hud1.displayAOASSA = false;
            this.hud1.displayCellVoltage = false;
            this.hud1.displayconninfo = false;
            this.hud1.displayekf = false;
            this.hud1.displaygps = false;
            this.hud1.displayheading = false;
            this.hud1.displayvibe = false;
            this.hud1.displayxtrack = false;
            this.hud1.disttowp = 0F;
            this.hud1.distunit = "";
            this.hud1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hud1.ekfstatus = 0F;
            this.hud1.failsafe = false;
            this.hud1.fuelLevel = 0F;
            this.hud1.gpsfix = 0F;
            this.hud1.gpsfix2 = 0F;
            this.hud1.gpshdop = 0F;
            this.hud1.gpshdop2 = 0F;
            this.hud1.groundalt = 0F;
            this.hud1.groundcourse = 0F;
            this.hud1.groundspeed = 0F;
            this.hud1.heading = 0F;
            this.hud1.hudcolor = System.Drawing.Color.LightGray;
            this.hud1.linkqualitygcs = 0F;
            this.hud1.Location = new System.Drawing.Point(0, 0);
            this.hud1.lowairspeed = false;
            this.hud1.lowgroundspeed = false;
            this.hud1.lowvoltagealert = false;
            this.hud1.message = "";
            this.hud1.messageSeverity = MAVLink.MAV_SEVERITY.EMERGENCY;
            this.hud1.mode = "Manual";
            this.hud1.Name = "hud1";
            this.hud1.navpitch = 0F;
            this.hud1.navroll = 0F;
            this.hud1.pitch = 0F;
            this.hud1.roll = 15F;
            this.hud1.Russian = false;
            this.hud1.Size = new System.Drawing.Size(801, 511);
            this.hud1.skyColor1 = System.Drawing.Color.Blue;
            this.hud1.skyColor2 = System.Drawing.Color.LightBlue;
            this.hud1.speedunit = "";
            this.hud1.SSA = 0F;
            this.hud1.status = false;
            this.hud1.sysid = 0F;
            this.hud1.TabIndex = 0;
            this.hud1.targetalt = 0F;
            this.hud1.targetheading = 0F;
            this.hud1.targetspeed = 42.5F;
            this.hud1.turnrate = 0F;
            this.hud1.verticalspeed = 0F;
            this.hud1.vibex = 0F;
            this.hud1.vibey = 0F;
            this.hud1.vibez = 0F;
            this.hud1.VSync = false;
            this.hud1.wpno = 0;
            this.hud1.xtrack_error = 0F;
            // 
            // hudtest_tobedelete
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(801, 511);
            this.Controls.Add(this.hud1);
            this.Name = "hudtest_tobedelete";
            this.Text = "hudtest_tobedelete";
            this.ResumeLayout(false);

        }

        #endregion

        private Controls.HUD hud1;
    }
}