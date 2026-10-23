using SysScope.Core.Formatting;
using SysScope.Core.Models;
using SysScope.Core.Wmi;

namespace SysScope.Core.Parsing;

public static class OverviewParser
{
    public static void ApplyComputerSystem(OverviewInfo info, IReadOnlyList<WmiInstance> rows)
    {
        var row = rows.FirstOrDefault();
        if (row is null)
            return;
        info.ComputerName = row.Text("Name");
        info.Manufacturer = row.Text("Manufacturer");
        info.Model = row.Text("Model");
        info.PartOfDomain = row.Bool("PartOfDomain") ?? false;
        var domain = row.Text("Domain");
        var workgroup = row.Text("Workgroup");
        if (info.PartOfDomain && domain.Length > 0)
            info.DomainOrWorkgroup = domain;
        else
            info.DomainOrWorkgroup = workgroup.Length > 0 ? workgroup : domain;
        info.LoggedOnUser = row.Text("UserName");
        info.RamBytes = row.Int64("TotalPhysicalMemory");
    }

    public static void ApplyIdentity(OverviewInfo info, IReadOnlyList<WmiInstance> bios, IReadOnlyList<WmiInstance> product)
    {
        var biosRow = bios.FirstOrDefault();
        var productRow = product.FirstOrDefault();
        info.SerialNumber = SerialNumbers.Prefer(productRow?.Text("IdentifyingNumber"), biosRow?.Text("SerialNumber"));
        if (biosRow is null)
            return;
        info.BiosVersion = biosRow.Text("SMBIOSBIOSVersion");
        info.BiosDate = WmiDateParser.Parse(biosRow.Text("ReleaseDate"));
    }

    public static void ApplyOperatingSystem(OverviewInfo info, IReadOnlyList<WmiInstance> rows, int? ubr, string? displayVersion)
    {
        var row = rows.FirstOrDefault();
        if (row is null)
            return;
        info.OsEdition = row.Text("Caption");
        var version = row.Text("Version");
        var arch = row.Text("OSArchitecture");
        var build = row.Text("BuildNumber");
        info.OsBuild = ubr is > 0 && build.Length > 0 ? $"{build}.{ubr.Value}" : build;
        var display = string.IsNullOrWhiteSpace(displayVersion) ? "" : $" ({displayVersion.Trim()})";
        var architecture = string.IsNullOrWhiteSpace(arch) ? "" : $", {arch.Trim()}";
        info.OsVersion = $"{version}{display}{architecture}".Trim().TrimStart(',').Trim();
        info.InstallDate = WmiDateParser.Parse(row.Text("InstallDate"));
        info.LastBoot = WmiDateParser.Parse(row.Text("LastBootUpTime"));
        info.RamUsedBytes = MemoryMath.UsedBytes(info.RamBytes, row.Int64("FreePhysicalMemory"));
    }

    public static void ApplyTpm(OverviewInfo info, IReadOnlyList<WmiInstance>? rows, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error) && (rows is null || rows.Count == 0))
        {
            info.TpmPresent = null;
            info.TpmVersion = "";
            return;
        }

        if (rows is null || rows.Count == 0)
        {
            info.TpmPresent = false;
            info.TpmVersion = "";
            return;
        }

        var row = rows[0];
        var enabled = row.Bool("IsEnabled_InitialValue");
        info.TpmPresent = enabled ?? true;
        var spec = row.Text("SpecVersion");
        info.TpmVersion = spec.Length == 0 ? "" : spec.Split(',')[0].Trim();
    }

    public static void ApplySecureBoot(OverviewInfo info, int? value, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error) && value is null)
        {
            info.SecureBoot = SecureBootState.Unknown;
            return;
        }

        info.SecureBoot = value switch
        {
            1 => SecureBootState.Enabled,
            0 => SecureBootState.Disabled,
            null => SecureBootState.Unsupported,
            _ => SecureBootState.Unknown
        };
    }

    public static List<BitLockerVolume> ParseBitLocker(IReadOnlyList<WmiInstance>? rows, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error) && (rows is null || rows.Count == 0))
        {
            return
            [
                new BitLockerVolume
                {
                    DriveLetter = "",
                    Status = "Not readable",
                    ConversionStatus = error.Trim()
                }
            ];
        }

        var list = new List<BitLockerVolume>();
        if (rows is null)
            return list;
        foreach (var row in rows)
        {
            var letter = row.Text("DriveLetter");
            list.Add(new BitLockerVolume
            {
                DriveLetter = letter.Length == 0 ? "(no letter)" : letter,
                Status = Protection(row.Int32("ProtectionStatus")),
                EncryptionMethod = Encryption(row.Int32("EncryptionMethod")),
                ConversionStatus = Conversion(row.Int32("ConversionStatus"))
            });
        }
        return list.OrderBy(v => v.DriveLetter, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Protection(int? code) => code switch
    {
        0 => "Off",
        1 => "On",
        2 => "Unknown",
        null => "",
        _ => $"Unknown ({code})"
    };

    public static string Encryption(int? code) => code switch
    {
        0 => "None",
        1 => "AES-128 with Diffuser",
        2 => "AES-256 with Diffuser",
        3 => "AES-128",
        4 => "AES-256",
        5 => "Hardware encryption",
        6 => "XTS-AES-128",
        7 => "XTS-AES-256",
        null => "",
        _ => $"Method {code}"
    };

    public static string Conversion(int? code) => code switch
    {
        0 => "Fully decrypted",
        1 => "Fully encrypted",
        2 => "Encryption in progress",
        3 => "Decryption in progress",
        4 => "Encryption paused",
        5 => "Decryption paused",
        null => "",
        _ => $"Status {code}"
    };

    public static List<NetworkAdapter> ParseAdapters(
        IReadOnlyList<WmiInstance> configs,
        IReadOnlyList<WmiInstance> adapters,
        bool ipEnabledOnly)
    {
        var byIndex = new Dictionary<int, WmiInstance>();
        foreach (var adapter in adapters)
        {
            var index = adapter.Int32("Index");
            if (index is int key && !byIndex.ContainsKey(key))
                byIndex[key] = adapter;
        }

        var list = new List<NetworkAdapter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var config in configs)
        {
            var enabled = config.Bool("IPEnabled") ?? false;
            if (ipEnabledOnly && !enabled)
                continue;
            var index = config.Int32("Index");
            byIndex.TryGetValue(index ?? -1, out var adapter);
            var item = BuildAdapter(config, adapter);
            if (IsNoise(item.Name) && ipEnabledOnly)
                continue;
            if (item.Mac.Length == 0 && item.IpAddresses.Length == 0 && item.Name.Length == 0)
                continue;
            var key = $"{item.Mac}|{item.Name}|{item.IpAddresses}";
            if (!seen.Add(key))
                continue;
            list.Add(item);
        }

        if (!ipEnabledOnly)
        {
            foreach (var adapter in adapters)
            {
                var physical = adapter.Bool("PhysicalAdapter") ?? false;
                if (!physical)
                    continue;
                var name = FriendlyName(adapter, null);
                var mac = MacAddressFormatter.Format(adapter.Text("MACAddress"));
                if (list.Any(item => MacEquals(item.Mac, mac) || (mac.Length == 0 && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
                    continue;
                if (IsNoise(name))
                    continue;
                list.Add(new NetworkAdapter
                {
                    Name = name,
                    Mac = mac,
                    LinkSpeed = LinkSpeedFormatter.Format(adapter.Int64("Speed")),
                    Dhcp = ""
                });
            }
        }

        return list;
    }

    private static NetworkAdapter BuildAdapter(WmiInstance config, WmiInstance? adapter)
    {
        var dhcp = config.Bool("DHCPEnabled");
        return new NetworkAdapter
        {
            Name = FriendlyName(adapter, config),
            Mac = MacAddressFormatter.Format(FirstNonEmpty(config.Text("MACAddress"), adapter?.Text("MACAddress"))),
            IpAddresses = string.Join(", ", config.Texts("IPAddress")),
            Gateway = string.Join(", ", config.Texts("DefaultIPGateway")),
            Dhcp = dhcp is null ? "" : dhcp.Value ? "Yes" : "No",
            LinkSpeed = LinkSpeedFormatter.Format(adapter?.Int64("Speed"))
        };
    }

    private static string FriendlyName(WmiInstance? adapter, WmiInstance? config)
    {
        var connection = adapter?.Text("NetConnectionID") ?? "";
        if (connection.Length > 0)
            return connection;
        var description = config?.Text("Description") ?? "";
        if (description.Length > 0)
            return description;
        return adapter?.Text("Name") ?? "";
    }

    private static string FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return first.Trim();
        return second?.Trim() ?? "";
    }

    private static bool MacEquals(string left, string right) =>
        left.Length > 0 && left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool IsNoise(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string[] noise = ["WAN Miniport", "Kernel Debug", "Teredo", "isatap", "Microsoft ISATAP", "QoS Packet Scheduler"];
        return noise.Any(item => name.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
}
