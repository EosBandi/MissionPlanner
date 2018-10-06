namespace MissionPlanner.TestPlugin
{
    partial class EliPLuginUI
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
            this.ucPlayerControl1 = new racPlayerControl.racPlayerControl();
            this.SuspendLayout();
            // 
            // ucPlayerControl1
            // 
            this.ucPlayerControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ucPlayerControl1.AutoRecconect = false;
            this.ucPlayerControl1.ffmegParams = "";
            this.ucPlayerControl1.ffmegPath = "";
            this.ucPlayerControl1.Location = new System.Drawing.Point(0, 0);
            this.ucPlayerControl1.MediaUrl = "";
            this.ucPlayerControl1.Name = "ucPlayerControl1";
            this.ucPlayerControl1.RecordPath = "";
            this.ucPlayerControl1.Size = new System.Drawing.Size(545, 340);
            this.ucPlayerControl1.TabIndex = 0;
            this.ucPlayerControl1.VideoRate = racPlayerControl.racPlayerControl.ratelist.OriginalRate;
            this.ucPlayerControl1.VisiblePlayerMenu = true;
            this.ucPlayerControl1.VisibleStatus = true;
            // 
            // PtestUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(619, 374);
            this.Controls.Add(this.ucPlayerControl1);
            this.Name = "PtestUI";
            this.Text = "PtestUI";
            this.ResumeLayout(false);

        }

        #endregion

        private racPlayerControl.racPlayerControl ucPlayerControl1;
    }
}