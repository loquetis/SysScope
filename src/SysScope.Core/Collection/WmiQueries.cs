namespace SysScope.Core.Collection;

public static class WmiQueries
{
    public const string Cimv2 = @"root\cimv2";
    public const string Storage = @"root\microsoft\windows\storage";
    public const string Wmi = @"root\wmi";
    public const string BitLocker = @"root\cimv2\Security\MicrosoftVolumeEncryption";
    public const string Tpm = @"root\cimv2\Security\MicrosoftTpm";

    public const string ComputerSystem = "SELECT Name, Domain, Workgroup, PartOfDomain, Manufacturer, Model, UserName, TotalPhysicalMemory FROM Win32_ComputerSystem";
    public const string Bios = "SELECT SerialNumber, SMBIOSBIOSVersion, ReleaseDate, Manufacturer FROM Win32_BIOS";
    public const string Product = "SELECT IdentifyingNumber, Name, Vendor, UUID FROM Win32_ComputerSystemProduct";
    public const string OperatingSystem = "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime, FreePhysicalMemory FROM Win32_OperatingSystem";
    public const string Processor = "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation, L2CacheSize, L3CacheSize FROM Win32_Processor";
    public const string PhysicalMemory = "SELECT BankLabel, DeviceLocator, Capacity, Speed, PartNumber, Manufacturer, SerialNumber FROM Win32_PhysicalMemory";
    public const string LogicalDisk = "SELECT DeviceID, VolumeName, FileSystem, Size, FreeSpace, DriveType FROM Win32_LogicalDisk";
    public const string DiskDrive = "SELECT Model, Size, MediaType, InterfaceType, SerialNumber, Status FROM Win32_DiskDrive";
    public const string PhysicalDisk = "SELECT FriendlyName, Size, MediaType, HealthStatus, SerialNumber, BusType FROM MSFT_PhysicalDisk";
    public const string Video = "SELECT Name, AdapterRAM, DriverVersion, DriverDate, VideoModeDescription, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController";
    public const string Monitor = "SELECT UserFriendlyName, ManufacturerName, SerialNumberID, ProductCodeID, InstanceName FROM WmiMonitorID";
    public const string Nic = "SELECT Name, NetConnectionID, MACAddress, Speed, Index, PhysicalAdapter, NetEnabled FROM Win32_NetworkAdapter";
    public const string NicConfig = "SELECT Description, MACAddress, IPAddress, DefaultIPGateway, DHCPEnabled, Index, IPEnabled FROM Win32_NetworkAdapterConfiguration";
    public const string TpmClass = "SELECT SpecVersion, IsEnabled_InitialValue, IsActivated_InitialValue FROM Win32_Tpm";
    public const string EncryptableVolume = "SELECT DriveLetter, ProtectionStatus, ConversionStatus, EncryptionMethod FROM Win32_EncryptableVolume";
    public const string Battery = "SELECT Name, EstimatedChargeRemaining, DeviceID FROM Win32_Battery";
    public const string BatteryStatic = "SELECT DesignedCapacity FROM BatteryStaticData";
    public const string BatteryFull = "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity";
    public const string Hotfix = "SELECT HotFixID, Description, InstalledOn, InstalledBy FROM Win32_QuickFixEngineering";
    public const string Service = "SELECT Name, DisplayName, State, StartMode, StartName FROM Win32_Service";
    public const string Startup = "SELECT Name, Command, Location, User FROM Win32_StartupCommand";
}
