using System.Globalization;
using System.Management;
using SysScope.Core.Collection;
using SysScope.Core.Models;
using SysScope.Core.Wmi;

namespace SysScope;

public sealed class DcomTransport : ITransportProbe
{
    public string Name => "DCOM";

    public async Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run<IWmiSession>(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var session = new DcomWmiSession(target);
                session.Probe();
                return session;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw TransportExceptionMapper.Map(Name, ex);
        }
    }
}

public sealed class DcomWmiSession : IWmiSession, IDisposable
{
    private readonly ConnectionTarget _target;
    private readonly ConnectionOptions _options;

    public DcomWmiSession(ConnectionTarget target)
    {
        _target = target;
        _options = new ConnectionOptions
        {
            Impersonation = ImpersonationLevel.Impersonate,
            Authentication = AuthenticationLevel.PacketPrivacy,
            EnablePrivileges = true,
            Timeout = TimeSpan.FromSeconds(25)
        };
        if (!target.IsLocal && target.HasCredentials)
        {
            _options.Username = target.Username!.Trim();
            _options.Password = target.Password ?? "";
        }
    }

    public string Computer => _target.IsLocal ? Environment.MachineName : _target.ComputerName;

    public void Probe() => Open(WmiQueries.Cimv2);

    public Task<IReadOnlyList<WmiInstance>> QueryAsync(string scope, string wql, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var managementScope = Open(scope);
                using var searcher = new ManagementObjectSearcher(managementScope, new ObjectQuery(wql));
                searcher.Options.Timeout = TimeSpan.FromSeconds(60);
                using var collection = searcher.Get();
                var list = new List<WmiInstance>();
                foreach (ManagementObject item in collection)
                {
                    using (item)
                        list.Add(Read(item));
                }
                return (IReadOnlyList<WmiInstance>)list;
            }
            catch (ManagementException ex)
            {
                throw new InvalidOperationException($"{ex.Message} (0x{(uint)ex.ErrorCode:X8})", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("Access is denied. " + ex.Message, ex);
            }
        }, cancellationToken);
    }

    public Task<WmiMethodResult> InvokeAsync(string scope, string className, string method, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var managementScope = Open(scope);
                using var managementClass = new ManagementClass(managementScope, new ManagementPath(className), null);
                using var input = managementClass.GetMethodParameters(method);
                foreach (var pair in arguments)
                {
                    if (pair.Value is not null)
                        input[pair.Key] = pair.Value;
                }
                using var output = managementClass.InvokeMethod(method, input, null);
                return new WmiMethodResult { Outputs = output is null ? new WmiInstance() : Read(output) };
            }
            catch (ManagementException ex)
            {
                throw new InvalidOperationException($"{ex.Message} (0x{(uint)ex.ErrorCode:X8})", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("Access is denied. " + ex.Message, ex);
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
    }

    private ManagementScope Open(string scope)
    {
        var path = _target.IsLocal
            ? @"\\.\" + scope.Trim().TrimStart('\\')
            : @"\\" + _target.ComputerName + @"\" + scope.Trim().TrimStart('\\');
        var managementScope = new ManagementScope(path, _options);
        managementScope.Connect();
        return managementScope;
    }

    private static WmiInstance Read(ManagementBaseObject item)
    {
        var instance = new WmiInstance();
        foreach (PropertyData property in item.Properties)
        {
            try
            {
                instance.Set(property.Name, ConvertValue(property.Value));
            }
            catch (Exception)
            {
                // Some providers expose properties that cannot be marshalled. Skip those values.
            }
        }
        return instance;
    }

    private static object? ConvertValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return value;
            case string[]:
            case ushort[]:
            case byte[]:
                return value;
            case Array array:
            {
                if (array.Length == 0)
                    return Array.Empty<string>();
                var element = array.GetType().GetElementType();
                if (element == typeof(ushort))
                {
                    var units = new ushort[array.Length];
                    for (var i = 0; i < array.Length; i++)
                        units[i] = Convert.ToUInt16(array.GetValue(i), CultureInfo.InvariantCulture);
                    return units;
                }
                var text = new string[array.Length];
                for (var i = 0; i < array.Length; i++)
                    text[i] = Convert.ToString(array.GetValue(i), CultureInfo.InvariantCulture) ?? "";
                return text;
            }
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
