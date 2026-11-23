using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class CharmPreview : AppForm
{
    readonly DataGridView grid = new DataGridView();
    readonly TextBox search = new TextBox(), details = new TextBox();
    readonly Label count = new Label(), selectedTitle = new Label();
    readonly InventoryRow[] source;
    InventoryRow[] visibleRows = new InventoryRow[0];
    readonly bool acrossWeapons, recommendation;

    internal CharmPreview(Inventory inventory, string modeLabel, bool scored, bool recommended, bool allWeapons, InventoryRow[] rows)
    {
        source = rows;
        acrossWeapons = allWeapons;
        recommendation = recommended;
        Text = inventory.DisplayName + " · 护石清单 · " + modeLabel;
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(630, 430);
        ClientSize = new Size(1010, 585);
        BackColor = Color.FromArgb(247, 247, 241);
        ForeColor = Color.FromArgb(34, 51, 45);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 5) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        string searchLabel = "搜索技能、护石名或箱位";
        toolbar.Controls.Add(new Label { Text = searchLabel, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        search.Name = "previewSearch"; search.AccessibleName = searchLabel; search.Dock = DockStyle.Fill;
        toolbar.Controls.Add(search, 1, 0);
        count.Name = "previewCount"; count.AutoSize = true; count.Anchor = AnchorStyles.Right;
        toolbar.Controls.Add(count, 2, 0);
        layout.Controls.Add(toolbar, 0, 0);

        grid.Name = "previewGrid"; grid.AccessibleName = "护石清单"; grid.Dock = DockStyle.Fill;
        grid.VirtualMode = true; grid.ReadOnly = true; grid.MultiSelect = false;
        grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Color.White; grid.GridColor = Color.FromArgb(218, 227, 222);
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 92, 74);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(212, 234, 222);
        grid.DefaultCellStyle.SelectionForeColor = ForeColor;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 246);
        grid.RowTemplate.Height = Font.Height + 9;
        AddColumn("box", "箱位", 58);
        if (scored) AddColumn("score", "护石评分", 104);
        AddColumn("name", "护石名称", 104);
        AddColumn("skills", "技能", 230);
        AddColumn("slots", "装饰品孔位", 145);
        if (recommended) AddColumn("match", allWeapons ? "适配武器" : "推荐玩法", 122);
        AddColumn("state", "状态", 90);
        grid.CellValueNeeded += CellValue;
        grid.CurrentCellChanged += (s, e) => UpdateDetails();
        layout.Controls.Add(grid, 0, 1);

        selectedTitle.AutoSize = true; selectedTitle.Dock = DockStyle.Top;
        selectedTitle.Margin = new Padding(3, 6, 3, 4);
        layout.Controls.Add(selectedTitle, 0, 2);
        details.Name = "previewDetails"; details.AccessibleName = "选中护石详情";
        details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true;
        details.BackColor = Color.White; details.ScrollBars = ScrollBars.Vertical;
        layout.Controls.Add(details, 0, 3);
        var close = new Button { Text = "关闭", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel };
        layout.Controls.Add(close, 0, 4); CancelButton = close;
        search.TextChanged += (s,e) => ApplyFilter();
        ApplyFilter();
    }

    void AddColumn(string key, string title, int weight)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = key, HeaderText = title,
            FillWeight = weight, MinimumWidth = Math.Max(53, weight * 3 / 4), SortMode = DataGridViewColumnSortMode.NotSortable });
    }

    static bool ContainsText(string text, string query)
    {
        return text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void ApplyFilter()
    {
        string query = search.Text.Trim();
        int box;
        if (query.Length == 0) visibleRows = source;
        else if (Int32.TryParse(query, out box)) visibleRows = source.Where(r => r.Charm.boxIndex == box).ToArray();
        else visibleRows = source.Where(r => ContainsText(r.Charm.name, query) ||
            r.Charm.skills.Any(skill => ContainsText(skill.name, query)) ||
            ContainsText(r.Weapons, query) || ContainsText(r.BuildNames, query)).ToArray();
        UpdateGrid(query.Length == 0 ? source.Length + " 枚护石" : visibleRows.Length + " / " + source.Length + " 枚");
    }

    void UpdateGrid(string summary)
    {
        grid.RowCount = visibleRows.Length;
        grid.Invalidate();
        if (visibleRows.Length > 0) grid.CurrentCell = grid.Rows[0].Cells[0];
        count.Text = summary;
        UpdateDetails();
    }

    static string Skills(Charm charm)
    {
        return String.Join("、", charm.skills.Select(s => s.name + " Lv" + s.level));
    }

    static string Slots(Charm charm)
    {
        if (charm.slots.Count == 0) return charm.warnings.Count > 0 ? "待核对" : "无";
        return String.Join("、", charm.slots.Select(s => InventoryRow.SocketName(s.type) + " Lv" + s.level));
    }

    void CellValue(object sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= visibleRows.Length) return;
        var row = visibleRows[e.RowIndex];
        var charm = row.Charm;
        switch (grid.Columns[e.ColumnIndex].Name)
        {
            case "box": e.Value = charm.boxIndex; break;
            case "score": e.Value = row.Score.HasValue ? row.Score.Value.ToString() : "待核对"; break;
            case "name": e.Value = charm.name; break;
            case "skills": e.Value = Skills(charm); break;
            case "slots": e.Value = Slots(charm); break;
            case "match": e.Value = (acrossWeapons ? row.Weapons : row.BuildNames) ?? "待核对"; break;
            case "state": e.Value = charm.favorite && charm.equipped ? "已收藏、已装备" :
                charm.favorite ? "已收藏" : charm.equipped ? "已装备" : ""; break;
        }
    }

    void UpdateDetails()
    {
        var cell = grid.CurrentCell;
        if (cell == null || cell.RowIndex >= visibleRows.Length)
        {
            selectedTitle.Text = source.Length == 0 ? "这个角色还没有护石" : "没有匹配的护石";
            details.Clear();
            return;
        }
        var row = visibleRows[cell.RowIndex];
        var charm = row.Charm;
        selectedTitle.Text = "箱位 " + charm.boxIndex + "　" + charm.name + "　" +
            (charm.kind == "appraised" ? "鉴定护石" : "制作护石") +
            (charm.rarity.HasValue ? " · 稀有度 " + charm.rarity.Value : "");
        var lines = new List<string> { "技能　" + Skills(charm) + "　｜　孔位　" + Slots(charm) };
        if (acrossWeapons && !String.IsNullOrEmpty(row.Weapons)) lines.Add("适配武器　" + row.Weapons);
        if (recommendation && !String.IsNullOrEmpty(row.BuildNames)) lines.Add("推荐玩法　" + row.BuildNames);
        if (!String.IsNullOrEmpty(row.Detail)) lines.Add("得分明细　" + row.Detail);
        lines.AddRange(charm.warnings);
        details.Text = String.Join(Environment.NewLine, lines);
    }
}
