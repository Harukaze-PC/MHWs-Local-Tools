using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class DeskTheme
{
    static Color Tone(int rgb, Color system)
    {
        return SystemInformation.HighContrast ? system : Color.FromArgb((int)(0xff000000u | (uint)rgb));
    }

    internal static readonly Color Canvas = Tone(0xF4F8F4, SystemColors.Control);
    internal static readonly Color Surface = Tone(0xFFFFFF, SystemColors.Window);
    internal static readonly Color Tint = Tone(0xE9F4EC, SystemColors.Control);
    internal static readonly Color SaveHeader = Tone(0xD5E9DA, SystemColors.Control);
    internal static readonly Color Selected = Tone(0xD8EEDF, SystemColors.Highlight);
    internal static readonly Color Border = Tone(0xBFD2C4, SystemColors.WindowFrame);
    internal static readonly Color TabBorder = Tone(0x779589, SystemColors.WindowFrame);
    internal static readonly Color Ink = Tone(0x263B31, SystemColors.WindowText);
    internal static readonly Color Muted = Tone(0x50695C, SystemColors.ControlText);
    internal static readonly Color Forest = Tone(0x2D5845, SystemColors.ControlText);
    internal static readonly Color Accent = Tone(0x397958, SystemColors.Highlight);
    internal static readonly Color OnAccent = Tone(0xFFFFFF, SystemColors.HighlightText);
    internal static readonly Color Gold = Tone(0x76551D, SystemColors.WindowText);
    internal static readonly Color GoldTint = Tone(0xF8EED8, SystemColors.Window);
    internal static readonly Font Body = new Font("Microsoft YaHei UI", 10f);
    internal static readonly Font Heading = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
    internal static readonly Font Title = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
    internal static readonly Font Caption = new Font("Microsoft YaHei UI", 9f);
    internal static readonly Font Stat = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold);
    internal static readonly Color SelectionText = Tone(0x2D5845, SystemColors.HighlightText);

    internal static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        Action update = () => {
            button.BackColor = !button.Enabled ? Canvas : primary ? Accent : Surface;
            button.ForeColor = !button.Enabled ? Muted : primary ? OnAccent : Forest;
            button.FlatAppearance.BorderColor = primary && button.Enabled ? Accent : Border;
        };
        update();
        button.EnabledChanged += (s, e) => update();
        button.FlatAppearance.MouseOverBackColor = primary ? Tone(0x336F52, SystemColors.Highlight) : Tint;
        button.FlatAppearance.MouseDownBackColor = primary ? Tone(0x2E644B, SystemColors.Highlight) : Tone(0xDFEEE3, SystemColors.Control);
    }

    internal static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Tone(0xE2EBE4, SystemColors.WindowFrame);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Tint;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Forest;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tint;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Forest;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        grid.ColumnHeadersDefaultCellStyle.Font = grid.Font;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.SelectionBackColor = Selected;
        grid.DefaultCellStyle.SelectionForeColor = SelectionText;
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 3, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Tone(0xF7FAF7, SystemColors.Window);
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, grid.Font.Height + 9);
    }
}

internal sealed class DeskTabs : TabControl
{
    static readonly Font selectedFont = new Font(DeskTheme.Body, FontStyle.Bold);
    int hoveredTab = -1;

    internal DeskTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new Point(19, 7);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(DeskTheme.Canvas);
        Rectangle page = DisplayRectangle;
        using (var surface = new SolidBrush(DeskTheme.Surface))
            e.Graphics.FillRectangle(surface, 0, page.Top - 1, Width, Height - page.Top + 1);
        int inset = Math.Max(2, (int)Math.Round(e.Graphics.DpiX / 48f));
        for (int i = 0; i < TabCount; i++) {
            Rectangle tab = GetTabRect(i);
            tab.Inflate(-inset, -inset);
            tab.Height = Math.Min(tab.Height, page.Top - inset - tab.Top);
            if (tab.Width <= 0 || tab.Height <= 0) continue;
            bool active = i == SelectedIndex;
            bool hover = i == hoveredTab && Enabled;
            Color fill = active ? DeskTheme.Selected : hover ? DeskTheme.Tint : DeskTheme.Surface;
            Color text = active ? DeskTheme.SelectionText : hover ? DeskTheme.Forest : DeskTheme.Ink;
            var smoothing = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var outline = ButtonOutline(tab, inset * 5))
            using (var background = new SolidBrush(fill))
            using (var border = new Pen(active || hover ? DeskTheme.Accent : DeskTheme.TabBorder)) {
                e.Graphics.FillPath(background, outline);
                e.Graphics.DrawPath(border, outline);
            }
            e.Graphics.SmoothingMode = smoothing;
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(e.Graphics, TabPages[i].Text, active ? selectedFont : Font, tab, text, flags);
            if (active && Focused && ShowFocusCues) {
                tab.Inflate(-2 * inset, -2 * inset);
                ControlPaint.DrawFocusRectangle(e.Graphics, tab, text, fill);
            }
        }
        base.OnPaint(e);
    }

    static GraphicsPath ButtonOutline(Rectangle bounds, int diameter)
    {
        diameter = Math.Min(diameter, Math.Min(bounds.Width, bounds.Height));
        int right = bounds.Right - diameter, bottom = bounds.Bottom - diameter;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(right, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(right, bottom, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bottom, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    void SetHoveredTab(int index)
    {
        if (hoveredTab == index) return;
        if (hoveredTab >= 0 && hoveredTab < TabCount) Invalidate(GetTabRect(hoveredTab));
        hoveredTab = index;
        if (index >= 0) Invalidate(GetTabRect(index));
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hovered = -1;
        for (int i = 0; i < TabCount; i++)
            if (GetTabRect(i).Contains(e.Location)) { hovered = i; break; }
        SetHoveredTab(hovered);
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHoveredTab(-1); }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
