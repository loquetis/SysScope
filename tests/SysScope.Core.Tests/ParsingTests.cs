using SysScope.Core.Formatting;
using SysScope.Core.Models;
using SysScope.Core.Parsing;
using SysScope.Core.Registry;

namespace SysScope.Core.Tests;

public class ParsingTests
{
    [Fact]
    public void WmiDate_ParsesUtcOffset()
    {
        var parsed = WmiDateParser.Parse("20240315120000.000000+000");
        Assert.NotNull(parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Value.Offset);
        Assert.Equal(new DateTime(2024, 3, 15, 12, 0, 0), parsed.Value.DateTime);
    }

    [Fact]
    public void WmiDate_ParsesNegativeOffset()
    {
        var parsed = WmiDateParser.Parse("20231102081422.000000-300");
        Assert.NotNull(parsed);
        Assert.Equal(TimeSpan.FromHours(-5), parsed.Value.Offset);
        Assert.Equal(8, parsed.Value.Hour);
        Assert.Equal(2, parsed.Value.Day);
    }

    [Fact]
    public void WmiDate_TreatsUnspecifiedOffsetAsZero()
    {
        var parsed = WmiDateParser.Parse("20231102081422.000000+***");
        Assert.NotNull(parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Value.Offset);
        Assert.Equal(2023, parsed.Value.Year);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    public void WmiDate_RejectsGarbage(string? text)
    {
        Assert.Null(WmiDateParser.Parse(text));
    }

    [Fact]
    public void InstallDate_ParsesCompactAndUs()
    {
        var compact = WmiDateParser.ParseInstallDate("20240618");
        Assert.Equal(new DateTime(2024, 6, 18), compact!.Value.Date);
        var us = WmiDateParser.ParseInstallDate("10/8/2025");
        Assert.Equal(10, us!.Value.Month);
        Assert.Equal(8, us.Value.Day);
        Assert.Null(WmiDateParser.ParseInstallDate("nope"));
    }

    [Fact]
    public void MonitorId_StopsAtNullAndDecodesString()
    {
        var units = new ushort[] { (ushort)'D', (ushort)'e', (ushort)'l', (ushort)'l', 0, (ushort)'X' };
        Assert.Equal("Dell", MonitorIdDecoder.Decode(units));
        Assert.Equal("CN0", MonitorIdDecoder.Decode("CN0"));
        Assert.Equal("", MonitorIdDecoder.Decode(Array.Empty<ushort>()));
        Assert.Equal("HP", MonitorIdDecoder.Decode(new int[] { 72, 80, 0 }));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("To be filled by O.E.M.", true)]
    [InlineData("Default string", true)]
    [InlineData("7F3K2C1", false)]
    public void Serial_DetectsPlaceholders(string? value, bool placeholder)
    {
        Assert.Equal(placeholder, SerialNumbers.IsPlaceholder(value));
    }

    [Fact]
    public void Serial_PrefersProductOverBiosPlaceholder()
    {
        Assert.Equal("7F3K2C1", SerialNumbers.Prefer("7F3K2C1", "To be filled by O.E.M."));
        Assert.Equal("BIOS-1", SerialNumbers.Prefer("System Serial Number", "BIOS-1"));
        Assert.Equal("", SerialNumbers.Prefer(null, "None"));
    }

    [Fact]
    public void MemoryMath_SubtractsFreeKilobytes()
    {
        long total = 32L * 1024 * 1024 * 1024;
        long freeKb = 14L * 1024 * 1024;
        Assert.Equal(18L * 1024 * 1024 * 1024, MemoryMath.UsedBytes(total, freeKb));
        Assert.Null(MemoryMath.UsedBytes(total, freeKb * 4));
        Assert.Null(MemoryMath.UsedBytes(null, freeKb));
    }

    [Fact]
    public void ComputerSystem_DomainAndWorkgroup()
    {
        var domain = new OverviewInfo();
        OverviewParser.ApplyComputerSystem(domain, [Support.Row(
            ("Name", "NW-LAP-014"), ("Domain", "NORTHWIND.LOCAL"), ("Workgroup", "WORKGROUP"),
            ("PartOfDomain", true), ("Manufacturer", "Dell Inc."), ("Model", "Latitude 7450"),
            ("UserName", @"NORTHWIND\a.chen"), ("TotalPhysicalMemory", 32L * 1024 * 1024 * 1024))]);
        Assert.Equal("NW-LAP-014", domain.ComputerName);
        Assert.True(domain.PartOfDomain);
        Assert.Equal("NORTHWIND.LOCAL (domain)", DisplayFormatter.Domain(domain.PartOfDomain, domain.DomainOrWorkgroup));

        var workgroup = new OverviewInfo();
        OverviewParser.ApplyComputerSystem(workgroup, [Support.Row(
            ("Name", "RECEPTION-01"), ("Domain", "RECEPTION-01"), ("Workgroup", "NORTHWIND"), ("PartOfDomain", false))]);
        Assert.Equal("NORTHWIND (workgroup)", DisplayFormatter.Domain(workgroup.PartOfDomain, workgroup.DomainOrWorkgroup));
    }

    [Fact]
    public void OperatingSystem_AppendsUbrAndRamUsed()
    {
        var info = new OverviewInfo { RamBytes = 32L * 1024 * 1024 * 1024 };
        OverviewParser.ApplyOperatingSystem(info, [Support.Row(
            ("Caption", "Microsoft Windows 11 Pro"),
            ("Version", "10.0.22631"),
            ("BuildNumber", "22631"),
            ("OSArchitecture", "64-bit"),
            ("InstallDate", "20240618093000.000000+000"),
            ("LastBootUpTime", "20261018081422.000000+000"),
            ("FreePhysicalMemory", 14L * 1024 * 1024))], 4317, "23H2");
        Assert.Equal("22631.4317", info.OsBuild);
        Assert.Contains("23H2", info.OsVersion, StringComparison.Ordinal);
        Assert.Contains("64-bit", info.OsVersion, StringComparison.Ordinal);
        Assert.Equal(18L * 1024 * 1024 * 1024, info.RamUsedBytes);
        Assert.Equal(2024, info.InstallDate!.Value.Year);
    }

    [Fact]
    public void TpmAndSecureBoot_MapStates()
    {
        var info = new OverviewInfo();
        OverviewParser.ApplyTpm(info, [Support.Row(("SpecVersion", "2.0, 0, 1.38"), ("IsEnabled_InitialValue", true))], null);
        Assert.Equal("Present (2.0)", DisplayFormatter.Tpm(info.TpmPresent, info.TpmVersion));

        var missing = new OverviewInfo();
        OverviewParser.ApplyTpm(missing, Array.Empty<Wmi.WmiInstance>(), null);
        Assert.Equal("Not present", DisplayFormatter.Tpm(missing.TpmPresent, missing.TpmVersion));

        var blocked = new OverviewInfo();
        OverviewParser.ApplyTpm(blocked, Array.Empty<Wmi.WmiInstance>(), "Access is denied.");
        Assert.Equal("Not readable", DisplayFormatter.Tpm(blocked.TpmPresent, blocked.TpmVersion));

        OverviewParser.ApplySecureBoot(info, 1, null);
        Assert.Equal(SecureBootState.Enabled, info.SecureBoot);
        OverviewParser.ApplySecureBoot(info, 0, null);
        Assert.Equal(SecureBootState.Disabled, info.SecureBoot);
        OverviewParser.ApplySecureBoot(info, null, null);
        Assert.Equal(SecureBootState.Unsupported, info.SecureBoot);
        OverviewParser.ApplySecureBoot(info, null, "Access is denied.");
        Assert.Equal(SecureBootState.Unknown, info.SecureBoot);
        Assert.Equal("Enabled", DisplayFormatter.SecureBoot(SecureBootState.Enabled));
    }

    [Theory]
    [InlineData(0, "Off")]
    [InlineData(1, "On")]
    [InlineData(2, "Unknown")]
    public void BitLocker_Protection(int code, string expected)
    {
        Assert.Equal(expected, OverviewParser.Protection(code));
    }

    [Theory]
    [InlineData(6, "XTS-AES-128")]
    [InlineData(7, "XTS-AES-256")]
    [InlineData(3, "AES-128")]
    [InlineData(0, "None")]
    public void BitLocker_Encryption(int code, string expected)
    {
        Assert.Equal(expected, OverviewParser.Encryption(code));
    }

    [Fact]
    public void BitLocker_AccessDeniedIsNotReadable()
    {
        var rows = OverviewParser.ParseBitLocker(null, "Access is denied.");
        Assert.Single(rows);
        Assert.Equal("Not readable", rows[0].Status);
        Assert.Contains("Access is denied", rows[0].ConversionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void BitLocker_ParsesVolumes()
    {
        var rows = OverviewParser.ParseBitLocker(
        [
            Support.Row(("DriveLetter", "C:"), ("ProtectionStatus", 1), ("EncryptionMethod", 6), ("ConversionStatus", 1)),
            Support.Row(("DriveLetter", ""), ("ProtectionStatus", 0), ("EncryptionMethod", 0), ("ConversionStatus", 0))
        ], null);
        Assert.Equal("On", rows.Single(r => r.DriveLetter == "C:").Status);
        Assert.Equal("XTS-AES-128", rows.Single(r => r.DriveLetter == "C:").EncryptionMethod);
        Assert.Contains(rows, r => r.DriveLetter == "(no letter)");
    }

    [Fact]
    public void Network_JoinsIpAndSkipsTunnels()
    {
        var configs = new[]
        {
            Support.Row(("Index", 1), ("Description", "Intel Wi-Fi"), ("MACAddress", "a483e72c1004"),
                ("IPAddress", new[] { "10.20.14.42", "fe80::1" }), ("DefaultIPGateway", new[] { "10.20.14.1" }),
                ("DHCPEnabled", true), ("IPEnabled", true)),
            Support.Row(("Index", 9), ("Description", "Teredo Tunneling Pseudo-Interface"), ("MACAddress", ""),
                ("IPAddress", new[] { "fe80::2" }), ("IPEnabled", true))
        };
        var adapters = new[]
        {
            Support.Row(("Index", 1), ("NetConnectionID", "Wi-Fi"), ("MACAddress", "A4:83:E7:2C:10:04"), ("Speed", 2_400_000_000L), ("PhysicalAdapter", true))
        };
        var overview = OverviewParser.ParseAdapters(configs, adapters, ipEnabledOnly: true);
        Assert.Single(overview);
        Assert.Equal("Wi-Fi", overview[0].Name);
        Assert.Equal("A4:83:E7:2C:10:04", overview[0].Mac);
        Assert.Contains("10.20.14.42", overview[0].IpAddresses, StringComparison.Ordinal);
        Assert.Equal("Yes", overview[0].Dhcp);
        Assert.Equal("2400 Mbps", overview[0].LinkSpeed);
    }

    [Fact]
    public void Cpu_CollapsesSpacesAndSummarizes()
    {
        var cpus = HardwareParser.ParseCpus([Support.Row(
            ("Name", "Intel(R)  Core(TM)   i7-10700"),
            ("Manufacturer", "GenuineIntel"),
            ("NumberOfCores", 8),
            ("NumberOfLogicalProcessors", 16),
            ("MaxClockSpeed", 2900),
            ("SocketDesignation", "U3E1"),
            ("L2CacheSize", 2048),
            ("L3CacheSize", 16384))]);
        Assert.Equal("Intel(R) Core(TM) i7-10700", cpus[0].Name);
        Assert.Equal("Intel(R) Core(TM) i7-10700, 8 cores / 16 threads", HardwareParser.SummarizeCpus(cpus));
    }

    [Fact]
    public void Memory_TrimsPartNumberAndSkipsEmptySlots()
    {
        var modules = HardwareParser.ParseMemory(
        [
            Support.Row(("DeviceLocator", "DIMM 1"), ("Capacity", 16L * 1024 * 1024 * 1024), ("Speed", 5600), ("PartNumber", "  NWM16G5600  "), ("Manufacturer", "Micron"), ("SerialNumber", "M1")),
            Support.Row(("DeviceLocator", "DIMM 2"), ("Capacity", 0L), ("Speed", 5600))
        ]);
        Assert.Single(modules);
        Assert.Equal("NWM16G5600", modules[0].PartNumber);
        Assert.Equal(5600, modules[0].SpeedMhz);
    }

    [Theory]
    [InlineData(3, "HDD")]
    [InlineData(4, "SSD")]
    [InlineData(5, "SCM")]
    public void Disk_MediaType(int code, string expected)
    {
        Assert.Equal(expected, HardwareParser.MediaType(code));
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Warning")]
    [InlineData(2, "Unhealthy")]
    public void Disk_Health(int code, string expected)
    {
        Assert.Equal(expected, HardwareParser.Health(code));
    }

    [Fact]
    public void Disks_PreferStorageNamespaceAndFallBack()
    {
        var smart = HardwareParser.ParseDisks(
            [Support.Row(("FriendlyName", "Samsung PM9B1"), ("Size", 1024L), ("MediaType", 4), ("HealthStatus", 0), ("BusType", 17), ("SerialNumber", "S1"))],
            [Support.Row(("Model", "Ignored"), ("Size", 1L))]);
        Assert.Equal("SSD", smart[0].MediaType);
        Assert.Equal("Healthy", smart[0].Health);
        Assert.Equal("NVMe", smart[0].Bus);
        Assert.Equal("MSFT_PhysicalDisk", smart[0].SmartSource);

        var plain = HardwareParser.ParseDisks(Array.Empty<Wmi.WmiInstance>(),
            [Support.Row(("Model", "WD Blue SSD"), ("Size", 500L), ("MediaType", "Fixed hard disk media"), ("InterfaceType", "SATA"), ("SerialNumber", "WD"))]);
        Assert.Equal("SSD", plain[0].MediaType);
        Assert.Equal("Not available", plain[0].Health);
        Assert.Equal("MSFT_PhysicalDisk not available", plain[0].SmartSource);
    }

    [Fact]
    public void Volumes_SkipOpticalAndComputeFree()
    {
        var volumes = HardwareParser.ParseVolumes(
        [
            Support.Row(("DeviceID", "C:"), ("VolumeName", "Windows"), ("FileSystem", "NTFS"), ("Size", 1000L), ("FreeSpace", 250L), ("DriveType", 3)),
            Support.Row(("DeviceID", "D:"), ("VolumeName", "DVD"), ("FileSystem", "UDF"), ("Size", 100L), ("FreeSpace", 0L), ("DriveType", 5))
        ]);
        Assert.Single(volumes);
        Assert.Equal(0.25, VolumeMath.FreeFraction(volumes[0].SizeBytes, volumes[0].FreeBytes));
        Assert.Contains("free of", VolumeMath.FreeLabel(volumes[0].SizeBytes, volumes[0].FreeBytes), StringComparison.Ordinal);
        Assert.Null(VolumeMath.FreeFraction(0, 0));
        Assert.Equal(250L, VolumeMath.TotalFree(volumes));
    }

    [Fact]
    public void Gpu_DropsSaturatedAdapterRam()
    {
        Assert.Null(HardwareParser.AdapterRam(uint.MaxValue));
        Assert.Equal(128L * 1024 * 1024, HardwareParser.AdapterRam(128L * 1024 * 1024));
        var gpus = HardwareParser.ParseGpus([Support.Row(("Name", "Intel Graphics"), ("AdapterRAM", 128 * 1024 * 1024), ("DriverVersion", "32.0"), ("VideoModeDescription", "1920 x 1200"))]);
        Assert.Equal("1920 x 1200", gpus[0].VideoMode);
    }

    [Fact]
    public void Monitors_DecodeUint16Serials()
    {
        var monitors = HardwareParser.ParseMonitors([Support.Row(
            ("UserFriendlyName", "Dell C2423H".Select(c => (ushort)c).ToArray()),
            ("ManufacturerName", new ushort[] { (ushort)'D', (ushort)'E', (ushort)'L', 0 }),
            ("ProductCodeID", "3C24"),
            ("SerialNumberID", "CN0NW14L7440".Select(c => (ushort)c).Append((ushort)0).ToArray()))]);
        Assert.Equal("Dell C2423H", monitors[0].Name);
        Assert.Equal("DEL", monitors[0].Manufacturer);
        Assert.Equal("CN0NW14L7440", monitors[0].Serial);
    }

    [Fact]
    public void Battery_ComputesHealthAndAbsence()
    {
        var battery = HardwareParser.ParseBattery(
            [Support.Row(("Name", "DELL Y0XJ9"), ("EstimatedChargeRemaining", 76))],
            [Support.Row(("DesignedCapacity", 57000))],
            [Support.Row(("FullChargedCapacity", 49100))]);
        Assert.True(battery.Present);
        Assert.Equal(86.1, battery.HealthPercent);
        Assert.Equal(76, battery.ChargePercent);
        Assert.False(HardwareParser.ParseBattery(Array.Empty<Wmi.WmiInstance>(), Array.Empty<Wmi.WmiInstance>(), Array.Empty<Wmi.WmiInstance>()).Present);
    }

    [Fact]
    public void Software_DedupesHivesAndSkipsNoise()
    {
        var programs = SoftwareParser.Parse(
        [
            new UninstallRecord("HKLM", "Google Chrome", "130.0", "Google LLC", "20250902", 310_000, false, null, null),
            new UninstallRecord("HKCU", "Google Chrome", "130.0", "", "20250902", 310_000, false, null, null),
            new UninstallRecord("HKCU", "Slack", "4.40", "Slack Technologies", "20250601", 210_000, false, null, null),
            new UninstallRecord("HKLM (32-bit)", "Legacy Label Printer", "2.1.0", "Contoso Peripherals", "20191102", 18_000, false, null, null),
            new UninstallRecord("HKLM", "Hidden Component", "1", "Microsoft", "20240101", 10, true, null, null),
            new UninstallRecord("HKLM", "Update for Microsoft Office", "1", "Microsoft", "20240101", 10, false, null, null),
            new UninstallRecord("HKLM", "Child patch", "1", "Microsoft", "20240101", 10, false, "{GUID}", null),
            new UninstallRecord("HKLM", "Security Update for Windows", "1", "Microsoft", "20240101", 10, false, null, "Security Update"),
            new UninstallRecord("HKLM", "Dell Command Update", "5.4.0", "Dell Inc.", "20250303", 0, false, null, null),
            new UninstallRecord("HKLM", "", "1", "Nobody", null, null, false, null, null)
        ]);
        Assert.Equal(4, programs.Count);
        Assert.Single(programs, p => p.Name == "Google Chrome");
        Assert.Equal("HKLM", programs.Single(p => p.Name == "Google Chrome").Source);
        Assert.Equal("Google LLC", programs.Single(p => p.Name == "Google Chrome").Publisher);
        Assert.Equal(310_000L * 1024, programs.Single(p => p.Name == "Google Chrome").SizeBytes);
        Assert.Equal(new DateTime(2025, 9, 2), programs.Single(p => p.Name == "Google Chrome").InstallDate!.Value.Date);
        Assert.Contains(programs, p => p.Name == "Slack" && p.Source == "HKCU");
        Assert.Contains(programs, p => p.Source == "HKLM (32-bit)");
        Assert.Contains(programs, p => p.Name == "Dell Command Update" && p.SizeBytes is null);
        Assert.DoesNotContain(programs, p => p.Name.Contains("Update for", StringComparison.Ordinal));
    }

    [Fact]
    public void Hotfixes_SortNewestFirst()
    {
        var fixes = SoftwareParser.ParseHotfixes(
        [
            Support.Row(("HotFixID", "KB5036893"), ("Description", "Update"), ("InstalledOn", "3/12/2025"), ("InstalledBy", "SYSTEM")),
            Support.Row(("HotFixID", "KB5044285"), ("Description", "Security Update"), ("InstalledOn", "10/8/2025"), ("InstalledBy", "SYSTEM"))
        ]);
        Assert.Equal("KB5044285", fixes[0].HotFixId);
        Assert.Equal(10, fixes[0].InstalledOn!.Value.Month);
    }

    [Theory]
    [InlineData("Auto", "Automatic")]
    [InlineData("Manual", "Manual")]
    [InlineData("Disabled", "Disabled")]
    public void Services_MapStartMode(string raw, string expected)
    {
        Assert.Equal(expected, ServiceParser.StartMode(raw));
    }

    [Fact]
    public void Services_SortByDisplayName()
    {
        var services = ServiceParser.Parse(
        [
            Support.Row(("Name", "Winmgmt"), ("DisplayName", "Windows Management Instrumentation"), ("State", "Running"), ("StartMode", "Auto"), ("StartName", "LocalSystem")),
            Support.Row(("Name", "BITS"), ("DisplayName", "Background Intelligent Transfer Service"), ("State", "Running"), ("StartMode", "Manual"), ("StartName", "LocalSystem"))
        ]);
        Assert.Equal("BITS", services[0].Name);
        Assert.Equal("Automatic", services[1].StartMode);
    }

    [Theory]
    [InlineData(new byte[] { 2, 0 }, "Yes")]
    [InlineData(new byte[] { 6, 0 }, "Yes")]
    [InlineData(new byte[] { 3, 1, 2 }, "No")]
    public void Startup_EnabledBytes(byte[] data, string expected)
    {
        Assert.Equal(expected, StartupParser.EnabledFromApproved(data));
    }

    [Fact]
    public void Startup_NullApprovedMeansYesAndDedupesWmi()
    {
        Assert.Equal("Yes", StartupParser.EnabledFromApproved(null));
        var items = StartupParser.Parse(
            [new RunKeyRecord(@"HKCU\Run", "Spotify", @"C:\Spotify.exe", "a.chen")],
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase) { ["Spotify"] = [3] },
            [Support.Row(("Name", "Spotify"), ("Command", @"C:\Spotify.exe"), ("Location", "HKCU"), ("User", "a.chen")),
             Support.Row(("Name", "Teams"), ("Command", @"C:\Teams.exe"), ("Location", "Startup"), ("User", "a.chen"))]);
        Assert.Equal(2, items.Count);
        Assert.Equal("No", items.Single(i => i.Name == "Spotify").Enabled);
        Assert.Equal("Yes", items.Single(i => i.Name == "Teams").Enabled);
    }
}
