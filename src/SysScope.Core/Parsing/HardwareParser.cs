using SysScope.Core.Models;
using SysScope.Core.Wmi;

namespace SysScope.Core.Parsing;

public static class HardwareParser
{
    public static List<CpuInfo> ParseCpus(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<CpuInfo>();
        foreach (var row in rows)
        {
            var name = Collapse(row.Text("Name"));
            if (name.Length == 0)
                continue;
            list.Add(new CpuInfo
            {
                Name = name,
                Manufacturer = row.Text("Manufacturer"),
                Cores = row.Int32("NumberOfCores"),
                LogicalProcessors = row.Int32("NumberOfLogicalProcessors"),
                MaxClockMhz = row.Int32("MaxClockSpeed"),
                Socket = row.Text("SocketDesignation"),
                L2CacheKb = row.Int32("L2CacheSize"),
                L3CacheKb = row.Int32("L3CacheSize")
            });
        }
        return list;
    }

    public static string SummarizeCpus(IReadOnlyList<CpuInfo> cpus)
    {
        if (cpus.Count == 0)
            return "";
        if (cpus.Count == 1)
        {
            var cpu = cpus[0];
            var cores = cpu.Cores is null ? "" : $", {cpu.Cores} cores";
            var threads = cpu.LogicalProcessors is null ? "" : $" / {cpu.LogicalProcessors} threads";
            return $"{cpu.Name}{cores}{threads}";
        }
        return string.Join("; ", cpus.Select(cpu => cpu.Name));
    }

    public static string Collapse(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static List<MemoryModule> ParseMemory(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<MemoryModule>();
        foreach (var row in rows)
        {
            var size = row.Int64("Capacity");
            if (size is null or <= 0)
                continue;
            var slot = row.Text("DeviceLocator");
            if (slot.Length == 0)
                slot = row.Text("BankLabel");
            list.Add(new MemoryModule
            {
                Slot = slot,
                SizeBytes = size,
                SpeedMhz = row.Int32("Speed"),
                PartNumber = Collapse(row.Text("PartNumber")),
                Manufacturer = Collapse(row.Text("Manufacturer")),
                Serial = Collapse(row.Text("SerialNumber"))
            });
        }
        return list;
    }

    public static List<PhysicalDisk> ParseDisks(IReadOnlyList<WmiInstance> msft, IReadOnlyList<WmiInstance> drives)
    {
        if (msft.Count > 0)
        {
            return msft.Select(row => new PhysicalDisk
            {
                Model = FirstNonEmpty(row.Text("FriendlyName"), row.Text("Model")),
                SizeBytes = row.Int64("Size"),
                MediaType = MediaType(row.Int32("MediaType")),
                Health = Health(row.Int32("HealthStatus")),
                Bus = Bus(row.Int32("BusType")),
                Serial = Collapse(row.Text("SerialNumber")),
                SmartSource = "MSFT_PhysicalDisk"
            }).Where(disk => disk.Model.Length > 0 || disk.SizeBytes is > 0).ToList();
        }

        return drives.Select(row =>
        {
            var model = row.Text("Model");
            return new PhysicalDisk
            {
                Model = Collapse(model),
                SizeBytes = row.Int64("Size"),
                MediaType = DriveMedia(row.Text("MediaType"), row.Text("InterfaceType"), model),
                Health = "Not available",
                Bus = row.Text("InterfaceType"),
                Serial = Collapse(row.Text("SerialNumber")),
                SmartSource = "MSFT_PhysicalDisk not available"
            };
        }).Where(disk => disk.Model.Length > 0 || disk.SizeBytes is > 0).ToList();
    }

    public static string MediaType(int? code) => code switch
    {
        0 => "Unspecified",
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        null => "",
        _ => $"Other ({code})"
    };

    public static string Health(int? code) => code switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        null => "Not available",
        _ => "Unknown"
    };

    public static string Bus(int? code) => code switch
    {
        7 => "USB",
        8 => "SD",
        11 => "SATA",
        17 => "NVMe",
        null or 0 => "",
        _ => $"Bus {code}"
    };

    private static string DriveMedia(string media, string interfaceType, string model)
    {
        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) || interfaceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            return "SSD";
        if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase))
            return "SSD";
        return string.IsNullOrWhiteSpace(media) ? interfaceType : media.Trim();
    }

    public static List<VolumeInfo> ParseVolumes(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<VolumeInfo>();
        foreach (var row in rows)
        {
            var type = row.Int32("DriveType");
            if (type is not (2 or 3))
                continue;
            list.Add(new VolumeInfo
            {
                Drive = row.Text("DeviceID"),
                Label = row.Text("VolumeName"),
                FileSystem = row.Text("FileSystem"),
                SizeBytes = row.Int64("Size"),
                FreeBytes = row.Int64("FreeSpace")
            });
        }
        return list.OrderBy(v => v.Drive, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<GpuInfo> ParseGpus(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<GpuInfo>();
        foreach (var row in rows)
        {
            var name = row.Text("Name");
            if (name.Length == 0)
                continue;
            var horizontal = row.Int32("CurrentHorizontalResolution");
            var vertical = row.Int32("CurrentVerticalResolution");
            var mode = row.Text("VideoModeDescription");
            if (mode.Length == 0 && horizontal is > 0 && vertical is > 0)
                mode = $"{horizontal} x {vertical}";
            list.Add(new GpuInfo
            {
                Name = name,
                AdapterRamBytes = AdapterRam(row.Int64("AdapterRAM")),
                DriverVersion = row.Text("DriverVersion"),
                DriverDate = WmiDateParser.Parse(row.Text("DriverDate")),
                VideoMode = mode
            });
        }
        return list;
    }

    public static long? AdapterRam(long? value)
    {
        if (value is null or <= 0)
            return null;
        if (value >= uint.MaxValue)
            return null;
        return value;
    }

    public static List<MonitorInfo> ParseMonitors(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<MonitorInfo>();
        foreach (var row in rows)
        {
            var name = MonitorIdDecoder.Decode(row.Get("UserFriendlyName"));
            if (name.Length == 0)
                name = MonitorIdDecoder.Decode(row.Get("InstanceName"));
            var serial = MonitorIdDecoder.Decode(row.Get("SerialNumberID"));
            var manufacturer = MonitorIdDecoder.Decode(row.Get("ManufacturerName"));
            var product = MonitorIdDecoder.Decode(row.Get("ProductCodeID"));
            if (name.Length == 0 && serial.Length == 0)
                continue;
            list.Add(new MonitorInfo
            {
                Name = name,
                Manufacturer = manufacturer,
                ProductCode = product,
                Serial = serial
            });
        }
        return list;
    }

    public static BatteryInfo ParseBattery(
        IReadOnlyList<WmiInstance> batteries,
        IReadOnlyList<WmiInstance> staticData,
        IReadOnlyList<WmiInstance> fullCharge)
    {
        if (batteries.Count == 0 && staticData.Count == 0 && fullCharge.Count == 0)
            return new BatteryInfo { Present = false };

        var battery = batteries.FirstOrDefault();
        var design = staticData.FirstOrDefault()?.Int32("DesignedCapacity");
        var full = fullCharge.FirstOrDefault()?.Int32("FullChargedCapacity");
        double? health = null;
        if (design is > 0 && full is > 0)
            health = Math.Round(100.0 * full.Value / design.Value, 1);

        return new BatteryInfo
        {
            Present = true,
            Name = battery is null || battery.Text("Name").Length == 0 ? "Battery" : battery.Text("Name"),
            ChargePercent = battery?.Int32("EstimatedChargeRemaining"),
            DesignCapacityMwh = design,
            FullChargeCapacityMwh = full,
            HealthPercent = health
        };
    }

    private static string FirstNonEmpty(string first, string second) =>
        first.Length > 0 ? first : second;
}
