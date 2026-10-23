using System.Globalization;
using SysScope.Core.Models;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;
using SysScope.Core.WinRm;

namespace SysScope.Core.Demo;

public sealed class DemoProfile
{
    public string ComputerName { get; init; } = "";
    public bool PartOfDomain { get; init; } = true;
    public string Domain { get; init; } = "NORTHWIND.LOCAL";
    public string Workgroup { get; init; } = "WORKGROUP";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string Serial { get; init; } = "";
    public string BiosSerial { get; init; } = "To be filled by O.E.M.";
    public string BiosVersion { get; init; } = "1.8.1";
    public DateTimeOffset BiosDate { get; init; }
    public string OsCaption { get; init; } = "Microsoft Windows 11 Pro";
    public string OsVersion { get; init; } = "10.0.22631";
    public string OsBuild { get; init; } = "22631";
    public int Ubr { get; init; } = 4317;
    public string DisplayVersion { get; init; } = "23H2";
    public string OsArchitecture { get; init; } = "64-bit";
    public DateTimeOffset InstallDate { get; init; }
    public double UptimeHours { get; init; } = 26;
    public string LoggedOnUser { get; init; } = "";
    public string CpuName { get; init; } = "";
    public string CpuManufacturer { get; init; } = "GenuineIntel";
    public int Cores { get; init; }
    public int Logical { get; init; }
    public int Mhz { get; init; }
    public int L2 { get; init; } = 2048;
    public int L3 { get; init; } = 12288;
    public long RamBytes { get; init; }
    public long FreeMemoryKb { get; init; }
    public bool TpmPresent { get; init; } = true;
    public string TpmSpec { get; init; } = "2.0, 0, 1.38";
    public int SecureBoot { get; init; } = 1;
    public List<DemoModule> Modules { get; init; } = new();
    public List<DemoDisk> Disks { get; init; } = new();
    public List<DemoVolume> Volumes { get; init; } = new();
    public List<DemoGpu> Gpus { get; init; } = new();
    public List<DemoMonitor> Monitors { get; init; } = new();
    public List<DemoNic> Nics { get; init; } = new();
    public DemoBattery? Battery { get; init; }
    public List<DemoBitLocker> BitLocker { get; init; } = new();
    public List<UninstallRecord> Programs { get; init; } = new();
    public List<DemoHotfix> Hotfixes { get; init; } = new();
    public List<DemoService> Services { get; init; } = new();
    public List<RunKeyRecord> RunKeys { get; init; } = new();
    public Dictionary<string, byte[]> StartupApproved { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DemoStartup> StartupCommands { get; init; } = new();
}

public sealed record DemoModule(string Slot, long Size, int Speed, string Part, string Maker, string Serial);
public sealed record DemoDisk(string Model, long Size, int MediaType, int Health, int Bus, string Serial);
public sealed record DemoVolume(string Drive, string Label, string FileSystem, long Size, long Free);
public sealed record DemoGpu(string Name, long Ram, string Driver, DateTimeOffset DriverDate, string Mode);
public sealed record DemoMonitor(string Name, string Maker, string Product, string Serial);
public sealed record DemoNic(int Index, string Name, string Connection, string Mac, long Speed, string[] Ips, string[] Gateways, bool Dhcp, bool Physical);
public sealed record DemoBattery(string Name, int Charge, int Design, int Full);
public sealed record DemoBitLocker(string Drive, int Protection, int Method, int Conversion);
public sealed record DemoHotfix(string Id, string Description, string InstalledOn, string InstalledBy);
public sealed record DemoService(string Name, string Display, string State, string Start, string Account);
public sealed record DemoStartup(string Name, string Command, string Location, string User);

public static class DemoCatalog
{
    public const string PrimaryName = "NW-LAP-014";

    public static IReadOnlyList<string> SampleNames { get; } =
    [
        "NW-LAP-014",
        "NW-DESK-102",
        "NW-DESK-118",
        "FS-LAB-007",
        "RECEPTION-01",
        "BLOCKED-PC",
        "OFFLINE-PC"
    ];

    public static bool IsOffline(string name) =>
        name.Contains("OFFLINE", StringComparison.OrdinalIgnoreCase);

    public static bool IsBlocked(string name) =>
        name.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase)
        || name.Contains("DENIED", StringComparison.OrdinalIgnoreCase);

    public static DemoProfile Resolve(string computer)
    {
        var key = computer.Trim();
        foreach (var profile in All())
        {
            if (profile.ComputerName.Equals(key, StringComparison.OrdinalIgnoreCase))
                return profile;
        }
        return Generated(key);
    }

    public static DemoProfile Primary() => Resolve(PrimaryName);

    public static IEnumerable<DemoProfile> All()
    {
        yield return Laptop();
        yield return Desk102();
        yield return Desk118();
        yield return Lab();
        yield return Reception();
    }

    private static DemoProfile Laptop() => new()
    {
        ComputerName = PrimaryName,
        Manufacturer = "Dell Inc.",
        Model = "Latitude 7450",
        Serial = "7F3K2C1",
        BiosVersion = "1.8.1",
        BiosDate = new DateTimeOffset(2025, 11, 2, 0, 0, 0, TimeSpan.Zero),
        InstallDate = new DateTimeOffset(2024, 6, 18, 9, 30, 0, TimeSpan.Zero),
        UptimeHours = 26,
        LoggedOnUser = @"NORTHWIND\a.chen",
        CpuName = "Intel(R) Core(TM) Ultra 7 165U",
        Cores = 12,
        Logical = 14,
        Mhz = 2100,
        L3 = 12288,
        RamBytes = 32L * 1024 * 1024 * 1024,
        FreeMemoryKb = 14L * 1024 * 1024,
        Modules =
        [
            new("DIMM 1", 16L * 1024 * 1024 * 1024, 5600, "NWM16G5600-7F3", "Micron", "M16A7F3"),
            new("DIMM 2", 16L * 1024 * 1024 * 1024, 5600, "NWM16G5600-7F4", "Micron", "M16A7F4")
        ],
        Disks = [new("Samsung PM9B1 NVMe 1024GB", 1024L * 1024 * 1024 * 1024, 4, 0, 17, "S6B1NW014")],
        Volumes =
        [
            new("C:", "Windows", "NTFS", 952L * 1024 * 1024 * 1024, 680L * 1024 * 1024 * 1024),
            new("D:", "Data", "NTFS", 48L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024)
        ],
        Gpus = [new("Intel(R) Graphics", 128L * 1024 * 1024, "32.0.101.5972", new DateTimeOffset(2025, 9, 12, 0, 0, 0, TimeSpan.Zero), "1920 x 1200 x 4294967296 colors")],
        Monitors = [new("Dell C2423H", "DEL", "3C24", "CN0NW14L7440")],
        Nics =
        [
            new(1, "Intel(R) Wi-Fi 6E AX211", "Wi-Fi", "A4:83:E7:2C:10:04", 2_400_000_000, ["10.20.14.42"], ["10.20.14.1"], true, true),
            new(2, "Intel(R) Ethernet Connection", "Ethernet", "A4:83:E7:2C:10:05", 1_000_000_000, ["172.16.8.55"], ["172.16.8.1"], true, true)
        ],
        Battery = new("DELL Y0XJ9", 76, 57000, 49100),
        BitLocker =
        [
            new("C:", 1, 6, 1),
            new("D:", 0, 0, 0)
        ],
        Programs = LaptopPrograms(),
        Hotfixes = StandardHotfixes(),
        Services = StandardServices(),
        RunKeys = LaptopRunKeys(),
        StartupApproved = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["SecurityHealth"] = [0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            ["Spotify"] = [0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
        },
        StartupCommands =
        [
            new("Microsoft Teams", @"C:\Users\a.chen\AppData\Local\Microsoft\Teams\Update.exe --processStart ""Teams.exe""", "Startup", @"NORTHWIND\a.chen")
        ]
    };

    private static DemoProfile Desk102() => Desktop(
        "NW-DESK-102", "Dell Inc.", "OptiPlex 7010", "9D2L8Q4", @"NORTHWIND\m.owens",
        "13th Gen Intel(R) Core(TM) i5-13500", 14, 20, 2500, 16L * 1024 * 1024 * 1024, 6L * 1024 * 1024,
        "NVIDIA GeForce RTX 4060", 2L * 1024 * 1024 * 1024);

    private static DemoProfile Desk118() => new()
    {
        ComputerName = "NW-DESK-118",
        Manufacturer = "Lenovo",
        Model = "ThinkCentre M90q",
        Serial = "PC2N4W118",
        BiosVersion = "M4CKT45A",
        BiosDate = new DateTimeOffset(2024, 3, 11, 0, 0, 0, TimeSpan.Zero),
        OsCaption = "Microsoft Windows 10 Pro",
        OsVersion = "10.0.19045",
        OsBuild = "19045",
        Ubr = 4780,
        DisplayVersion = "22H2",
        InstallDate = new DateTimeOffset(2022, 4, 4, 8, 0, 0, TimeSpan.Zero),
        UptimeHours = 140,
        LoggedOnUser = @"NORTHWIND\j.patel",
        CpuName = "Intel(R) Core(TM) i7-10700T",
        Cores = 8,
        Logical = 16,
        Mhz = 2000,
        RamBytes = 32L * 1024 * 1024 * 1024,
        FreeMemoryKb = 18L * 1024 * 1024,
        Modules = [new("DIMM 1", 32L * 1024 * 1024 * 1024, 3200, "M378A4G43AB2", "Samsung", "S32N118")],
        Disks = [new("Kioxia KBG40ZNS512G", 512L * 1024 * 1024 * 1024, 4, 0, 17, "KIO118NW")],
        Volumes = [new("C:", "OS", "NTFS", 476L * 1024 * 1024 * 1024, 210L * 1024 * 1024 * 1024)],
        Gpus = [new("Intel(R) UHD Graphics 630", 128L * 1024 * 1024, "27.20.100.9664", new DateTimeOffset(2023, 5, 1, 0, 0, 0, TimeSpan.Zero), "1920 x 1080 x 4294967296 colors")],
        Monitors = [new("Lenovo ThinkVision T24i-20", "LEN", "T24i", "VNA118NW")],
        Nics = [new(4, "Intel(R) Ethernet Connection I219-LM", "Ethernet", "70:B5:E8:11:80:18", 1_000_000_000, ["10.20.8.118"], ["10.20.8.1"], true, true)],
        BitLocker = [new("C:", 1, 7, 1)],
        Programs = StandardPrograms(),
        Hotfixes = StandardHotfixes(),
        Services = StandardServices(),
        RunKeys = [new(@"HKLM\Run", "SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe", "All users")]
    };

    private static DemoProfile Lab() => Desktop(
        "FS-LAB-007", "HP", "Z2 Tower G9", "HPZ2007NW", @"NORTHWIND\lab",
        "Intel(R) Xeon(R) w3-2435", 8, 16, 3200, 64L * 1024 * 1024 * 1024, 40L * 1024 * 1024,
        "NVIDIA RTX A2000", 2L * 1024 * 1024 * 1024);

    private static DemoProfile Reception() => new()
    {
        ComputerName = "RECEPTION-01",
        PartOfDomain = false,
        Domain = "RECEPTION-01",
        Workgroup = "NORTHWIND",
        Manufacturer = "HP",
        Model = "ProDesk 400 G7",
        Serial = "8CC104NW01",
        BiosVersion = "S21 Ver. 02.12.00",
        BiosDate = new DateTimeOffset(2023, 1, 20, 0, 0, 0, TimeSpan.Zero),
        InstallDate = new DateTimeOffset(2023, 2, 2, 11, 0, 0, TimeSpan.Zero),
        UptimeHours = 8.5,
        LoggedOnUser = @"RECEPTION-01\frontdesk",
        CpuName = "Intel(R) Core(TM) i3-10100",
        Cores = 4,
        Logical = 8,
        Mhz = 3600,
        RamBytes = 8L * 1024 * 1024 * 1024,
        FreeMemoryKb = 3L * 1024 * 1024,
        Modules = [new("DIMM 1", 8L * 1024 * 1024 * 1024, 2666, "HP8G2666", "SK Hynix", "HY8NW01")],
        Disks = [new("WD Blue SA510 2.5 500GB", 500L * 1024 * 1024 * 1024, 4, 1, 11, "WD500NW01")],
        Volumes = [new("C:", "", "NTFS", 476L * 1024 * 1024 * 1024, 40L * 1024 * 1024 * 1024)],
        Gpus = [new("Intel(R) UHD Graphics 630", 64L * 1024 * 1024, "27.20.100.8935", new DateTimeOffset(2022, 8, 1, 0, 0, 0, TimeSpan.Zero), "1600 x 900 x 4294967296 colors")],
        Monitors = [new("HP P22v G5", "HWP", "P22v", "3CQNW0101")],
        Nics = [new(3, "Realtek PCIe GbE Family Controller", "Ethernet", "18:C0:4D:01:10:01", 1_000_000_000, ["192.168.1.40"], ["192.168.1.1"], true, true)],
        BitLocker = [new("C:", 0, 0, 0)],
        Programs = StandardPrograms(),
        Hotfixes = StandardHotfixes(),
        Services = StandardServices(),
        RunKeys = [new(@"HKLM\Run", "SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe", "All users")]
    };

    private static DemoProfile Desktop(string name, string maker, string model, string serial, string user, string cpu, int cores, int logical, int mhz, long ram, long freeKb, string gpu, long gpuRam) => new()
    {
        ComputerName = name,
        Manufacturer = maker,
        Model = model,
        Serial = serial,
        BiosDate = new DateTimeOffset(2025, 4, 9, 0, 0, 0, TimeSpan.Zero),
        InstallDate = new DateTimeOffset(2024, 9, 1, 10, 0, 0, TimeSpan.Zero),
        UptimeHours = 50,
        LoggedOnUser = user,
        CpuName = cpu,
        Cores = cores,
        Logical = logical,
        Mhz = mhz,
        RamBytes = ram,
        FreeMemoryKb = freeKb,
        Modules = [new("DIMM 1", ram, 4800, "NWM-" + serial, maker, serial + "M")],
        Disks = [new(maker + " NVMe 1TB", 1024L * 1024 * 1024 * 1024, 4, 0, 17, serial + "D")],
        Volumes = [new("C:", "Windows", "NTFS", 952L * 1024 * 1024 * 1024, 400L * 1024 * 1024 * 1024)],
        Gpus = [new(gpu, gpuRam, "32.0.15.6094", new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), "2560 x 1440 x 4294967296 colors")],
        Monitors = [new(maker + " 27", maker[..Math.Min(3, maker.Length)].ToUpperInvariant(), "P27", "MON" + serial)],
        Nics = [new(5, "Ethernet", "Ethernet", "00:1A:2B:" + serial[..Math.Min(2, serial.Length)] + ":00:11", 1_000_000_000, ["10.20.4.20"], ["10.20.4.1"], true, true)],
        BitLocker = [new("C:", 1, 6, 1)],
        Programs = StandardPrograms(),
        Hotfixes = StandardHotfixes(),
        Services = StandardServices(),
        RunKeys = [new(@"HKLM\Run", "SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe", "All users")]
    };

    private static DemoProfile Generated(string name)
    {
        var hash = StableHash(name);
        var serial = "GEN" + (hash % 900000 + 100000).ToString(CultureInfo.InvariantCulture);
        var ramGb = 8 * (1 + hash % 4);
        return Desktop(name, "Dell Inc.", "OptiPlex 5000", serial, @"NORTHWIND\user", "Intel(R) Core(TM) i5-12500", 6, 12, 3000, ramGb * 1024L * 1024 * 1024, 4L * 1024 * 1024, "Intel(R) UHD Graphics 770", 128L * 1024 * 1024);
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in text.ToUpperInvariant())
                hash = (hash * 31) + c;
            return hash & 0x7fffffff;
        }
    }

    public static List<UninstallRecord> StandardPrograms() =>
    [
        Program("HKLM", "Microsoft Edge", "130.0.2849.56", "Microsoft Corporation", "20250612", 250_000),
        Program("HKLM", "Google Chrome", "130.0.6723.92", "Google LLC", "20250902", 310_000),
        Program("HKLM", "7-Zip 24.08", "24.08", "Igor Pavlov", "20240815", 5_600),
        Program("HKLM", "Adobe Acrobat Reader", "24.003.20112", "Adobe Inc.", "20241001", 480_000),
        Program("HKLM", "Northwind Endpoint Agent", "4.3.1", "Northwind Field Services", "20250120", 82_000)
    ];

    private static List<UninstallRecord> LaptopPrograms()
    {
        var list = StandardPrograms();
        list.Add(Program("HKCU", "Google Chrome", "130.0.6723.92", "", "20250902", 310_000));
        list.Add(Program("HKLM", "Microsoft 365 Apps for enterprise", "16.0.17928.20216", "Microsoft Corporation", "20240711", 3_400_000));
        list.Add(Program("HKLM", "Zoom Workplace", "6.2.3", "Zoom Communications, Inc.", "20250819", 140_000));
        list.Add(Program("HKLM", "Visual Studio Code", "1.94.2", "Microsoft Corporation", "20241008", 380_000));
        list.Add(Program("HKLM", "Dell Command Update", "5.4.0", "Dell Inc.", "20250303", 92_000));
        list.Add(Program("HKLM", "Notepad++", "8.7", "Don Ho", "20240930", 8_200));
        list.Add(Program("HKCU", "Slack", "4.40.128", "Slack Technologies", "20250601", 210_000));
        list.Add(Program("HKLM (32-bit)", "Legacy Label Printer", "2.1.0", "Contoso Peripherals", "20191102", 18_000));
        list.Add(new UninstallRecord("HKLM", "Microsoft Update Health Tools", "3.74", "Microsoft Corporation", "20240101", 12_000, true, null, null));
        list.Add(new UninstallRecord("HKLM", "Update for Microsoft Office", "16.0.1", "Microsoft Corporation", "20240101", 1_000, false, null, null));
        list.Add(new UninstallRecord("HKLM", "Security Update for Windows", "1.0", "Microsoft Corporation", "20240101", 1_000, false, null, "Security Update"));
        return list;
    }

    private static UninstallRecord Program(string source, string name, string version, string publisher, string installed, long sizeKb) =>
        new(source, name, version, publisher, installed, sizeKb, false, null, null);

    public static List<DemoHotfix> StandardHotfixes() =>
    [
        new("KB5044285", "Security Update", "10/8/2025", @"NT AUTHORITY\SYSTEM"),
        new("KB5044021", "Update", "9/12/2025", @"NT AUTHORITY\SYSTEM"),
        new("KB5039336", ".NET Framework update", "6/11/2025", @"NT AUTHORITY\SYSTEM"),
        new("KB5036893", "Update", "3/12/2025", @"NORTHWIND\a.chen")
    ];

    public static List<DemoService> StandardServices() =>
    [
        new("Winmgmt", "Windows Management Instrumentation", "Running", "Auto", "LocalSystem"),
        new("WinRM", "Windows Remote Management (WS-Management)", "Running", "Auto", "NT AUTHORITY\\NetworkService"),
        new("Spooler", "Print Spooler", "Running", "Auto", "LocalSystem"),
        new("BITS", "Background Intelligent Transfer Service", "Running", "Manual", "LocalSystem"),
        new("Wuauserv", "Windows Update", "Stopped", "Manual", "LocalSystem"),
        new("W32Time", "Windows Time", "Running", "Manual", "LocalSystem"),
        new("EventLog", "Windows Event Log", "Running", "Auto", "LocalSystem"),
        new("Dhcp", "DHCP Client", "Running", "Auto", "NT AUTHORITY\\LocalService"),
        new("Dnscache", "DNS Client", "Running", "Auto", "NT AUTHORITY\\NetworkService"),
        new("BFE", "Base Filtering Engine", "Running", "Auto", "NT AUTHORITY\\LocalService"),
        new("mpssvc", "Windows Defender Firewall", "Running", "Auto", "NT AUTHORITY\\LocalService"),
        new("LanmanWorkstation", "Workstation", "Running", "Auto", "LocalSystem"),
        new("Schedule", "Task Scheduler", "Running", "Auto", "LocalSystem"),
        new("TermService", "Remote Desktop Services", "Stopped", "Manual", "NT AUTHORITY\\NetworkService")
    ];

    private static List<RunKeyRecord> LaptopRunKeys() =>
    [
        new(@"HKLM\Run", "SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe", "All users"),
        new(@"HKLM\Run", "Northwind Agent", @"C:\Program Files\Northwind\Agent\nwagent.exe --tray", "All users"),
        new(@"HKCU\Run", "OneDrive", @"C:\Users\a.chen\AppData\Local\Microsoft\OneDrive\OneDrive.exe /background", @"NORTHWIND\a.chen"),
        new(@"HKCU\Run", "Spotify", @"C:\Users\a.chen\AppData\Roaming\Spotify\Spotify.exe --autostart", @"NORTHWIND\a.chen")
    ];
}

public static class DemoInstances
{
    public static string ToDmtf(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".000000+000";
    }

    public static IReadOnlyList<WmiInstance> Create(DemoProfile profile, string className)
    {
        var nowBoot = DateTimeOffset.Now.AddHours(-profile.UptimeHours);
        return className switch
        {
            "Win32_ComputerSystem" => [Row(
                ("Name", profile.ComputerName),
                ("Domain", profile.Domain),
                ("Workgroup", profile.Workgroup),
                ("PartOfDomain", profile.PartOfDomain),
                ("Manufacturer", profile.Manufacturer),
                ("Model", profile.Model),
                ("UserName", profile.LoggedOnUser),
                ("TotalPhysicalMemory", profile.RamBytes))],
            "Win32_BIOS" => [Row(
                ("SerialNumber", profile.BiosSerial),
                ("SMBIOSBIOSVersion", profile.BiosVersion),
                ("ReleaseDate", ToDmtf(profile.BiosDate)),
                ("Manufacturer", profile.Manufacturer))],
            "Win32_ComputerSystemProduct" => [Row(("IdentifyingNumber", profile.Serial), ("Name", profile.Model), ("Vendor", profile.Manufacturer))],
            "Win32_OperatingSystem" => [Row(
                ("Caption", profile.OsCaption),
                ("Version", profile.OsVersion),
                ("BuildNumber", profile.OsBuild),
                ("OSArchitecture", profile.OsArchitecture),
                ("InstallDate", ToDmtf(profile.InstallDate)),
                ("LastBootUpTime", ToDmtf(nowBoot)),
                ("FreePhysicalMemory", profile.FreeMemoryKb))],
            "Win32_Processor" => [Row(
                ("Name", profile.CpuName),
                ("Manufacturer", profile.CpuManufacturer),
                ("NumberOfCores", profile.Cores),
                ("NumberOfLogicalProcessors", profile.Logical),
                ("MaxClockSpeed", profile.Mhz),
                ("SocketDesignation", "U3E1"),
                ("L2CacheSize", profile.L2),
                ("L3CacheSize", profile.L3))],
            "Win32_PhysicalMemory" => profile.Modules.Select(m => Row(
                ("DeviceLocator", m.Slot), ("BankLabel", m.Slot), ("Capacity", m.Size), ("Speed", m.Speed),
                ("PartNumber", m.Part), ("Manufacturer", m.Maker), ("SerialNumber", m.Serial))).ToList(),
            "Win32_LogicalDisk" => profile.Volumes.Select(v => Row(
                ("DeviceID", v.Drive), ("VolumeName", v.Label), ("FileSystem", v.FileSystem),
                ("Size", v.Size), ("FreeSpace", v.Free), ("DriveType", 3))).ToList(),
            "MSFT_PhysicalDisk" => profile.Disks.Select(d => Row(
                ("FriendlyName", d.Model), ("Size", d.Size), ("MediaType", d.MediaType),
                ("HealthStatus", d.Health), ("BusType", d.Bus), ("SerialNumber", d.Serial))).ToList(),
            "Win32_DiskDrive" => profile.Disks.Select(d => Row(
                ("Model", d.Model), ("Size", d.Size), ("MediaType", "Fixed hard disk media"),
                ("InterfaceType", d.Bus == 17 ? "NVMe" : "SCSI"), ("SerialNumber", d.Serial))).ToList(),
            "Win32_VideoController" => profile.Gpus.Select(g => Row(
                ("Name", g.Name), ("AdapterRAM", g.Ram), ("DriverVersion", g.Driver),
                ("DriverDate", ToDmtf(g.DriverDate)), ("VideoModeDescription", g.Mode))).ToList(),
            "WmiMonitorID" => profile.Monitors.Select(m => Row(
                ("UserFriendlyName", Units(m.Name)), ("ManufacturerName", Units(m.Maker)),
                ("ProductCodeID", Units(m.Product)), ("SerialNumberID", Units(m.Serial)))).ToList(),
            "Win32_NetworkAdapter" => profile.Nics.Select(n => Row(
                ("Index", n.Index), ("Name", n.Name), ("NetConnectionID", n.Connection),
                ("MACAddress", n.Mac), ("Speed", n.Speed), ("PhysicalAdapter", n.Physical), ("NetEnabled", true))).ToList(),
            "Win32_NetworkAdapterConfiguration" => profile.Nics.Select(n => Row(
                ("Index", n.Index), ("Description", n.Name), ("MACAddress", n.Mac),
                ("IPAddress", n.Ips), ("DefaultIPGateway", n.Gateways), ("DHCPEnabled", n.Dhcp), ("IPEnabled", true))).ToList(),
            "Win32_Tpm" => profile.TpmPresent
                ? [Row(("SpecVersion", profile.TpmSpec), ("IsEnabled_InitialValue", true), ("IsActivated_InitialValue", true))]
                : Array.Empty<WmiInstance>(),
            "Win32_EncryptableVolume" => profile.BitLocker.Select(b => Row(
                ("DriveLetter", b.Drive), ("ProtectionStatus", b.Protection),
                ("EncryptionMethod", b.Method), ("ConversionStatus", b.Conversion))).ToList(),
            "Win32_Battery" => profile.Battery is null
                ? Array.Empty<WmiInstance>()
                : [Row(("Name", profile.Battery.Name), ("EstimatedChargeRemaining", profile.Battery.Charge))],
            "BatteryStaticData" => profile.Battery is null
                ? Array.Empty<WmiInstance>()
                : [Row(("DesignedCapacity", profile.Battery.Design))],
            "BatteryFullChargedCapacity" => profile.Battery is null
                ? Array.Empty<WmiInstance>()
                : [Row(("FullChargedCapacity", profile.Battery.Full))],
            "Win32_QuickFixEngineering" => profile.Hotfixes.Select(h => Row(
                ("HotFixID", h.Id), ("Description", h.Description), ("InstalledOn", h.InstalledOn), ("InstalledBy", h.InstalledBy))).ToList(),
            "Win32_Service" => profile.Services.Select(s => Row(
                ("Name", s.Name), ("DisplayName", s.Display), ("State", s.State), ("StartMode", s.Start), ("StartName", s.Account))).ToList(),
            "Win32_StartupCommand" => profile.StartupCommands.Select(s => Row(
                ("Name", s.Name), ("Command", s.Command), ("Location", s.Location), ("User", s.User))).ToList(),
            _ => Array.Empty<WmiInstance>()
        };
    }

    private static WmiInstance Row(params (string Name, object? Value)[] props)
    {
        var instance = new WmiInstance();
        foreach (var (name, value) in props)
            instance.Set(name, value);
        return instance;
    }

    private static ushort[] Units(string text)
    {
        var data = new ushort[text.Length + 1];
        for (var i = 0; i < text.Length; i++)
            data[i] = text[i];
        return data;
    }
}

public sealed class DemoWmiSession : IWmiSession
{
    private readonly DemoProfile _profile;

    public DemoWmiSession(DemoProfile profile) => _profile = profile;

    public string Computer => _profile.ComputerName;

    public Task<IReadOnlyList<WmiInstance>> QueryAsync(string scope, string wql, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!WinRmProtocol.TryClassFromWql(wql, out var className))
            return Task.FromResult<IReadOnlyList<WmiInstance>>(Array.Empty<WmiInstance>());
        return Task.FromResult(DemoInstances.Create(_profile, className));
    }

    public Task<WmiMethodResult> InvokeAsync(string scope, string className, string method, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputs = new WmiInstance();
        outputs.Set("ReturnValue", 0);
        return Task.FromResult(new WmiMethodResult { Outputs = outputs });
    }
}

public sealed class DemoWmiSessionFactory : IWmiSessionFactory
{
    public Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = target.IsLocal ? DemoCatalog.PrimaryName : target.ComputerName;
        if (DemoCatalog.IsOffline(name))
        {
            throw new CollectionFailedException(name,
            [
                ConnectionErrorFormatter.Format(new TransportException("DCOM", TransportErrorCode.RpcServerUnavailable, "The RPC server is unavailable. (0x800706BA)")),
                ConnectionErrorFormatter.Format(new TransportException("WinRM", TransportErrorCode.WinRmConnection, "The client cannot connect to the destination specified in the request."))
            ]);
        }

        if (DemoCatalog.IsBlocked(name))
        {
            throw new CollectionFailedException(name,
            [
                ConnectionErrorFormatter.Format(new TransportException("DCOM", TransportErrorCode.AccessDenied, "Access is denied. (0x80070005)")),
                ConnectionErrorFormatter.Format(new TransportException("WinRM", TransportErrorCode.AccessDenied, "Access is denied."))
            ]);
        }

        return Task.FromResult<IWmiSession>(new DemoWmiSession(DemoCatalog.Resolve(name)));
    }
}

public sealed class DemoRegistryReader : IRegistryReader
{
    private readonly DemoProfile _profile;

    public DemoRegistryReader(DemoProfile profile) => _profile = profile;

    public IReadOnlyList<string> Warnings { get; } = Array.Empty<string>();

    public IReadOnlyList<UninstallRecord> ReadUninstallRecords() => _profile.Programs;

    public IReadOnlyList<RunKeyRecord> ReadRunKeys() => _profile.RunKeys;

    public string? ReadString(string hive, string keyPath, string valueName)
    {
        if (valueName.Equals("DisplayVersion", StringComparison.OrdinalIgnoreCase))
            return _profile.DisplayVersion;
        return null;
    }

    public int? ReadDword(string hive, string keyPath, string valueName)
    {
        if (valueName.Equals("UBR", StringComparison.OrdinalIgnoreCase))
            return _profile.Ubr;
        if (valueName.Equals("UEFISecureBootEnabled", StringComparison.OrdinalIgnoreCase))
            return _profile.SecureBoot;
        return null;
    }

    public IReadOnlyDictionary<string, byte[]> ReadStartupApproved(string hive, string keyPath) => _profile.StartupApproved;
}

public sealed class DemoRegistryFactory : IRegistryReaderFactory
{
    public IRegistryReader Create(ConnectionTarget target, IWmiSession session)
    {
        var name = session.Computer.Length > 0
            ? session.Computer
            : target.IsLocal ? DemoCatalog.PrimaryName : target.ComputerName;
        return new DemoRegistryReader(DemoCatalog.Resolve(name));
    }
}
