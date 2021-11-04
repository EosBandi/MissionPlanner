using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.StatusForms
{
    public partial class payloadIgnite : Form
    {

        public int igniteMask { get; set; }

        public payloadIgnite()
        {
            InitializeComponent();
        }

        private void payloadIgnite_Load(object sender, EventArgs e)
        {

            int a = igniteMask;

            if ((a & 1) > 0) c1.Checked = true; else c1.Checked = false;
            if ((a & (1 << 1)) > 0) c2.Checked = true; else c2.Checked = false;
            if ((a & (1 << 2)) > 0) c3.Checked = true; else c3.Checked = false;
            if ((a & (1 << 3)) > 0) c4.Checked = true; else c4.Checked = false;
            if ((a & (1 << 4)) > 0) c5.Checked = true; else c5.Checked = false;
            if ((a & (1 << 5)) > 0) c6.Checked = true; else c6.Checked = false;
            if ((a & (1 << 6)) > 0) c7.Checked = true; else c7.Checked = false;
            if ((a & (1 << 7)) > 0) c8.Checked = true; else c8.Checked = false;
            if ((a & (1 << 8)) > 0) c9.Checked = true; else c9.Checked = false;
            if ((a & (1 << 9)) > 0) c10.Checked = true; else c10.Checked = false;

        }

        private void bOK_Click(object sender, EventArgs e)
        {

            int a = 0;
            if (c1.Checked) a = a + (1 << 0);
            if (c2.Checked) a = a + (1 << 1);
            if (c3.Checked) a = a + (1 << 2);
            if (c4.Checked) a = a + (1 << 3);
            if (c5.Checked) a = a + (1 << 4);
            if (c6.Checked) a = a + (1 << 5);
            if (c7.Checked) a = a + (1 << 6);
            if (c8.Checked) a = a + (1 << 7);
            if (c9.Checked) a = a + (1 << 8);
            if (c10.Checked) a = a + (1 << 9);

            igniteMask = a;
            this.Close();


        }
    }
}
