namespace SysScope;

/// <summary>
/// Flat button painted entirely in managed code so the system theme cannot add a black border.
/// Primary buttons are teal-filled. Secondary buttons are white with a thin teal border.
/// </summary>
internal sealed class FlatButton : Button
{
    private bool _hover;

    public FlatButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public Color HoverColor { get; set; } = Theme.TealDark;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(text.Width + 28, text.Height + 16);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var fill = !Enabled ? Color.FromArgb(241, 245, 249) : _hover || Focused ? HoverColor : BackColor;
        var border = Enabled ? Theme.Teal : Theme.Line;
        var fore = Enabled ? ForeColor : Theme.Muted;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var brush = new SolidBrush(fill))
            e.Graphics.FillRectangle(brush, ClientRectangle);
        using (var pen = new Pen(border, Focused && Enabled ? 2 : 1))
        {
            if (Focused && Enabled)
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            else
                e.Graphics.DrawRectangle(pen, bounds);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(2, 1, Math.Max(0, Width - 4), Math.Max(0, Height - 2)),
            fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }
}
