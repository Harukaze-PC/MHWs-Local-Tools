using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class SaveLocationGuide : ToolStripDropDown
{
    sealed class GuideRenderer : ToolStripSystemRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(DeskTheme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    readonly Button closeButton = new Button {
        Name = "closeSaveLocationGuide", Text = "关闭", AutoSize = true,
        Anchor = AnchorStyles.Right, Margin = Padding.Empty
    };

    internal SaveLocationGuide()
    {
        Name = "saveLocationGuide";
        AccessibleName = "Steam 存档位置";
        Padding = new Padding(1);
        BackColor = DeskTheme.Surface;
        Renderer = new GuideRenderer();
        var card = new TableLayoutPanel {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Ink,
            Font = DeskTheme.Body, Padding = new Padding(14, 10, 14, 12)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(new Label {
            Text = AccessibleName, Font = DeskTheme.Heading, ForeColor = DeskTheme.Forest, AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 16, 0)
        }, 0, 0);
        DeskTheme.StyleButton(closeButton);
        closeButton.Click += (s,e) => Close();
        heading.Controls.Add(closeButton, 1, 0);
        card.Controls.Add(heading, 0, 0);
        card.Controls.Add(new Label {
            Text = "右键 Steam 快捷方式，选择「打开文件所在的位置」。\r\n在 Steam 安装目录里，按下面的路径找到并选择存档文件。",
            AutoSize = true, Margin = new Padding(0, 10, 0, 8)
        }, 0, 1);
        card.Controls.Add(new Label {
            Text = @"userdata\你的数字账号\2246340\remote\win64_save\data001Slot.bin",
            AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(8, 6, 8, 6),
            BackColor = DeskTheme.Canvas, ForeColor = DeskTheme.Forest
        }, 0, 2);
        Items.Add(new ToolStripControlHost(card) {
            AutoSize = false, Size = card.GetPreferredSize(Size.Empty), Padding = Padding.Empty, Margin = Padding.Empty
        });
    }

    internal void ShowBy(LinkLabel link)
    {
        Show(link, new Point(link.Width, link.Height + Math.Max(4, link.Font.Height / 3)), ToolStripDropDownDirection.BelowLeft);
        closeButton.Focus();
    }
}
