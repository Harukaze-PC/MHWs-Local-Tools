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
        Font = DeskTheme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(630, 475);
        ClientSize = new Size(1010, 620);
        BackColor = DeskTheme.Canvas;
        ForeColor = DeskTheme.Ink;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 12, 16, 12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        search.Name = "previewSearch"; count.Name = "previewCount";
        layout.Controls.Add(ListHeader.Create("护石清单", inventory.DisplayName + " · " + modeLabel,
            "搜索技能或护石名", search, count), 0, 0);

        grid.Name = "previewGrid"; grid.AccessibleName = "护石清单"; grid.Dock = DockStyle.Fill;
        grid.VirtualMode = true; grid.ReadOnly = true; grid.MultiSelect = false;
        grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Margin = Padding.Empty; grid.Font = Font;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        DeskTheme.StyleGrid(grid);
        if (scored) {
            AddColumn("score", "护石评分", 92);
            grid.Columns["score"].MinimumWidth = 84;
            grid.Columns["score"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns["score"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns["score"].DefaultCellStyle.ForeColor = DeskTheme.Forest;
        }
        AddColumn("name", "护石名称", 104);
        AddColumn("skills", "技能", 230);
        AddColumn("slots", "装饰品孔位", 145);
        if (recommended) AddColumn("match", allWeapons ? "适配武器" : "推荐流派", 122);
        AddColumn("state", "状态", 90);
        grid.CellValueNeeded += CellValue;
        grid.CurrentCellChanged += (s, e) => UpdateDetails();
        layout.Controls.Add(grid, 0, 1);

        selectedTitle.AutoSize = true; selectedTitle.Dock = DockStyle.Fill;
        selectedTitle.Font = DeskTheme.Heading; selectedTitle.ForeColor = DeskTheme.Forest;
        selectedTitle.BackColor = DeskTheme.Surface; selectedTitle.UseMnemonic = false;
        selectedTitle.Padding = new Padding(12, 7, 12, 3); selectedTitle.Margin = new Padding(0, 10, 0, 0);
        layout.Controls.Add(selectedTitle, 0, 2);
        details.Name = "previewDetails"; details.AccessibleName = "选中护石详情";
        details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true;
        details.BorderStyle = BorderStyle.None; details.BackColor = DeskTheme.Surface;
        details.ForeColor = DeskTheme.Ink; details.ScrollBars = ScrollBars.Vertical;
        var detailBody = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = DeskTheme.Surface, Padding = new Padding(12, 1, 8, 8) };
        detailBody.Controls.Add(details); layout.Controls.Add(detailBody, 0, 3);
        var close = new Button { Text = "关闭", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel };
        DeskTheme.StyleButton(close); close.Margin = new Padding(0, 8, 0, 0);
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
        if (query.Length == 0) visibleRows = source;
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
        selectedTitle.Text = charm.name + "　" +
            (charm.kind == "appraised" ? "鉴定护石" : "制作护石") +
            (charm.rarity.HasValue ? " · 稀有度 " + charm.rarity.Value : "");
        var lines = new List<string> { "技能　" + Skills(charm) + "　｜　孔位　" + Slots(charm) };
        if (acrossWeapons && !String.IsNullOrEmpty(row.Weapons)) lines.Add("适配武器　" + row.Weapons);
        if (recommendation && !String.IsNullOrEmpty(row.BuildNames)) lines.Add("推荐流派　" + row.BuildNames);
        if (!String.IsNullOrEmpty(row.Detail)) lines.Add("得分明细　" + row.Detail);
        lines.AddRange(charm.warnings);
        details.Text = String.Join(Environment.NewLine, lines);
    }
}
