using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SysScope.Core.Models;
using SysScope.Core.WinRm;
using SysScope.Core.Wmi;

namespace SysScope;

public sealed class WinRmTransport : ITransportProbe
{
    public string Name => "WinRM";

    public async Task<IWmiSession> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken)
    {
        var session = new WinRmWmiSession(target);
        try
        {
            await session.ProbeAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            session.Dispose();
            throw TransportExceptionMapper.Map(Name, ex);
        }
    }
}

public sealed class WinRmWmiSession : IWmiSession, IDisposable
{
    private readonly ConnectionTarget _target;
    private readonly HttpClient _client;
    private bool _https;

    public WinRmWmiSession(ConnectionTarget target)
    {
        _target = target;
        var handler = new HttpClientHandler { PreAuthenticate = true };
        if (target.HasCredentials)
            handler.Credentials = Credential(target.Username!, target.Password);
        else
            handler.UseDefaultCredentials = true;
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public string Computer => _target.IsLocal ? Environment.MachineName : _target.ComputerName;

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        const string probe = "SELECT Name FROM Win32_ComputerSystem";
        try
        {
            await QueryOnce(false, probe, cancellationToken).ConfigureAwait(false);
            _https = false;
        }
        catch (Exception first) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await QueryOnce(true, probe, cancellationToken).ConfigureAwait(false);
                _https = true;
            }
            catch (Exception second) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"WinRM could not connect. HTTP 5985: {first.Message} HTTPS 5986: {second.Message}",
                    second);
            }
        }
    }

    public async Task<IReadOnlyList<WmiInstance>> QueryAsync(string scope, string wql, CancellationToken cancellationToken)
    {
        if (!WinRmProtocol.TryClassFromWql(wql, out var className))
            throw new InvalidOperationException("The WQL statement is missing a class name.");
        var resource = WinRmProtocol.ResourceUri(scope, className);
        var endpoint = Endpoint(_https);
        var page = await PostAsync(endpoint, WinRmProtocol.BuildEnumerate(endpoint, resource, wql, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
        var all = new List<WmiInstance>(page.Items);
        var context = page.Context;
        var guard = 0;
        while (!string.IsNullOrEmpty(context) && !page.EndOfSequence && guard++ < 40)
        {
            var previous = context;
            page = await PostAsync(endpoint, WinRmProtocol.BuildPull(endpoint, resource, context, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
            if (page.Items.Count == 0 && (string.IsNullOrEmpty(page.Context) || page.Context == previous))
                break;
            all.AddRange(page.Items);
            context = page.Context;
        }
        return all;
    }

    public async Task<WmiMethodResult> InvokeAsync(string scope, string className, string method, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var endpoint = Endpoint(_https);
        var envelope = WinRmProtocol.BuildInvoke(endpoint, scope, className, method, arguments, Guid.NewGuid().ToString("N"));
        var xml = await SendAsync(endpoint, envelope, cancellationToken).ConfigureAwait(false);
        return WinRmProtocol.ParseMethodResult(xml);
    }

    public void Dispose() => _client.Dispose();

    private async Task<WinRmPage> QueryOnce(bool https, string wql, CancellationToken cancellationToken)
    {
        if (!WinRmProtocol.TryClassFromWql(wql, out var className))
            throw new InvalidOperationException("The WQL statement is missing a class name.");
        var resource = WinRmProtocol.ResourceUri(@"root\cimv2", className);
        var endpoint = Endpoint(https);
        return await PostAsync(endpoint, WinRmProtocol.BuildEnumerate(endpoint, resource, wql, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
    }

    private async Task<WinRmPage> PostAsync(string endpoint, string envelope, CancellationToken cancellationToken)
    {
        var xml = await SendAsync(endpoint, envelope, cancellationToken).ConfigureAwait(false);
        var page = WinRmProtocol.ParseResponse(xml);
        if (!string.IsNullOrWhiteSpace(page.Fault))
            throw new InvalidOperationException(page.Fault);
        return page;
    }

    private async Task<string> SendAsync(string endpoint, string envelope, CancellationToken cancellationToken)
    {
        using var content = new StringContent(envelope, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/soap+xml;charset=UTF-8");
        using var response = await _client.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Access is denied.");
        if (!response.IsSuccessStatusCode)
        {
            var detail = body.Length > 300 ? body[..300] : body;
            throw new InvalidOperationException($"WinRM returned HTTP {(int)response.StatusCode}. {detail}".Trim());
        }
        return body;
    }

    private string Endpoint(bool https)
    {
        var host = _target.IsLocal ? "localhost" : _target.ComputerName;
        if (host.Contains(':') && !host.StartsWith('['))
            host = "[" + host + "]";
        return $"{(https ? "https" : "http")}://{host}:{(https ? 5986 : 5985)}/wsman";
    }

    private static NetworkCredential Credential(string username, string? password)
    {
        var user = username.Trim();
        var secret = password ?? "";
        var slash = user.IndexOf('\\');
        if (slash > 0)
            return new NetworkCredential(user[(slash + 1)..], secret, user[..slash]);
        return new NetworkCredential(user, secret);
    }
}
