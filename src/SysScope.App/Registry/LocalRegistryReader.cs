using Microsoft.Win32;
using SysScope.Core.Models;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;

namespace SysScope;

public sealed class LocalRegistryReader : IRegistryReader
{
    public IReadOnlyList<string> Warnings { get; } = Array.Empty<string>();

    public IReadOnlyList<UninstallRecord> ReadUninstallRecords()
    {
        var list = new List<UninstallRecord>();
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM", list);
        if (Environment.Is64BitOperatingSystem)
            ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM (32-bit)", list);
        ReadUninstall(RegistryHive.CurrentUser, RegistryView.Registry64, "HKCU", list);
        if (Environment.Is64BitOperatingSystem)
            ReadUninstall(RegistryHive.CurrentUser, RegistryView.Registry32, "HKCU (32-bit)", list);
        return list;
    }

    public IReadOnlyList<RunKeyRecord> ReadRunKeys()
    {
        var list = new List<RunKeyRecord>();
        ReadRun(RegistryHive.LocalMachine, RegistryView.Registry64, RegistryPaths.Run, @"HKLM\Run", "All users", list);
        if (Environment.Is64BitOperatingSystem)
            ReadRun(RegistryHive.LocalMachine, RegistryView.Registry32, RegistryPaths.Run, @"HKLM\Run (32-bit)", "All users", list);
        ReadRun(RegistryHive.CurrentUser, RegistryView.Registry64, RegistryPaths.Run, @"HKCU\Run", "Current user", list);
        return list;
    }

    public string? ReadString(string hive, string keyPath, string valueName)
    {
        using var key = Open(hive, keyPath, RegistryView.Registry64);
        return key?.GetValue(valueName) as string;
    }

    public int? ReadDword(string hive, string keyPath, string valueName)
    {
        using var key = Open(hive, keyPath, RegistryView.Registry64);
        return key?.GetValue(valueName) switch
        {
            int number => number,
            long number => number > int.MaxValue ? null : (int)number,
            _ => null
        };
    }

    public IReadOnlyDictionary<string, byte[]> ReadStartupApproved(string hive, string keyPath)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var key = Open(hive, keyPath, RegistryView.Registry64);
        if (key is null)
            return map;
        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is byte[] bytes && bytes.Length > 0)
                map[name] = bytes;
        }
        return map;
    }

    private static void ReadUninstall(RegistryHive hive, RegistryView view, string source, List<UninstallRecord> list)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(RegistryPaths.Uninstall);
        if (key is null)
            return;
        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            if (sub is null)
                continue;
            list.Add(new UninstallRecord(
                source,
                sub.GetValue("DisplayName") as string,
                sub.GetValue("DisplayVersion") as string,
                sub.GetValue("Publisher") as string,
                Convert.ToString(sub.GetValue("InstallDate")),
                ToLong(sub.GetValue("EstimatedSize")),
                ToLong(sub.GetValue("SystemComponent")) is > 0,
                sub.GetValue("ParentKeyName") as string,
                sub.GetValue("ReleaseType") as string));
        }
    }

    private static void ReadRun(RegistryHive hive, RegistryView view, string path, string source, string user, List<RunKeyRecord> list)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(path);
        if (key is null)
            return;
        foreach (var name in key.GetValueNames())
        {
            var command = key.GetValue(name) as string;
            if (string.IsNullOrWhiteSpace(command))
                continue;
            list.Add(new RunKeyRecord(source, name, command, user));
        }
    }

    private static RegistryKey? Open(string hive, string keyPath, RegistryView view)
    {
        var rootHive = hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        using var root = RegistryKey.OpenBaseKey(rootHive, view);
        return root.OpenSubKey(keyPath);
    }

    private static long? ToLong(object? value) => value switch
    {
        int number => number,
        long number => number,
        _ => null
    };
}

public sealed class WindowsRegistryFactory : IRegistryReaderFactory
{
    public IRegistryReader Create(ConnectionTarget target, IWmiSession session) =>
        target.IsLocal ? new LocalRegistryReader() : new WmiStdRegReader(session);
}
