using SysScope.Core;

namespace SysScope;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About SysScope";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Body();
        BackColor = Color.White;
        Padding = new Padding(20);
        MinimumSize = new Size(440, 320);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(Label(ProductInfo.Name, Theme.Title(), Theme.Navy));
        layout.Controls.Add(Label("Version " + ProductInfo.Version, Theme.Emphasis(), Theme.TealDark));
        layout.Controls.Add(Label(ProductInfo.Tagline, Theme.Body(), Theme.Ink));
        layout.Controls.Add(Label(
            "SysScope reads hardware, software, services, and startup items from this PC or from remote computers over WMI. DCOM is tried first, then WinRM.",
            Theme.Body(),
            Theme.Ink));
        layout.Controls.Add(Label(
            "It sits alongside " + ProductInfo.Family + ".",
            Theme.Body(),
            Theme.Ink));
        layout.Controls.Add(Label(
            "No install is required. This build is a self-contained Windows x64 application. Services and startup items are read-only.",
            Theme.Body(),
            Theme.Muted));

        var close = Theme.Primary("Close");
        close.DialogResult = DialogResult.OK;
        close.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        Controls.Add(layout);
    }

    private static Label Label(string text, Font font, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(460, 0),
        Font = font,
        ForeColor = color,
        Margin = new Padding(0, 0, 0, 8)
    };
}
