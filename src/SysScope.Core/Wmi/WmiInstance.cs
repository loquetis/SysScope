using System.Collections;
using System.Globalization;

namespace SysScope.Core.Wmi;

public sealed class WmiInstance
{
    private readonly Dictionary<string, object?> _props = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object?> Properties => _props;

    public void Set(string name, object? value) => _props[name] = value;

    public object? Get(string name) => _props.TryGetValue(name, out var value) ? value : null;

    public string Text(string name)
    {
        if (!_props.TryGetValue(name, out var value) || value is null)
            return "";
        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
    }

    public long? Int64(string name)
    {
        if (!_props.TryGetValue(name, out var value) || value is null)
            return null;
        switch (value)
        {
            case long l:
                return l;
            case int i:
                return i;
            case uint u:
                return u;
            case ulong ul when ul <= long.MaxValue:
                return (long)ul;
            case short s:
                return s;
            case ushort us:
                return us;
            case byte b:
                return b;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                return (long)d;
            case string str when long.TryParse(str.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                return parsed;
            default:
                return null;
        }
    }

    public int? Int32(string name)
    {
        var value = Int64(name);
        if (value is null || value > int.MaxValue || value < int.MinValue)
            return null;
        return (int)value.Value;
    }

    public bool? Bool(string name)
    {
        if (!_props.TryGetValue(name, out var value) || value is null)
            return null;
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s when bool.TryParse(s, out var parsed) => parsed,
            string s when s.Trim() == "0" => false,
            string s when s.Trim() == "1" => true,
            _ => null
        };
    }

    public IReadOnlyList<string> Texts(string name)
    {
        if (!_props.TryGetValue(name, out var value) || value is null)
            return Array.Empty<string>();
        if (value is string s)
            return string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() : new[] { s.Trim() };
        if (value is IEnumerable<string> strings)
            return strings.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is null)
                    continue;
                var text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                    list.Add(text.Trim());
            }
            return list;
        }

        var single = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single.Trim() };
    }

    public byte[]? Bytes(string name)
    {
        if (!_props.TryGetValue(name, out var value) || value is null)
            return null;
        if (value is byte[] bytes)
            return bytes;
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<byte>();
            foreach (var item in enumerable)
            {
                if (item is null)
                    continue;
                list.Add(Convert.ToByte(item, CultureInfo.InvariantCulture));
            }
            return list.ToArray();
        }
        return null;
    }
}

public sealed class WmiMethodResult
{
    public WmiInstance Outputs { get; init; } = new();

    public int? ReturnValue => Outputs.Int32("ReturnValue");
}
