namespace SysScope.Core.Wmi;

public enum TransportErrorCode
{
    Unknown = 0,
    AccessDenied,
    RpcServerUnavailable,
    HostUnreachable,
    TimedOut,
    WinRmNotAvailable,
    WinRmConnection
}

public sealed class TransportException : Exception
{
    public TransportException(string transport, TransportErrorCode code, string? detail, Exception? inner = null)
        : base(string.IsNullOrWhiteSpace(detail) ? code.ToString() : detail, inner)
    {
        Transport = transport;
        Code = code;
    }

    public string Transport { get; }
    public TransportErrorCode Code { get; }
}

public sealed class CollectionFailedException : Exception
{
    public CollectionFailedException(string computer, IReadOnlyList<string> attempts)
        : base(Build(computer, attempts))
    {
        Computer = computer;
        Attempts = attempts;
    }

    public string Computer { get; }
    public IReadOnlyList<string> Attempts { get; }

    private static string Build(string computer, IReadOnlyList<string> attempts)
    {
        var who = string.IsNullOrWhiteSpace(computer) ? "this PC" : computer.Trim();
        var body = string.Join(Environment.NewLine + Environment.NewLine, attempts.Where(a => !string.IsNullOrWhiteSpace(a)));
        return $"Could not inventory {who}.{Environment.NewLine}{Environment.NewLine}{body}";
    }
}
