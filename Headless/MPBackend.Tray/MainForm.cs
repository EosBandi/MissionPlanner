using MPBackend.Core;
using MPBackend.Core.Links;

namespace MPBackend.Tray;

/// <summary>
/// Status window: host state and URLs, the links and vehicles the backend knows about (read-only; links are
/// created through the API) and the last log lines. Closing or minimising hides it to the tray.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TrayContext context;
    private readonly ServerHost host;
    private readonly TrayOptions options;

    private readonly Label lblState = Value();
    private readonly Label lblUrls = Value();
    private readonly Label lblData = Value();
    private readonly Label lblUptime = Value();
    private readonly Button btnStartStop = Btn("Start");
    private readonly Button btnDev = Btn("Dev console");
    private readonly Button btnSwagger = Btn("Swagger");
    private readonly Button btnCopy = Btn("Copy URL");
    private readonly Button btnSettings = Btn("Settings…");
    private readonly DataGridView gridLinks = Grid();
    private readonly DataGridView gridVehicles = Grid();
    private readonly ListBox lstLog = new()
    {
        Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true,
        Font = new Font(FontFamily.GenericMonospace, 9f), SelectionMode = SelectionMode.One
    };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    private string linksKey = "";
    private string vehiclesKey = "";
    private readonly Action<string, object> onEvent;
    private readonly Action<LogLine> onLog;
    private readonly Action onHostChanged;

    /// <summary>Set by the tray context on exit; otherwise closing only hides the window.</summary>
    public bool AllowClose { get; set; }

    public MainForm(TrayContext context, ServerHost host, TrayOptions options, Icon icon)
    {
        this.context = context;
        this.host = host;
        this.options = options;

        Text = "Mission Planner Headless Backend";
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 680);
        MinimumSize = new Size(720, 480);
        Build();

        btnStartStop.Click += (_, _) => _ = context.ToggleServerAsync();
        btnDev.Click += (_, _) => context.OpenPage("/dev.html");
        btnSwagger.Click += (_, _) => context.OpenPage("/swagger");
        btnCopy.Click += (_, _) => context.CopyUrl();
        btnSettings.Click += (_, _) => _ = context.ShowSettingsAsync();

        onHostChanged = () => Post(RefreshHeader);
        onLog = line => Post(() => AppendLog(line));
        onEvent = OnBackendEvent;
        host.Changed += onHostChanged;
        LogBuffer.Added += onLog;
        host.Backend.Events.Published += onEvent;

        lstLog.BeginUpdate();
        foreach (var line in LogBuffer.Snapshot())
            lstLog.Items.Add(line.ToString());
        lstLog.EndUpdate();
        ScrollLog();

        timer.Tick += (_, _) =>
        {
            RefreshUptime();
            RefreshGrids();
        };
        timer.Start();
        RefreshHeader();
        RefreshGrids();
    }

    private void Build()
    {
        // header rows and the button strip dock to the top; the grids and the log share the rest
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4, Padding = new Padding(8, 8, 8, 0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(header, 0, "API", lblState);
        AddRow(header, 1, "URL", lblUrls);
        AddRow(header, 2, "Data", lblData);
        AddRow(header, 3, "Uptime", lblUptime);
        // wrap long values (the URL list) inside the value column instead of widening the window
        header.SizeChanged += (_, _) =>
        {
            var widths = header.GetColumnWidths();
            if (widths.Length < 2)
                return;
            var max = new Size(Math.Max(120, widths[1] - 8), 0);
            foreach (var label in new[] { lblState, lblUrls, lblData, lblUptime })
                label.MaximumSize = max;
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8, 4, 8, 4) };
        buttons.Controls.AddRange([btnStartStop, btnDev, btnSwagger, btnCopy, btnSettings]);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8, 0, 8, 8) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        root.Controls.Add(Group("Links  (create with POST /api/v1/links, remove with DELETE /api/v1/links/{id})", gridLinks), 0, 0);
        root.Controls.Add(Group("Vehicles", gridVehicles), 0, 1);
        root.Controls.Add(Group("Log", lstLog), 0, 2);

        // docking is resolved from the last-added control: header at the top, buttons under it, root fills the rest
        Controls.Add(root);
        Controls.Add(buttons);
        Controls.Add(header);
    }

    private static void AddRow(TableLayoutPanel table, int row, string key, Control value)
    {
        table.Controls.Add(new Label { Text = key, AutoSize = true, Margin = new Padding(0, 4, 8, 4), Font = new Font(DefaultFont, FontStyle.Bold) }, 0, row);
        table.Controls.Add(value, 1, row);
    }

    /// <summary>Buttons size to their (DPI-scaled) font instead of the 23 px WinForms default.</summary>
    private static Button Btn(string text) => new()
    {
        Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(110, 0),
        Padding = new Padding(6, 3, 6, 3), Margin = new Padding(0, 0, 8, 0)
    };

    private static Label Value() => new() { AutoSize = true, Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left | AnchorStyles.Top };

    private static GroupBox Group(string title, Control content)
    {
        var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6) };
        box.Controls.Add(content);
        return box;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None
    };

    // ---- data

    private sealed record LinkRow(int Id, string Connection, string State, string Vehicles, string Connected, string Error, string Tlog);

    private sealed record VehicleRow(int Link, string Vehicle, string Type, string Firmware, string Mode, string Armed,
        string Quality, string Params, string LastPacket);

    private void RefreshHeader()
    {
        var state = host.State;
        lblState.Text = state switch
        {
            ServerState.Running => $"Running on {host.Address}:{host.Port}",
            ServerState.Failed => "Failed: " + host.Error,
            _ => state.ToString()
        };
        lblState.ForeColor = state switch
        {
            ServerState.Running => Color.FromArgb(30, 130, 60),
            ServerState.Failed => Color.Firebrick,
            _ => SystemColors.ControlText
        };
        lblUrls.Text = state == ServerState.Running
            ? string.Join("    ", host.ClientUrls(host.Address, host.Port))
            : options.Url + "  (not listening)";
        lblData.Text = options.EffectiveDataDirectory;
        btnStartStop.Text = state is ServerState.Running or ServerState.Starting ? "Stop" : "Start";
        btnStartStop.Enabled = state is not (ServerState.Starting or ServerState.Stopping);
        btnDev.Enabled = btnSwagger.Enabled = btnCopy.Enabled = state == ServerState.Running;
        RefreshUptime();
    }

    private void RefreshUptime()
    {
        var since = host.StartedAt;
        lblUptime.Text = since.HasValue ? (DateTime.Now - since.Value).ToString(@"d\.hh\:mm\:ss") : "–";
    }

    private void RefreshGrids()
    {
        var links = host.Backend.Links.Links.Select(l => l.ToInfo()).Select(l => new LinkRow(
            l.Id, l.Connection, l.State.ToString(),
            string.Join(", ", l.Vehicles.Select(v => $"{v.SysId}/{v.CompId}")),
            l.ConnectedAt?.ToLocalTime().ToString("HH:mm:ss") ?? "", l.Error ?? "", l.TlogPath ?? "")).ToList();
        Rebind(gridLinks, links, ref linksKey);

        var vehicles = host.Backend.Vehicles.List().Select(v => new VehicleRow(
            v.LinkId, $"{v.SysId}/{v.CompId}", v.Type ?? "", $"{v.Firmware} {v.Version}".Trim(), v.Mode ?? "",
            v.Armed ? "ARMED" : "disarmed", v.LinkQuality + " %",
            v.ParamsTotal > 0 ? $"{v.ParamsReceived}/{v.ParamsTotal}" : v.ParamsReceived.ToString(),
            v.LastPacket.ToLocalTime().ToString("HH:mm:ss"))).ToList();
        Rebind(gridVehicles, vehicles, ref vehiclesKey);
    }

    /// <summary>Rebind only when something changed so selection and scrolling survive the 1 s refresh.</summary>
    private static void Rebind<T>(DataGridView grid, List<T> rows, ref string key)
    {
        var next = string.Join("\n", rows);
        if (next == key)
            return;
        key = next;
        grid.DataSource = rows;
    }

    private void OnBackendEvent(string topic, object data)
    {
        switch (topic)
        {
            case Topics.Status when data is StatusMessage s:
                Post(() => AppendLog(new LogLine(s.Time.ToLocalTime(), "STATUS", $"{s.SysId}/{s.CompId}", $"[{s.Severity}] {s.Text}")));
                break;
            case Topics.Links:
            case Topics.Vehicles:
                Post(RefreshGrids);
                break;
        }
    }

    private void AppendLog(LogLine line)
    {
        lstLog.BeginUpdate();
        lstLog.Items.Add(line.ToString());
        while (lstLog.Items.Count > LogBuffer.Capacity)
            lstLog.Items.RemoveAt(0);
        lstLog.EndUpdate();
        ScrollLog();
    }

    private void ScrollLog()
    {
        if (lstLog.Items.Count > 0)
            lstLog.TopIndex = lstLog.Items.Count - 1;
    }

    private void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // handle gone (ObjectDisposedException derives from it)
        }
    }

    // ---- hide instead of close

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            WindowState = FormWindowState.Normal;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            host.Changed -= onHostChanged;
            LogBuffer.Added -= onLog;
            host.Backend.Events.Published -= onEvent;
        }
        base.Dispose(disposing);
    }
}
