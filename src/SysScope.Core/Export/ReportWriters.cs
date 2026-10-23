using System.Net;
using SysScope.Core;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SysScope.Core.Formatting;
using SysScope.Core.Models;

namespace SysScope.Core.Export;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Write(MachineReport report) => JsonSerializer.Serialize(report, Options);
}

public static class CsvInventoryWriter
{
    public static readonly string[] Headers =
    [
        "Name", "Online", "Model", "Serial", "OS", "Build", "CPU", "RAM", "Disk free", "Last boot", "Logged-on user", "Detail"
    ];

    public static byte[] Write(IEnumerable<InventoryRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', Headers.Select(Escape)));
        builder.Append("\r\n");
        foreach (var row in rows)
        {
            string[] fields =
            [
                row.Name, row.Online, row.Model, row.Serial, row.Os, row.Build, row.Cpu, row.Ram,
                row.DiskFree, row.LastBoot, row.LoggedOnUser, row.Detail
            ];
            builder.Append(string.Join(',', fields.Select(Escape)));
            builder.Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var all = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);
        return all;
    }

    public static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;
        var mustQuote = text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r') || text.StartsWith('\'');
        if (!mustQuote)
            return text;
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

public static class HtmlReportWriter
{
    public static string Write(MachineReport report)
    {
        var builder = new StringBuilder();
        var title = string.IsNullOrWhiteSpace(report.Overview.ComputerName) ? report.RequestedName : report.Overview.ComputerName;
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.Append("<title>").Append(Encode(ProductInfo.Name)).Append(" report — ").Append(Encode(title)).AppendLine("</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body{margin:0;font-family:\"Segoe UI\",Tahoma,sans-serif;background:#f4f7fb;color:#12263a;}");
        builder.AppendLine("header{background:#0B1F3A;color:#fff;padding:24px 32px;}");
        builder.AppendLine("header h1{margin:0;font-size:28px;} header p{margin:6px 0 0;color:#c5d4e8;}");
        builder.AppendLine(".accent{height:4px;background:#0D9488;}");
        builder.AppendLine("main{padding:24px 32px 48px;}");
        builder.AppendLine("section{background:#fff;border:1px solid #dae2ea;border-radius:8px;padding:16px 18px;margin:0 0 16px;}");
        builder.AppendLine("h2{margin:0 0 12px;font-size:18px;color:#0B1F3A;}");
        builder.AppendLine("table{width:100%;border-collapse:collapse;}");
        builder.AppendLine("th{text-align:left;background:#e8eff6;padding:8px;} td{padding:8px;border-top:1px solid #dae2ea;vertical-align:top;}");
        builder.AppendLine("dl{display:grid;grid-template-columns:220px 1fr;gap:6px 12px;margin:0;} dt{color:#5c6b7a;}");
        builder.AppendLine(".error{background:#fee2e2;color:#991b1b;padding:12px 14px;border-radius:8px;margin:0 0 16px;white-space:pre-wrap;}");
        builder.AppendLine(".note{background:#fff7ed;color:#9a3412;padding:12px 14px;border-radius:8px;margin:0 0 16px;white-space:pre-wrap;}");
        builder.AppendLine(".bar{height:10px;background:#e2e8f0;border-radius:99px;overflow:hidden;} .bar>span{display:block;height:10px;background:#0D9488;}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head><body>");
        builder.AppendLine("<header>");
        builder.Append("<h1>").Append(Encode(ProductInfo.Name)).AppendLine("</h1>");
        builder.Append("<p>").Append(Encode(title)).Append(" · ").Append(Encode(ProductInfo.Version));
        builder.Append(" · Collected ").Append(Encode(DisplayFormatter.DateTime(report.CollectedAt))).AppendLine("</p>");
        builder.AppendLine("</header><div class=\"accent\"></div><main>");

        if (!report.Online)
            builder.Append("<div class=\"error\">").Append(Encode(report.Error)).AppendLine("</div>");
        else if (report.Notes.Count > 0)
            builder.Append("<div class=\"note\">").Append(Encode(string.Join(Environment.NewLine, report.Notes))).AppendLine("</div>");

        var overview = report.Overview;
        builder.AppendLine("<section><h2>Overview</h2><dl>");
        Field(builder, "Computer name", overview.ComputerName);
        Field(builder, "Domain / workgroup", DisplayFormatter.Domain(overview.PartOfDomain, overview.DomainOrWorkgroup));
        Field(builder, "Logged-on user", DisplayFormatter.LoggedOnUser(overview.LoggedOnUser));
        Field(builder, "Manufacturer", overview.Manufacturer);
        Field(builder, "Model", overview.Model);
        Field(builder, "Serial number", overview.SerialNumber);
        Field(builder, "BIOS version", overview.BiosVersion);
        Field(builder, "BIOS date", DisplayFormatter.Date(overview.BiosDate));
        Field(builder, "Operating system", overview.OsEdition);
        Field(builder, "OS version", overview.OsVersion);
        Field(builder, "OS build", overview.OsBuild);
        Field(builder, "Install date", DisplayFormatter.Date(overview.InstallDate));
        Field(builder, "Last boot", DisplayFormatter.DateTime(overview.LastBoot));
        Field(builder, "Uptime", UptimeFormatter.Format(overview.Uptime));
        Field(builder, "Processor", overview.Cpu);
        Field(builder, "Memory", DisplayFormatter.Memory(overview.RamBytes, overview.RamUsedBytes));
        Field(builder, "TPM", DisplayFormatter.Tpm(overview.TpmPresent, overview.TpmVersion));
        Field(builder, "Secure Boot", DisplayFormatter.SecureBoot(overview.SecureBoot));
        builder.AppendLine("</dl></section>");

        Table(builder, "Network adapters", ["Adapter", "MAC address", "IP address", "Gateway", "DHCP", "Link speed"],
            overview.Adapters.Select(a => new[] { a.Name, a.Mac, a.IpAddresses, a.Gateway, a.Dhcp, a.LinkSpeed }));
        Table(builder, "BitLocker", ["Drive", "Status", "Encryption", "Conversion"],
            overview.BitLocker.Select(b => new[] { b.DriveLetter, b.Status, b.EncryptionMethod, b.ConversionStatus }));

        var hardware = report.Hardware;
        Table(builder, "Processors", ["Name", "Manufacturer", "Cores", "Logical processors", "Max clock (MHz)", "Socket", "L2 (KB)", "L3 (KB)"],
            hardware.Cpus.Select(c => new[] { c.Name, c.Manufacturer, Num(c.Cores), Num(c.LogicalProcessors), Num(c.MaxClockMhz), c.Socket, Num(c.L2CacheKb), Num(c.L3CacheKb) }));
        Table(builder, "Memory modules", ["Slot", "Size", "Speed (MHz)", "Manufacturer", "Part number", "Serial"],
            hardware.MemoryModules.Select(m => new[] { m.Slot, ByteSizeFormatter.Format(m.SizeBytes), Num(m.SpeedMhz), m.Manufacturer, m.PartNumber, m.Serial }));
        Table(builder, "Disks", ["Model", "Size", "Media type", "Health", "Bus", "Serial", "SMART"],
            hardware.Disks.Select(d => new[] { d.Model, ByteSizeFormatter.Format(d.SizeBytes), d.MediaType, d.Health, d.Bus, d.Serial, d.SmartSource }));

        builder.AppendLine("<section><h2>Volumes</h2>");
        if (hardware.Volumes.Count == 0)
            builder.AppendLine("<p>No volumes returned.</p>");
        else
        {
            builder.AppendLine("<table><thead><tr><th>Drive</th><th>Label</th><th>File system</th><th>Free space</th></tr></thead><tbody>");
            foreach (var volume in hardware.Volumes)
            {
                var fraction = VolumeMath.FreeFraction(volume.SizeBytes, volume.FreeBytes);
                var width = fraction is null ? 0 : (int)Math.Round(Math.Clamp(fraction.Value, 0, 1) * 100);
                builder.Append("<tr><td>").Append(Encode(volume.Drive)).Append("</td><td>").Append(Encode(volume.Label));
                builder.Append("</td><td>").Append(Encode(volume.FileSystem)).Append("</td><td>");
                builder.Append(Encode(VolumeMath.FreeLabel(volume.SizeBytes, volume.FreeBytes)));
                builder.Append("<div class=\"bar\"><span style=\"width:").Append(width.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("%\"></span></div>");
                builder.AppendLine("</td></tr>");
            }
            builder.AppendLine("</tbody></table>");
        }
        builder.AppendLine("</section>");

        Table(builder, "GPUs", ["Name", "Memory", "Driver version", "Driver date", "Video mode"],
            hardware.Gpus.Select(g => new[] { g.Name, ByteSizeFormatter.Format(g.AdapterRamBytes), g.DriverVersion, DisplayFormatter.Date(g.DriverDate), g.VideoMode }));
        Table(builder, "Monitors", ["Name", "Manufacturer", "Product code", "Serial"],
            hardware.Monitors.Select(m => new[] { m.Name, m.Manufacturer, m.ProductCode, m.Serial }));
        Table(builder, "Network adapters (hardware)", ["Adapter", "MAC address", "IP address", "Gateway", "DHCP", "Link speed"],
            hardware.Adapters.Select(a => new[] { a.Name, a.Mac, a.IpAddresses, a.Gateway, a.Dhcp, a.LinkSpeed }));

        builder.AppendLine("<section><h2>Battery</h2><dl>");
        if (!hardware.Battery.Present)
            Field(builder, "Battery", "No battery detected");
        else
        {
            Field(builder, "Name", hardware.Battery.Name);
            Field(builder, "Charge", hardware.Battery.ChargePercent is int charge ? $"{charge}%" : "");
            Field(builder, "Design capacity (mWh)", Num(hardware.Battery.DesignCapacityMwh));
            Field(builder, "Full charge capacity (mWh)", Num(hardware.Battery.FullChargeCapacityMwh));
            Field(builder, "Health", hardware.Battery.HealthPercent is double health ? $"{health.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%" : "");
        }
        builder.AppendLine("</dl></section>");

        Table(builder, "Installed programs", ["Name", "Version", "Publisher", "Install date", "Size", "Source"],
            report.Programs.Select(p => new[] { p.Name, p.Version, p.Publisher, DisplayFormatter.Date(p.InstallDate), ByteSizeFormatter.Format(p.SizeBytes), p.Source }));
        Table(builder, "Hotfixes", ["Hotfix", "Description", "Installed on", "Installed by"],
            report.Hotfixes.Select(h => new[] { h.HotFixId, h.Description, DisplayFormatter.Date(h.InstalledOn), h.InstalledBy }));
        Table(builder, "Services", ["Name", "Display name", "Status", "Start mode", "Log on as"],
            report.Services.Select(s => new[] { s.Name, s.DisplayName, s.Status, s.StartMode, s.Account }));
        Table(builder, "Startup items", ["Name", "Command", "Location", "User", "Enabled"],
            report.StartupItems.Select(s => new[] { s.Name, s.Command, s.Location, s.User, s.Enabled }));

        builder.Append("<p style=\"color:#5c6b7a\">Generated by ").Append(Encode(ProductInfo.Name)).Append(' ').Append(Encode(ProductInfo.Version)).AppendLine(".</p>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static void Field(StringBuilder builder, string label, string? value)
    {
        builder.Append("<dt>").Append(Encode(label)).Append("</dt><dd>").Append(Encode(string.IsNullOrWhiteSpace(value) ? "—" : value)).AppendLine("</dd>");
    }

    private static void Table(StringBuilder builder, string title, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var materialized = rows.ToList();
        builder.Append("<section><h2>").Append(Encode(title)).AppendLine("</h2>");
        if (materialized.Count == 0)
        {
            builder.AppendLine("<p>None returned.</p></section>");
            return;
        }
        builder.Append("<table><thead><tr>");
        foreach (var header in headers)
            builder.Append("<th>").Append(Encode(header)).Append("</th>");
        builder.AppendLine("</tr></thead><tbody>");
        foreach (var row in materialized)
        {
            builder.Append("<tr>");
            foreach (var cell in row)
                builder.Append("<td>").Append(Encode(cell)).Append("</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table></section>");
    }

    private static string Num(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
