using SysScope.Core.Collection;
using SysScope.Core.Models;
using SysScope.Core.Wmi;

namespace SysScope.Core.Tests;

internal static class Support
{
    public static WmiInstance Row(params (string Name, object? Value)[] props)
    {
        var instance = new WmiInstance();
        foreach (var (name, value) in props)
            instance.Set(name, value);
        return instance;
    }

    public static CollectRequest Request(string computer) =>
        new(new ConnectionTarget(computer, null, null), computer);
}

internal sealed class ScriptSession : IWmiSession
{
    public List<string> Queries { get; } = new();
    public Func<string, string, IReadOnlyList<WmiInstance>>? OnQuery { get; init; }
    public Func<string, IReadOnlyDictionary<string, object?>, WmiMethodResult>? OnInvoke { get; init; }

    public string Computer { get; init; } = "PC";

    public Task<IReadOnlyList<WmiInstance>> QueryAsync(string scope, string wql, CancellationToken cancellationToken)
    {
        Queries.Add(wql);
        IReadOnlyList<WmiInstance> rows = OnQuery is null ? Array.Empty<WmiInstance>() : OnQuery(scope, wql);
        return Task.FromResult(rows);
    }

    public Task<WmiMethodResult> InvokeAsync(string scope, string className, string method, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        if (OnInvoke is null)
            throw new InvalidOperationException("No invoke handler.");
        return Task.FromResult(OnInvoke(method, arguments));
    }
}

internal sealed class FixedSessionFactory : IWmiSessionFactory
{
    private readonly IWmiSession _session;
    public int Calls { get; private set; }
    public FixedSessionFactory(IWmiSession session) => _session = session;

    public Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(_session);
    }
}

internal sealed class ListRegistryFactory : Registry.IRegistryReaderFactory
{
    private readonly Registry.IRegistryReader _reader;
    public ListRegistryFactory(Registry.IRegistryReader reader) => _reader = reader;
    public Registry.IRegistryReader Create(ConnectionTarget target, IWmiSession session) => _reader;
}

internal sealed class MemoryRegistry : Registry.IRegistryReader
{
    public int? Ubr { get; init; }
    public string? DisplayVersion { get; init; }
    public int? SecureBoot { get; init; }
    public List<Registry.UninstallRecord> Programs { get; init; } = new();
    public List<Registry.RunKeyRecord> RunKeys { get; init; } = new();
    public Dictionary<string, byte[]> Approved { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Warnings { get; } = Array.Empty<string>();
    public IReadOnlyList<Registry.UninstallRecord> ReadUninstallRecords() => Programs;
    public IReadOnlyList<Registry.RunKeyRecord> ReadRunKeys() => RunKeys;
    public string? ReadString(string hive, string keyPath, string valueName) =>
        valueName.Equals("DisplayVersion", StringComparison.OrdinalIgnoreCase) ? DisplayVersion : null;
    public int? ReadDword(string hive, string keyPath, string valueName)
    {
        if (valueName.Equals("UBR", StringComparison.OrdinalIgnoreCase))
            return Ubr;
        if (valueName.Equals("UEFISecureBootEnabled", StringComparison.OrdinalIgnoreCase))
            return SecureBoot;
        return null;
    }
    public IReadOnlyDictionary<string, byte[]> ReadStartupApproved(string hive, string keyPath) => Approved;
}

internal sealed class DelegateCollector : IMachineCollector
{
    private readonly Func<CollectRequest, CancellationToken, Task<MachineReport>> _handler;
    public DelegateCollector(Func<CollectRequest, CancellationToken, Task<MachineReport>> handler) => _handler = handler;
    public Task<MachineReport> CollectAsync(CollectRequest request, CancellationToken cancellationToken) => _handler(request, cancellationToken);
    public Task<MachineReport> CollectSummaryAsync(CollectRequest request, CancellationToken cancellationToken) => _handler(request, cancellationToken);
}

internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;
    public SyncProgress(Action<T> report) => _report = report;
    public void Report(T value) => _report(value);
}

internal sealed class Probe : ITransportProbe
{
    public Probe(string name, Exception? error = null, IWmiSession? session = null)
    {
        Name = name;
        Error = error;
        Session = session;
    }

    public string Name { get; }
    public Exception? Error { get; }
    public IWmiSession? Session { get; }
    public int Calls { get; private set; }

    public Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        if (Error is not null)
            throw Error;
        return Task.FromResult(Session ?? throw new InvalidOperationException("No session."));
    }
}
