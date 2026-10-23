using SysScope.Core.Models;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;

namespace SysScope.Core.Parsing;

public static class SoftwareParser
{
    public static List<InstalledProgram> Parse(IReadOnlyList<UninstallRecord> records)
    {
        var chosen = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.DisplayName))
                continue;
            if (record.SystemComponent)
                continue;
            if (!string.IsNullOrWhiteSpace(record.ParentKeyName))
                continue;
            if (IsUpdate(record.ReleaseType) || IsUpdateName(record.DisplayName))
                continue;

            var program = new InstalledProgram
            {
                Name = record.DisplayName.Trim(),
                Version = record.DisplayVersion?.Trim() ?? "",
                Publisher = record.Publisher?.Trim() ?? "",
                InstallDate = WmiDateParser.ParseInstallDate(record.InstallDate),
                SizeBytes = record.EstimatedSizeKb is > 0 ? record.EstimatedSizeKb.Value * 1024L : null,
                Source = record.Source
            };
            var key = program.Name + "\u001f" + program.Version;
            if (!chosen.TryGetValue(key, out var existing))
            {
                chosen[key] = program;
                continue;
            }

            if (existing.Publisher.Length == 0 && program.Publisher.Length > 0)
                chosen[key] = program;
        }

        return chosen.Values
            .OrderBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(program => program.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsUpdate(string? releaseType)
    {
        if (string.IsNullOrWhiteSpace(releaseType))
            return false;
        return releaseType.Trim() switch
        {
            "Security Update" => true,
            "Update" => true,
            "Update Rollup" => true,
            "Hotfix" => true,
            "Service Pack" => true,
            _ => false
        };
    }

    public static bool IsUpdateName(string name)
    {
        var text = name.Trim();
        return text.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Hotfix for ", StringComparison.OrdinalIgnoreCase);
    }

    public static List<Hotfix> ParseHotfixes(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<Hotfix>();
        foreach (var row in rows)
        {
            var id = row.Text("HotFixID");
            if (id.Length == 0)
                continue;
            list.Add(new Hotfix
            {
                HotFixId = id,
                Description = row.Text("Description"),
                InstalledBy = row.Text("InstalledBy"),
                InstalledOn = WmiDateParser.ParseInstallDate(row.Text("InstalledOn"))
            });
        }
        return list
            .OrderByDescending(item => item.InstalledOn ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.HotFixId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
