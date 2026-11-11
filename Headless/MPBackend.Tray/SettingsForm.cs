using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MPBackend.Tray;

/// <summary>Edits a copy of <see cref="TrayOptions"/>; the caller applies <see cref="Result"/> on OK.</summary>
public sealed class SettingsForm : Form
{
    private readonly ComboBox cmbAddress = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
    private readonly NumericUpDown numPort = new() { Minimum = 1, Maximum = 65535, Width = 90 };
    private readonly TextBox txtData = new() { Width = 360, PlaceholderText = "default: %LOCALAPPDATA%\\MPBackend" };
    private readonly Button btnBrowse = new() { Text = "…", AutoSize = true, MinimumSize = new Size(36, 0), Padding = new Padding(4, 3, 4, 3) };
    private readonly CheckBox chkTlogs = new() { Text = "Record .tlog / .rlog per link", AutoSize = true };
    private readonly ComboBox cmbLevel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly CheckBox chkMinimized = new() { Text = "Start minimised to the tray", AutoSize = true };
    private readonly CheckBox chkAutoStart = new() { Text = "Start the API when the app starts", AutoSize = true };

    public TrayOptions Result { get; }

    public SettingsForm(TrayOptions options)
    {
        Result = options;
        Text = "MPBackend settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        cmbAddress.Items.Add("0.0.0.0");
        cmbAddress.Items.Add("127.0.0.1");
        foreach (var ip in LocalAddresses())
            cmbAddress.Items.Add(ip);
        cmbAddress.Text = options.ListenAddress;
        numPort.Value = Math.Clamp(options.Port, 1, 65535);
        txtData.Text = options.DataDirectory ?? "";
        chkTlogs.Checked = options.RecordTlogs;
        cmbLevel.Items.AddRange(["DEBUG", "INFO", "WARN", "ERROR"]);
        cmbLevel.SelectedItem = cmbLevel.Items.Contains(options.LogLevel.ToUpperInvariant()) ? options.LogLevel.ToUpperInvariant() : "INFO";
        chkMinimized.Checked = options.StartMinimized;
        chkAutoStart.Checked = options.AutoStartServer;

        btnBrowse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = options.EffectiveDataDirectory, ShowNewFolderButton = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                txtData.Text = dlg.SelectedPath;
        };

        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(12), Dock = DockStyle.Fill };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var row = 0;
        Add(table, row++, "Listen address", cmbAddress,
            new Label { Text = "0.0.0.0 = all interfaces (LAN access, no authentication)", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(6, 6, 0, 0) });
        Add(table, row++, "Port", numPort);
        Add(table, row++, "Data directory", txtData, btnBrowse);
        Add(table, row++, "", chkTlogs);
        Add(table, row++, "Log level", cmbLevel);
        Add(table, row++, "", chkMinimized);
        Add(table, row++, "", chkAutoStart);
        var note = new Label
        {
            Text = "Address and port apply immediately (the API is restarted). Data directory, tlog recording and\n" +
                   "log level apply after the app is restarted.",
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 10, 0, 0)
        };
        table.Controls.Add(note, 0, row);
        table.SetColumnSpan(note, 3);
        row++;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0), Padding = new Padding(6, 3, 6, 3) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 0), Padding = new Padding(6, 3, 6, 3) };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        table.Controls.Add(buttons, 0, row);
        table.SetColumnSpan(buttons, 3);
        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, e) =>
        {
            if (!Commit())
                DialogResult = DialogResult.None;
        };
    }

    private static void Add(TableLayoutPanel table, int row, string label, Control control, Control? extra = null)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 10, 0) }, 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(control, 1, row);
        if (extra != null)
        {
            extra.Margin = new Padding(4, 3, 0, 3);
            table.Controls.Add(extra, 2, row);
        }
    }

    private bool Commit()
    {
        var address = cmbAddress.Text.Trim();
        if (!IPAddress.TryParse(address, out _))
        {
            MessageBox.Show(this, "Enter an IPv4 address: 0.0.0.0 for all interfaces, 127.0.0.1 for this machine only, or one of the listed addresses.",
                "Listen address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        var data = txtData.Text.Trim();
        if (data.Length > 0)
        {
            try
            {
                Path.GetFullPath(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Data directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        Result.ListenAddress = address;
        Result.Port = (int)numPort.Value;
        Result.DataDirectory = data.Length > 0 ? data : null;
        Result.RecordTlogs = chkTlogs.Checked;
        Result.LogLevel = (string)(cmbLevel.SelectedItem ?? "INFO");
        Result.StartMinimized = chkMinimized.Checked;
        Result.AutoStartServer = chkAutoStart.Checked;
        return true;
    }

    private static IEnumerable<string> LocalAddresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        list.Add(ip.Address.ToString());
            }
        }
        catch
        {
            // no interfaces listed; the user can still type an address
        }
        return list;
    }
}
