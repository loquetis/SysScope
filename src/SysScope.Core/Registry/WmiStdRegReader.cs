using SysScope.Core.Wmi;

namespace SysScope.Core.Registry;

public sealed class WmiStdRegReader : IRegistryReader
{
    public const string Scope = @"root\default";
    public const string ClassName = "StdRegProv";
    public const uint HiveLocalMachine = 0x80000002;
    public const uint HiveCurrentUser = 0x80000001;

    private readonly IWmiSession _session;
    private readonly List<string> _warnings = new();

    public WmiStdRegReader(IWmiSession session) => _session = session;

    public IReadOnlyList<string> Warnings => _warnings;

    public IReadOnlyList<UninstallRecord> ReadUninstallRecords()
    {
        var list = new List<UninstallRecord>();
        ReadUninstall("HKLM", RegistryPaths.Uninstall, "HKLM", list);
        ReadUninstall("HKLM", RegistryPaths.Uninstall32, "HKLM (32-bit)", list);
        ReadUninstall("HKCU", RegistryPaths.Uninstall, "HKCU", list);
        ReadUninstall("HKCU", RegistryPaths.Uninstall32, "HKCU (32-bit)", list);
        return list;
    }

    public IReadOnlyList<RunKeyRecord> ReadRunKeys()
    {
        var list = new List<RunKeyRecord>();
        ReadRun("HKLM", RegistryPaths.Run, @"HKLM\Run", "All users", list);
        ReadRun("HKLM", RegistryPaths.Run32, @"HKLM\Run (32-bit)", "All users", list);
        ReadRun("HKCU", RegistryPaths.Run, @"HKCU\Run", "Current user", list);
        return list;
    }

    public string? ReadString(string hive, string keyPath, string valueName)
    {
        var result = Invoke("GetStringValue", hive, keyPath, valueName);
        if (result?.ReturnValue is not 0)
            return null;
        var text = result.Outputs.Text("sValue");
        return text.Length == 0 ? null : text;
    }

    public int? ReadDword(string hive, string keyPath, string valueName)
    {
        var result = Invoke("GetDWORDValue", hive, keyPath, valueName);
        if (result?.ReturnValue is not 0)
            return null;
        return result.Outputs.Int32("uValue");
    }

    public IReadOnlyDictionary<string, byte[]> ReadStartupApproved(string hive, string keyPath)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var names = EnumValues(hive, keyPath);
        if (names is null)
            return map;
        foreach (var name in names)
        {
            var result = Invoke("GetBinaryValue", hive, keyPath, name);
            if (result?.ReturnValue is not 0)
                continue;
            var bytes = result.Outputs.Bytes("uValue");
            if (bytes is { Length: > 0 })
                map[name] = bytes;
        }
        return map;
    }

    private void ReadUninstall(string hive, string path, string source, List<UninstallRecord> list)
    {
        var names = EnumKeys(hive, path);
        if (names is null)
            return;
        foreach (var name in names)
        {
            var sub = path + "\\" + name;
            var size = ReadDword(hive, sub, "EstimatedSize");
            var system = ReadDword(hive, sub, "SystemComponent");
            list.Add(new UninstallRecord(
                source,
                ReadString(hive, sub, "DisplayName"),
                ReadString(hive, sub, "DisplayVersion"),
                ReadString(hive, sub, "Publisher"),
                ReadString(hive, sub, "InstallDate"),
                size,
                system is > 0,
                ReadString(hive, sub, "ParentKeyName"),
                ReadString(hive, sub, "ReleaseType")));
        }
    }

    private void ReadRun(string hive, string path, string source, string user, List<RunKeyRecord> list)
    {
        var names = EnumValues(hive, path);
        if (names is null)
            return;
        foreach (var name in names)
        {
            var command = ReadString(hive, path, name);
            if (string.IsNullOrWhiteSpace(command))
                continue;
            list.Add(new RunKeyRecord(source, name, command, user));
        }
    }

    private IReadOnlyList<string>? EnumKeys(string hive, string path)
    {
        var result = Invoke("EnumKey", hive, path, null);
        if (result is null)
            return null;
        if (result.ReturnValue is 2)
            return Array.Empty<string>();
        if (result.ReturnValue is not 0)
        {
            _warnings.Add($"Registry EnumKey {path} returned {result.ReturnValue}.");
            return Array.Empty<string>();
        }
        return result.Outputs.Texts("sNames");
    }

    private IReadOnlyList<string>? EnumValues(string hive, string path)
    {
        var result = Invoke("EnumValues", hive, path, null);
        if (result is null)
            return null;
        if (result.ReturnValue is 2)
            return Array.Empty<string>();
        if (result.ReturnValue is not 0)
        {
            _warnings.Add($"Registry EnumValues {path} returned {result.ReturnValue}.");
            return Array.Empty<string>();
        }
        return result.Outputs.Texts("sNames");
    }

    private WmiMethodResult? Invoke(string method, string hive, string keyPath, string? valueName)
    {
        try
        {
            var args = new Dictionary<string, object?>
            {
                ["hDefKey"] = HiveId(hive),
                ["sSubKeyName"] = keyPath
            };
            if (valueName is not null)
                args["sValueName"] = valueName;
            return _session.InvokeAsync(Scope, ClassName, method, args, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _warnings.Add($"Registry {method} {keyPath}{(valueName is null ? "" : "\\" + valueName)}: {ex.Message}");
            return null;
        }
    }

    public static uint HiveId(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" => HiveCurrentUser,
        _ => HiveLocalMachine
    };
}
