
namespace MissionPlanner.Controls
{


    partial class annunciator
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
            this.components = new System.ComponentModel.Container();
            this.layoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.contextMenu1 = new System.Windows.Forms.ContextMenu();
            this.menuUndockDock = new System.Windows.Forms.MenuItem();
            this.SuspendLayout();
            // 
            // layoutPanel
            // 
            this.layoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutPanel.Location = new System.Drawing.Point(0, 0);
            this.layoutPanel.Margin = new System.Windows.Forms.Padding(2);
            this.layoutPanel.Name = "layoutPanel";
            this.layoutPanel.Size = new System.Drawing.Size(1300, 100);
            this.layoutPanel.TabIndex = 0;
            // 
            // contextMenu1
            // 
            this.contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuUndockDock});
            // 
            // menuUndockDock
            // 
            this.menuUndockDock.Index = 0;
            this.menuUndockDock.Text = "Undock/Dock";
            this.menuUndockDock.Click += new System.EventHandler(this.menuUndockDock_Click);
            // 
            // annunciator
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.Controls.Add(this.layoutPanel);
            this.Name = "annunciator";
            this.Size = new System.Drawing.Size(1300, 100);
            this.EnabledChanged += new System.EventHandler(this.annunciator_EnabledChanged);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel layoutPanel;
        private System.Windows.Forms.ContextMenu contextMenu1;
        private System.Windows.Forms.MenuItem menuUndockDock;
    }
}
