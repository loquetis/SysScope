using System.Globalization;

namespace SysScope.Core.Formatting;

public static class ByteSizeFormatter
{
    public static string Format(long? bytes)
    {
        if (bytes is null)
            return "";
        if (bytes.Value < 0)
            return "";
        double value = bytes.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        if (index == 0)
            return $"{bytes.Value.ToString(CultureInfo.InvariantCulture)} B";
        return $"{value.ToString("0.0", CultureInfo.InvariantCulture)} {units[index]}";
    }
}

public sealed class ByteQuantity : IComparable<ByteQuantity>, IComparable
{
    public ByteQuantity(long? bytes) => Bytes = bytes;

    public long? Bytes { get; }

    public int CompareTo(ByteQuantity? other) => Nullable.Compare(Bytes, other?.Bytes);

    public int CompareTo(object? obj) => obj is ByteQuantity other ? CompareTo(other) : CompareTo(null);

    public override string ToString() => ByteSizeFormatter.Format(Bytes);
}

public static class UptimeFormatter
{
    public static string Format(TimeSpan? uptime)
    {
        if (uptime is null)
            return "";
        var value = uptime.Value < TimeSpan.Zero ? TimeSpan.Zero : uptime.Value;
        if (value.TotalDays >= 1)
            return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours}h {value.Minutes:00}m";
        return $"{(int)value.TotalMinutes}m";
    }
}

public static class MacAddressFormatter
{
    public static string Format(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
            return "";
        var hex = new string(mac.Where(IsHex).ToArray()).ToUpperInvariant();
        if (hex.Length != 12)
            return mac.Trim().ToUpperInvariant();
        return string.Create(17, hex, static (span, source) =>
        {
            var position = 0;
            for (var i = 0; i < 6; i++)
            {
                if (i > 0)
                    span[position++] = ':';
                span[position++] = source[i * 2];
                span[position++] = source[i * 2 + 1];
            }
        });
    }

    private static bool IsHex(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}

public static class DisplayFormatter
{
    public static string Date(DateTimeOffset? value) =>
        value is null ? "" : value.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string DateTime(DateTimeOffset? value) =>
        value is null ? "" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string Domain(bool partOfDomain, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        return partOfDomain ? $"{name.Trim()} (domain)" : $"{name.Trim()} (workgroup)";
    }

    public static string Memory(long? total, long? used)
    {
        if (total is null)
            return "";
        var totalText = ByteSizeFormatter.Format(total);
        if (used is null)
            return totalText;
        return $"{totalText} total, {ByteSizeFormatter.Format(used)} used";
    }

    public static string Tpm(bool? present, string? version)
    {
        if (present is null)
            return "Not readable";
        if (present == false)
            return "Not present";
        return string.IsNullOrWhiteSpace(version) ? "Present" : $"Present ({version.Trim()})";
    }

    public static string SecureBoot(Models.SecureBootState state) => state switch
    {
        Models.SecureBootState.Enabled => "Enabled",
        Models.SecureBootState.Disabled => "Disabled",
        Models.SecureBootState.Unsupported => "Not supported",
        _ => "Unknown"
    };

    public static string LoggedOnUser(string? user) =>
        string.IsNullOrWhiteSpace(user) ? "None signed in" : user.Trim();

    public static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}

public static class MemoryMath
{
    public static long? UsedBytes(long? totalBytes, long? freeKilobytes)
    {
        if (totalBytes is null || freeKilobytes is null)
            return null;
        var free = freeKilobytes.Value * 1024L;
        if (free < 0 || free > totalBytes.Value)
            return null;
        return totalBytes.Value - free;
    }
}

public static class LinkSpeedFormatter
{
    public static string Format(long? bitsPerSecond)
    {
        if (bitsPerSecond is null or <= 0)
            return "";
        var bps = bitsPerSecond.Value;
        if (bps % 1_000_000_000 == 0)
            return $"{(bps / 1_000_000_000).ToString(CultureInfo.InvariantCulture)} Gbps";
        if (bps % 1_000_000 == 0)
            return $"{(bps / 1_000_000).ToString(CultureInfo.InvariantCulture)} Mbps";
        return $"{bps.ToString(CultureInfo.InvariantCulture)} bps";
    }
}

public static class VolumeMath
{
    public static double? FreeFraction(long? size, long? free)
    {
        if (size is null or <= 0 || free is null || free < 0)
            return null;
        var clamped = Math.Min(free.Value, size.Value);
        return clamped / (double)size.Value;
    }

    public static string FreeLabel(long? size, long? free)
    {
        if (size is null && free is null)
            return "";
        var fraction = FreeFraction(size, free);
        var percent = fraction is null ? "" : $" ({Math.Round(fraction.Value * 100).ToString(CultureInfo.InvariantCulture)}%)";
        if (free is null)
            return ByteSizeFormatter.Format(size) + percent;
        if (size is null)
            return ByteSizeFormatter.Format(free) + " free";
        return $"{ByteSizeFormatter.Format(free)} free of {ByteSizeFormatter.Format(size)}{percent}";
    }

    public static long? TotalFree(IEnumerable<Models.VolumeInfo> volumes)
    {
        long sum = 0;
        var any = false;
        foreach (var volume in volumes)
        {
            if (volume.FreeBytes is long free)
            {
                sum += free;
                any = true;
            }
        }
        return any ? sum : null;
    }
}
