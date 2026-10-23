using System.Globalization;
using SysScope.Core;
using SysScope.Core.Collection;
using SysScope.Core.Demo;
using SysScope.Core.Export;
using SysScope.Core.Formatting;
using SysScope.Core.Models;

namespace SysScope;

public sealed class MainForm : Form
{
    private readonly AppServices _app;
    private readonly Dictionary<string, Label> _fields = new(StringComparer.Ordinal);
    private readonly Label _banner;
    private readonly TextBox _computer;
    private readonly CheckBox _credentials;
    private readonly TextBox _user;
    private readonly TextBox _password;
    private readonly ToolStripStatusLabel _status;
    private readonly ToolStripStatusLabel _collected;
    private readonly FlowLayoutPanel _overviewFlow;
    private readonly DataGridView _network;
    private readonly DataGridView _bitlocker;
    private readonly DataGridView _cpus;
    private readonly DataGridView _memory;
    private readonly DataGridView _disks;
    private readonly DataGridView _volumes;
    private readonly DataGridView _gpus;
    private readonly DataGridView _monitors;
    private readonly DataGridView _adapters;
    private readonly DataGridView _programs;
    private readonly DataGridView _hotfixes;
    private readonly DataGridView _serviceGrid;
    private readonly DataGridView _startupGrid;
    private readonly DataGridView _inventoryGrid;
    private readonly TextBox _programSearch;
    private readonly TextBox _hotfixSearch;
    private readonly TextBox _serviceSearch;
    private readonly TextBox _startupSearch;
    private readonly TextBox _names;
    private readonly ProgressBar _progress;
    private readonly Label _programCount;
    private readonly Label _hotfixCount;
    private readonly Label _serviceCount;
    private readonly Label _startupCount;
    private readonly Label _battery;
    private readonly Button _connect;
    private readonly Button _refresh;
    private readonly Button _scan;
    private readonly Button _cancelScan;
    private MachineReport? _report;
    private List<InstalledProgram> _programRows = new();
    private List<Hotfix> _hotfixRows = new();
    private List<ServiceInfo> _serviceRows = new();
    private List<StartupItem> _startupRows = new();
    private List<InventoryRow> _inventoryRows = new();
    private CancellationTokenSource? _scanCts;
    private readonly Panel _overviewHost;
    private readonly TableLayoutPanel _overviewFields;
    private readonly List<(Label Caption, Label Value)> _overviewPairs = new();
    private bool _overviewTwoColumn = true;
    private bool _overviewPlaced;
    private bool _layingOut;

    public MainForm(AppServices app)
    {
        _app = app;
        Font = Theme.Body();
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = ProductInfo.Name;
        BackColor = Theme.Page;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        Size = new Size(1280, 800);
        MinimumSize = new Size(960, 640);
        KeyPreview = true;

        _banner = new Label
        {
            AutoSize = true,
            Visible = false,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 8)
        };
        _computer = new TextBox { Width = 200, Margin = new Padding(0, 4, 12, 4) };
        _credentials = new CheckBox { Text = "Alternate credentials", AutoSize = true, Margin = new Padding(0, 6, 12, 4) };
        _user = new TextBox { Width = 160, Margin = new Padding(0, 4, 8, 4), Enabled = false };
        _password = new TextBox { Width = 140, Margin = new Padding(0, 4, 12, 4), UseSystemPasswordChar = true, Enabled = false };
        _status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _collected = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight };
        _network = GridKit.Create();
        _bitlocker = GridKit.Create();
        _cpus = GridKit.Create();
        _memory = GridKit.Create();
        _disks = GridKit.Create();
        _volumes = GridKit.Create();
        _gpus = GridKit.Create();
        _monitors = GridKit.Create();
        _adapters = GridKit.Create();
        _programs = GridKit.Create();
        _hotfixes = GridKit.Create();
        _serviceGrid = GridKit.Create();
        _startupGrid = GridKit.Create();
        _inventoryGrid = GridKit.Create();
        _programSearch = SearchBox();
        _hotfixSearch = SearchBox();
        _serviceSearch = SearchBox();
        _startupSearch = SearchBox();
        _names = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, WordWrap = false, Font = Theme.Body() };
        _progress = new ProgressBar { Height = 18, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 4) };
        _programCount = CountLabel();
        _hotfixCount = CountLabel();
        _serviceCount = CountLabel();
        _startupCount = CountLabel();
        _battery = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12), ForeColor = Theme.Ink, MaximumSize = new Size(640, 0) };
        _overviewFields = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White,
            Padding = new Padding(12)
        };
        _connect = Theme.Primary("Connect");
        _refresh = Theme.Secondary("Refresh");
        _scan = Theme.Primary("Scan");
        _cancelScan = Theme.Secondary("Cancel");
        _overviewFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Theme.Page
        };
        _overviewHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Page, Padding = new Padding(12) };

        BuildColumns();
        BuildShell();
        _credentials.CheckedChanged += (_, _) =>
        {
            var enabled = _credentials.Checked;
            _user.Enabled = enabled;
            _password.Enabled = enabled;
        };
        _connect.Click += async (_, _) => await ConnectAsync();
        _refresh.Click += async (_, _) => await ConnectAsync();
        _computer.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            await ConnectAsync();
        };
        _programSearch.TextChanged += (_, _) => BindPrograms();
        _hotfixSearch.TextChanged += (_, _) => BindHotfixes();
        _serviceSearch.TextChanged += (_, _) => BindServices();
        _startupSearch.TextChanged += (_, _) => BindStartup();
        _volumes.CellPainting += PaintFreeSpace;
        _inventoryGrid.CellFormatting += ColorOnline;
        GridKit.Copied += text => SetStatus("Copied \"" + Trim(text, 80) + "\".");
        _overviewHost.Resize += (_, _) => LayoutOverview();
        Shown += async (_, _) =>
        {
            LayoutOverview();
            if (!_app.Demo)
                await ConnectAsync();
        };
        Resize += (_, _) => LayoutOverview();

        if (_app.Demo)
            LoadDemo();
        else
            SetStatus("Ready. Leave the computer blank to read this PC.");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var area = Screen.FromHandle(Handle).WorkingArea;
        var scale = Math.Clamp(DeviceDpi / 96f, 1f, 1.75f);
        var min = new Size(Math.Min((int)(1000 * scale), Math.Max(640, area.Width)), Math.Min((int)(680 * scale), Math.Max(480, area.Height)));
        MinimumSize = min;
        if (Width > area.Width || Height > area.Height)
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        if (Location.X < area.X || Location.Y < area.Y)
            Location = new Point(area.X, area.Y);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _scanCts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            _ = ConnectAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildShell()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Export HTML report…", null, (_, _) => ExportHtml());
        file.DropDownItems.Add("Export JSON report…", null, (_, _) => ExportJson());
        file.DropDownItems.Add("Export inventory CSV…", null, (_, _) => ExportInventory());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var edit = new ToolStripMenuItem("Edit");
        edit.DropDownItems.Add("Copy", null, (_, _) => CopyFocused());
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About SysScope", null, (_, _) => ShowAbout());
        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(help);

        var header = BuildHeader();
        var accent = new Panel { Height = 3, Dock = DockStyle.Fill, BackColor = Theme.Teal, Margin = new Padding(0) };
        var command = BuildCommandBar();
        var tabs = BuildTabs();
        var status = new StatusStrip { SizingGrip = true, BackColor = Color.White };
        status.Items.Add(_status);
        status.Items.Add(_collected);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Theme.Page };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        menu.Dock = DockStyle.Fill;
        header.Dock = DockStyle.Fill;
        command.Dock = DockStyle.Fill;
        tabs.Dock = DockStyle.Fill;
        status.Dock = DockStyle.Fill;
        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(header, 0, 1);
        root.Controls.Add(accent, 0, 2);
        root.Controls.Add(command, 0, 3);
        root.Controls.Add(tabs, 0, 4);
        root.Controls.Add(status, 0, 5);
        Controls.Add(root);
        MainMenuStrip = menu;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Navy,
            Padding = new Padding(16, 10, 16, 10),
            Margin = new Padding(0)
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Theme.Navy,
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var title = new Label
        {
            Text = ProductInfo.Name,
            AutoSize = true,
            ForeColor = Color.White,
            Font = Theme.Title(),
            Margin = new Padding(0),
            BackColor = Theme.Navy
        };
        var version = new Label
        {
            Text = "v" + ProductInfo.Version,
            AutoSize = true,
            ForeColor = Color.FromArgb(197, 212, 232),
            Font = Theme.Emphasis(),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(12, 8, 0, 0),
            BackColor = Theme.Navy
        };
        var tagline = new Label
        {
            Text = ProductInfo.Tagline,
            AutoSize = true,
            ForeColor = Color.FromArgb(197, 212, 232),
            Font = Theme.Tagline(),
            Margin = new Padding(0, 2, 0, 0),
            BackColor = Theme.Navy
        };
        table.Controls.Add(title, 0, 0);
        table.Controls.Add(version, 1, 0);
        table.Controls.Add(tagline, 0, 1);
        table.SetColumnSpan(tagline, 2);
        header.Controls.Add(table);
        header.Resize += (_, _) => tagline.MaximumSize = new Size(Math.Max(180, header.ClientSize.Width - 36), 0);
        return header;
    }

    private Control BuildCommandBar()
    {
        var bar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        bar.Controls.Add(InlineLabel("Computer"));
        bar.Controls.Add(_computer);
        bar.Controls.Add(_credentials);
        bar.Controls.Add(InlineLabel("User"));
        bar.Controls.Add(_user);
        bar.Controls.Add(InlineLabel("Password"));
        bar.Controls.Add(_password);
        bar.Controls.Add(_connect);
        bar.Controls.Add(_refresh);
        var exportHtml = Theme.Secondary("Export HTML");
        var exportJson = Theme.Secondary("Export JSON");
        exportHtml.Click += (_, _) => ExportHtml();
        exportJson.Click += (_, _) => ExportJson();
        bar.Controls.Add(exportHtml);
        bar.Controls.Add(exportJson);
        _computer.AccessibleName = "Computer";
        _user.AccessibleName = "User name";
        _password.AccessibleName = "Password";
        return bar;
    }

    private TabControl BuildTabs()
    {
        var tabs = new TabControl { Font = Theme.Body(), Padding = new Point(12, 6) };
        tabs.TabPages.Add(Page("Overview", BuildOverview()));
        tabs.TabPages.Add(Page("Hardware", BuildHardware()));
        tabs.TabPages.Add(Page("Software", BuildSoftware()));
        tabs.TabPages.Add(Page("Services", BuildSearchPage(_serviceSearch, _serviceCount, _serviceGrid, "Search services")));
        tabs.TabPages.Add(Page("Startup", BuildSearchPage(_startupSearch, _startupCount, _startupGrid, "Search startup items")));
        tabs.TabPages.Add(Page("Inventory", BuildInventory()));
        return tabs;
    }

    private Control BuildOverview()
    {
        AddPair("Computer name", "name");
        AddPair("Operating system", "os");
        AddPair("Domain / workgroup", "domain");
        AddPair("OS version", "version");
        AddPair("Logged-on user", "user");
        AddPair("OS build", "build");
        AddPair("Manufacturer", "maker");
        AddPair("Install date", "installed");
        AddPair("Model", "model");
        AddPair("Last boot", "boot");
        AddPair("Serial number", "serial");
        AddPair("Uptime", "uptime");
        AddPair("BIOS version", "bios");
        AddPair("Memory", "memory");
        AddPair("BIOS date", "biosDate");
        AddPair("TPM", "tpm");
        AddPair("Processor", "cpu");
        AddPair("Secure Boot", "secureBoot");

        _overviewFlow.Controls.Add(_banner);
        _overviewFlow.Controls.Add(_overviewFields);
        _overviewFlow.Controls.Add(SectionBlock("Network adapters", _network));
        _overviewFlow.Controls.Add(SectionBlock("BitLocker", _bitlocker));
        _overviewHost.Controls.Add(_overviewFlow);
        return _overviewHost;
    }

    private Control BuildHardware()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(GridPage("CPU", _cpus));
        tabs.TabPages.Add(GridPage("Memory", _memory));
        tabs.TabPages.Add(GridPage("Disks", _disks));
        tabs.TabPages.Add(GridPage("Volumes", _volumes));
        tabs.TabPages.Add(GridPage("GPUs", _gpus));
        tabs.TabPages.Add(GridPage("Monitors", _monitors));
        tabs.TabPages.Add(GridPage("Network", _adapters));
        var batteryPage = new TabPage("Battery") { BackColor = Color.White, Padding = new Padding(8) };
        batteryPage.Controls.Add(_battery);
        batteryPage.Resize += (_, _) =>
            _battery.MaximumSize = new Size(Math.Max(200, batteryPage.ClientSize.Width - 16), 0);
        tabs.TabPages.Add(batteryPage);
        _battery.Text = "Connect to a computer to read battery health.";
        return tabs;
    }

    private Control BuildSoftware()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("Installed programs", BuildSearchPage(_programSearch, _programCount, _programs, "Search programs")));
        tabs.TabPages.Add(Page("Hotfixes", BuildSearchPage(_hotfixSearch, _hotfixCount, _hotfixes, "Search hotfixes")));
        return tabs;
    }

    private Control BuildInventory()
    {
        var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Theme.Page };
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Theme.Page
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var help = new Label
        {
            Text = "One computer name per line, or a CSV with a Computer or Name column. Scan reads a summary from each machine.",
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 0, 0, 6),
            Dock = DockStyle.Top
        };
        top.Resize += (_, _) => help.MaximumSize = new Size(Math.Max(200, top.ClientSize.Width - 4), 0);
        var line = TextRenderer.MeasureText("Ag", _names.Font).Height;
        _names.Height = (line * 6) + 8;
        _names.Dock = DockStyle.Top;
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 4)
        };
        var import = Theme.Secondary("Import CSV");
        var export = Theme.Secondary("Export CSV");
        import.Click += (_, _) => ImportNames();
        _scan.Click += async (_, _) => await ScanAsync();
        _cancelScan.Click += (_, _) => _scanCts?.Cancel();
        _cancelScan.Enabled = false;
        export.Click += (_, _) => ExportInventory();
        buttons.Controls.Add(import);
        buttons.Controls.Add(_scan);
        buttons.Controls.Add(_cancelScan);
        buttons.Controls.Add(export);
        top.Controls.Add(help);
        top.Controls.Add(_names);
        top.Controls.Add(buttons);
        top.Controls.Add(_progress);
        page.Controls.Add(_inventoryGrid);
        page.Controls.Add(top);
        return page;
    }

    private void BuildColumns()
    {
        _network.Columns.AddRange(
            GridKit.Column("Name", "Adapter", 140, 160),
            GridKit.Column("Mac", "MAC address", 140),
            GridKit.Column("IpAddresses", "IP address", 140, 140),
            GridKit.Column("Gateway", "Gateway", 120),
            GridKit.Column("Dhcp", "DHCP", 70, 60),
            GridKit.Column("LinkSpeed", "Link speed", 100));
        _bitlocker.Columns.AddRange(
            GridKit.Column("DriveLetter", "Drive", 90),
            GridKit.Column("Status", "Status", 110),
            GridKit.Column("EncryptionMethod", "Encryption", 160),
            GridKit.Column("ConversionStatus", "Conversion", 180, 160));
        _cpus.Columns.AddRange(
            GridKit.Column("Name", "Name", 220, 220),
            GridKit.Column("Manufacturer", "Manufacturer", 120),
            GridKit.Column("Cores", "Cores", 70, 60),
            GridKit.Column("LogicalProcessors", "Logical processors", 140),
            GridKit.Column("MaxClockMhz", "Max clock (MHz)", 130),
            GridKit.Column("Socket", "Socket", 90),
            GridKit.Column("L2CacheKb", "L2 cache (KB)", 120),
            GridKit.Column("L3CacheKb", "L3 cache (KB)", 120));
        _memory.Columns.AddRange(
            GridKit.Column("Slot", "Slot", 100),
            GridKit.Column("SizeBytes", "Size", 100),
            GridKit.Column("SpeedMhz", "Speed (MHz)", 110),
            GridKit.Column("Manufacturer", "Manufacturer", 120),
            GridKit.Column("PartNumber", "Part number", 160, 160),
            GridKit.Column("Serial", "Serial", 120));
        _disks.Columns.AddRange(
            GridKit.Column("Model", "Model", 180, 180),
            GridKit.Column("SizeBytes", "Size", 100),
            GridKit.Column("MediaType", "Media type", 110),
            GridKit.Column("Health", "Health", 110),
            GridKit.Column("Bus", "Bus", 80),
            GridKit.Column("Serial", "Serial", 140),
            GridKit.Column("SmartSource", "SMART", 180, 160));
        _volumes.Columns.AddRange(
            GridKit.Column("Drive", "Drive", 70, 60),
            GridKit.Column("Label", "Label", 100),
            GridKit.Column("FileSystem", "File system", 100),
            GridKit.Column("Size", "Size", 100),
            GridKit.Column("FreeText", "Free space", 240, 220));
        _gpus.Columns.AddRange(
            GridKit.Column("Name", "Name", 180, 180),
            GridKit.Column("AdapterRamBytes", "Memory", 100),
            GridKit.Column("DriverVersion", "Driver version", 130),
            GridKit.Column("DriverDate", "Driver date", 120),
            GridKit.Column("VideoMode", "Video mode", 180, 160));
        _monitors.Columns.AddRange(
            GridKit.Column("Name", "Name", 160, 160),
            GridKit.Column("Manufacturer", "Manufacturer", 120),
            GridKit.Column("ProductCode", "Product code", 120),
            GridKit.Column("Serial", "Serial", 160, 160));
        _adapters.Columns.AddRange(
            GridKit.Column("Name", "Adapter", 140, 150),
            GridKit.Column("Mac", "MAC address", 140),
            GridKit.Column("IpAddresses", "IP address", 140),
            GridKit.Column("Gateway", "Gateway", 120),
            GridKit.Column("Dhcp", "DHCP", 70, 60),
            GridKit.Column("LinkSpeed", "Link speed", 100));
        _programs.Columns.AddRange(
            GridKit.Column("Name", "Name", 180, 200),
            GridKit.Column("Version", "Version", 120),
            GridKit.Column("Publisher", "Publisher", 160, 150),
            GridKit.Column("InstallDate", "Install date", 120),
            GridKit.Column("SizeBytes", "Size", 90),
            GridKit.Column("Source", "Source", 120));
        _hotfixes.Columns.AddRange(
            GridKit.Column("HotFixId", "Hotfix", 120),
            GridKit.Column("Description", "Description", 180, 200),
            GridKit.Column("InstalledOn", "Installed on", 120),
            GridKit.Column("InstalledBy", "Installed by", 180, 160));
        _serviceGrid.Columns.AddRange(
            GridKit.Column("Name", "Name", 120),
            GridKit.Column("DisplayName", "Display name", 220, 220),
            GridKit.Column("Status", "Status", 100),
            GridKit.Column("StartMode", "Start mode", 110),
            GridKit.Column("Account", "Log on as", 180, 160));
        _startupGrid.Columns.AddRange(
            GridKit.Column("Name", "Name", 140),
            GridKit.Column("Command", "Command", 280, 260),
            GridKit.Column("Location", "Location", 140),
            GridKit.Column("User", "User", 140),
            GridKit.Column("Enabled", "Enabled", 90, 70));
        _inventoryGrid.Columns.AddRange(
            GridKit.Column("Name", "Name", 130),
            GridKit.Column("Online", "Online", 80, 70),
            GridKit.Column("Model", "Model", 140),
            GridKit.Column("Serial", "Serial", 110),
            GridKit.Column("Os", "OS", 180, 160),
            GridKit.Column("Build", "Build", 110),
            GridKit.Column("Cpu", "CPU", 180, 160),
            GridKit.Column("Ram", "RAM", 90),
            GridKit.Column("DiskFree", "Disk free", 110),
            GridKit.Column("LastBoot", "Last boot", 140),
            GridKit.Column("LoggedOnUser", "Logged-on user", 150),
            GridKit.Column("Detail", "Detail", 180, 160));
    }

    private void LoadDemo()
    {
        var local = new CollectRequest(new ConnectionTarget("", null, null), DemoCatalog.PrimaryName);
        var report = Task.Run(() => _app.Collector.CollectAsync(local, CancellationToken.None)).GetAwaiter().GetResult();
        BindReport(report);
        _computer.Text = DemoCatalog.PrimaryName;
        _names.Text = string.Join(Environment.NewLine, DemoCatalog.SampleNames);
        var rows = new List<InventoryRow>();
        foreach (var name in DemoCatalog.SampleNames)
        {
            var request = new CollectRequest(new ConnectionTarget(name, null, null), name);
            var summary = Task.Run(() => _app.Collector.CollectSummaryAsync(request, CancellationToken.None)).GetAwaiter().GetResult();
            rows.Add(InventoryRowMapper.FromReport(summary, name));
        }
        _inventoryRows = rows;
        GridKit.Bind(_inventoryGrid, _inventoryRows);
        SetStatus("Demo mode — fictional data for screenshots. Nothing was read from this PC.");
    }

    private async Task ConnectAsync()
    {
        if (_credentials.Checked && string.IsNullOrWhiteSpace(_user.Text))
        {
            ShowBanner("Enter a user name for the alternate credentials, or clear the checkbox.", true);
            SetStatus("Credentials are incomplete.");
            return;
        }

        var request = BuildRequest();
        ToggleBusy(true);
        SetStatus("Reading " + request.DisplayName + "…");
        try
        {
            var report = await Task.Run(() => _app.Collector.CollectAsync(request, CancellationToken.None));
            BindReport(report);
            if (request.Target.IsLocal && request.Target.HasCredentials)
                SetStatus("Alternate credentials are ignored for the local computer. " + _status.Text);
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, true);
            SetStatus(ex.Message);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private CollectRequest BuildRequest()
    {
        var name = _computer.Text.Trim();
        string? user = _credentials.Checked ? _user.Text.Trim() : null;
        string? password = _credentials.Checked ? _password.Text : null;
        var target = new ConnectionTarget(name, user, password);
        var display = target.IsLocal
            ? (_app.Demo ? DemoCatalog.PrimaryName : Environment.MachineName)
            : target.ComputerName;
        return new CollectRequest(target, display);
    }

    private void BindReport(MachineReport report)
    {
        _report = report;
        var overview = report.Overview;
        SetField("name", overview.ComputerName);
        SetField("domain", DisplayFormatter.Domain(overview.PartOfDomain, overview.DomainOrWorkgroup));
        SetField("user", DisplayFormatter.LoggedOnUser(overview.LoggedOnUser));
        SetField("maker", overview.Manufacturer);
        SetField("model", overview.Model);
        SetField("serial", overview.SerialNumber);
        SetField("bios", overview.BiosVersion);
        SetField("biosDate", DisplayFormatter.Date(overview.BiosDate));
        SetField("os", overview.OsEdition);
        SetField("version", overview.OsVersion);
        SetField("build", overview.OsBuild);
        SetField("installed", DisplayFormatter.Date(overview.InstallDate));
        SetField("boot", DisplayFormatter.DateTime(overview.LastBoot));
        SetField("uptime", UptimeFormatter.Format(overview.Uptime));
        SetField("cpu", overview.Cpu);
        SetField("memory", DisplayFormatter.Memory(overview.RamBytes, overview.RamUsedBytes));
        SetField("tpm", DisplayFormatter.Tpm(overview.TpmPresent, overview.TpmVersion));
        SetField("secureBoot", DisplayFormatter.SecureBoot(overview.SecureBoot));
        GridKit.Bind(_network, overview.Adapters);
        GridKit.Bind(_bitlocker, overview.BitLocker);
        GridKit.Bind(_cpus, report.Hardware.Cpus);
        GridKit.Bind(_memory, report.Hardware.MemoryModules);
        GridKit.Bind(_disks, report.Hardware.Disks);
        GridKit.Bind(_volumes, report.Hardware.Volumes.Select(volume => new VolumeRow
        {
            Drive = volume.Drive,
            Label = volume.Label,
            FileSystem = volume.FileSystem,
            Size = ByteSizeFormatter.Format(volume.SizeBytes),
            FreeText = VolumeMath.FreeLabel(volume.SizeBytes, volume.FreeBytes),
            FreeFraction = VolumeMath.FreeFraction(volume.SizeBytes, volume.FreeBytes)
        }));
        GridKit.Bind(_gpus, report.Hardware.Gpus);
        GridKit.Bind(_monitors, report.Hardware.Monitors);
        GridKit.Bind(_adapters, report.Hardware.Adapters);
        _programRows = report.Programs;
        _hotfixRows = report.Hotfixes;
        _serviceRows = report.Services;
        _startupRows = report.StartupItems;
        BindPrograms();
        BindHotfixes();
        BindServices();
        BindStartup();
        _battery.Text = BatteryText(report.Hardware.Battery);
        _collected.Text = "Collected " + DisplayFormatter.DateTime(report.CollectedAt);

        if (!report.Online)
        {
            ShowBanner(string.IsNullOrWhiteSpace(report.Error) ? "The computer could not be inventoried." : report.Error, true);
            SetStatus(FirstLine(report.Error));
        }
        else if (report.Notes.Count > 0)
        {
            ShowBanner("Some details could not be read:" + Environment.NewLine + string.Join(Environment.NewLine, report.Notes), false);
            SetStatus("Connected to " + overview.ComputerName + " with warnings.");
        }
        else
        {
            _banner.Visible = false;
            SetStatus("Connected to " + DisplayFormatter.Dash(overview.ComputerName) + ".");
        }
        LayoutOverview();
    }

    private void BindPrograms()
    {
        var query = _programSearch.Text.Trim();
        var rows = _programRows.Where(row => query.Length == 0 || Hit(row.Name, query) || Hit(row.Publisher, query) || Hit(row.Version, query)).ToList();
        GridKit.Bind(_programs, rows);
        _programCount.Text = rows.Count.ToString(CultureInfo.InvariantCulture) + " programs";
    }

    private void BindHotfixes()
    {
        var query = _hotfixSearch.Text.Trim();
        var rows = _hotfixRows.Where(row => query.Length == 0 || Hit(row.HotFixId, query) || Hit(row.Description, query) || Hit(row.InstalledBy, query)).ToList();
        GridKit.Bind(_hotfixes, rows);
        _hotfixCount.Text = rows.Count.ToString(CultureInfo.InvariantCulture) + " hotfixes";
    }

    private void BindServices()
    {
        var query = _serviceSearch.Text.Trim();
        var rows = _serviceRows.Where(row => query.Length == 0 || Hit(row.Name, query) || Hit(row.DisplayName, query) || Hit(row.Status, query) || Hit(row.Account, query)).ToList();
        GridKit.Bind(_serviceGrid, rows);
        _serviceCount.Text = rows.Count.ToString(CultureInfo.InvariantCulture) + " services";
    }

    private void BindStartup()
    {
        var query = _startupSearch.Text.Trim();
        var rows = _startupRows.Where(row => query.Length == 0 || Hit(row.Name, query) || Hit(row.Command, query) || Hit(row.Location, query) || Hit(row.User, query)).ToList();
        GridKit.Bind(_startupGrid, rows);
        _startupCount.Text = rows.Count.ToString(CultureInfo.InvariantCulture) + " startup items";
    }

    private async Task ScanAsync()
    {
        var names = ComputerListParser.Parse(_names.Text);
        if (names.Count == 0)
        {
            SetStatus("Add at least one computer name to scan.");
            return;
        }

        _scanCts = new CancellationTokenSource();
        ToggleScan(true);
        _inventoryRows = new List<InventoryRow>();
        GridKit.Bind(_inventoryGrid, _inventoryRows);
        _progress.Minimum = 0;
        _progress.Maximum = names.Count;
        _progress.Value = 0;
        string? user = _credentials.Checked ? _user.Text.Trim() : null;
        string? password = _credentials.Checked ? _password.Text : null;
        var requests = names.Select(name => new CollectRequest(new ConnectionTarget(name, user, password), name)).ToList();
        var progress = new Progress<ScanProgress>(update =>
        {
            _progress.Maximum = Math.Max(1, update.Total);
            _progress.Value = Math.Clamp(update.Completed, 0, _progress.Maximum);
            var shown = update.Total == 0 ? 0 : Math.Min(update.Completed + (update.Completed >= update.Total ? 0 : 1), update.Total);
            SetStatus($"Scanning {shown} of {update.Total} — {update.Current}");
        });
        try
        {
            var result = await _app.Scanner.ScanAsync(requests, progress, _scanCts.Token);
            _inventoryRows = result.Rows.ToList();
            GridKit.Bind(_inventoryGrid, _inventoryRows);
            var online = _inventoryRows.Count(row => row.Online == "Yes");
            SetStatus(result.Cancelled
                ? $"Scan cancelled. {online} online of {_inventoryRows.Count}."
                : $"Scan finished. {online} online of {_inventoryRows.Count}.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            ToggleScan(false);
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    private void ImportNames()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import computer names",
            Filter = "CSV or text (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var names = ComputerListParser.Parse(File.ReadAllText(dialog.FileName));
        _names.Text = string.Join(Environment.NewLine, names);
        SetStatus($"Imported {names.Count} computer names.");
    }

    private void ExportHtml() => ExportReport("HTML report (*.html)|*.html", ".html", report => HtmlReportWriter.Write(report));

    private void ExportJson() => ExportReport("JSON report (*.json)|*.json", ".json", report => JsonReportWriter.Write(report));

    private void ExportReport(string filter, string extension, Func<MachineReport, string> write)
    {
        if (_report is null || (!_report.Online && string.IsNullOrWhiteSpace(_report.Overview.ComputerName) && string.IsNullOrWhiteSpace(_report.Error)))
        {
            SetStatus("Connect to a computer before exporting a report.");
            return;
        }
        if (_report is null)
            return;
        var name = string.IsNullOrWhiteSpace(_report.Overview.ComputerName) ? _report.RequestedName : _report.Overview.ComputerName;
        using var dialog = new SaveFileDialog
        {
            Title = "Export report",
            Filter = filter,
            FileName = "SysScope-" + SafeFile(name) + extension
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        File.WriteAllText(dialog.FileName, write(_report));
        SetStatus("Saved " + dialog.FileName);
    }

    private void ExportInventory()
    {
        if (_inventoryRows.Count == 0)
        {
            SetStatus("Scan computers before exporting inventory.");
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export inventory",
            Filter = "CSV (*.csv)|*.csv",
            FileName = "SysScope-inventory.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        File.WriteAllBytes(dialog.FileName, CsvInventoryWriter.Write(_inventoryRows));
        SetStatus($"Saved {_inventoryRows.Count} rows to {dialog.FileName}");
    }

    private void CopyFocused()
    {
        var grid = ActiveGrid();
        if (grid is null)
        {
            SetStatus("Select a value to copy.");
            return;
        }
        GridKit.CopyCell(grid);
    }

    private DataGridView? ActiveGrid()
    {
        var control = ActiveControl;
        while (control is not null)
        {
            if (control is DataGridView grid)
                return grid;
            control = control.Parent;
        }
        return null;
    }

    private void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog(this);
    }

    private void ShowBanner(string text, bool danger)
    {
        _banner.Text = text;
        _banner.BackColor = danger ? Theme.DangerBg : Theme.WarnBg;
        _banner.ForeColor = danger ? Theme.DangerFg : Theme.WarnFg;
        _banner.Visible = true;
        LayoutOverview();
    }

    private void SetStatus(string text)
    {
        var line = FirstLine(text);
        _status.Text = line.Length > 220 ? line[..217] + "…" : line;
    }

    private void SetField(string key, string? value)
    {
        if (_fields.TryGetValue(key, out var label))
            label.Text = string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private void ToggleBusy(bool busy)
    {
        _connect.Enabled = !busy;
        _refresh.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ToggleScan(bool scanning)
    {
        _scan.Enabled = !scanning;
        _cancelScan.Enabled = scanning;
        _connect.Enabled = !scanning;
        _names.ReadOnly = scanning;
        UseWaitCursor = scanning;
    }

    private void LayoutOverview()
    {
        if (_layingOut)
            return;
        _layingOut = true;
        try
        {
            LayoutOverviewCore();
        }
        finally
        {
            _layingOut = false;
        }
    }

    private void LayoutOverviewCore()
    {
        var width = _overviewHost.ClientSize.Width;
        if (width <= 0)
            width = Math.Max(240, ClientSize.Width - 32);
        width = Math.Max(240, width - _overviewHost.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        _overviewFlow.MaximumSize = new Size(width, int.MaxValue);
        _overviewFlow.Width = width;
        foreach (Control child in _overviewFlow.Controls)
        {
            var inner = Math.Max(200, width - child.Margin.Horizontal);
            child.MaximumSize = new Size(inner, int.MaxValue);
            child.Width = inner;
        }

        var captionCol = 80;
        foreach (var (caption, _) in _overviewPairs)
        {
            caption.AutoEllipsis = false;
            captionCol = Math.Max(captionCol, TextRenderer.MeasureText(caption.Text, caption.Font).Width + 14);
        }

        var innerWidth = Math.Max(200, width - _overviewFields.Padding.Horizontal - _overviewFields.Margin.Horizontal);
        var twoColumns = innerWidth >= (captionCol * 2) + 240;
        var valueWidth = twoColumns
            ? Math.Max(80, (innerWidth / 2) - captionCol)
            : Math.Max(80, innerWidth - captionCol);
        foreach (var (_, value) in _overviewPairs)
            value.MaximumSize = new Size(valueWidth, 0);

        // Absolute columns keep caption text intact. Percent columns shrink and clip the titles.
        if (!_overviewPlaced || twoColumns != _overviewTwoColumn)
        {
            _overviewTwoColumn = twoColumns;
            _overviewPlaced = true;
            _overviewFields.SuspendLayout();
            _overviewFields.Controls.Clear();
            _overviewFields.ColumnStyles.Clear();
            _overviewFields.RowStyles.Clear();
            _overviewFields.ColumnCount = twoColumns ? 4 : 2;
            _overviewFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, captionCol));
            _overviewFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, valueWidth));
            if (twoColumns)
            {
                _overviewFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, captionCol));
                _overviewFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, valueWidth));
            }

            for (var i = 0; i < _overviewPairs.Count; i++)
            {
                var pairColumn = twoColumns ? i % 2 : 0;
                var row = twoColumns ? i / 2 : i;
                _overviewFields.Controls.Add(_overviewPairs[i].Caption, pairColumn * 2, row);
                _overviewFields.Controls.Add(_overviewPairs[i].Value, pairColumn * 2 + 1, row);
            }
            _overviewFields.ResumeLayout();
        }

        if (_overviewFields.ColumnStyles.Count >= 2)
        {
            _overviewFields.ColumnStyles[0].SizeType = SizeType.Absolute;
            _overviewFields.ColumnStyles[0].Width = captionCol;
            _overviewFields.ColumnStyles[1].SizeType = SizeType.Absolute;
            _overviewFields.ColumnStyles[1].Width = valueWidth;
            if (twoColumns && _overviewFields.ColumnStyles.Count >= 4)
            {
                _overviewFields.ColumnStyles[2].SizeType = SizeType.Absolute;
                _overviewFields.ColumnStyles[2].Width = captionCol;
                _overviewFields.ColumnStyles[3].SizeType = SizeType.Absolute;
                _overviewFields.ColumnStyles[3].Width = valueWidth;
            }
        }

        _banner.MaximumSize = new Size(Math.Max(200, width - 16), 0);
        SizeEmbedded(_network);
        SizeEmbedded(_bitlocker);
    }

    private static void SizeEmbedded(DataGridView grid)
    {
        if (grid.Parent is not Panel block)
            return;
        GridKit.Fit(grid);
        var rows = Math.Max(grid.Rows.Count, 1);
        var columnWidth = 0;
        foreach (DataGridViewColumn column in grid.Columns)
            columnWidth += column.Width;
        var inner = block.ClientSize.Width - block.Padding.Horizontal;
        if (inner <= 0)
            inner = block.Width - block.Padding.Horizontal;
        var horizontal = columnWidth + 6 > Math.Max(0, inner);
        grid.ScrollBars = horizontal ? ScrollBars.Horizontal : ScrollBars.None;
        var scroll = horizontal ? SystemInformation.HorizontalScrollBarHeight : 0;
        grid.Height = grid.ColumnHeadersHeight + (grid.RowTemplate.Height * rows) + scroll + 2;
        var title = 28;
        foreach (Control child in block.Controls)
        {
            if (child is Label label)
                title = Math.Max(title, label.PreferredSize.Height + 6);
        }
        block.Height = grid.Height + title + block.Padding.Vertical;
    }

    private void AddPair(string caption, string key)
    {
        var name = new Label
        {
            Text = caption,
            AutoSize = true,
            AutoEllipsis = false,
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 4, 8, 4),
            Anchor = AnchorStyles.Left
        };
        var value = new Label
        {
            Text = "—",
            AutoSize = true,
            ForeColor = Theme.Ink,
            Margin = new Padding(0, 3, 16, 3),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };
        var menu = new ContextMenuStrip();
        var copy = new ToolStripMenuItem("Copy value");
        copy.Click += (_, _) =>
        {
            if (value.Text.Length == 0 || value.Text == "—")
                return;
            Clipboard.SetText(value.Text);
            SetStatus("Copied \"" + Trim(value.Text, 80) + "\".");
        };
        menu.Items.Add(copy);
        value.ContextMenuStrip = menu;
        value.DoubleClick += (_, _) => copy.PerformClick();
        value.Cursor = Cursors.IBeam;
        _fields[key] = value;
        _overviewPairs.Add((name, value));
    }

    private void PaintFreeSpace(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null)
            return;
        if (_volumes.Columns[e.ColumnIndex].Name != "FreeText")
            return;
        if (_volumes.Rows[e.RowIndex].DataBoundItem is not VolumeRow volume)
            return;
        e.PaintBackground(e.CellBounds, true);
        var font = e.CellStyle?.Font ?? Font;
        var text = volume.FreeText;
        var textWidth = TextRenderer.MeasureText(text, font).Width;
        var textBounds = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y, Math.Min(textWidth + 4, Math.Max(0, e.CellBounds.Width - 16)), e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, text, font, textBounds, Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        var barLeft = textBounds.Right + 8;
        var barWidth = e.CellBounds.Right - 8 - barLeft;
        if (barWidth > 12)
        {
            var bar = new Rectangle(barLeft, e.CellBounds.Y + (e.CellBounds.Height / 2) - 4, barWidth, 8);
            using (var back = new SolidBrush(Color.FromArgb(226, 232, 240)))
                e.Graphics.FillRectangle(back, bar);
            if (volume.FreeFraction is double fraction)
            {
                var width = (int)Math.Round(bar.Width * Math.Clamp(fraction, 0, 1));
                var color = fraction < 0.10 ? Color.FromArgb(185, 28, 28) : fraction < 0.20 ? Color.FromArgb(180, 83, 9) : Theme.Teal;
                using var fill = new SolidBrush(color);
                e.Graphics.FillRectangle(fill, bar.X, bar.Y, width, bar.Height);
            }
        }
        e.Handled = true;
    }

    private void ColorOnline(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle is null)
            return;
        if (_inventoryGrid.Columns[e.ColumnIndex].DataPropertyName != "Online" || e.Value is not string online)
            return;
        e.CellStyle.ForeColor = online == "Yes" ? Color.FromArgb(4, 120, 87) : Theme.DangerFg;
        e.CellStyle.Font = Theme.Emphasis();
    }

    private static Control SectionBlock(string title, DataGridView grid)
    {
        var block = new Panel { Margin = new Padding(0, 8, 0, 0), BackColor = Color.White, Padding = new Padding(12, 8, 12, 12) };
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = Theme.Section(),
            ForeColor = Theme.Navy,
            Padding = new Padding(0, 0, 0, 6)
        };
        grid.Dock = DockStyle.Top;
        grid.ScrollBars = ScrollBars.Horizontal;
        grid.Height = grid.ColumnHeadersHeight + grid.RowTemplate.Height + 8;
        block.Height = grid.Height + 40;
        block.Controls.Add(grid);
        block.Controls.Add(label);
        return block;
    }

    private static TabPage GridPage(string title, DataGridView grid)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(8) };
        grid.Dock = DockStyle.Fill;
        page.Controls.Add(grid);
        return page;
    }

    private static Control BuildSearchPage(TextBox search, Label count, DataGridView grid, string caption)
    {
        var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Theme.Page };
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 6)
        };
        top.Controls.Add(InlineLabel(caption));
        top.Controls.Add(search);
        top.Controls.Add(count);
        page.Controls.Add(grid);
        page.Controls.Add(top);
        return page;
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Theme.Page, Padding = new Padding(0) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private static Label InlineLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 8, 6, 0),
        ForeColor = Theme.Muted
    };

    private static TextBox SearchBox() => new() { Width = 260, Margin = new Padding(0, 4, 8, 4) };

    private static Label CountLabel() => new()
    {
        AutoSize = true,
        Margin = new Padding(8, 8, 0, 0),
        ForeColor = Theme.Muted,
        Text = ""
    };

    private static string BatteryText(BatteryInfo battery)
    {
        if (!battery.Present)
            return "No battery detected. This is typical for a desktop.";
        var health = battery.HealthPercent is double value
            ? value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : "—";
        return string.Join(Environment.NewLine,
        [
            battery.Name,
            "Charge: " + (battery.ChargePercent is int charge ? charge + "%" : "—"),
            "Design capacity: " + (battery.DesignCapacityMwh is int design ? design + " mWh" : "—"),
            "Full charge capacity: " + (battery.FullChargeCapacityMwh is int full ? full + " mWh" : "—"),
            "Health (full charge / design): " + health
        ]);
    }

    private static bool Hit(string? value, string query) =>
        !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var line = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[0].Trim();
        return line;
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string SafeFile(string name)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            name = name.Replace(character, '-');
        return string.IsNullOrWhiteSpace(name) ? "computer" : name;
    }
}

internal sealed class VolumeRow
{
    public string Drive { get; init; } = "";
    public string Label { get; init; } = "";
    public string FileSystem { get; init; } = "";
    public string Size { get; init; } = "";
    public string FreeText { get; init; } = "";
    public double? FreeFraction { get; init; }
}
