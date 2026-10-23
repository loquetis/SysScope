using System.Text;

namespace SysScope.Core.Collection;

public static class ComputerListParser
{
    private static readonly HashSet<string> HeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "computer", "computername", "computer name", "name", "hostname", "host", "dns", "machine", "machine name"
    };

    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var start = 0;
        int? column = null;
        var firstIndex = Array.FindIndex(lines, line =>
        {
            var trimmed = line.Trim();
            return trimmed.Length > 0 && !trimmed.StartsWith('#');
        });
        if (firstIndex >= 0)
        {
            var first = lines[firstIndex].Trim();
            var headers = SplitCsvLine(first);
            var headerIndex = -1;
            for (var i = 0; i < headers.Count; i++)
            {
                if (HeaderNames.Contains(headers[i].Trim()))
                {
                    headerIndex = i;
                    break;
                }
            }

            // A lone "Name" (or similar) is a header even when that line has no comma,
            // which is how a one-column CSV with a quoted computer name is pasted.
            if (headerIndex >= 0 && (first.Contains(',') || headers.Count == 1))
            {
                column = headerIndex;
                start = firstIndex + 1;
            }
            else if (first.Contains(',') && headers.Count > 1)
            {
                column = 0;
            }
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            string name;
            if (column is int col && line.Contains(','))
            {
                var fields = SplitCsvLine(line);
                if (col >= fields.Count)
                    continue;
                name = fields[col].Trim();
            }
            else if (line.Contains(','))
            {
                name = SplitCsvLine(line)[0].Trim();
            }
            else
            {
                name = line.Trim().Trim('"');
            }

            if (name.Length == 0 || name.StartsWith('#'))
                continue;
            if (seen.Add(name))
                result.Add(name);
        }
        return result;
    }

    public static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString().Trim());
        return fields;
    }
}
