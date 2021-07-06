using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class preflightCheckControl : UserControl
    {

        public event EventHandler statusUpdated;


        public preflightCheckControl()
        {
            InitializeComponent();

        }

        public Image noimage { get; set; }
        public Image ready { get; set; }
        public Image notready { get; set; }

        public void displayStep(PreflightStep s)
        {
            string preflightPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + "Mission Planner" +
                Path.DirectorySeparatorChar + "preflight" + Path.DirectorySeparatorChar;

            if (s is null) return;

            this.lName.Text = s.name;
            this.lDesc.Text = s.desc;

            try
            {
                Image i = Image.FromFile(preflightPath + s.imageFile);
                pictureBox1.Image = i;

            }
            catch
            {

                pictureBox1.Image = noimage;

            }

        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (PreflightList.currentStep is null) return;
            Graphics g = e.Graphics;
            Image i;

            if (PreflightList.currentStep.status)
            {
                g.DrawImage(ready, 300, 300, 100, 100);

            }
            else
            {
                g.DrawImage(notready, 300, 300, 100, 100);
            }

        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {



            if (PreflightList.currentStep is null) return;
            int c = PreflightList.steps.Count();
            int h = pictureBox2.Height;
            int w = pictureBox2.Width;
            Graphics g = e.Graphics;

            SolidBrush greenBrush = new SolidBrush(Color.Lime);
            SolidBrush redBrush = new SolidBrush(Color.Red);
            Pen whitePen = new Pen(Color.White, 1);

            float bh = (h / c);

            int y = 0;

            for (int i = 0; i < c; i++)
            {
                if (PreflightList.steps[i].status)
                {
                    g.FillRectangle(greenBrush, 1, i * bh + 1, w - 2, bh - 2);
                }
                else
                {
                    g.FillRectangle(redBrush, 1, i * bh + 1, w - 2, bh - 2);

                }
                if (i == PreflightList.stepNumber)
                    g.DrawRectangle(whitePen, 0, i * bh, w - 1, bh);
            }
        }

        private void bPrev_Click(object sender, EventArgs e)
        {
            if (PreflightList.stepNumber == 0) return;
            PreflightList.stepNumber--;
            PreflightList.currentStep = PreflightList.steps[PreflightList.stepNumber];
            displayStep(PreflightList.currentStep);
            pictureBox2.Invalidate();

        }

        private void bDone_Click(object sender, EventArgs e)
        {
            if (PreflightList.steps.Count == 0) return;

            PreflightList.currentStep.status = true;
            pictureBox1.Invalidate();
            pictureBox2.Invalidate();
            PreflightList.updateStatus();

            //Fire event on change
            EventHandler handler = this.statusUpdated;
            if (handler != null) handler(this, EventArgs.Empty);

            bNext_Click(null, EventArgs.Empty); //skip to the next item;

        }

        private void bNext_Click(object sender, EventArgs e)
        {
            if (PreflightList.steps.Count == 0) return;
            if (PreflightList.stepNumber == PreflightList.steps.Count - 1) return;
            PreflightList.stepNumber++;
            PreflightList.currentStep = PreflightList.steps[PreflightList.stepNumber];
            displayStep(PreflightList.currentStep);
            pictureBox2.Invalidate();
        }
    }


    public class PreflightStep
    {
        public String id;
        public String name;
        public String desc;
        public bool status;

        public String imageFile;


        public PreflightStep(String i, String n, String d, String file)
        {
            id = i;
            name = n;
            desc = d;
            imageFile = file;
            status = false;
        }
    }


    public enum preflightStatus
    {
        notStarted = 0,
        inProgress,
        finished
    }

    public static class PreflightList
    {
        public static List<PreflightStep> steps = new List<PreflightStep>();

        public static PreflightStep currentStep;

        public static int stepNumber { get; set; }

        public static preflightStatus stat { get; set; }

        static PreflightList()
        {

            string preflightPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + "Mission Planner" +
                Path.DirectorySeparatorChar + "preflight" + Path.DirectorySeparatorChar ;

            if ((LicenseManager.UsageMode != LicenseUsageMode.Designtime))
            {
                if (!loadPreflight(preflightPath + "preflight.txt")) return;
                invalidateAllSteps();
                initPreflight();
                updateStatus();
            }

        }

        public static void updateStatus()
        {
            stat = preflightStatus.notStarted;
            int okCount = 0;
            foreach (PreflightStep s in steps)
            {
                if (s.status) okCount++;
            }

            if (okCount != steps.Count)
                stat = preflightStatus.inProgress;
            else
                stat = preflightStatus.finished;
        }


        public static bool loadPreflight(String filename)
        {
            try
            {
                PreflightList.steps.Clear();
                string[] lines = System.IO.File.ReadAllLines(filename);
                foreach (string line in lines)
                {
                    string[] col = line.Split(';');
                    //Ignore lines with less or more than four items.
                    if (col.Length == 4)
                    {
                        PreflightStep s = new PreflightStep(col[0], col[1], col[2], col[3]);
                        PreflightList.steps.Add(s);
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static void initPreflight()
        {
            stepNumber = 0;
            invalidateAllSteps();
            currentStep = PreflightList.steps[0];

        }


        private static void invalidateAllSteps()
        {
            foreach (PreflightStep s in PreflightList.steps)
            {
                s.status = false;
            }
        }

    }






}
