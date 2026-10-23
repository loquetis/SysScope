using SysScope.Core.Models;

namespace SysScope.Core.Wmi;

public static class TransportExceptionMapper
{
    public static TransportException Map(string transport, Exception exception)
    {
        if (exception is TransportException transportException)
            return transportException;
        var message = exception.Message ?? "";
        return new TransportException(transport, Classify(message), message, exception);
    }

    public static TransportErrorCode Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return TransportErrorCode.Unknown;
        if (Has(message, "0x800706BA") || Has(message, "RPC server is unavailable"))
            return TransportErrorCode.RpcServerUnavailable;
        if (Has(message, "0x80070005") || Has(message, "0x80041003") || Has(message, "Access is denied") || Has(message, "Access denied"))
            return TransportErrorCode.AccessDenied;
        if (Has(message, "timed out") || Has(message, "timeout"))
            return TransportErrorCode.TimedOut;
        if (Has(message, "no such host") || Has(message, "could not be resolved") || Has(message, "host is unreachable"))
            return TransportErrorCode.HostUnreachable;
        if (Has(message, "WinRM") && (Has(message, "cannot connect") || Has(message, "could not connect")))
            return TransportErrorCode.WinRmConnection;
        return TransportErrorCode.Unknown;
    }

    private static bool Has(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

public static class ConnectionErrorFormatter
{
    public static string Format(TransportException exception)
    {
        var hint = exception.Code switch
        {
            TransportErrorCode.AccessDenied =>
                "Access was denied. Use an account that is allowed to query WMI remotely (Remote Enable, and typically Administrators). If a firewall is in the way, allow Windows Management Instrumentation (DCOM) or Windows Remote Management (WinRM).",
            TransportErrorCode.RpcServerUnavailable =>
                "The RPC server is unavailable. The computer may be offline, the name or IP may be wrong, or a firewall is blocking DCOM (WMI).",
            TransportErrorCode.HostUnreachable =>
                "The host could not be reached. Check the name or IP address and the network path.",
            TransportErrorCode.TimedOut =>
                "The connection timed out before the computer answered.",
            TransportErrorCode.WinRmNotAvailable =>
                "WinRM fallback is not available on this computer.",
            TransportErrorCode.WinRmConnection =>
                "WinRM could not connect. Enable Windows Remote Management on the remote PC (TCP 5985). Workgroup PCs also need the host listed in TrustedHosts. HTTPS (TCP 5986) requires a certificate this PC trusts.",
            _ => "The query failed."
        };

        var detail = exception.Message;
        var showDetail = !string.IsNullOrWhiteSpace(detail)
            && !detail.Equals(exception.Code.ToString(), StringComparison.Ordinal)
            && !hint.Contains(detail, StringComparison.Ordinal);
        var suffix = showDetail ? $" ({detail})" : "";
        return $"{exception.Transport}: {hint}{suffix}";
    }
}

public sealed class FallbackConnector : IWmiSessionFactory
{
    private readonly IReadOnlyList<ITransportProbe> _probes;

    public FallbackConnector(IEnumerable<ITransportProbe> probes)
    {
        _probes = probes.ToList();
        if (_probes.Count == 0)
            throw new ArgumentException("At least one transport is required.", nameof(probes));
    }

    public async Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var errors = new List<string>();
        foreach (var probe in _probes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await probe.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TransportException ex)
            {
                errors.Add(ConnectionErrorFormatter.Format(ex));
            }
            catch (Exception ex)
            {
                errors.Add(ConnectionErrorFormatter.Format(TransportExceptionMapper.Map(probe.Name, ex)));
            }
        }

        var name = target.IsLocal ? "this PC" : target.ComputerName;
        throw new CollectionFailedException(name, errors);
    }
}
