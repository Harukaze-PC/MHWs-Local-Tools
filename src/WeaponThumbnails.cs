using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class WeaponThumbnails
{
    // The strip has one square icon per weapon in BuildFit.Weapons order.
    static readonly Lazy<Bitmap> icons = new Lazy<Bitmap>(LoadIcons);

    static Bitmap LoadIcons()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("weaponThumbnails"))
        using (var bitmap = new Bitmap(stream))
            return new Bitmap(bitmap);
    }

    static void DrawWeapon(Graphics canvas, Rectangle bounds, int index)
    {
        var sheet = icons.Value;
        int cell = sheet.Height;
        var state = canvas.Save();
        try {
            canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
            canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
            canvas.DrawImage(sheet, bounds, index * cell, 0, cell, cell, GraphicsUnit.Pixel);
        } finally {
            canvas.Restore(state);
        }
    }

    internal static void ConfigureWeaponPicker(ComboBox box)
    {
        ConfigurePicker(box, true);
    }

    internal static void ConfigureTextPicker(ComboBox box)
    {
        ConfigurePicker(box, false);
    }

    static void ConfigurePicker(ComboBox box, bool showIcons)
    {
        box.DrawMode = DrawMode.OwnerDrawFixed;
        Action sizeRows = () => box.ItemHeight = box.Font.Height + (box.Font.Height * 3 + 3) / 4;
        sizeRows();
        box.FontChanged += (s,e) => sizeRows();
        box.MaxDropDownItems = 13;
        box.DropDown += (s,e) => {
            if (box.Items.Count == 0) return;
            int textWidth = box.Items.Cast<object>().Max(item => TextRenderer.MeasureText(box.GetItemText(item), box.Font).Width);
            int iconWidth = showIcons ? box.ItemHeight : 0;
            box.DropDownWidth = Math.Max(box.Width, textWidth + iconWidth + SystemInformation.VerticalScrollBarWidth + 12);
        };
        box.DrawItem += (s,e) => {
            bool selected = box.Enabled && (e.State & DrawItemState.Selected) != 0;
            bool closedField = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool highlight = selected && !closedField;
            Color fill = !box.Enabled ? DeskTheme.Canvas : highlight ? DeskTheme.Selected : DeskTheme.Surface;
            Color ink = !box.Enabled ? DeskTheme.Muted : highlight ? DeskTheme.SelectionText : DeskTheme.Ink;
            using (var background = new SolidBrush(fill)) e.Graphics.FillRectangle(background, e.Bounds);
            if (e.Index < 0) return;
            string label = box.GetItemText(box.Items[e.Index]);
            int left = e.Bounds.Left + 5;
            int index = showIcons ? Array.IndexOf(BuildFit.Weapons, label) : -1;
            if (index >= 0) {
                int edge = Math.Max(1, e.Bounds.Height - 5);
                var picture = new Rectangle(left, e.Bounds.Top + 2, edge, edge);
                DrawWeapon(e.Graphics, picture, index);
                left = picture.Right + 6;
            }
            var caption = new Rectangle(left, e.Bounds.Top, Math.Max(0, e.Bounds.Right - left - 2), e.Bounds.Height);
            var ellipsis = box.Name == "saves" && closedField ? TextFormatFlags.PathEllipsis : TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, label, e.Font, caption, ink,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | ellipsis);
            if ((e.State & (DrawItemState.Focus | DrawItemState.NoFocusRect)) == DrawItemState.Focus)
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, ink, fill);
        };
    }
}
