using SysScope.Core.Models;

namespace SysScope.Core.Wmi;

public interface IWmiSession
{
    string Computer { get; }

    Task<IReadOnlyList<WmiInstance>> QueryAsync(string scope, string wql, CancellationToken cancellationToken);

    Task<WmiMethodResult> InvokeAsync(
        string scope,
        string className,
        string method,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}

public interface ITransportProbe
{
    string Name { get; }

    Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken);
}

public interface IWmiSessionFactory
{
    Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken);
}
