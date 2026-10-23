using SysScope.Core.Models;
using SysScope.Core.Parsing;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;

namespace SysScope.Core.Collection;

public interface IMachineCollector
{
    Task<MachineReport> CollectAsync(CollectRequest request, CancellationToken cancellationToken);
    Task<MachineReport> CollectSummaryAsync(CollectRequest request, CancellationToken cancellationToken);
}

public sealed class MachineCollector : IMachineCollector
{
    private readonly IWmiSessionFactory _sessions;
    private readonly IRegistryReaderFactory _registries;

    public MachineCollector(IWmiSessionFactory sessions, IRegistryReaderFactory registries)
    {
        _sessions = sessions;
        _registries = registries;
    }

    public Task<MachineReport> CollectAsync(CollectRequest request, CancellationToken cancellationToken) =>
        CollectCoreAsync(request, summaryOnly: false, cancellationToken);

    public Task<MachineReport> CollectSummaryAsync(CollectRequest request, CancellationToken cancellationToken) =>
        CollectCoreAsync(request, summaryOnly: true, cancellationToken);

    private async Task<MachineReport> CollectCoreAsync(CollectRequest request, bool summaryOnly, CancellationToken cancellationToken)
    {
        var report = new MachineReport
        {
            RequestedName = request.DisplayName,
            CollectedAt = DateTimeOffset.Now,
            Overview = { ComputerName = request.DisplayName }
        };

        IWmiSession? session = null;
        try
        {
            session = await _sessions.ConnectAsync(request.Target, cancellationToken).ConfigureAwait(false);
        }
        catch (CollectionFailedException ex)
        {
            report.Online = false;
            report.Error = ex.Message;
            return report;
        }

        try
        {
            report.Online = true;
            var notes = report.Notes;

            async Task<(IReadOnlyList<WmiInstance> Rows, string? Error)> Query(string scope, string wql, string label)
            {
                try
                {
                    var rows = await session.QueryAsync(scope, wql, cancellationToken).ConfigureAwait(false);
                    return (rows, null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var message = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message.Trim();
                    notes.Add($"{label}: {message}");
                    return (Array.Empty<WmiInstance>(), message);
                }
            }

            var computer = await Query(WmiQueries.Cimv2, WmiQueries.ComputerSystem, "Computer system").ConfigureAwait(false);
            OverviewParser.ApplyComputerSystem(report.Overview, computer.Rows);
            if (string.IsNullOrWhiteSpace(report.Overview.ComputerName))
                report.Overview.ComputerName = request.DisplayName;

            var bios = await Query(WmiQueries.Cimv2, WmiQueries.Bios, "BIOS").ConfigureAwait(false);
            var product = await Query(WmiQueries.Cimv2, WmiQueries.Product, "System product").ConfigureAwait(false);
            OverviewParser.ApplyIdentity(report.Overview, bios.Rows, product.Rows);

            var os = await Query(WmiQueries.Cimv2, WmiQueries.OperatingSystem, "Operating system").ConfigureAwait(false);

            IRegistryReader? registry = null;
            try
            {
                registry = _registries.Create(request.Target, session);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notes.Add($"Registry: {ex.Message}");
            }

            int? ubr = null;
            string? displayVersion = null;
            if (registry is not null)
            {
                try
                {
                    ubr = registry.ReadDword("HKLM", RegistryPaths.WindowsCurrentVersion, "UBR");
                    displayVersion = registry.ReadString("HKLM", RegistryPaths.WindowsCurrentVersion, "DisplayVersion");
                    var secureBoot = registry.ReadDword("HKLM", RegistryPaths.SecureBootState, "UEFISecureBootEnabled");
                    var secureError = registry.Warnings.FirstOrDefault(w => w.Contains("SecureBoot", StringComparison.OrdinalIgnoreCase));
                    OverviewParser.ApplySecureBoot(report.Overview, secureBoot, secureBoot is null ? secureError : null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    notes.Add($"Registry values: {ex.Message}");
                    OverviewParser.ApplySecureBoot(report.Overview, null, ex.Message);
                }
            }

            OverviewParser.ApplyOperatingSystem(report.Overview, os.Rows, ubr, displayVersion);
            if (report.Overview.LastBoot is DateTimeOffset boot)
                report.Overview.Uptime = report.CollectedAt - boot;

            var processors = await Query(WmiQueries.Cimv2, WmiQueries.Processor, "Processor").ConfigureAwait(false);
            report.Hardware.Cpus = HardwareParser.ParseCpus(processors.Rows);
            report.Overview.Cpu = HardwareParser.SummarizeCpus(report.Hardware.Cpus);

            var volumes = await Query(WmiQueries.Cimv2, WmiQueries.LogicalDisk, "Volumes").ConfigureAwait(false);
            report.Hardware.Volumes = HardwareParser.ParseVolumes(volumes.Rows);

            if (!summaryOnly)
                await CollectDetailsAsync(report, registry, Query, cancellationToken).ConfigureAwait(false);

            if (registry is not null)
            {
                foreach (var warning in registry.Warnings)
                {
                    if (!notes.Contains(warning))
                        notes.Add(warning);
                }
            }

            return report;
        }
        finally
        {
            if (session is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static async Task CollectDetailsAsync(
        MachineReport report,
        IRegistryReader? registry,
        Func<string, string, string, Task<(IReadOnlyList<WmiInstance> Rows, string? Error)>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var memory = await query(WmiQueries.Cimv2, WmiQueries.PhysicalMemory, "Memory modules").ConfigureAwait(false);
        report.Hardware.MemoryModules = HardwareParser.ParseMemory(memory.Rows);

        var msft = await query(WmiQueries.Storage, WmiQueries.PhysicalDisk, "Physical disks").ConfigureAwait(false);
        var drives = await query(WmiQueries.Cimv2, WmiQueries.DiskDrive, "Disk drives").ConfigureAwait(false);
        report.Hardware.Disks = HardwareParser.ParseDisks(msft.Error is null ? msft.Rows : Array.Empty<WmiInstance>(), drives.Rows);
        if (msft.Error is not null && report.Hardware.Disks.Count > 0)
        {
            foreach (var disk in report.Hardware.Disks)
            {
                disk.Health = "Not available";
                disk.SmartSource = "MSFT_PhysicalDisk not available";
            }
        }

        var gpus = await query(WmiQueries.Cimv2, WmiQueries.Video, "Video controllers").ConfigureAwait(false);
        report.Hardware.Gpus = HardwareParser.ParseGpus(gpus.Rows);

        var monitors = await query(WmiQueries.Wmi, WmiQueries.Monitor, "Monitors").ConfigureAwait(false);
        report.Hardware.Monitors = HardwareParser.ParseMonitors(monitors.Rows);

        var nics = await query(WmiQueries.Cimv2, WmiQueries.Nic, "Network adapters").ConfigureAwait(false);
        var configs = await query(WmiQueries.Cimv2, WmiQueries.NicConfig, "Network configuration").ConfigureAwait(false);
        report.Overview.Adapters = OverviewParser.ParseAdapters(configs.Rows, nics.Rows, ipEnabledOnly: true);
        report.Hardware.Adapters = OverviewParser.ParseAdapters(configs.Rows, nics.Rows, ipEnabledOnly: false);

        var tpm = await query(WmiQueries.Tpm, WmiQueries.TpmClass, "TPM").ConfigureAwait(false);
        OverviewParser.ApplyTpm(report.Overview, tpm.Rows, tpm.Error);

        var bitlocker = await query(WmiQueries.BitLocker, WmiQueries.EncryptableVolume, "BitLocker").ConfigureAwait(false);
        report.Overview.BitLocker = OverviewParser.ParseBitLocker(bitlocker.Rows, bitlocker.Error);

        var batteries = await query(WmiQueries.Cimv2, WmiQueries.Battery, "Battery").ConfigureAwait(false);
        var design = await query(WmiQueries.Wmi, WmiQueries.BatteryStatic, "Battery design capacity").ConfigureAwait(false);
        var full = await query(WmiQueries.Wmi, WmiQueries.BatteryFull, "Battery full charge").ConfigureAwait(false);
        report.Hardware.Battery = HardwareParser.ParseBattery(batteries.Rows, design.Rows, full.Rows);

        var hotfixes = await query(WmiQueries.Cimv2, WmiQueries.Hotfix, "Hotfixes").ConfigureAwait(false);
        report.Hotfixes = SoftwareParser.ParseHotfixes(hotfixes.Rows);

        var services = await query(WmiQueries.Cimv2, WmiQueries.Service, "Services").ConfigureAwait(false);
        report.Services = ServiceParser.Parse(services.Rows);

        var startup = await query(WmiQueries.Cimv2, WmiQueries.Startup, "Startup commands").ConfigureAwait(false);
        if (registry is not null)
        {
            try
            {
                report.Programs = SoftwareParser.Parse(registry.ReadUninstallRecords());
                var approved = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in registry.ReadStartupApproved("HKLM", RegistryPaths.StartupApprovedRun))
                    approved[pair.Key] = pair.Value;
                foreach (var pair in registry.ReadStartupApproved("HKCU", RegistryPaths.StartupApprovedRun))
                    approved.TryAdd(pair.Key, pair.Value);
                report.StartupItems = StartupParser.Parse(registry.ReadRunKeys(), approved, startup.Rows);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.Notes.Add($"Installed software: {ex.Message}");
            }
        }
        else if (startup.Rows.Count > 0)
        {
            report.StartupItems = StartupParser.Parse(Array.Empty<Registry.RunKeyRecord>(), new Dictionary<string, byte[]>(), startup.Rows);
        }
    }
}
