using SysScope.Core.Models;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;

namespace SysScope.Core.Parsing;

public static class ServiceParser
{
    public static List<ServiceInfo> Parse(IReadOnlyList<WmiInstance> rows)
    {
        var list = new List<ServiceInfo>();
        foreach (var row in rows)
        {
            var name = row.Text("Name");
            if (name.Length == 0)
                continue;
            list.Add(new ServiceInfo
            {
                Name = name,
                DisplayName = row.Text("DisplayName"),
                Status = row.Text("State"),
                StartMode = StartMode(row.Text("StartMode")),
                Account = row.Text("StartName")
            });
        }
        return list
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string StartMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return "";
        return mode.Trim().ToLowerInvariant() switch
        {
            "auto" => "Automatic",
            "manual" => "Manual",
            "disabled" => "Disabled",
            "boot" => "Boot",
            "system" => "System",
            _ => mode.Trim()
        };
    }
}

public static class StartupParser
{
    public static List<StartupItem> Parse(
        IReadOnlyList<RunKeyRecord> runKeys,
        IReadOnlyDictionary<string, byte[]> approved,
        IReadOnlyList<WmiInstance> commands)
    {
        var list = new List<StartupItem>();
        foreach (var run in runKeys)
        {
            if (string.IsNullOrWhiteSpace(run.Name) && string.IsNullOrWhiteSpace(run.Command))
                continue;
            approved.TryGetValue(run.Name, out var blob);
            list.Add(new StartupItem
            {
                Name = run.Name,
                Command = run.Command,
                Location = run.Source,
                User = run.User,
                Enabled = EnabledFromApproved(blob)
            });
        }

        foreach (var row in commands)
        {
            var name = row.Text("Name");
            var command = row.Text("Command");
            if (name.Length == 0 && command.Length == 0)
                continue;
            if (list.Any(item => Same(item.Name, name) && Same(item.Command, command)))
                continue;
            list.Add(new StartupItem
            {
                Name = name,
                Command = command,
                Location = row.Text("Location"),
                User = row.Text("User"),
                Enabled = "Yes"
            });
        }

        return list
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string EnabledFromApproved(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return "Yes";
        return data[0] switch
        {
            2 or 6 => "Yes",
            3 => "No",
            _ => "Unknown"
        };
    }

    private static bool Same(string left, string right) =>
        left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);
}
