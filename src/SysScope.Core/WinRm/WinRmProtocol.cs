using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SysScope.Core.Wmi;

namespace SysScope.Core.WinRm;

public sealed class WinRmPage
{
    public List<WmiInstance> Items { get; } = new();
    public string? Context { get; init; }
    public bool EndOfSequence { get; init; }
    public string? Fault { get; init; }
}

public static class WinRmProtocol
{
    public const string EnumerateAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate";
    public const string PullAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Pull";
    public const string WqlDialect = "http://schemas.microsoft.com/wbem/wsman/1/WQL";

    public static string ResourceUri(string scope, string className)
    {
        var path = scope.Trim().Trim('\\').Replace('\\', '/');
        return $"http://schemas.microsoft.com/wbem/wsman/1/wmi/{path}/{className}";
    }

    public static bool TryClassFromWql(string wql, out string className)
    {
        className = "";
        if (string.IsNullOrWhiteSpace(wql))
            return false;
        var marker = " FROM ";
        var index = wql.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;
        var rest = wql[(index + marker.Length)..].Trim();
        if (rest.StartsWith('['))
        {
            var endBracket = rest.IndexOf(']');
            if (endBracket > 1)
            {
                className = rest[1..endBracket];
                return className.Length > 0;
            }
        }
        var end = rest.IndexOfAny([' ', '\r', '\n', '\t']);
        className = end < 0 ? rest : rest[..end];
        return className.Length > 0;
    }

    public static string BuildEnumerate(string to, string resourceUri, string wql, string messageId) =>
        Envelope(to, resourceUri, EnumerateAction, messageId,
            $"""
            <n:Enumerate xmlns:n="http://schemas.xmlsoap.org/ws/2004/09/enumeration">
              <w:Filter Dialect="{WqlDialect}">{Encode(wql)}</w:Filter>
            </n:Enumerate>
            """);

    public static string BuildPull(string to, string resourceUri, string context, string messageId) =>
        Envelope(to, resourceUri, PullAction, messageId,
            $"""
            <n:Pull xmlns:n="http://schemas.xmlsoap.org/ws/2004/09/enumeration">
              <n:EnumerationContext>{Encode(context)}</n:EnumerationContext>
              <n:MaxElements>50</n:MaxElements>
            </n:Pull>
            """);

    public static string BuildInvoke(
        string to,
        string scope,
        string className,
        string method,
        IReadOnlyDictionary<string, object?> arguments,
        string messageId)
    {
        var resource = ResourceUri(scope, className);
        var action = resource + "/" + method;
        var body = new StringBuilder();
        body.Append("<p:").Append(method).Append("_INPUT xmlns:p=\"").Append(Encode(resource)).Append("\">");
        foreach (var pair in arguments)
        {
            if (pair.Value is null)
                continue;
            body.Append("<p:").Append(pair.Key).Append('>');
            body.Append(Encode(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)));
            body.Append("</p:").Append(pair.Key).Append('>');
        }
        body.Append("</p:").Append(method).Append("_INPUT>");
        return Envelope(to, resource, action, messageId, body.ToString());
    }

    public static WinRmPage ParseResponse(string xml)
    {
        var document = LoadXml(xml);
        var fault = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var text = fault.Descendants().FirstOrDefault(element => element.Name.LocalName == "Text")?.Value
                ?? fault.Descendants().FirstOrDefault(element => element.Name.LocalName == "Reason")?.Value;
            return new WinRmPage { Fault = string.IsNullOrWhiteSpace(text) ? fault.Value.Trim() : text.Trim() };
        }

        var page = new WinRmPage
        {
            Context = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "EnumerationContext")?.Value,
            EndOfSequence = document.Descendants().Any(element => element.Name.LocalName == "EndOfSequence")
        };
        var items = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Items");
        if (items is not null)
        {
            foreach (var item in items.Elements())
                page.Items.Add(ReadInstance(item));
        }

        var output = document.Descendants().FirstOrDefault(element => element.Name.LocalName.EndsWith("_OUTPUT", StringComparison.OrdinalIgnoreCase));
        if (output is not null && page.Items.Count == 0)
            page.Items.Add(ReadInstance(output));
        return page;
    }

    public static WmiMethodResult ParseMethodResult(string xml)
    {
        var page = ParseResponse(xml);
        if (!string.IsNullOrWhiteSpace(page.Fault))
            throw new InvalidOperationException(page.Fault);
        return new WmiMethodResult { Outputs = page.Items.FirstOrDefault() ?? new WmiInstance() };
    }

    private static WmiInstance ReadInstance(XElement item)
    {
        var instance = new WmiInstance();
        foreach (var group in item.Elements().GroupBy(element => element.Name.LocalName))
        {
            var elements = group.ToList();
            instance.Set(group.Key, elements.Count == 1 ? ReadValue(elements[0]) : elements.Select(ReadScalar).Where(value => value is not null).Cast<string>().ToArray());
        }
        return instance;
    }

    private static object? ReadValue(XElement element)
    {
        if (IsNil(element))
            return null;
        var children = element.Elements().ToList();
        if (children.Count == 0)
            return element.Value;
        if (children.All(child => child.Name.LocalName is "string" or "char"))
            return children.Select(child => child.Value).ToArray();
        return element.Value;
    }

    private static string? ReadScalar(XElement element)
    {
        if (IsNil(element))
            return null;
        return element.Value;
    }

    private static bool IsNil(XElement element) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "nil" && attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

    private static string Envelope(string to, string resourceUri, string action, string messageId, string body) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:w="http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd" xmlns:p="http://schemas.microsoft.com/wbem/wsman/1/wsman.xsd">
          <s:Header>
            <a:To>{Encode(to)}</a:To>
            <w:ResourceURI s:mustUnderstand="true">{Encode(resourceUri)}</w:ResourceURI>
            <a:ReplyTo><a:Address s:mustUnderstand="true">http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address></a:ReplyTo>
            <a:Action s:mustUnderstand="true">{Encode(action)}</a:Action>
            <w:MaxEnvelopeSize s:mustUnderstand="true">153600</w:MaxEnvelopeSize>
            <a:MessageID>uuid:{Encode(messageId)}</a:MessageID>
            <w:OperationTimeout>PT60S</w:OperationTimeout>
          </s:Header>
          <s:Body>
            {body}
          </s:Body>
        </s:Envelope>
        """;

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");

    /// <summary>
    /// WinRM bodies sometimes use a prefix (commonly p:) without declaring it.
    /// Declare any missing prefixes so the document can still be read.
    /// </summary>
    private static XDocument LoadXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml);
        }
        catch (XmlException) when (DeclareMissingPrefixes(xml) is string repaired)
        {
            return XDocument.Parse(repaired);
        }
    }

    private static string? DeclareMissingPrefixes(string xml)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in PrefixUse.Matches(xml))
        {
            var prefix = match.Groups[1].Length > 0 ? match.Groups[1].Value : match.Groups[2].Value;
            if (prefix.Length > 0 && !prefix.Equals("xmlns", StringComparison.Ordinal))
                used.Add(prefix);
        }
        if (used.Count == 0)
            return null;

        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in PrefixDecl.Matches(xml))
            declared.Add(match.Groups[1].Value);

        var missing = used.Where(prefix => prefix != "xml" && !declared.Contains(prefix)).ToList();
        if (missing.Count == 0)
            return null;

        var declarations = string.Join(" ", missing.Select(prefix => $"xmlns:{prefix}=\"urn:sysscope:{prefix}\""));
        var start = 0;
        var trimmed = xml.AsSpan().TrimStart();
        if (trimmed.StartsWith("<?", StringComparison.Ordinal))
        {
            var declEnd = xml.IndexOf("?>", StringComparison.Ordinal);
            if (declEnd < 0)
                return null;
            start = xml.IndexOf('<', declEnd + 2);
        }
        else
        {
            start = xml.IndexOf('<');
        }
        if (start < 0)
            return null;
        var close = xml.IndexOf('>', start);
        if (close < 0)
            return null;
        var insertAt = xml[close - 1] == '/' ? close - 1 : close;
        return xml.Insert(insertAt, " " + declarations);
    }

    private static readonly Regex PrefixUse = new(@"</?([A-Za-z_][\w.-]*):|[\s]([A-Za-z_][\w.-]*):[A-Za-z_]", RegexOptions.Compiled);
    private static readonly Regex PrefixDecl = new(@"xmlns:([A-Za-z_][\w.-]*)", RegexOptions.Compiled);
}
