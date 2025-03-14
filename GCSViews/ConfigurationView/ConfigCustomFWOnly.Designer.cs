namespace MissionPlanner.GCSViews.ConfigurationView
{
    partial class ConfigCustomFWOnly
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.progress = new System.Windows.Forms.ProgressBar();
            this.lbl_status = new System.Windows.Forms.Label();
            this.myButton1 = new MissionPlanner.Controls.MyButton();
            this.SuspendLayout();
            // 
            // progress
            // 
            this.progress.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.progress.Location = new System.Drawing.Point(13, 90);
            this.progress.Name = "progress";
            this.progress.Size = new System.Drawing.Size(525, 23);
            this.progress.Step = 1;
            this.progress.TabIndex = 7;
            // 
            // lbl_status
            // 
            this.lbl_status.AutoSize = true;
            this.lbl_status.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lbl_status.Location = new System.Drawing.Point(10, 116);
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Size = new System.Drawing.Size(37, 13);
            this.lbl_status.TabIndex = 8;
            this.lbl_status.Text = "Status";
            // 
            // myButton1
            // 
            this.myButton1.Location = new System.Drawing.Point(13, 20);
            this.myButton1.Name = "myButton1";
            this.myButton1.Size = new System.Drawing.Size(283, 42);
            this.myButton1.TabIndex = 9;
            this.myButton1.Text = "Upload Firmware \r\nUse firmware.apj file only from the manufacturer\r\n";
            this.myButton1.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.myButton1.UseVisualStyleBackColor = true;
            this.myButton1.Click += new System.EventHandler(this.myButton1_Click);
            // 
            // ConfigCustomFWOnly
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.myButton1);
            this.Controls.Add(this.lbl_status);
            this.Controls.Add(this.progress);
            this.Name = "ConfigCustomFWOnly";
            this.Size = new System.Drawing.Size(552, 156);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ProgressBar progress;
        private System.Windows.Forms.Label lbl_status;
        private Controls.MyButton myButton1;
    }
}
