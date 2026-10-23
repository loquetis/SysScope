using System.Collections;
using System.Globalization;

namespace SysScope.Core.Parsing;

public static class MonitorIdDecoder
{
    public static string Decode(object? value)
    {
        if (value is null)
            return "";
        if (value is string text)
            return text.Trim();
        if (value is ushort[] units)
            return DecodeUnits(units);
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<ushort>();
            foreach (var item in enumerable)
            {
                if (item is null)
                    continue;
                try
                {
                    list.Add(Convert.ToUInt16(item, CultureInfo.InvariantCulture));
                }
                catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
                {
                    return "";
                }
            }
            return DecodeUnits(list);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
    }

    public static string DecodeUnits(IReadOnlyList<ushort> units)
    {
        if (units.Count == 0)
            return "";
        var chars = new List<char>(units.Count);
        foreach (var unit in units)
        {
            if (unit == 0)
                break;
            chars.Add((char)unit);
        }
        return new string(chars.ToArray()).Trim();
    }
}
