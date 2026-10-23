namespace SysScope;

internal static class Theme
{
    public static readonly Color Navy = Color.FromArgb(11, 31, 58);
    public static readonly Color Teal = Color.FromArgb(13, 148, 136);
    public static readonly Color TealDark = Color.FromArgb(15, 118, 110);
    public static readonly Color Page = Color.FromArgb(244, 247, 251);
    public static readonly Color Ink = Color.FromArgb(18, 38, 58);
    public static readonly Color Muted = Color.FromArgb(90, 107, 122);
    public static readonly Color Line = Color.FromArgb(218, 226, 234);
    public static readonly Color HeaderRow = Color.FromArgb(232, 239, 246);
    public static readonly Color DangerBg = Color.FromArgb(254, 226, 226);
    public static readonly Color DangerFg = Color.FromArgb(153, 27, 27);
    public static readonly Color WarnBg = Color.FromArgb(255, 247, 237);
    public static readonly Color WarnFg = Color.FromArgb(154, 52, 18);
    public static readonly Color Select = Color.FromArgb(204, 251, 241);

    private static string? _family;

    public static string Family => _family ??= PickFamily();

    public static Font Body() => new(Family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font Title() => new(Family, 18f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Tagline() => new(Family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font Section() => new(Family, 11f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Emphasis() => new(Family, 9.5f, FontStyle.Bold, GraphicsUnit.Point);

    public static Button Primary(string text) => Paint(text, Teal, Color.White, TealDark);

    public static Button Secondary(string text) => Paint(text, Color.White, TealDark, Color.FromArgb(240, 253, 250));

    private static Button Paint(string text, Color back, Color fore, Color hover)
    {
        return new FlatButton
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 8, 2),
            BackColor = back,
            ForeColor = fore,
            HoverColor = hover,
            Font = Emphasis(),
            Cursor = Cursors.Hand
        };
    }

    private static string PickFamily()
    {
        try
        {
            foreach (var name in new[] { "Segoe UI", "Tahoma", "Liberation Sans", "Arial" })
            {
                if (FontFamily.Families.Any(family => family.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return name;
            }
        }
        catch (Exception)
        {
            // A partial GDI install can fail font enumeration. The generic sans font still renders.
        }

        return FontFamily.GenericSansSerif.Name;
    }
}
