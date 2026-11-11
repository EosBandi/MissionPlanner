using System.Diagnostics;
namespace MPBackend.Tray;

/// <summary>
/// The application: tray icon and menu, the status window, and the server host. Lives for the whole process.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly TrayOptions options;
    private readonly ServerHost host;
    private readonly TrayIcons icons = new();
    private readonly NotifyIcon tray;
    private readonly MainForm form;
    private readonly System.Windows.Forms.Timer timer;
    private readonly ToolStripMenuItem startStopItem;
    private readonly RegisteredWaitHandle showWait;
    private bool exiting;

    public TrayContext(TrayOptions options, EventWaitHandle showEvent)
    {
        this.options = options;
        host = new ServerHost(options.ToBackendOptions());

        form = new MainForm(this, host, options, icons.Running);
        _ = form.Handle; // created now so BeginInvoke works before the window is first shown

        startStopItem = new ToolStripMenuItem("Start server", null, (_, _) => _ = ToggleServerAsync());
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show status", null, (_, _) => ShowWindow());
        menu.Items.Add("Open dev console", null, (_, _) => OpenPage("/dev.html"));
        menu.Items.Add("Open Swagger", null, (_, _) => OpenPage("/swagger"));
        menu.Items.Add("Copy API URL", null, (_, _) => CopyUrl());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startStopItem);
        menu.Items.Add("Settings…", null, (_, _) => _ = ShowSettingsAsync());
        menu.Items.Add("Add Windows Firewall rule…", null, (_, _) => AddFirewallRule());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        menu.Opening += (_, _) => UpdateMenu();

        tray = new NotifyIcon { Icon = icons.Stopped, Text = "MPBackend", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();

        host.Changed += () => Post(UpdateTray);
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => UpdateTray();
        timer.Start();

        // a second instance signals this event instead of starting
        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Post(ShowWindow), null, -1, false);

        if (!options.StartMinimized)
            ShowWindow();
        if (options.AutoStartServer)
            _ = StartAsync();
        UpdateTray();
    }

    public TrayOptions Options => options;

    public async Task StartAsync()
    {
        await host.StartAsync(options.ListenAddress, options.Port);
        if (host.State == ServerState.Running && !form.Visible)
            Post(() => tray.ShowBalloonTip(3000, "MPBackend", "API listening on " + options.Url, ToolTipIcon.Info));
    }

    public Task StopAsync() => host.StopAsync();

    public Task ToggleServerAsync() =>
        host.State is ServerState.Running or ServerState.Starting ? StopAsync() : StartAsync();

    /// <summary>Save new settings; rebinds the host if the address or port changed. True = app restart needed.</summary>
    public async Task<bool> ApplySettingsAsync(TrayOptions edited)
    {
        var restartNeeded = options.NeedsRestart(edited);
        var rebind = options.ListenAddress != edited.ListenAddress || options.Port != edited.Port;
        options.CopyFrom(edited);
        options.Save();
        LogBuffer.Add("INFO", "Tray", $"settings saved to {TrayOptions.FilePath}");

        if (rebind && host.State is ServerState.Running or ServerState.Failed or ServerState.Starting)
        {
            await host.StopAsync();
            await StartAsync();
        }
        return restartNeeded;
    }

    public async Task ShowSettingsAsync()
    {
        using var dialog = new SettingsForm(options.Clone());
        if (dialog.ShowDialog(form.Visible ? form : null) != DialogResult.OK)
            return;
        if (await ApplySettingsAsync(dialog.Result))
            MessageBox.Show(form.Visible ? form : null,
                "The data directory, tlog recording and log level are read when the app starts.\n" +
                "Restart the app to apply them.", "MPBackend", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void ShowWindow()
    {
        if (exiting)
            return;
        form.Show();
        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Normal;
        form.Activate();
        form.BringToFront();
    }

    public void OpenPage(string path)
    {
        var url = host.LocalUrl(options.Port) + path;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogBuffer.Add("WARN", "Tray", $"could not open {url}: {ex.Message}");
        }
    }

    public void CopyUrl()
    {
        var urls = host.ClientUrls(options.ListenAddress, options.Port);
        // prefer an address other machines can reach
        var url = urls.FirstOrDefault(u => !u.Contains("127.0.0.1")) ?? urls.First();
        try
        {
            Clipboard.SetText(url);
            tray.ShowBalloonTip(2000, "MPBackend", "Copied " + url, ToolTipIcon.None);
        }
        catch (Exception ex)
        {
            LogBuffer.Add("WARN", "Tray", $"clipboard: {ex.Message}");
        }
    }

    public void AddFirewallRule() => Task.Run(() => Firewall.AddRule(options.Port));

    public void Exit()
    {
        if (exiting)
            return;
        exiting = true;
        timer.Stop();
        showWait.Unregister(null);
        tray.Visible = false;
        form.AllowClose = true;
        form.Close();
        host.Dispose();
        ExitThread();
    }

    private void UpdateMenu()
    {
        startStopItem.Text = host.State is ServerState.Running or ServerState.Starting ? "Stop server" : "Start server";
        startStopItem.Enabled = host.State is not (ServerState.Starting or ServerState.Stopping);
    }

    private void UpdateTray()
    {
        if (exiting)
            return;
        Icon icon;
        string text;
        switch (host.State)
        {
            case ServerState.Running:
            {
                var links = host.Backend.Links.Links;
                var vehicles = host.Backend.Vehicles.List().Count;
                icon = links.Any(l => l.State == MPBackend.Core.Links.LinkState.Failed) ? icons.Error
                    : vehicles > 0 ? icons.Vehicle
                    : icons.Running;
                text = $"MPBackend – API on port {host.Port}, {links.Count} link(s), {vehicles} vehicle(s)";
                break;
            }
            case ServerState.Failed:
                icon = icons.Error;
                text = "MPBackend – API failed: " + host.Error;
                break;
            case ServerState.Starting:
            case ServerState.Stopping:
                icon = icons.Stopped;
                text = "MPBackend – " + host.State.ToString().ToLowerInvariant();
                break;
            default:
                icon = icons.Stopped;
                text = "MPBackend – API stopped";
                break;
        }
        if (!ReferenceEquals(tray.Icon, icon))
            tray.Icon = icon;
        // NotifyIcon.Text is limited to 127 characters
        if (text.Length > 120)
            text = text.Substring(0, 120) + "…";
        if (tray.Text != text)
            tray.Text = text;
    }

    /// <summary>Run on the UI thread (events arrive on backend / host threads).</summary>
    private void Post(Action action)
    {
        if (exiting || form.IsDisposed || !form.IsHandleCreated)
            return;
        try
        {
            form.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // handle gone (ObjectDisposedException derives from it)
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            tray.Dispose();
            form.Dispose();
        }
        base.Dispose(disposing);
    }
}
