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
        box.DrawMode = DrawMode.OwnerDrawFixed;
        Action sizeRows = () => box.ItemHeight = box.Font.Height + 12;
        sizeRows();
        box.FontChanged += (s,e) => sizeRows();
        box.MaxDropDownItems = 13;
        box.DropDown += (s,e) => {
            if (box.Items.Count == 0) return;
            int textWidth = box.Items.Cast<string>().Max(item => TextRenderer.MeasureText(item, box.Font).Width);
            box.DropDownWidth = Math.Max(box.Width, textWidth + box.ItemHeight + SystemInformation.VerticalScrollBarWidth + 12);
        };
        box.DrawItem += (s,e) => {
            e.DrawBackground();
            if (e.Index < 0) return;
            string label = (string)box.Items[e.Index];
            int left = e.Bounds.Left + 3;
            int index = Array.IndexOf(BuildFit.Weapons, label);
            if (index >= 0) {
                int edge = Math.Max(1, e.Bounds.Height - 5);
                var picture = new Rectangle(left, e.Bounds.Top + 2, edge, edge);
                DrawWeapon(e.Graphics, picture, index);
                left = picture.Right + 6;
            }
            var caption = new Rectangle(left, e.Bounds.Top, Math.Max(0, e.Bounds.Right - left - 2), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, label, e.Font, caption, e.ForeColor,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
    }
}
