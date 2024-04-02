using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using Newtonsoft.Json;

namespace MissionPlanner.Controls
{

    public partial class MapBoxURLSelector : Form
    {

        List<(string name, string url)> urlList = new List<(string name, string url)>();


        public MapBoxURLSelector()
        {
            InitializeComponent();
            getlistfromSettings();
            listView1.View = View.Details;
            listView1.Columns.Add("name", 200);
            listView1.Columns.Add("url", 500);
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.MultiSelect = false;

            updateListView();        }

        void updateListView()
        {
            listView1.Items.Clear();
            foreach (var item in urlList)
            {
                ListViewItem lvi = new ListViewItem(item.name);
                lvi.SubItems.Add(item.url);
                listView1.Items.Add(lvi);
            }
        }


        void getlistfromSettings()
        {
            if (Settings.Instance["MapBoxURLList"] != null)
            {
                urlList = JsonConvert.DeserializeObject<List<(string name, string url)>>(Settings.Instance["MapBoxURLList"].ToString());
            }
        }
        
        void setlisttoSettings()
        {
            Settings.Instance["MapBoxURLList"] = JsonConvert.SerializeObject(urlList);
        }

        private void myButton1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void myButton2_Click(object sender, EventArgs e)
        {

        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            string name = listView1.SelectedItems[0].SubItems[0].Text;
            string url = listView1.SelectedItems[0].SubItems[1].Text;

            var match = Regex.Matches(url, @"\/styles\/[^\/]+\/([^\/]+)\/([^\/\.]+).*access_token=([^#&=]+)");
            if (match is null)
            {
                CustomMessageBox.Show("Invalid URL!", Strings.ERROR);
                return;
            }
            Settings.Instance["MapBoxURL"] = url;
            Settings.Instance["MapBoxName"] = name;
            this.DialogResult = DialogResult.OK;
            Settings.Instance.Save();
            this.Close();

        }

        private void useToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Right click on a line to use it", "No line selected");
                return;
            }
            string name = listView1.SelectedItems[0].SubItems[0].Text;
            string url = listView1.SelectedItems[0].SubItems[1].Text;

            var match = Regex.Matches(url, @"\/styles\/[^\/]+\/([^\/]+)\/([^\/\.]+).*access_token=([^#&=]+)");
            if (match is null)
            {
                CustomMessageBox.Show("Invalid URL!", Strings.ERROR);
                return;
            }
            Settings.Instance["MapBoxURL"] = url;
            Settings.Instance["MapBoxName"] = name;
            Settings.Instance.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();

        }
        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string url = "";
            InputBox.Show("Enter MapBox Share URL", "Enter MapBox Share URL", ref url);
            string name = "";
            InputBox.Show("Enter MapBox map NAME", "Enter MapBox map NAME", ref name);

            var match = Regex.Matches(url, @"\/styles\/[^\/]+\/([^\/]+)\/([^\/\.]+).*access_token=([^#&=]+)");
            if (match is null)
            {
                CustomMessageBox.Show("Invalid URL!", Strings.ERROR);
                return;
            }
            urlList.Add((name, url));
            updateListView();
            Settings.Instance["MapBoxURL"] = url;
            Settings.Instance["MapBoxName"] = name;
            setlisttoSettings();
            Settings.Instance.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Right click on a line to delete it", "No line selected");
                return;
            }

            string nameToDelete = listView1.SelectedItems[0].SubItems[0].Text;
            urlList.RemoveAll(x => x.name == nameToDelete);
            updateListView();
            setlisttoSettings();
            Settings.Instance.Save();

        }
    }
}
