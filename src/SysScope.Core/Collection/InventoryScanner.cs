using SysScope.Core.Formatting;
using SysScope.Core.Models;

namespace SysScope.Core.Collection;

public readonly record struct ScanProgress(int Completed, int Total, string Current, InventoryRow? Row);

public sealed class InventoryScanResult
{
    public required IReadOnlyList<InventoryRow> Rows { get; init; }
    public bool Cancelled { get; init; }
}

public static class InventoryRowMapper
{
    public static InventoryRow FromReport(MachineReport report, string fallbackName)
    {
        var name = string.IsNullOrWhiteSpace(report.Overview.ComputerName) ? fallbackName : report.Overview.ComputerName;
        if (!report.Online)
        {
            return new InventoryRow
            {
                Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name,
                Online = "No",
                Detail = string.IsNullOrWhiteSpace(report.Error) ? "Offline" : report.Error
            };
        }

        var free = VolumeMath.TotalFree(report.Hardware.Volumes);
        return new InventoryRow
        {
            Name = name,
            Online = "Yes",
            Model = report.Overview.Model,
            Serial = report.Overview.SerialNumber,
            Os = report.Overview.OsEdition,
            Build = report.Overview.OsBuild,
            Cpu = report.Overview.Cpu,
            Ram = ByteSizeFormatter.Format(report.Overview.RamBytes),
            DiskFree = ByteSizeFormatter.Format(free),
            LastBoot = DisplayFormatter.DateTime(report.Overview.LastBoot),
            LoggedOnUser = report.Overview.LoggedOnUser,
            Detail = string.Join("; ", report.Notes.Where(note => !string.IsNullOrWhiteSpace(note)))
        };
    }
}

public sealed class InventoryScanner
{
    private readonly IMachineCollector _collector;
    private readonly int _maxDegree;

    public InventoryScanner(IMachineCollector collector, int maxDegreeOfParallelism = 8)
    {
        _collector = collector;
        _maxDegree = Math.Max(1, maxDegreeOfParallelism);
    }

    public async Task<InventoryScanResult> ScanAsync(
        IReadOnlyList<CollectRequest> requests,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var rows = new InventoryRow?[requests.Count];
        var completed = 0;
        var total = requests.Count;
        var cancelled = false;

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, requests.Count),
                new ParallelOptions { MaxDegreeOfParallelism = _maxDegree, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    var request = requests[index];
                    progress?.Report(new ScanProgress(Volatile.Read(ref completed), total, request.DisplayName, null));
                    MachineReport report;
                    try
                    {
                        report = await _collector.CollectSummaryAsync(request, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        report = new MachineReport
                        {
                            Online = false,
                            RequestedName = request.DisplayName,
                            Error = ex.Message,
                            Overview = { ComputerName = request.DisplayName }
                        };
                    }

                    var row = InventoryRowMapper.FromReport(report, request.DisplayName);
                    rows[index] = row;
                    var done = Interlocked.Increment(ref completed);
                    progress?.Report(new ScanProgress(done, total, request.DisplayName, row));
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }

        var list = new List<InventoryRow>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            list.Add(rows[i] ?? new InventoryRow
            {
                Name = requests[i].DisplayName,
                Online = "No",
                Detail = "Cancelled"
            });
        }

        return new InventoryScanResult { Rows = list, Cancelled = cancelled };
    }
}
