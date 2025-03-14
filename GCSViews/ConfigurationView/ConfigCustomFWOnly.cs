using log4net;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView

{
    public partial class ConfigCustomFWOnly : MyUserControl, IActivate, IDeactivate
    {

        private string custom_fw_dir = Settings.Instance["FirmwareFileDirectory"] ?? "";
        private readonly Firmware fw = new Firmware();
        public static Func<List<ArduPilot.DeviceInfo>> ExtraDeviceInfo;
        private IProgressReporterDialogue pdr;
        public ConfigCustomFWOnly()
        {
            InitializeComponent();
        }

        public void Activate()
        {

        }

        public void Deactivate()
        {

        }

        private void myButton1_Click(object sender, EventArgs e)
        {
            using (var fd = new OpenFileDialog
            { Filter = "Firmware (*.hex;*.px4;*.vrx;*.apj)|*.hex;*.px4;*.vrx;*.apj|All files (*.*)|*.*" })
            {
                if (Directory.Exists(custom_fw_dir))
                    fd.InitialDirectory = custom_fw_dir;
                fd.ShowDialog();
                if (File.Exists(fd.FileName))
                {
                    custom_fw_dir = Path.GetDirectoryName(fd.FileName);
                    Settings.Instance["FirmwareFileDirectory"] = custom_fw_dir;

                    //fw.Progress -= fw_ProgressPDR;
                    fw.Progress += fw_Progress1;

                    var boardtype = BoardDetect.boards.none;
                    try
                    {
                        if (fd.FileName.ToLower().EndsWith(".px4") || fd.FileName.ToLower().EndsWith(".apj"))
                        {
                            if (solo.Solo.is_solo_alive &&
                                CustomMessageBox.Show("Solo", "Is this a Solo?",
                                    CustomMessageBox.MessageBoxButtons.YesNo) == CustomMessageBox.DialogResult.Yes)
                            {
                                boardtype = BoardDetect.boards.solo;
                            }
                            else
                            {
                                boardtype = BoardDetect.boards.px4v3;
                            }
                        }
                        else
                        {
                            var ports = Win32DeviceMgmt.GetAllCOMPorts();
                            ports.AddRange(Linux.GetAllCOMPorts());

                            if (ExtraDeviceInfo != null)
                            {
                                try
                                {
                                    ports.AddRange(ExtraDeviceInfo.Invoke());
                                }
                                catch
                                {

                                }
                            }

                            boardtype = BoardDetect.DetectBoard(MainV2.comPortName, ports);
                        }

                        if (boardtype == BoardDetect.boards.none)
                        {
                            CustomMessageBox.Show(Strings.CantDetectBoardVersion);
                            return;
                        }
                    }
                    catch
                    {
                        CustomMessageBox.Show(Strings.CanNotConnectToComPortAnd, Strings.ERROR);
                        return;
                    }

                    try
                    {
                        fw.UploadFlash(MainV2.comPortName, fd.FileName, boardtype);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show(ex.ToString(), Strings.ERROR);
                    }
                }
            }
        }


        /// <summary>
        ///     for updating fw list
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="status"></param>
        private void fw_ProgressPDR(int progress, string status)
        {
            pdr.UpdateProgressAndStatus(progress, status);
        }

        /// <summary>
        ///     for when updating fw to hardware
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="status"></param>
        private void fw_Progress1(int progress, string status)
        {
            this.BeginInvokeIfRequired(() =>
            {
                var change = false;

                if (progress != -1)
                {
                    if (this.progress.Value != progress)
                    {
                        this.progress.Value = progress;
                        change = true;
                    }
                }

                if (lbl_status.Text != status)
                {
                    lbl_status.Text = status;
                    change = true;
                }

                if (change)
                    this.Refresh();
            });
        }


    }

}