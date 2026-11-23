using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

internal sealed class RatingHelp : AppForm
{
    internal const string Caption = "技能等效分赋值说明";
    readonly TabControl tabs = new TabControl { Name = "ratingHelpTabs", Dock = DockStyle.Fill, AccessibleName = Caption };

    internal RatingHelp()
    {
        Text = Caption;
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 460);
        MinimumSize = new Size(400, 285);
        BackColor = Color.FromArgb(247, 247, 241);
        ForeColor = Color.FromArgb(34, 51, 45);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        layout.Controls.Add(tabs, 0, 0);

        AddRules();
        AddPriceTable();

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(3, 8, 3, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var close = new Button {
            Name = "closeRatingHelp", Text = "关闭", AutoSize = true, Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };
        footer.Controls.Add(SourceLink("等效分赋值参考（B站 · 这是一只ZY）", Ratings.WeightSource), 0, 0);
        footer.Controls.Add(close, 1, 0);
        layout.Controls.Add(footer, 0, 1);
        CancelButton = close;
    }

    LinkLabel SourceLink(string title, string url)
    {
        var link = new LinkLabel { Text = title, AutoSize = true, Anchor = AnchorStyles.Left };
        link.LinkClicked += (sender, args) => {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Win32Exception) { MessageBox.Show(this, "请在浏览器中打开 " + url, title); }
        };
        return link;
    }

    void AddRules()
    {
        var page = new TabPage("计分规则") { Name = "计分规则", Padding = new Padding(15), BackColor = Color.White };
        var text = new RichTextBox {
            AccessibleName = "计分规则", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            BackColor = Color.White, ForeColor = ForeColor, Font = Font, ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, DetectUrls = false
        };
        page.Controls.Add(text);
        tabs.TabPages.Add(page);
        using (var heading = new Font(Font, FontStyle.Bold))
        {
            text.SelectionFont = heading;
            text.AppendText("护石评分 = 技能的等效分 + 孔位的等效分\n");
            text.SelectionFont = Font;
            text.AppendText("⭐只计算当前方案选中的有效技能。\n\n");
            text.SelectionFont = heading;
            text.AppendText("护石技能等效分：\n");
            text.SelectionFont = Font;
            text.AppendText("武器技能每级 3 分\n防具技能每级按装饰珠孔级计 1～3 分\n");
            text.AppendText("没有装饰珠的技能每级 3 分（例如：属性变换、属性吸收）\n\n");
            text.SelectionFont = heading;
            text.AppendText("护石孔位等效分：\n");
            text.SelectionFont = Font;
            text.AppendText("武器 1 级孔计 3 分\n防具孔按孔级分别计 1、2、3 分\n\n");
            text.AppendText("有多个推荐玩法时，程序按玩法分别计算，显示最高分。");
        }
        text.Select(0, 0);
        text.ScrollToCaret();
    }

    void AddPriceTable()
    {
        var page = new TabPage("技能分值") { Name = "技能分值", BackColor = Color.White, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var search = new TextBox { Name = "priceSearch", AccessibleName = "搜索技能", Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "搜索技能", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(search, 1, 0);
        var grid = new DataGridView {
            Name = "skillPrices", AccessibleName = "技能分值表", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White
        };
        grid.Columns.Add("skill", "技能");
        grid.Columns.Add("price", "每级分值");
        grid.Columns.Add("jewel", "计分依据");
        grid.Columns["skill"].FillWeight = 37;
        grid.Columns["price"].FillWeight = 23;
        grid.Columns["jewel"].FillWeight = 40;
        Action populate = () => {
            string query = search.Text.Trim();
            grid.Rows.Clear();
            foreach (string skill in Ratings.Skills)
                if (skill.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    grid.Rows.Add(skill, Ratings.PriceLabel(skill), Ratings.Evidence(skill));
        };
        search.TextChanged += (sender, args) => populate();
        populate();
        layout.Controls.Add(grid, 0, 1);
        layout.SetColumnSpan(grid, 2);
        page.Controls.Add(layout);
        tabs.TabPages.Add(page);
    }
}
