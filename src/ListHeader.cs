using System.Drawing;
using System.Windows.Forms;

internal static class ListHeader
{
    internal static Control Create(string title, string context, string prompt, TextBox search, Label count)
    {
        var header = new TableLayoutPanel {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = new TableLayoutPanel {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = DeskTheme.Tint,
            Padding = new Padding(12, 5, 12, 5), Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label {
            Text = title, Font = DeskTheme.Title, ForeColor = DeskTheme.Forest, AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new Label {
            Text = context, AccessibleName = context, UseMnemonic = false, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight, ForeColor = DeskTheme.Muted,
            Dock = DockStyle.Fill, Margin = new Padding(12, 0, 0, 0)
        }, 1, 0);
        header.Controls.Add(heading, 0, 0);
        if (search == null) return header;

        var filter = new TableLayoutPanel {
            AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, Margin = new Padding(0, 8, 0, 0)
        };
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filter.Controls.Add(new Label {
            Text = prompt, AutoSize = true, ForeColor = DeskTheme.Muted, Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        }, 0, 0);
        search.AccessibleName = prompt; search.Dock = DockStyle.Fill;
        search.Margin = new Padding(0, 2, 0, 2);
        filter.Controls.Add(search, 1, 0);
        count.AutoSize = true; count.Anchor = AnchorStyles.Right; count.ForeColor = DeskTheme.Forest;
        count.Margin = new Padding(10, 0, 0, 0);
        filter.Controls.Add(count, 2, 0);
        header.Controls.Add(filter, 0, 1);
        return header;
    }
}
