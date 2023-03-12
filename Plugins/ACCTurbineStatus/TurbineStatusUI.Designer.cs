﻿namespace TurbineStatus
{
    partial class TurbineStatusUI
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
            this.components = new System.ComponentModel.Container();
            this.txt_messages = new System.Windows.Forms.TextBox();
            this.table_gauges = new System.Windows.Forms.TableLayoutPanel();
            this.aGauge1 = new AGaugeApp.AGauge();
            this.aGauge2 = new AGaugeApp.AGauge();
            this.aGauge3 = new AGaugeApp.AGauge();
            this.table_gauges2 = new System.Windows.Forms.TableLayoutPanel();
            this.aGauge4 = new AGaugeApp.AGauge();
            this.aGauge5 = new AGaugeApp.AGauge();
            this.aGauge6 = new AGaugeApp.AGauge();
            this.aGauge7 = new AGaugeApp.AGauge();
            this.aGauge8 = new AGaugeApp.AGauge();
            this.table_main = new System.Windows.Forms.TableLayoutPanel();
            this.lbl_ecumode = new System.Windows.Forms.Label();
            this.lbl_turbinemode = new System.Windows.Forms.Label();
            this.table_control = new System.Windows.Forms.TableLayoutPanel();
            this.label3 = new System.Windows.Forms.Label();
            this.cmb_mode = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.but_setmode = new MissionPlanner.Controls.MyButton();
            this.but_mainpump = new MissionPlanner.Controls.MyButton();
            this.led_mainpump = new Bulb.LedBulb();
            this.but_empump = new MissionPlanner.Controls.MyButton();
            this.led_empump = new Bulb.LedBulb();
            this.but_alternatorconn = new MissionPlanner.Controls.MyButton();
            this.led_alternatorconn = new Bulb.LedBulb();
            this.but_alternator = new MissionPlanner.Controls.MyButton();
            this.led_alternator = new Bulb.LedBulb();
            this.but_oilcooler = new MissionPlanner.Controls.MyButton();
            this.led_oilcooler = new Bulb.LedBulb();
            this.but_totalstop = new MissionPlanner.Controls.MyButton();
            this.led_totalstop = new Bulb.LedBulb();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.ui_timer = new System.Windows.Forms.Timer(this.components);
            this.table_gauges.SuspendLayout();
            this.table_gauges2.SuspendLayout();
            this.table_main.SuspendLayout();
            this.table_control.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // txt_messages
            // 
            this.txt_messages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txt_messages.Location = new System.Drawing.Point(3, 391);
            this.txt_messages.Multiline = true;
            this.txt_messages.Name = "txt_messages";
            this.txt_messages.ReadOnly = true;
            this.txt_messages.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txt_messages.Size = new System.Drawing.Size(717, 114);
            this.txt_messages.TabIndex = 9;
            // 
            // table_gauges
            // 
            this.table_gauges.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.table_gauges.ColumnCount = 3;
            this.table_gauges.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33332F));
            this.table_gauges.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.table_gauges.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.table_gauges.Controls.Add(this.aGauge1, 0, 0);
            this.table_gauges.Controls.Add(this.aGauge2, 1, 0);
            this.table_gauges.Controls.Add(this.aGauge3, 2, 0);
            this.table_gauges.Controls.Add(this.table_gauges2, 0, 1);
            this.table_gauges.Location = new System.Drawing.Point(0, 0);
            this.table_gauges.Margin = new System.Windows.Forms.Padding(0);
            this.table_gauges.Name = "table_gauges";
            this.table_gauges.RowCount = 2;
            this.table_gauges.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 62.5F));
            this.table_gauges.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 37.5F));
            this.table_gauges.Size = new System.Drawing.Size(723, 388);
            this.table_gauges.TabIndex = 15;
            // 
            // aGauge1
            // 
            this.aGauge1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge1.BackColor = System.Drawing.Color.Transparent;
            this.aGauge1.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge1.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge1.BaseArcRadius = 70;
            this.aGauge1.BaseArcStart = 135;
            this.aGauge1.BaseArcSweep = 270;
            this.aGauge1.BaseArcWidth = 2;
            this.aGauge1.Cap_Idx = ((byte)(0));
            this.aGauge1.CapColor = System.Drawing.Color.White;
            this.aGauge1.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge1.CapPosition = new System.Drawing.Point(67, 110);
            this.aGauge1.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(67, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge1.CapsText = new string[] {
        "N1",
        "",
        "",
        "",
        ""};
            this.aGauge1.CapText = "N1";
            this.aGauge1.Center = new System.Drawing.Point(75, 75);
            this.aGauge1.Location = new System.Drawing.Point(0, 0);
            this.aGauge1.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge1.MaxValue = 120F;
            this.aGauge1.MinValue = 0F;
            this.aGauge1.Name = "aGauge1";
            this.aGauge1.Need_Idx = ((byte)(3));
            this.aGauge1.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge1.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge1.NeedleEnabled = false;
            this.aGauge1.NeedleRadius = 70;
            this.aGauge1.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge1.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge1.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge1.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge1.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge1.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge1.NeedleType = 0;
            this.aGauge1.NeedleWidth = 2;
            this.aGauge1.Range_Idx = ((byte)(2));
            this.aGauge1.RangeColor = System.Drawing.Color.Red;
            this.aGauge1.RangeEnabled = true;
            this.aGauge1.RangeEndValue = 120F;
            this.aGauge1.RangeInnerRadius = 55;
            this.aGauge1.RangeOuterRadius = 60;
            this.aGauge1.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge1.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge1.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge1.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge1.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge1.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge1.RangeStartValue = 108F;
            this.aGauge1.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge1.ScaleLinesInterInnerRadius = 52;
            this.aGauge1.ScaleLinesInterOuterRadius = 60;
            this.aGauge1.ScaleLinesInterWidth = 1;
            this.aGauge1.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge1.ScaleLinesMajorInnerRadius = 50;
            this.aGauge1.ScaleLinesMajorOuterRadius = 60;
            this.aGauge1.ScaleLinesMajorStepValue = 20F;
            this.aGauge1.ScaleLinesMajorWidth = 2;
            this.aGauge1.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge1.ScaleLinesMinorInnerRadius = 55;
            this.aGauge1.ScaleLinesMinorNumOf = 9;
            this.aGauge1.ScaleLinesMinorOuterRadius = 60;
            this.aGauge1.ScaleLinesMinorWidth = 1;
            this.aGauge1.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge1.ScaleNumbersFormat = null;
            this.aGauge1.ScaleNumbersRadius = 38;
            this.aGauge1.ScaleNumbersRotation = 0;
            this.aGauge1.ScaleNumbersStartScaleLine = 1;
            this.aGauge1.ScaleNumbersStepScaleLines = 1;
            this.aGauge1.Size = new System.Drawing.Size(242, 242);
            this.aGauge1.TabIndex = 0;
            this.aGauge1.Value = 0F;
            this.aGauge1.Value0 = 0F;
            this.aGauge1.Value1 = 0F;
            this.aGauge1.Value2 = 0F;
            this.aGauge1.Value3 = 0F;
            // 
            // aGauge2
            // 
            this.aGauge2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge2.BackColor = System.Drawing.Color.Transparent;
            this.aGauge2.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge2.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge2.BaseArcRadius = 70;
            this.aGauge2.BaseArcStart = 135;
            this.aGauge2.BaseArcSweep = 270;
            this.aGauge2.BaseArcWidth = 2;
            this.aGauge2.Cap_Idx = ((byte)(0));
            this.aGauge2.CapColor = System.Drawing.Color.White;
            this.aGauge2.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge2.CapPosition = new System.Drawing.Point(67, 110);
            this.aGauge2.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(67, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge2.CapsText = new string[] {
        "N2",
        "",
        "",
        "",
        ""};
            this.aGauge2.CapText = "N2";
            this.aGauge2.Center = new System.Drawing.Point(75, 75);
            this.aGauge2.Location = new System.Drawing.Point(240, 0);
            this.aGauge2.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge2.MaxValue = 120F;
            this.aGauge2.MinValue = 0F;
            this.aGauge2.Name = "aGauge2";
            this.aGauge2.Need_Idx = ((byte)(3));
            this.aGauge2.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge2.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge2.NeedleEnabled = false;
            this.aGauge2.NeedleRadius = 70;
            this.aGauge2.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge2.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge2.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge2.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge2.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge2.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge2.NeedleType = 0;
            this.aGauge2.NeedleWidth = 2;
            this.aGauge2.Range_Idx = ((byte)(2));
            this.aGauge2.RangeColor = System.Drawing.Color.Red;
            this.aGauge2.RangeEnabled = true;
            this.aGauge2.RangeEndValue = 120F;
            this.aGauge2.RangeInnerRadius = 55;
            this.aGauge2.RangeOuterRadius = 60;
            this.aGauge2.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge2.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge2.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge2.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge2.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge2.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge2.RangeStartValue = 108F;
            this.aGauge2.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge2.ScaleLinesInterInnerRadius = 52;
            this.aGauge2.ScaleLinesInterOuterRadius = 60;
            this.aGauge2.ScaleLinesInterWidth = 1;
            this.aGauge2.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge2.ScaleLinesMajorInnerRadius = 50;
            this.aGauge2.ScaleLinesMajorOuterRadius = 60;
            this.aGauge2.ScaleLinesMajorStepValue = 20F;
            this.aGauge2.ScaleLinesMajorWidth = 2;
            this.aGauge2.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge2.ScaleLinesMinorInnerRadius = 55;
            this.aGauge2.ScaleLinesMinorNumOf = 9;
            this.aGauge2.ScaleLinesMinorOuterRadius = 60;
            this.aGauge2.ScaleLinesMinorWidth = 1;
            this.aGauge2.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge2.ScaleNumbersFormat = null;
            this.aGauge2.ScaleNumbersRadius = 38;
            this.aGauge2.ScaleNumbersRotation = 0;
            this.aGauge2.ScaleNumbersStartScaleLine = 1;
            this.aGauge2.ScaleNumbersStepScaleLines = 1;
            this.aGauge2.Size = new System.Drawing.Size(242, 242);
            this.aGauge2.TabIndex = 12;
            this.aGauge2.Value = 0F;
            this.aGauge2.Value0 = 0F;
            this.aGauge2.Value1 = 0F;
            this.aGauge2.Value2 = 0F;
            this.aGauge2.Value3 = 0F;
            // 
            // aGauge3
            // 
            this.aGauge3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge3.BackColor = System.Drawing.Color.Transparent;
            this.aGauge3.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge3.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge3.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge3.BaseArcRadius = 70;
            this.aGauge3.BaseArcStart = 135;
            this.aGauge3.BaseArcSweep = 270;
            this.aGauge3.BaseArcWidth = 2;
            this.aGauge3.Cap_Idx = ((byte)(0));
            this.aGauge3.CapColor = System.Drawing.Color.White;
            this.aGauge3.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge3.CapPosition = new System.Drawing.Point(58, 110);
            this.aGauge3.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(58, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge3.CapsText = new string[] {
        "Torque",
        "",
        "",
        "",
        ""};
            this.aGauge3.CapText = "Torque";
            this.aGauge3.Center = new System.Drawing.Point(75, 75);
            this.aGauge3.Location = new System.Drawing.Point(481, 0);
            this.aGauge3.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge3.MaxValue = 120F;
            this.aGauge3.MinValue = 0F;
            this.aGauge3.Name = "aGauge3";
            this.aGauge3.Need_Idx = ((byte)(3));
            this.aGauge3.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge3.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge3.NeedleEnabled = false;
            this.aGauge3.NeedleRadius = 70;
            this.aGauge3.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge3.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge3.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge3.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge3.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge3.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge3.NeedleType = 0;
            this.aGauge3.NeedleWidth = 2;
            this.aGauge3.Range_Idx = ((byte)(2));
            this.aGauge3.RangeColor = System.Drawing.Color.Red;
            this.aGauge3.RangeEnabled = true;
            this.aGauge3.RangeEndValue = 120F;
            this.aGauge3.RangeInnerRadius = 55;
            this.aGauge3.RangeOuterRadius = 60;
            this.aGauge3.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge3.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge3.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge3.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge3.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge3.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge3.RangeStartValue = 108F;
            this.aGauge3.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge3.ScaleLinesInterInnerRadius = 52;
            this.aGauge3.ScaleLinesInterOuterRadius = 60;
            this.aGauge3.ScaleLinesInterWidth = 1;
            this.aGauge3.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge3.ScaleLinesMajorInnerRadius = 50;
            this.aGauge3.ScaleLinesMajorOuterRadius = 60;
            this.aGauge3.ScaleLinesMajorStepValue = 20F;
            this.aGauge3.ScaleLinesMajorWidth = 2;
            this.aGauge3.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge3.ScaleLinesMinorInnerRadius = 55;
            this.aGauge3.ScaleLinesMinorNumOf = 9;
            this.aGauge3.ScaleLinesMinorOuterRadius = 60;
            this.aGauge3.ScaleLinesMinorWidth = 1;
            this.aGauge3.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge3.ScaleNumbersFormat = null;
            this.aGauge3.ScaleNumbersRadius = 38;
            this.aGauge3.ScaleNumbersRotation = 0;
            this.aGauge3.ScaleNumbersStartScaleLine = 1;
            this.aGauge3.ScaleNumbersStepScaleLines = 1;
            this.aGauge3.Size = new System.Drawing.Size(242, 242);
            this.aGauge3.TabIndex = 1;
            this.aGauge3.Value = 0F;
            this.aGauge3.Value0 = 0F;
            this.aGauge3.Value1 = 0F;
            this.aGauge3.Value2 = 0F;
            this.aGauge3.Value3 = 0F;
            // 
            // table_gauges2
            // 
            this.table_gauges2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.table_gauges2.ColumnCount = 5;
            this.table_gauges.SetColumnSpan(this.table_gauges2, 3);
            this.table_gauges2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.table_gauges2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.table_gauges2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.table_gauges2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.table_gauges2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.table_gauges2.Controls.Add(this.aGauge4, 0, 0);
            this.table_gauges2.Controls.Add(this.aGauge5, 1, 0);
            this.table_gauges2.Controls.Add(this.aGauge6, 2, 0);
            this.table_gauges2.Controls.Add(this.aGauge7, 3, 0);
            this.table_gauges2.Controls.Add(this.aGauge8, 4, 0);
            this.table_gauges2.Location = new System.Drawing.Point(0, 242);
            this.table_gauges2.Margin = new System.Windows.Forms.Padding(0);
            this.table_gauges2.Name = "table_gauges2";
            this.table_gauges2.RowCount = 1;
            this.table_gauges2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.table_gauges2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 146F));
            this.table_gauges2.Size = new System.Drawing.Size(723, 146);
            this.table_gauges2.TabIndex = 14;
            // 
            // aGauge4
            // 
            this.aGauge4.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge4.BackColor = System.Drawing.Color.Transparent;
            this.aGauge4.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge4.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge4.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge4.BaseArcRadius = 70;
            this.aGauge4.BaseArcStart = 135;
            this.aGauge4.BaseArcSweep = 270;
            this.aGauge4.BaseArcWidth = 2;
            this.aGauge4.Cap_Idx = ((byte)(0));
            this.aGauge4.CapColor = System.Drawing.Color.White;
            this.aGauge4.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge4.CapPosition = new System.Drawing.Point(63, 110);
            this.aGauge4.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(63, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge4.CapsText = new string[] {
        "EGT",
        "",
        "",
        "",
        ""};
            this.aGauge4.CapText = "EGT";
            this.aGauge4.Center = new System.Drawing.Point(75, 75);
            this.aGauge4.Location = new System.Drawing.Point(0, 0);
            this.aGauge4.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge4.MaxValue = 1200F;
            this.aGauge4.MinValue = 0F;
            this.aGauge4.Name = "aGauge4";
            this.aGauge4.Need_Idx = ((byte)(3));
            this.aGauge4.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge4.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge4.NeedleEnabled = false;
            this.aGauge4.NeedleRadius = 70;
            this.aGauge4.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge4.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge4.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge4.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge4.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge4.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge4.NeedleType = 0;
            this.aGauge4.NeedleWidth = 2;
            this.aGauge4.Range_Idx = ((byte)(2));
            this.aGauge4.RangeColor = System.Drawing.Color.Red;
            this.aGauge4.RangeEnabled = true;
            this.aGauge4.RangeEndValue = 1200F;
            this.aGauge4.RangeInnerRadius = 55;
            this.aGauge4.RangeOuterRadius = 60;
            this.aGauge4.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge4.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge4.RangesEndValue = new float[] {
        1200F,
        1200F,
        1200F,
        0F,
        0F};
            this.aGauge4.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge4.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge4.RangesStartValue = new float[] {
        150F,
        600F,
        650F,
        0F,
        0F};
            this.aGauge4.RangeStartValue = 650F;
            this.aGauge4.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge4.ScaleLinesInterInnerRadius = 52;
            this.aGauge4.ScaleLinesInterOuterRadius = 60;
            this.aGauge4.ScaleLinesInterWidth = 1;
            this.aGauge4.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge4.ScaleLinesMajorInnerRadius = 50;
            this.aGauge4.ScaleLinesMajorOuterRadius = 60;
            this.aGauge4.ScaleLinesMajorStepValue = 200F;
            this.aGauge4.ScaleLinesMajorWidth = 2;
            this.aGauge4.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge4.ScaleLinesMinorInnerRadius = 55;
            this.aGauge4.ScaleLinesMinorNumOf = 9;
            this.aGauge4.ScaleLinesMinorOuterRadius = 60;
            this.aGauge4.ScaleLinesMinorWidth = 1;
            this.aGauge4.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge4.ScaleNumbersFormat = null;
            this.aGauge4.ScaleNumbersRadius = 34;
            this.aGauge4.ScaleNumbersRotation = 0;
            this.aGauge4.ScaleNumbersStartScaleLine = 1;
            this.aGauge4.ScaleNumbersStepScaleLines = 1;
            this.aGauge4.Size = new System.Drawing.Size(146, 146);
            this.aGauge4.TabIndex = 2;
            this.aGauge4.Value = 400F;
            this.aGauge4.Value0 = 0F;
            this.aGauge4.Value1 = 0F;
            this.aGauge4.Value2 = 0F;
            this.aGauge4.Value3 = 400F;
            // 
            // aGauge5
            // 
            this.aGauge5.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge5.BackColor = System.Drawing.Color.Transparent;
            this.aGauge5.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge5.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge5.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge5.BaseArcRadius = 70;
            this.aGauge5.BaseArcStart = 135;
            this.aGauge5.BaseArcSweep = 270;
            this.aGauge5.BaseArcWidth = 2;
            this.aGauge5.Cap_Idx = ((byte)(0));
            this.aGauge5.CapColor = System.Drawing.Color.White;
            this.aGauge5.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge5.CapPosition = new System.Drawing.Point(52, 110);
            this.aGauge5.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(52, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge5.CapsText = new string[] {
        "Oil Temp.",
        "",
        "",
        "",
        ""};
            this.aGauge5.CapText = "Oil Temp.";
            this.aGauge5.Center = new System.Drawing.Point(75, 75);
            this.aGauge5.Location = new System.Drawing.Point(144, 0);
            this.aGauge5.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge5.MaxValue = 120F;
            this.aGauge5.MinValue = 0F;
            this.aGauge5.Name = "aGauge5";
            this.aGauge5.Need_Idx = ((byte)(3));
            this.aGauge5.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge5.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge5.NeedleEnabled = false;
            this.aGauge5.NeedleRadius = 70;
            this.aGauge5.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge5.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge5.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge5.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge5.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge5.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge5.NeedleType = 0;
            this.aGauge5.NeedleWidth = 2;
            this.aGauge5.Range_Idx = ((byte)(2));
            this.aGauge5.RangeColor = System.Drawing.Color.Red;
            this.aGauge5.RangeEnabled = true;
            this.aGauge5.RangeEndValue = 120F;
            this.aGauge5.RangeInnerRadius = 55;
            this.aGauge5.RangeOuterRadius = 60;
            this.aGauge5.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge5.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge5.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge5.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge5.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge5.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge5.RangeStartValue = 108F;
            this.aGauge5.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge5.ScaleLinesInterInnerRadius = 52;
            this.aGauge5.ScaleLinesInterOuterRadius = 60;
            this.aGauge5.ScaleLinesInterWidth = 1;
            this.aGauge5.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge5.ScaleLinesMajorInnerRadius = 50;
            this.aGauge5.ScaleLinesMajorOuterRadius = 60;
            this.aGauge5.ScaleLinesMajorStepValue = 20F;
            this.aGauge5.ScaleLinesMajorWidth = 2;
            this.aGauge5.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge5.ScaleLinesMinorInnerRadius = 55;
            this.aGauge5.ScaleLinesMinorNumOf = 9;
            this.aGauge5.ScaleLinesMinorOuterRadius = 60;
            this.aGauge5.ScaleLinesMinorWidth = 1;
            this.aGauge5.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge5.ScaleNumbersFormat = null;
            this.aGauge5.ScaleNumbersRadius = 38;
            this.aGauge5.ScaleNumbersRotation = 0;
            this.aGauge5.ScaleNumbersStartScaleLine = 1;
            this.aGauge5.ScaleNumbersStepScaleLines = 1;
            this.aGauge5.Size = new System.Drawing.Size(146, 146);
            this.aGauge5.TabIndex = 13;
            this.aGauge5.Value = 0F;
            this.aGauge5.Value0 = 0F;
            this.aGauge5.Value1 = 0F;
            this.aGauge5.Value2 = 0F;
            this.aGauge5.Value3 = 0F;
            // 
            // aGauge6
            // 
            this.aGauge6.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge6.BackColor = System.Drawing.Color.Transparent;
            this.aGauge6.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge6.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge6.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge6.BaseArcRadius = 70;
            this.aGauge6.BaseArcStart = 135;
            this.aGauge6.BaseArcSweep = 270;
            this.aGauge6.BaseArcWidth = 2;
            this.aGauge6.Cap_Idx = ((byte)(0));
            this.aGauge6.CapColor = System.Drawing.Color.White;
            this.aGauge6.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge6.CapPosition = new System.Drawing.Point(52, 110);
            this.aGauge6.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(52, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge6.CapsText = new string[] {
        "Oil Press.",
        "",
        "",
        "",
        ""};
            this.aGauge6.CapText = "Oil Press.";
            this.aGauge6.Center = new System.Drawing.Point(75, 75);
            this.aGauge6.Location = new System.Drawing.Point(288, 0);
            this.aGauge6.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge6.MaxValue = 120F;
            this.aGauge6.MinValue = 0F;
            this.aGauge6.Name = "aGauge6";
            this.aGauge6.Need_Idx = ((byte)(3));
            this.aGauge6.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge6.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge6.NeedleEnabled = false;
            this.aGauge6.NeedleRadius = 70;
            this.aGauge6.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge6.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge6.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge6.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge6.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge6.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge6.NeedleType = 0;
            this.aGauge6.NeedleWidth = 2;
            this.aGauge6.Range_Idx = ((byte)(2));
            this.aGauge6.RangeColor = System.Drawing.Color.Red;
            this.aGauge6.RangeEnabled = true;
            this.aGauge6.RangeEndValue = 120F;
            this.aGauge6.RangeInnerRadius = 55;
            this.aGauge6.RangeOuterRadius = 60;
            this.aGauge6.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge6.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge6.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge6.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge6.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge6.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge6.RangeStartValue = 108F;
            this.aGauge6.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge6.ScaleLinesInterInnerRadius = 52;
            this.aGauge6.ScaleLinesInterOuterRadius = 60;
            this.aGauge6.ScaleLinesInterWidth = 1;
            this.aGauge6.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge6.ScaleLinesMajorInnerRadius = 50;
            this.aGauge6.ScaleLinesMajorOuterRadius = 60;
            this.aGauge6.ScaleLinesMajorStepValue = 20F;
            this.aGauge6.ScaleLinesMajorWidth = 2;
            this.aGauge6.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge6.ScaleLinesMinorInnerRadius = 55;
            this.aGauge6.ScaleLinesMinorNumOf = 9;
            this.aGauge6.ScaleLinesMinorOuterRadius = 60;
            this.aGauge6.ScaleLinesMinorWidth = 1;
            this.aGauge6.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge6.ScaleNumbersFormat = null;
            this.aGauge6.ScaleNumbersRadius = 38;
            this.aGauge6.ScaleNumbersRotation = 0;
            this.aGauge6.ScaleNumbersStartScaleLine = 1;
            this.aGauge6.ScaleNumbersStepScaleLines = 1;
            this.aGauge6.Size = new System.Drawing.Size(146, 146);
            this.aGauge6.TabIndex = 3;
            this.aGauge6.Value = 0F;
            this.aGauge6.Value0 = 0F;
            this.aGauge6.Value1 = 0F;
            this.aGauge6.Value2 = 0F;
            this.aGauge6.Value3 = 0F;
            // 
            // aGauge7
            // 
            this.aGauge7.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge7.BackColor = System.Drawing.Color.Transparent;
            this.aGauge7.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge7.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge7.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge7.BaseArcRadius = 70;
            this.aGauge7.BaseArcStart = 135;
            this.aGauge7.BaseArcSweep = 270;
            this.aGauge7.BaseArcWidth = 2;
            this.aGauge7.Cap_Idx = ((byte)(0));
            this.aGauge7.CapColor = System.Drawing.Color.White;
            this.aGauge7.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge7.CapPosition = new System.Drawing.Point(48, 110);
            this.aGauge7.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(48, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge7.CapsText = new string[] {
        "Fuel Press.",
        "",
        "",
        "",
        ""};
            this.aGauge7.CapText = "Fuel Press.";
            this.aGauge7.Center = new System.Drawing.Point(75, 75);
            this.aGauge7.Location = new System.Drawing.Point(432, 0);
            this.aGauge7.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge7.MaxValue = 120F;
            this.aGauge7.MinValue = 0F;
            this.aGauge7.Name = "aGauge7";
            this.aGauge7.Need_Idx = ((byte)(3));
            this.aGauge7.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge7.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge7.NeedleEnabled = false;
            this.aGauge7.NeedleRadius = 70;
            this.aGauge7.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge7.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge7.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge7.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge7.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge7.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge7.NeedleType = 0;
            this.aGauge7.NeedleWidth = 2;
            this.aGauge7.Range_Idx = ((byte)(2));
            this.aGauge7.RangeColor = System.Drawing.Color.Red;
            this.aGauge7.RangeEnabled = true;
            this.aGauge7.RangeEndValue = 120F;
            this.aGauge7.RangeInnerRadius = 55;
            this.aGauge7.RangeOuterRadius = 60;
            this.aGauge7.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge7.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge7.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge7.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge7.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge7.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge7.RangeStartValue = 108F;
            this.aGauge7.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge7.ScaleLinesInterInnerRadius = 52;
            this.aGauge7.ScaleLinesInterOuterRadius = 60;
            this.aGauge7.ScaleLinesInterWidth = 1;
            this.aGauge7.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge7.ScaleLinesMajorInnerRadius = 50;
            this.aGauge7.ScaleLinesMajorOuterRadius = 60;
            this.aGauge7.ScaleLinesMajorStepValue = 20F;
            this.aGauge7.ScaleLinesMajorWidth = 2;
            this.aGauge7.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge7.ScaleLinesMinorInnerRadius = 55;
            this.aGauge7.ScaleLinesMinorNumOf = 9;
            this.aGauge7.ScaleLinesMinorOuterRadius = 60;
            this.aGauge7.ScaleLinesMinorWidth = 1;
            this.aGauge7.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge7.ScaleNumbersFormat = null;
            this.aGauge7.ScaleNumbersRadius = 38;
            this.aGauge7.ScaleNumbersRotation = 0;
            this.aGauge7.ScaleNumbersStartScaleLine = 1;
            this.aGauge7.ScaleNumbersStepScaleLines = 1;
            this.aGauge7.Size = new System.Drawing.Size(146, 146);
            this.aGauge7.TabIndex = 14;
            this.aGauge7.Value = 0F;
            this.aGauge7.Value0 = 0F;
            this.aGauge7.Value1 = 0F;
            this.aGauge7.Value2 = 0F;
            this.aGauge7.Value3 = 0F;
            // 
            // aGauge8
            // 
            this.aGauge8.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.aGauge8.BackColor = System.Drawing.Color.Transparent;
            this.aGauge8.BackgroundImage = global::ACCTurbineStatus.Properties.Resources.Gaugebg;
            this.aGauge8.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.aGauge8.BaseArcColor = System.Drawing.Color.Transparent;
            this.aGauge8.BaseArcRadius = 70;
            this.aGauge8.BaseArcStart = 135;
            this.aGauge8.BaseArcSweep = 270;
            this.aGauge8.BaseArcWidth = 2;
            this.aGauge8.Cap_Idx = ((byte)(0));
            this.aGauge8.CapColor = System.Drawing.Color.White;
            this.aGauge8.CapColors = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black,
        System.Drawing.Color.Black};
            this.aGauge8.CapPosition = new System.Drawing.Point(50, 110);
            this.aGauge8.CapsPosition = new System.Drawing.Point[] {
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(50, 110),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10),
        new System.Drawing.Point(10, 10)};
            this.aGauge8.CapsText = new string[] {
        "Fuel Flow",
        "",
        "",
        "",
        ""};
            this.aGauge8.CapText = "Fuel Flow";
            this.aGauge8.Center = new System.Drawing.Point(75, 75);
            this.aGauge8.Location = new System.Drawing.Point(576, 0);
            this.aGauge8.Margin = new System.Windows.Forms.Padding(0);
            this.aGauge8.MaxValue = 120F;
            this.aGauge8.MinValue = 0F;
            this.aGauge8.Name = "aGauge8";
            this.aGauge8.Need_Idx = ((byte)(3));
            this.aGauge8.NeedleColor1 = AGaugeApp.AGauge.NeedleColorEnum.Gray;
            this.aGauge8.NeedleColor2 = System.Drawing.Color.Brown;
            this.aGauge8.NeedleEnabled = false;
            this.aGauge8.NeedleRadius = 70;
            this.aGauge8.NeedlesColor1 = new AGaugeApp.AGauge.NeedleColorEnum[] {
        AGaugeApp.AGauge.NeedleColorEnum.Gray,
        AGaugeApp.AGauge.NeedleColorEnum.Red,
        AGaugeApp.AGauge.NeedleColorEnum.Blue,
        AGaugeApp.AGauge.NeedleColorEnum.Gray};
            this.aGauge8.NeedlesColor2 = new System.Drawing.Color[] {
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.White,
        System.Drawing.Color.Brown};
            this.aGauge8.NeedlesEnabled = new bool[] {
        true,
        false,
        false,
        false};
            this.aGauge8.NeedlesRadius = new int[] {
        50,
        50,
        70,
        70};
            this.aGauge8.NeedlesType = new int[] {
        0,
        0,
        0,
        0};
            this.aGauge8.NeedlesWidth = new int[] {
        2,
        1,
        2,
        2};
            this.aGauge8.NeedleType = 0;
            this.aGauge8.NeedleWidth = 2;
            this.aGauge8.Range_Idx = ((byte)(2));
            this.aGauge8.RangeColor = System.Drawing.Color.Red;
            this.aGauge8.RangeEnabled = true;
            this.aGauge8.RangeEndValue = 120F;
            this.aGauge8.RangeInnerRadius = 55;
            this.aGauge8.RangeOuterRadius = 60;
            this.aGauge8.RangesColor = new System.Drawing.Color[] {
        System.Drawing.Color.Green,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Red,
        System.Drawing.SystemColors.Control,
        System.Drawing.SystemColors.Control};
            this.aGauge8.RangesEnabled = new bool[] {
        true,
        true,
        true,
        false,
        false};
            this.aGauge8.RangesEndValue = new float[] {
        120F,
        120F,
        120F,
        0F,
        0F};
            this.aGauge8.RangesInnerRadius = new int[] {
        55,
        55,
        55,
        70,
        70};
            this.aGauge8.RangesOuterRadius = new int[] {
        60,
        60,
        60,
        80,
        80};
            this.aGauge8.RangesStartValue = new float[] {
        50F,
        102F,
        108F,
        0F,
        0F};
            this.aGauge8.RangeStartValue = 108F;
            this.aGauge8.ScaleLinesInterColor = System.Drawing.Color.White;
            this.aGauge8.ScaleLinesInterInnerRadius = 52;
            this.aGauge8.ScaleLinesInterOuterRadius = 60;
            this.aGauge8.ScaleLinesInterWidth = 1;
            this.aGauge8.ScaleLinesMajorColor = System.Drawing.Color.White;
            this.aGauge8.ScaleLinesMajorInnerRadius = 50;
            this.aGauge8.ScaleLinesMajorOuterRadius = 60;
            this.aGauge8.ScaleLinesMajorStepValue = 20F;
            this.aGauge8.ScaleLinesMajorWidth = 2;
            this.aGauge8.ScaleLinesMinorColor = System.Drawing.Color.White;
            this.aGauge8.ScaleLinesMinorInnerRadius = 55;
            this.aGauge8.ScaleLinesMinorNumOf = 9;
            this.aGauge8.ScaleLinesMinorOuterRadius = 60;
            this.aGauge8.ScaleLinesMinorWidth = 1;
            this.aGauge8.ScaleNumbersColor = System.Drawing.Color.White;
            this.aGauge8.ScaleNumbersFormat = null;
            this.aGauge8.ScaleNumbersRadius = 38;
            this.aGauge8.ScaleNumbersRotation = 0;
            this.aGauge8.ScaleNumbersStartScaleLine = 1;
            this.aGauge8.ScaleNumbersStepScaleLines = 1;
            this.aGauge8.Size = new System.Drawing.Size(146, 146);
            this.aGauge8.TabIndex = 15;
            this.aGauge8.Value = 0F;
            this.aGauge8.Value0 = 0F;
            this.aGauge8.Value1 = 0F;
            this.aGauge8.Value2 = 0F;
            this.aGauge8.Value3 = 0F;
            // 
            // table_main
            // 
            this.table_main.ColumnCount = 1;
            this.table_main.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.table_main.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.table_main.Controls.Add(this.table_gauges, 0, 0);
            this.table_main.Controls.Add(this.txt_messages, 0, 1);
            this.table_main.Dock = System.Windows.Forms.DockStyle.Fill;
            this.table_main.Location = new System.Drawing.Point(0, 0);
            this.table_main.Name = "table_main";
            this.table_main.RowCount = 2;
            this.table_main.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.table_main.Size = new System.Drawing.Size(723, 508);
            this.table_main.TabIndex = 16;
            // 
            // lbl_ecumode
            // 
            this.lbl_ecumode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lbl_ecumode.AutoSize = true;
            this.table_control.SetColumnSpan(this.lbl_ecumode, 2);
            this.lbl_ecumode.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_ecumode.Location = new System.Drawing.Point(3, 0);
            this.lbl_ecumode.Name = "lbl_ecumode";
            this.lbl_ecumode.Size = new System.Drawing.Size(219, 31);
            this.lbl_ecumode.TabIndex = 0;
            this.lbl_ecumode.Text = "ECU: False Start";
            // 
            // lbl_turbinemode
            // 
            this.lbl_turbinemode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lbl_turbinemode.AutoSize = true;
            this.table_control.SetColumnSpan(this.lbl_turbinemode, 2);
            this.lbl_turbinemode.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_turbinemode.Location = new System.Drawing.Point(3, 31);
            this.lbl_turbinemode.Name = "lbl_turbinemode";
            this.lbl_turbinemode.Size = new System.Drawing.Size(188, 31);
            this.lbl_turbinemode.TabIndex = 1;
            this.lbl_turbinemode.Text = "Turbine: Flight";
            // 
            // table_control
            // 
            this.table_control.ColumnCount = 2;
            this.table_control.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.table_control.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.table_control.Controls.Add(this.lbl_ecumode, 0, 0);
            this.table_control.Controls.Add(this.lbl_turbinemode, 0, 1);
            this.table_control.Controls.Add(this.label3, 0, 3);
            this.table_control.Controls.Add(this.cmb_mode, 0, 4);
            this.table_control.Controls.Add(this.label4, 0, 7);
            this.table_control.Controls.Add(this.but_setmode, 0, 5);
            this.table_control.Controls.Add(this.but_mainpump, 0, 8);
            this.table_control.Controls.Add(this.led_mainpump, 1, 8);
            this.table_control.Controls.Add(this.but_empump, 0, 9);
            this.table_control.Controls.Add(this.led_empump, 1, 9);
            this.table_control.Controls.Add(this.but_alternatorconn, 0, 10);
            this.table_control.Controls.Add(this.led_alternatorconn, 1, 10);
            this.table_control.Controls.Add(this.but_alternator, 0, 11);
            this.table_control.Controls.Add(this.led_alternator, 1, 11);
            this.table_control.Controls.Add(this.but_oilcooler, 0, 12);
            this.table_control.Controls.Add(this.led_oilcooler, 1, 12);
            this.table_control.Controls.Add(this.but_totalstop, 0, 14);
            this.table_control.Controls.Add(this.led_totalstop, 1, 14);
            this.table_control.Dock = System.Windows.Forms.DockStyle.Fill;
            this.table_control.Location = new System.Drawing.Point(0, 0);
            this.table_control.MaximumSize = new System.Drawing.Size(0, 508);
            this.table_control.Name = "table_control";
            this.table_control.RowCount = 16;
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.50328F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.49953F));
            this.table_control.Size = new System.Drawing.Size(285, 508);
            this.table_control.TabIndex = 16;
            this.table_control.Resize += new System.EventHandler(this.table_control_Resize);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 82);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Set Mode";
            // 
            // cmb_mode
            // 
            this.cmb_mode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.cmb_mode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmb_mode.FormattingEnabled = true;
            this.cmb_mode.Items.AddRange(new object[] {
            "False Start",
            "Cold Start",
            "Start",
            "Stop",
            "Cool",
            "Idle 1",
            "Idle 2",
            "Flight"});
            this.cmb_mode.Location = new System.Drawing.Point(3, 98);
            this.cmb_mode.Name = "cmb_mode";
            this.cmb_mode.Size = new System.Drawing.Size(250, 21);
            this.cmb_mode.TabIndex = 22;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 183);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(39, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Relays";
            // 
            // but_setmode
            // 
            this.but_setmode.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_setmode.Location = new System.Drawing.Point(3, 125);
            this.but_setmode.Name = "but_setmode";
            this.but_setmode.Size = new System.Drawing.Size(250, 35);
            this.but_setmode.TabIndex = 5;
            this.but_setmode.Text = "Set Mode";
            this.but_setmode.UseVisualStyleBackColor = true;
            this.but_setmode.Click += new System.EventHandler(this.but_setmode_Click);
            // 
            // but_mainpump
            // 
            this.but_mainpump.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_mainpump.Location = new System.Drawing.Point(3, 199);
            this.but_mainpump.Name = "but_mainpump";
            this.but_mainpump.Size = new System.Drawing.Size(250, 35);
            this.but_mainpump.TabIndex = 14;
            this.but_mainpump.Text = "Main Pump";
            this.but_mainpump.UseVisualStyleBackColor = true;
            this.but_mainpump.Click += new System.EventHandler(this.but_mainpump_Click);
            // 
            // led_mainpump
            // 
            this.led_mainpump.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_mainpump.Location = new System.Drawing.Point(259, 205);
            this.led_mainpump.Name = "led_mainpump";
            this.led_mainpump.On = false;
            this.led_mainpump.Size = new System.Drawing.Size(23, 23);
            this.led_mainpump.TabIndex = 17;
            this.led_mainpump.Text = "ledBulb1";
            // 
            // but_empump
            // 
            this.but_empump.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_empump.Location = new System.Drawing.Point(3, 240);
            this.but_empump.Name = "but_empump";
            this.but_empump.Size = new System.Drawing.Size(250, 35);
            this.but_empump.TabIndex = 15;
            this.but_empump.Text = "Emergency Pump";
            this.but_empump.UseVisualStyleBackColor = true;
            this.but_empump.Click += new System.EventHandler(this.but_empump_Click);
            // 
            // led_empump
            // 
            this.led_empump.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_empump.Location = new System.Drawing.Point(259, 246);
            this.led_empump.Name = "led_empump";
            this.led_empump.On = false;
            this.led_empump.Size = new System.Drawing.Size(23, 23);
            this.led_empump.TabIndex = 18;
            this.led_empump.Text = "ledBulb2";
            // 
            // but_alternatorconn
            // 
            this.but_alternatorconn.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_alternatorconn.Location = new System.Drawing.Point(3, 281);
            this.but_alternatorconn.Name = "but_alternatorconn";
            this.but_alternatorconn.Size = new System.Drawing.Size(250, 35);
            this.but_alternatorconn.TabIndex = 12;
            this.but_alternatorconn.Text = "Alternator Connection";
            this.but_alternatorconn.UseVisualStyleBackColor = true;
            this.but_alternatorconn.Click += new System.EventHandler(this.but_alternatorconn_Click);
            // 
            // led_alternatorconn
            // 
            this.led_alternatorconn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_alternatorconn.Location = new System.Drawing.Point(259, 287);
            this.led_alternatorconn.Name = "led_alternatorconn";
            this.led_alternatorconn.On = false;
            this.led_alternatorconn.Size = new System.Drawing.Size(23, 23);
            this.led_alternatorconn.TabIndex = 19;
            this.led_alternatorconn.Text = "ledBulb3";
            // 
            // but_alternator
            // 
            this.but_alternator.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_alternator.Location = new System.Drawing.Point(3, 322);
            this.but_alternator.Name = "but_alternator";
            this.but_alternator.Size = new System.Drawing.Size(250, 35);
            this.but_alternator.TabIndex = 13;
            this.but_alternator.Text = "Alternator";
            this.but_alternator.UseVisualStyleBackColor = true;
            this.but_alternator.Click += new System.EventHandler(this.but_alternator_Click);
            // 
            // led_alternator
            // 
            this.led_alternator.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_alternator.Location = new System.Drawing.Point(259, 328);
            this.led_alternator.Name = "led_alternator";
            this.led_alternator.On = false;
            this.led_alternator.Size = new System.Drawing.Size(23, 23);
            this.led_alternator.TabIndex = 20;
            this.led_alternator.Text = "ledBulb4";
            // 
            // but_oilcooler
            // 
            this.but_oilcooler.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_oilcooler.Location = new System.Drawing.Point(3, 363);
            this.but_oilcooler.Name = "but_oilcooler";
            this.but_oilcooler.Size = new System.Drawing.Size(250, 35);
            this.but_oilcooler.TabIndex = 23;
            this.but_oilcooler.Text = "Oil Cooler";
            this.but_oilcooler.UseVisualStyleBackColor = true;
            this.but_oilcooler.Click += new System.EventHandler(this.but_oilcooler_Click);
            // 
            // led_oilcooler
            // 
            this.led_oilcooler.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_oilcooler.Location = new System.Drawing.Point(259, 369);
            this.led_oilcooler.Name = "led_oilcooler";
            this.led_oilcooler.On = false;
            this.led_oilcooler.Size = new System.Drawing.Size(23, 23);
            this.led_oilcooler.TabIndex = 24;
            this.led_oilcooler.Text = "ledBulb4";
            // 
            // but_totalstop
            // 
            this.but_totalstop.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.but_totalstop.Location = new System.Drawing.Point(3, 424);
            this.but_totalstop.Name = "but_totalstop";
            this.but_totalstop.Size = new System.Drawing.Size(250, 35);
            this.but_totalstop.TabIndex = 16;
            this.but_totalstop.Text = "Emergency Stop";
            this.but_totalstop.UseVisualStyleBackColor = true;
            this.but_totalstop.Click += new System.EventHandler(this.but_totalstop_Click);
            // 
            // led_totalstop
            // 
            this.led_totalstop.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.led_totalstop.Location = new System.Drawing.Point(259, 430);
            this.led_totalstop.Name = "led_totalstop";
            this.led_totalstop.On = false;
            this.led_totalstop.Size = new System.Drawing.Size(23, 23);
            this.led_totalstop.TabIndex = 21;
            this.led_totalstop.Text = "ledBulb5";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.table_main);
            this.splitContainer1.Panel1.Resize += new System.EventHandler(this.table_gauges_Resize);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.table_control);
            this.splitContainer1.Size = new System.Drawing.Size(1012, 508);
            this.splitContainer1.SplitterDistance = 723;
            this.splitContainer1.TabIndex = 16;
            // 
            // ui_timer
            // 
            this.ui_timer.Enabled = true;
            this.ui_timer.Tick += new System.EventHandler(this.ui_timer_Tick);
            // 
            // TurbineStatusUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1012, 508);
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "TurbineStatusUI";
            this.ShowIcon = false;
            this.Text = "TurbineStatusUI";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TurbineStatusUI_FormClosing);
            this.Resize += new System.EventHandler(this.table_gauges_Resize);
            this.table_gauges.ResumeLayout(false);
            this.table_gauges2.ResumeLayout(false);
            this.table_main.ResumeLayout(false);
            this.table_main.PerformLayout();
            this.table_control.ResumeLayout(false);
            this.table_control.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AGaugeApp.AGauge aGauge1;
        private AGaugeApp.AGauge aGauge3;
        private AGaugeApp.AGauge aGauge4;
        private AGaugeApp.AGauge aGauge6;
        private System.Windows.Forms.TextBox txt_messages;
        private AGaugeApp.AGauge aGauge2;
        private AGaugeApp.AGauge aGauge5;
        private System.Windows.Forms.TableLayoutPanel table_gauges;
        private System.Windows.Forms.TableLayoutPanel table_main;
        private System.Windows.Forms.Label lbl_ecumode;
        private System.Windows.Forms.Label lbl_turbinemode;
        private System.Windows.Forms.TableLayoutPanel table_control;
        private MissionPlanner.Controls.MyButton but_setmode;
        private System.Windows.Forms.Label label3;
        private MissionPlanner.Controls.MyButton but_alternator;
        private MissionPlanner.Controls.MyButton but_alternatorconn;
        private MissionPlanner.Controls.MyButton but_empump;
        private MissionPlanner.Controls.MyButton but_mainpump;
        private System.Windows.Forms.Label label4;
        private MissionPlanner.Controls.MyButton but_totalstop;
        private Bulb.LedBulb led_mainpump;
        private Bulb.LedBulb led_empump;
        private Bulb.LedBulb led_alternatorconn;
        private Bulb.LedBulb led_alternator;
        private Bulb.LedBulb led_totalstop;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel table_gauges2;
        private AGaugeApp.AGauge aGauge7;
        private AGaugeApp.AGauge aGauge8;
        private System.Windows.Forms.ComboBox cmb_mode;
        private MissionPlanner.Controls.MyButton but_oilcooler;
        private Bulb.LedBulb led_oilcooler;
        private System.Windows.Forms.Timer ui_timer;
    }
}