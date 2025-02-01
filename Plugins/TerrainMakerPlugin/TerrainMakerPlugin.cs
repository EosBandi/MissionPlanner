using MissionPlanner;
using MissionPlanner.Plugin;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using MissionPlanner.Controls.PreFlight;
using MissionPlanner.Controls;
using System.Linq;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Maps;
using GMap.NET;
using GMap.NET.WindowsForms;
using System.Globalization;
using System.Drawing;
using Microsoft.Win32;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.RightsManagement;
using Org.BouncyCastle.Utilities;
using NetTopologySuite.Operation.Valid;
using Org.BouncyCastle.Asn1.X509;

namespace TerrainMakerPlugin
{

    public class TerrainMakerPlugin : Plugin
    {


        SplitContainer sc;
        MissionPlanner.Controls.MyButton button1;

        Stopwatch stopwatch = new Stopwatch();
        public override string Name
        {
            get { return "TerrainMakerPlugin"; }
        }

        public override string Version
        {
            get { return "2.1"; }
        }

        public override string Author
        {
            get { return "Andras \"EosBandi\" Schaffer"; }
        }

        public override bool Init()
        {
            return true;	 // If it is false then plugin will not load
        }

        public override bool Loaded()
        {

            Host.FPMenuMap.Items.Add(new ToolStripMenuItem("Make Terrain DAT", null, MakeTerrainDAT));

            return true;     //If it is false plugin will not start (loop will not called)
        }


        private int lat_int_togenerate = 0;
        private int lon_int_togenerate = 0;
        private ushort spacing_togenerate = 0;
        private bool maxaltcalc_togenerate = false;



        private void MakeTerrainDAT(object sender, EventArgs e)
        {
            RectLatLng area = Host.FPGMapControl.SelectedArea;
            if (area.IsEmpty)
            {
                var res = CustomMessageBox.Show("No area defined, use area displayed on screen?", "Terrain DAT",
                    MessageBoxButtons.YesNo);
                if (res == (int)DialogResult.Yes)
                {
                    area = Host.FPGMapControl.ViewArea;
                }
            }


            if (!area.IsEmpty)
            {
                string spacingstring = "4";
                if (InputBox.Show("SPACING", "Enter the grid spacing in meters (1-100).", ref spacingstring) !=
                    DialogResult.OK)
                    return;

                int spacing = 30;
                if (!int.TryParse(spacingstring, out spacing))
                {
                    CustomMessageBox.Show("Invalid Number", "ERROR");
                    return;
                }

                if (spacing < 1 || spacing > 100)
                {
                    CustomMessageBox.Show("Spacing must be between 1 and 100 meters", "ERROR");
                    return;
                }


                if (spacing < 4)
                {
                    var res = CustomMessageBox.Show("Spacing is less than 4 meters! It will create a file larger than 2 Gb. Due a filesystem bug, Ardupilot does not support files larger than 2Gb (yet) ", "WARNING", MessageBoxButtons.OKCancel);
                    if (res == (int)DialogResult.Cancel)
                    {
                        return;
                    }
                }
                bool maxaltcalc = false;
                if (CustomMessageBox.Show("Do you want calculate max alt within grid elements?", "Max Alt", MessageBoxButtons.YesNo) == (int)DialogResult.Yes)
                    maxaltcalc = true;




                    //Do it in the selected area with the selected spacing
                    int lat_start = (int)Math.Floor(area.Bottom);
                int lat_end = (int)Math.Ceiling(area.Top);

                int lon_start = (int)Math.Floor(area.Left);
                int lon_end = (int)Math.Ceiling(area.Right);



                for (int lat_int = lat_start; lat_int< lat_end; lat_int++)
                {
                    for (int lon_int = lon_start; lon_int < lon_end; lon_int++)
                    {
                        Console.WriteLine("Make Terrain DAT {0} {1}", lat_int, lon_int);

                        lat_int_togenerate = lat_int;
                        lon_int_togenerate = lon_int;
                        spacing_togenerate = (ushort)spacing;
                        maxaltcalc_togenerate = maxaltcalc;



                        IProgressReporterDialogue frmProgressReporter = new ProgressReporterDialogue
                        {
                            StartPosition = FormStartPosition.CenterScreen,
                            Text = String.Format("Generate terrain data for Lat {0} Lon {1}", lat_int, lon_int)
                        };

                        frmProgressReporter.DoWork += createTerrainDataFile;
                        frmProgressReporter.UpdateProgressAndStatus(-1, "Starting...");

                        ThemeManager.ApplyThemeTo(frmProgressReporter);

                        frmProgressReporter.RunBackgroundOperationAsync();

                        frmProgressReporter.Dispose();

                        CustomMessageBox.Show("Terrain DAT created in Documents/Mission Planner/TerrainDat folder", "Terrain DAT");


                    }
                }

            }


        }


        private void createTerrainDataFile(IProgressReporterDialogue sender)
        {

            int lat_int = lat_int_togenerate;
            int lon_int = lon_int_togenerate;
            ushort spacing = spacing_togenerate;
            bool maxaltcalc = maxaltcalc_togenerate;


            stopwatch.Start();
            TerrainDataFile.GRID_SPACING = spacing;

            TerrainDataFile.GridBlock grid;
            srtm.altresponce altresponce = new srtm.altresponce();

            string ns, ew;
            if (lat_int < 0) ns = "S"; else ns = "N";
            if (lon_int <0 ) ew = "W"; else ew = "E";

            string filename = string.Format("{0}{1:00}{2}{3:000}.DAT", ns, Math.Abs(lat_int), ew, Math.Abs(lon_int));
            string path = Path.Combine(Settings.GetUserDataDirectory(), "TerrainData");
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch
                {
                    MessageBox.Show("Can't create directory: " + path);
                    return;
                }
            }
            string fullpath = Path.Combine(path, filename);

            FileStream datafile = new FileStream(fullpath, FileMode.Create);
            BinaryWriter datafileWriter = new BinaryWriter(datafile);

            int a = TerrainDataFile.east_blocks(new Location(lat_int * 10 * 1000 * 1000, lon_int * 10 * 1000 * 1000));

            int n = -1;

            while (true)
            {
                n += 1;

                Location locOfBlock = TerrainDataFile.pos_from_file_offset(lat_int, lon_int, n * TerrainDataFile.IO_BLOCK_SIZE);
                if (locOfBlock.lat * 1.0e-7 - lat_int >= 1.0) break;
                grid = new TerrainDataFile.GridBlock((sbyte)lat_int, (short)lon_int, locOfBlock, spacing);
            }

            //We have the max block number in N
            for (int blocknum = 0; blocknum < n; blocknum++)
            {
                string message = string.Format("Making block {0} of {1}", blocknum, n);
                sender.UpdateProgressAndStatus((int)(100.0 * blocknum / n), message);
                if (sender.doWorkArgs.CancelRequested)
                {
                    sender.doWorkArgs.CancelAcknowledged = true;
                    datafileWriter.Close();
                    datafile.Close();
                    //delete the datafile sinc it is incomplete
                    File.Delete(fullpath);
                    return;
                }

                Location loc = TerrainDataFile.pos_from_file_offset(lat_int, lon_int, blocknum * TerrainDataFile.IO_BLOCK_SIZE);
                grid = new TerrainDataFile.GridBlock((sbyte)lat_int, (short)lon_int, loc, spacing);


                if (maxaltcalc)
                {

                    //create an array of altitudes for the block
                    short[,] maxalts = new short[TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_X, TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_Y];
                    //clear the array
                    for (int i = 0; i < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_X; i++)
                    {
                        for (int j = 0; j < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_Y; j++)
                        {
                            maxalts[i, j] = 0;
                        }
                    }


                    for (int gx = 0; gx < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_X; gx++)
                    {
                        for (int gy = 0; gy < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_Y; gy++)
                        {
                            Location pointLoc_top_left = grid.blockTopLeft.add_offset_meters(gx * TerrainDataFile.GRID_SPACING, gy * TerrainDataFile.GRID_SPACING);

                            double maxalt = 0;
                            for (int y = 0; y < TerrainDataFile.GRID_SPACING; y++)
                            {
                                for (int x = 0; x < TerrainDataFile.GRID_SPACING; x++)
                                {
                                    Location pointLoc = pointLoc_top_left.add_offset_meters(x, y);
                                    double lat = pointLoc.lat * 1.0e-7;
                                    double lon = pointLoc.lng * 1.0e-7;
                                    altresponce = srtm.getAltitude(lat, lon, 20); //get at max zoom
                                    if (altresponce.currenttype == srtm.tiletype.valid)
                                    {
                                        if (altresponce.alt > maxalt) maxalt = altresponce.alt;
                                    }
                                }
                            }

                            maxalts[gx, gy] = (short)Math.Round(maxalt);


                        }
                    }

                    for (int gx = 0; gx < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_X - 1; gx++)
                    {
                        for (int gy = 0; gy < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_Y - 1; gy++)
                        {
                            var alt = maxalts[gx, gy];
                            if (grid.GetHeight(gx, gy) < alt) grid.SetHeight(gx, gy, alt);
                            if (grid.GetHeight(gx + 1, gy) < alt) grid.SetHeight(gx + 1, gy, alt);
                            if (grid.GetHeight(gx, gy + 1) < alt) grid.SetHeight(gx, gy + 1, alt);
                            if (grid.GetHeight(gx + 1, gy + 1) < alt) grid.SetHeight(gx + 1, gy + 1, alt);

                        }
                    }
                }
                else
                {
                    for (int gx = 0; gx < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_X; gx++)
                    {
                        for (int gy = 0; gy < TerrainDataFile.TERRAIN_GRID_BLOCK_SIZE_Y; gy++)
                        {
                            Location pointLoc_top_left = grid.blockTopLeft.add_offset_meters(gx * TerrainDataFile.GRID_SPACING, gy * TerrainDataFile.GRID_SPACING);
                            double lat = pointLoc_top_left.lat * 1.0e-7;
                            double lon = pointLoc_top_left.lng * 1.0e-7;
                            altresponce = srtm.getAltitude(lat, lon, 20); //get at max zoom

                            if (altresponce.currenttype == srtm.tiletype.valid && altresponce.alt != 0)
                            {
                                grid.SetHeight(gx, gy, (short)Math.Round(altresponce.alt));
                            }
                            else
                            {
                                grid.SetHeight(gx, gy, 0);
                            }
                        }
                    }
                }


                //Check block validity and fill bitmap
                grid.Bitmap = 0; // Clear bitmap

                for (int x = 0; x < TerrainDataFile.TERRAIN_GRID_BLOCK_MUL_X; x++)
                {
                    for (int y = 0; y < TerrainDataFile.TERRAIN_GRID_BLOCK_MUL_Y; y++)
                    {
                        if (grid.isvalid(x, y))
                        {
                            grid.setBit((byte)(y + TerrainDataFile.TERRAIN_GRID_BLOCK_MUL_Y * x));
                        }

                    }
                }

                byte[] bytes = grid.getPackedBytes();
                datafileWriter.Write(bytes);

            }

            stopwatch.Stop();
            Console.WriteLine("Terrain Data Creator Time elapsed: {0}", stopwatch.Elapsed);
            datafileWriter.Close();
            datafile.Close();

        }

        public override bool Exit()
        {
            return true;
        }


    }
}