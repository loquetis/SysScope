using SysScope.Core.Models;
using SysScope.Core.Wmi;

namespace SysScope.Core.Registry;

public sealed record UninstallRecord(
    string Source,
    string? DisplayName,
    string? DisplayVersion,
    string? Publisher,
    string? InstallDate,
    long? EstimatedSizeKb,
    bool SystemComponent,
    string? ParentKeyName,
    string? ReleaseType);

public sealed record RunKeyRecord(string Source, string Name, string Command, string User);

public interface IRegistryReader
{
    IReadOnlyList<string> Warnings { get; }
    IReadOnlyList<UninstallRecord> ReadUninstallRecords();
    IReadOnlyList<RunKeyRecord> ReadRunKeys();
    string? ReadString(string hive, string keyPath, string valueName);
    int? ReadDword(string hive, string keyPath, string valueName);
    IReadOnlyDictionary<string, byte[]> ReadStartupApproved(string hive, string keyPath);
}

public interface IRegistryReaderFactory
{
    IRegistryReader Create(ConnectionTarget target, IWmiSession session);
}

public static class RegistryPaths
{
    public const string WindowsCurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    public const string SecureBootState = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    public const string StartupApprovedRun = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    public const string Uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
    public const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
}
