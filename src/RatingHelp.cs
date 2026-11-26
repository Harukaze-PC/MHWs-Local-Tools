using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

internal sealed class RatingHelp : AppForm
{
    internal const string Caption = "技能等效分赋值说明";
    readonly TabControl tabs = new DeskTabs { Name = "ratingHelpTabs", Dock = DockStyle.Fill, AccessibleName = Caption };

    internal RatingHelp()
    {
        Text = Caption;
        Font = DeskTheme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        ClientSize = new Size(720, 500);
        MinimumSize = new Size(400, 385);
        BackColor = DeskTheme.Canvas;
        ForeColor = DeskTheme.Ink;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 2, ColumnCount = 1 };
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
            Name = "closeRatingHelp", Text = "关闭", AutoSize = true, Padding = new Padding(8, 2, 8, 2), Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };
        DeskTheme.StyleButton(close);
        footer.Controls.Add(SourceLink("等效分赋值参考（B站 · 这是一只ZY）", Ratings.WeightSource), 0, 0);
        footer.Controls.Add(close, 1, 0);
        layout.Controls.Add(footer, 0, 1);
        CancelButton = close;
    }

    LinkLabel SourceLink(string title, string url)
    {
        var link = new LinkLabel { Text = title, AutoSize = true, Anchor = AnchorStyles.Left, Font = DeskTheme.Caption,
            LinkColor = DeskTheme.Accent, ActiveLinkColor = DeskTheme.Forest, VisitedLinkColor = DeskTheme.Accent };
        link.LinkClicked += (sender, args) => {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Win32Exception) { MessageBox.Show(this, "请在浏览器中打开 " + url, title); }
        };
        return link;
    }

    void AddRules()
    {
        var page = new TabPage("计分规则") { Name = "计分规则", Padding = new Padding(20, 16, 16, 12), BackColor = DeskTheme.Surface };
        var text = new RichTextBox {
            AccessibleName = "计分规则", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Ink, Font = Font, ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, DetectUrls = false
        };
        page.Controls.Add(text);
        tabs.TabPages.Add(page);
        AppendRule(text, "护石评分 = 技能的等效分 + 孔位的等效分\n", DeskTheme.Title, DeskTheme.Forest);
        AppendRule(text, "只计算当前方案选中的有效技能。\n\n", DeskTheme.Body, DeskTheme.Muted);
        AppendRule(text, "护石技能等效分\n", DeskTheme.Heading, DeskTheme.Forest);
        AppendRule(text, "武器技能每级 3 分\n防具技能每级按装饰珠孔级计 1～3 分\n"
            + "无珠技能中，属性变换、属性吸收、属性异常耐性和饭团按每级 3 分计入。\n\n", DeskTheme.Body, DeskTheme.Ink);
        AppendRule(text, "护石孔位等效分\n", DeskTheme.Heading, DeskTheme.Forest);
        AppendRule(text, "武器 1 级孔计 3 分\n防具孔按孔级分别计 1、2、3 分\n\n", DeskTheme.Body, DeskTheme.Ink);
        AppendRule(text, "所选范围有多个流派或属性变体时，程序分别计算，显示最高分。", DeskTheme.Body, DeskTheme.Muted);
        text.Select(0, 0);
        text.ScrollToCaret();
    }
    static void AppendRule(RichTextBox text, string value, Font font, Color color)
    {
        text.SelectionFont = font;
        text.SelectionColor = color;
        text.AppendText(value);
    }

    void AddPriceTable()
    {
        var page = new TabPage("技能分值") { Name = "技能分值", BackColor = DeskTheme.Surface, Padding = new Padding(10) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 0, 0, 8) };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var category = new DropdownPicker { Name = "priceCategory", AccessibleName = "技能分类",
            DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        category.Items.Add("全部分类");
        category.Items.AddRange(SkillCatalog.Categories);
        category.Font = DeskTheme.Body;
        WeaponThumbnails.ConfigureTextPicker(category);
        foreach (string name in SkillCatalog.Categories)
            category.Width = Math.Max(category.Width, TextRenderer.MeasureText(name, category.Font).Width + SystemInformation.VerticalScrollBarWidth + 24);
        category.SelectedIndex = 0;
        var search = new TextBox { Name = "priceSearch", AccessibleName = "搜索技能", BorderStyle = BorderStyle.FixedSingle,
            BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Ink, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        filters.Controls.Add(new Label { Text = "分类", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = DeskTheme.Muted }, 0, 0);
        filters.Controls.Add(category, 1, 0);
        filters.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left,
            ForeColor = DeskTheme.Muted, Margin = new Padding(10, 0, 3, 0) }, 2, 0);
        filters.Controls.Add(search, 3, 0);
        layout.Controls.Add(filters, 0, 0);
        var grid = new DataGridView {
            Name = "skillPrices", AccessibleName = "技能分值表", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Margin = Padding.Empty, Font = DeskTheme.Body
        };
        DeskTheme.StyleGrid(grid);
        grid.Columns.Add("skill", "技能");
        grid.Columns.Add("category", "分类");
        grid.Columns.Add("kind", "类型");
        grid.Columns.Add("price", "每级分值");
        grid.Columns.Add("jewel", "计分依据");
        grid.Columns["skill"].FillWeight = 26;
        grid.Columns["category"].FillWeight = 20;
        grid.Columns["kind"].FillWeight = 15;
        grid.Columns["price"].FillWeight = 15;
        grid.Columns["jewel"].FillWeight = 24;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Columns["price"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns["price"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns["jewel"].DefaultCellStyle.ForeColor = DeskTheme.Muted;
        var count = new Label { Name = "priceCount", AutoSize = true, Font = DeskTheme.Caption,
            ForeColor = DeskTheme.Muted, Margin = new Padding(0, 6, 0, 0) };
        Action populate = () => {
            string query = search.Text.Trim();
            grid.Rows.Clear();
            foreach (string skill in SkillCatalog.AllSkills)
            {
                string group = SkillCatalog.Category(skill);
                if (category.SelectedIndex > 0 && group != (string)category.SelectedItem) continue;
                if (skill.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                grid.Rows.Add(skill, group, SkillCatalog.Kind(skill), Ratings.PriceLabel(skill),
                    Ratings.PointsPerLevel(skill) > 0 ? Ratings.Evidence(skill) : "仅供查阅");
            }
            count.Text = "显示 " + grid.Rows.Count + " / " + SkillCatalog.AllSkills.Length + " 项技能";
        };
        category.SelectedIndexChanged += (sender, args) => populate();
        search.TextChanged += (sender, args) => populate();
        populate();
        // Preserve full cell contents when the window narrows or a search reduces the result set.
        foreach (DataGridViewColumn column in grid.Columns)
            column.MinimumWidth = column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(count, 0, 2);
        page.Controls.Add(layout);
        tabs.TabPages.Add(page);
    }
}
