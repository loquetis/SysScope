using System.Globalization;

namespace SysScope.Core.Parsing;

public static class WmiDateParser
{
    public static DateTimeOffset? Parse(string? dmtf)
    {
        if (string.IsNullOrWhiteSpace(dmtf))
            return null;
        var text = dmtf.Trim();
        if (text.Length < 14)
            return TryLoose(text);
        if (!DateTime.TryParseExact(text[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return TryLoose(text);

        if (text.Length >= 22 && text[21] is '+' or '-')
        {
            var offsetText = text[22..];
            if (offsetText.All(c => c == '*'))
                return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero);
            if (int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
            {
                var sign = text[21] == '-' ? -1 : 1;
                var offset = TimeSpan.FromMinutes(sign * minutes);
                if (offset >= TimeSpan.FromHours(-14) && offset <= TimeSpan.FromHours(14))
                    return new DateTimeOffset(date, offset);
            }
        }

        return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero);
    }

    public static DateTimeOffset? ParseInstallDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var value = text.Trim();
        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compact))
            return new DateTimeOffset(compact, TimeSpan.Zero);
        var wmi = Parse(value);
        if (wmi is not null && value.Length >= 14)
            return wmi;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var loose))
            return new DateTimeOffset(loose, TimeSpan.Zero);
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out var us))
            return new DateTimeOffset(us, TimeSpan.Zero);
        return null;
    }

    private static DateTimeOffset? TryLoose(string text)
    {
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var loose))
            return new DateTimeOffset(loose, TimeSpan.Zero);
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out var us))
            return new DateTimeOffset(us, TimeSpan.Zero);
        return null;
    }
}
