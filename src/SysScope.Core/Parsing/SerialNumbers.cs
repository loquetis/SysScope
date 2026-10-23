namespace SysScope.Core.Parsing;

public static class SerialNumbers
{
    private static readonly string[] Placeholders =
    [
        "To be filled by O.E.M.",
        "To Be Filled By O.E.M.",
        "System Serial Number",
        "Default string",
        "None",
        "Not Specified",
        "Chassis Serial Number",
        "0",
        "123456789"
    ];

    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        var text = value.Trim();
        return Placeholders.Any(item => text.Equals(item, StringComparison.OrdinalIgnoreCase));
    }

    public static string Prefer(string? productSerial, string? biosSerial)
    {
        if (!IsPlaceholder(productSerial))
            return productSerial!.Trim();
        if (!IsPlaceholder(biosSerial))
            return biosSerial!.Trim();
        return "";
    }
}
