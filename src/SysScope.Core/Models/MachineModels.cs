namespace SysScope.Core.Models;

public enum SecureBootState
{
    Unknown = 0,
    Enabled,
    Disabled,
    Unsupported
}

public sealed class MachineReport
{
    public string RequestedName { get; set; } = "";
    public bool Online { get; set; }
    public string Error { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public OverviewInfo Overview { get; set; } = new();
    public HardwareInfo Hardware { get; set; } = new();
    public List<InstalledProgram> Programs { get; set; } = new();
    public List<Hotfix> Hotfixes { get; set; } = new();
    public List<ServiceInfo> Services { get; set; } = new();
    public List<StartupItem> StartupItems { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public sealed class OverviewInfo
{
    public string ComputerName { get; set; } = "";
    public string DomainOrWorkgroup { get; set; } = "";
    public bool PartOfDomain { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string BiosVersion { get; set; } = "";
    public DateTimeOffset? BiosDate { get; set; }
    public string OsEdition { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string OsBuild { get; set; } = "";
    public DateTimeOffset? InstallDate { get; set; }
    public DateTimeOffset? LastBoot { get; set; }
    public TimeSpan? Uptime { get; set; }
    public string LoggedOnUser { get; set; } = "";
    public string Cpu { get; set; } = "";
    public long? RamBytes { get; set; }
    public long? RamUsedBytes { get; set; }
    public bool? TpmPresent { get; set; }
    public string TpmVersion { get; set; } = "";
    public SecureBootState SecureBoot { get; set; } = SecureBootState.Unknown;
    public List<BitLockerVolume> BitLocker { get; set; } = new();
    public List<NetworkAdapter> Adapters { get; set; } = new();
}

public sealed class HardwareInfo
{
    public List<CpuInfo> Cpus { get; set; } = new();
    public List<MemoryModule> MemoryModules { get; set; } = new();
    public List<PhysicalDisk> Disks { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public List<MonitorInfo> Monitors { get; set; } = new();
    public List<NetworkAdapter> Adapters { get; set; } = new();
    public BatteryInfo Battery { get; set; } = new();
}

public sealed class BitLockerVolume
{
    public string DriveLetter { get; set; } = "";
    public string Status { get; set; } = "";
    public string EncryptionMethod { get; set; } = "";
    public string ConversionStatus { get; set; } = "";
}

public sealed class NetworkAdapter
{
    public string Name { get; set; } = "";
    public string Mac { get; set; } = "";
    public string IpAddresses { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string Dhcp { get; set; } = "";
    public string LinkSpeed { get; set; } = "";
}

public sealed class CpuInfo
{
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public int? Cores { get; set; }
    public int? LogicalProcessors { get; set; }
    public int? MaxClockMhz { get; set; }
    public string Socket { get; set; } = "";
    public int? L2CacheKb { get; set; }
    public int? L3CacheKb { get; set; }
}

public sealed class MemoryModule
{
    public string Slot { get; set; } = "";
    public long? SizeBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public string PartNumber { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Serial { get; set; } = "";
}

public sealed class PhysicalDisk
{
    public string Model { get; set; } = "";
    public long? SizeBytes { get; set; }
    public string MediaType { get; set; } = "";
    public string Health { get; set; } = "";
    public string Bus { get; set; } = "";
    public string Serial { get; set; } = "";
    public string SmartSource { get; set; } = "";
}

public sealed class VolumeInfo
{
    public string Drive { get; set; } = "";
    public string Label { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public long? SizeBytes { get; set; }
    public long? FreeBytes { get; set; }
}

public sealed class GpuInfo
{
    public string Name { get; set; } = "";
    public long? AdapterRamBytes { get; set; }
    public string DriverVersion { get; set; } = "";
    public DateTimeOffset? DriverDate { get; set; }
    public string VideoMode { get; set; } = "";
}

public sealed class MonitorInfo
{
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string Serial { get; set; } = "";
}

public sealed class BatteryInfo
{
    public bool Present { get; set; }
    public string Name { get; set; } = "";
    public int? ChargePercent { get; set; }
    public int? DesignCapacityMwh { get; set; }
    public int? FullChargeCapacityMwh { get; set; }
    public double? HealthPercent { get; set; }
}

public sealed class InstalledProgram
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
    public DateTimeOffset? InstallDate { get; set; }
    public long? SizeBytes { get; set; }
    public string Source { get; set; } = "";
}

public sealed class Hotfix
{
    public string HotFixId { get; set; } = "";
    public string Description { get; set; } = "";
    public string InstalledBy { get; set; } = "";
    public DateTimeOffset? InstalledOn { get; set; }
}

public sealed class ServiceInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "";
    public string StartMode { get; set; } = "";
    public string Account { get; set; } = "";
}

public sealed class StartupItem
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Location { get; set; } = "";
    public string User { get; set; } = "";
    public string Enabled { get; set; } = "";
}

public sealed class InventoryRow
{
    public string Name { get; set; } = "";
    public string Online { get; set; } = "";
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Os { get; set; } = "";
    public string Build { get; set; } = "";
    public string Cpu { get; set; } = "";
    public string Ram { get; set; } = "";
    public string DiskFree { get; set; } = "";
    public string LastBoot { get; set; } = "";
    public string LoggedOnUser { get; set; } = "";
    public string Detail { get; set; } = "";
}
