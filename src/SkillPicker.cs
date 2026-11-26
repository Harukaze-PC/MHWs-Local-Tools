using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class SkillPicker : AppForm
{
    readonly HashSet<string> initial;
    readonly string[] ordered, defaults;
    internal readonly HashSet<string> SelectedSkills;
    internal bool RestoredDefaults { get; private set; }

    readonly ComboBox category = new DropdownPicker { Name = "skillCategory", DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox availableSearch = SearchBox("availableSearch", "搜索可添加技能");
    readonly TextBox selectedSearch = SearchBox("selectedSearch", "搜索已选技能");
    readonly ListBox available = SkillList("availableSkills", "可添加技能");
    readonly ListBox selected = SkillList("selectedSkills", "已选技能");
    readonly Label availableTitle = Heading(), selectedTitle = Heading(), reviewTitle = Heading();
    readonly Label availableCount = CountLabel(), selectedCount = CountLabel();
    readonly Label availableEmpty = EmptyLabel(), selectedEmpty = EmptyLabel();
    readonly TextBox changes = new TextBox { Name = "skillChanges", AccessibleName = "本次变更明细", Multiline = true,
        ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
        Font = DeskTheme.Body, BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Muted, Margin = Padding.Empty };
    readonly Button add = ActionButton("addSkills", "添加到已选 →"), remove = ActionButton("removeSkills", "← 移出已选");
    readonly Button apply = ActionButton("applySkills", "应用更改", true), revert = ActionButton("revertSkillChanges", "撤销本次修改");

    internal SkillPicker(string scope, IEnumerable<string> options, IEnumerable<string> initial, IEnumerable<string> defaults)
    {
        this.initial = new HashSet<string>(initial);
        this.defaults = defaults == null ? null : defaults.Distinct().ToArray();
        SelectedSkills = new HashSet<string>(this.initial);
        var all = options.Concat(this.initial).Concat(this.defaults ?? new string[0]).Distinct().ToArray();
        var categories = SkillCatalog.Categories.Where(group => all.Any(skill => SkillCatalog.Category(skill) == group)).ToArray();
        ordered = all.OrderBy(skill => Array.IndexOf(categories, SkillCatalog.Category(skill)))
            .ThenBy(skill => skill, StringComparer.CurrentCulture).ToArray();

        Text = "编辑评分技能 · " + scope;
        AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false; ShowInTaskbar = false;
        Font = DeskTheme.Body; ForeColor = DeskTheme.Ink; BackColor = DeskTheme.Canvas;
        MinimumSize = new Size(700, 495); ClientSize = new Size(880, 570);

        var page = new TableLayoutPanel { ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(64, Font.Height * 4)));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);

        var scopeLabel = Heading();
        scopeLabel.ForeColor = DeskTheme.Forest;
        scopeLabel.Text = "评分范围　" + scope;
        page.Controls.Add(scopeLabel, 0, 0);
        var hint = new Label { AutoSize = true, Dock = DockStyle.Top, ForeColor = DeskTheme.Muted,
            Font = DeskTheme.Caption, Margin = new Padding(3, 3, 3, 10),
            Text = this.defaults == null ? "按 Ctrl 或 Shift 多选后批量移动，双击可移动一项。"
                : "已选区汇总所选流派的有效技能。应用时只调整当前范围。按 Ctrl 或 Shift 多选，双击可移动一项。" };
        page.Controls.Add(hint, 0, 1);

        var panes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        panes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); panes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(panes, 0, 2);
        category.AccessibleName = "筛选可添加技能分类";
        category.Items.Add("全部分类"); category.Items.AddRange(categories);
        category.SelectedIndex = 0;
        category.Font = DeskTheme.Body;
        WeaponThumbnails.ConfigureTextPicker(category);
        category.Width = Math.Max(112, category.Items.Cast<string>().Max(name => TextRenderer.MeasureText(name, category.Font).Width)
            + SystemInformation.VerticalScrollBarWidth + 24);
        category.Anchor = AnchorStyles.Right;
        panes.Controls.Add(SkillPane(false), 0, 0); panes.Controls.Add(SkillPane(true), 1, 0);

        var review = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0),
            BackColor = DeskTheme.Surface, ColumnCount = 2, RowCount = 1, Padding = new Padding(10, 7, 8, 7) };
        review.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        review.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        review.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reviewTitle.Text = "本次变更"; reviewTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        reviewTitle.Margin = new Padding(0, 0, 16, 0);
        review.Controls.Add(reviewTitle, 0, 0); review.Controls.Add(changes, 1, 0);
        page.Controls.Add(review, 0, 3);
        var actions = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        page.Controls.Add(actions, 0, 4);
        if (this.defaults != null) {
            var restore = ActionButton("restoreRecommendation", "恢复推荐");
            restore.Click += (s,e) => {
                SelectedSkills.Clear(); SelectedSkills.UnionWith(this.defaults);
                RestoredDefaults = true; RefreshLists();
            };
            actions.Controls.Add(restore, 0, 0);
        }
        revert.Click += (s,e) => {
            SelectedSkills.Clear(); SelectedSkills.UnionWith(this.initial);
            RestoredDefaults = false; RefreshLists();
        };
        actions.Controls.Add(revert, 1, 0);
        var cancel = ActionButton("cancelSkills", "取消"); cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(cancel, 2, 0); CancelButton = cancel;
        apply.DialogResult = DialogResult.OK; actions.Controls.Add(apply, 3, 0);

        category.SelectedIndexChanged += (s,e) => RefreshSide(false);
        availableSearch.TextChanged += (s,e) => RefreshSide(false);
        selectedSearch.TextChanged += (s,e) => RefreshSide(true);
        available.SelectedIndexChanged += (s,e) => UpdateButtons();
        selected.SelectedIndexChanged += (s,e) => UpdateButtons();
        add.Click += (s,e) => MoveSkills(false, available.SelectedItems.Cast<string>());
        remove.Click += (s,e) => MoveSkills(true, selected.SelectedItems.Cast<string>());
        WireDoubleClick(available, false); WireDoubleClick(selected, true);
        RefreshLists();
    }

    static TextBox SearchBox(string name, string description)
    {
        return new TextBox { Name = name, AccessibleName = description, Anchor = AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle, BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Ink, Margin = Padding.Empty };
    }
    static Label Heading()
    {
        return new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = DeskTheme.Ink, Font = DeskTheme.Heading };
    }
    static Label CountLabel()
    {
        return new Label { AutoSize = true, Anchor = AnchorStyles.Left, Font = DeskTheme.Caption,
            ForeColor = DeskTheme.Forest, Margin = Padding.Empty };
    }
    static Label EmptyLabel()
    {
        return new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Muted, Visible = false };
    }
    static Button ActionButton(string name, string text, bool primary = false)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 6, 0), Padding = new Padding(8, 2, 8, 2) };
        DeskTheme.StyleButton(button, primary);
        return button;
    }
    Control SkillPane(bool chosen)
    {
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = DeskTheme.Surface,
            Margin = chosen ? new Padding(5, 0, 0, 0) : new Padding(0, 0, 5, 0), ColumnCount = 1, RowCount = 4 };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(category.ItemHeight, category.PreferredHeight) + 14));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(8, 0, 4, 0),
            BackColor = chosen ? DeskTheme.Selected : DeskTheme.Tint, RowCount = 1, ColumnCount = chosen ? 2 : 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = chosen ? selectedTitle : availableTitle;
        title.Text = chosen ? "已选技能" : "可添加技能";
        title.Margin = Padding.Empty; title.ForeColor = chosen ? DeskTheme.SelectionText : DeskTheme.Forest;
        header.Controls.Add(title, 0, 0);
        var count = chosen ? selectedCount : availableCount;
        count.ForeColor = title.ForeColor;
        header.Controls.Add(count, 1, 0);
        if (!chosen) {
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(category, 2, 0);
        }
        card.Controls.Add(header, 0, 0);
        card.Controls.Add(SearchRow(chosen ? selectedSearch : availableSearch), 0, 1);
        card.Controls.Add(ListPanel(chosen ? selected : available, chosen ? selectedEmpty : availableEmpty), 0, 2);
        var button = chosen ? remove : add;
        button.Margin = new Padding(10, 6, 10, 8);
        card.Controls.Add(button, 0, 3);
        return card;
    }
    static Control SearchRow(TextBox search)
    {
        var row = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(10, 7, 10, 7) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left,
            ForeColor = DeskTheme.Muted, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        row.Controls.Add(search, 1, 0); return row;
    }
    static Control ListPanel(ListBox list, Label empty)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 1, 0, 1),
            BackColor = DeskTheme.Border };
        panel.Controls.Add(list); panel.Controls.Add(empty); empty.BringToFront(); return panel;
    }
    static ListBox SkillList(string name, string description)
    {
        var list = new ListBox { Name = name, AccessibleName = description, Dock = DockStyle.Fill,
            BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Ink, BorderStyle = BorderStyle.None,
            SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed };
        list.FontChanged += (s,e) => list.ItemHeight = list.Font.Height + Math.Max(9, list.Font.Height / 2);
        list.ItemHeight = list.Font.Height + Math.Max(9, list.Font.Height / 2);
        list.DrawItem += (s,e) => {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            string skill = (string)list.Items[e.Index];
            bool marked = (e.State & DrawItemState.Selected) != 0;
            var background = marked ? DeskTheme.Selected : DeskTheme.Surface;
            using (var fill = new SolidBrush(background)) e.Graphics.FillRectangle(fill, e.Bounds);
            if (marked) using (var line = new SolidBrush(DeskTheme.Accent))
                e.Graphics.FillRectangle(line, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var bounds = e.Bounds; bounds.Inflate(-10, 0);
            string price = Ratings.PriceLabel(skill), group = SkillCatalog.Category(skill);
            int priceWidth = TextRenderer.MeasureText(e.Graphics, price, DeskTheme.Caption).Width;
            var score = new Rectangle(bounds.Right - priceWidth, bounds.Top, priceWidth, bounds.Height);
            int groupWidth = Math.Min(bounds.Width * 38 / 100,
                TextRenderer.MeasureText(e.Graphics, group, DeskTheme.Caption).Width);
            var meta = new Rectangle(score.Left - 10 - groupWidth, bounds.Top, groupWidth, bounds.Height);
            var nameBounds = new Rectangle(bounds.X, bounds.Y, Math.Max(0, meta.Left - 7 - bounds.Left), bounds.Height);
            TextRenderer.DrawText(e.Graphics, skill, e.Font, nameBounds, marked ? DeskTheme.SelectionText : DeskTheme.Ink, flags);
            TextRenderer.DrawText(e.Graphics, group, DeskTheme.Caption, meta, marked ? DeskTheme.SelectionText : DeskTheme.Muted, flags | TextFormatFlags.Right);
            TextRenderer.DrawText(e.Graphics, price, DeskTheme.Caption, score, marked ? DeskTheme.SelectionText : DeskTheme.Forest, flags | TextFormatFlags.Right);
            if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, DeskTheme.Forest, background);
        };
        return list;
    }

    void WireDoubleClick(ListBox list, bool removing)
    {
        list.MouseDoubleClick += (s,e) => {
            if (e.Button != MouseButtons.Left) return;
            int index = list.IndexFromPoint(e.Location);
            if (index >= 0) MoveSkills(removing, new[] { (string)list.Items[index] });
        };
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter) && apply.Enabled) { apply.PerformClick(); return true; }
        bool fromAvailable = available.Focused && (keyData == Keys.Enter || keyData == (Keys.Control | Keys.Right));
        bool fromSelected = selected.Focused && (keyData == Keys.Enter || keyData == Keys.Delete || keyData == (Keys.Control | Keys.Left));
        if (fromAvailable || fromSelected) {
            MoveSkills(fromSelected, (fromSelected ? selected : available).SelectedItems.Cast<string>());
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void MoveSkills(bool removing, IEnumerable<string> names)
    {
        var moving = names.ToArray();
        if (moving.Length == 0) return;
        if (removing) SelectedSkills.ExceptWith(moving); else SelectedSkills.UnionWith(moving);
        RefreshLists();
        var destination = removing ? available : selected;
        destination.ClearSelected();
        int first = -1;
        foreach (string name in moving) {
            int i = destination.Items.IndexOf(name);
            if (i >= 0) { destination.SetSelected(i, true); if (first < 0) first = i; }
        }
        if (first >= 0) destination.TopIndex = first;
    }
    void RefreshLists()
    {
        RefreshSide(false); RefreshSide(true);
        var added = ordered.Where(name => SelectedSkills.Contains(name) && !initial.Contains(name)).ToArray();
        var removed = ordered.Where(name => initial.Contains(name) && !SelectedSkills.Contains(name)).ToArray();
        bool changed = added.Length != 0 || removed.Length != 0 || RestoredDefaults;
        var lines = new List<string>();
        if (RestoredDefaults) lines.Add("应用时按所选范围恢复各流派的默认推荐，再应用后续调整。");
        if (added.Length > 0) lines.Add("新增 " + added.Length + " 项　" + String.Join("、", added));
        if (removed.Length > 0) lines.Add("移除 " + removed.Length + " 项　" + String.Join("、", removed));
        changes.Text = changed ? String.Join(Environment.NewLine, lines) : "本次尚未更改技能。";
        changes.ForeColor = changed ? DeskTheme.Ink : DeskTheme.Muted;
        reviewTitle.ForeColor = changed ? DeskTheme.Forest : DeskTheme.Muted;
        apply.Enabled = revert.Enabled = changed;
    }
    void RefreshSide(bool chosen)
    {
        var list = chosen ? selected : available;
        string query = (chosen ? selectedSearch : availableSearch).Text.Trim();
        var currentGroup = !chosen && category.SelectedIndex > 0 ? (string)category.SelectedItem : null;
        var visible = ordered.Where(skill => SelectedSkills.Contains(skill) == chosen &&
            (currentGroup == null || SkillCatalog.Category(skill) == currentGroup) &&
            (query.Length == 0 || skill.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToArray();
        var marked = new HashSet<string>(list.SelectedItems.Cast<string>());
        string top = list.TopIndex >= 0 && list.TopIndex < list.Items.Count ? (string)list.Items[list.TopIndex] : null;
        list.BeginUpdate();
        try {
            list.Items.Clear(); list.Items.AddRange(visible);
            for (int i = 0; i < visible.Length; i++) if (marked.Contains(visible[i])) list.SetSelected(i, true);
            int topIndex = top == null ? -1 : list.Items.IndexOf(top);
            if (topIndex >= 0) list.TopIndex = topIndex;
        }
        finally { list.EndUpdate(); }
        int total = chosen ? SelectedSkills.Count : ordered.Length - SelectedSkills.Count;
        string count = visible.Length == total ? total.ToString() : visible.Length + "/" + total;
        (chosen ? selectedCount : availableCount).Text = count;
        var empty = chosen ? selectedEmpty : availableEmpty;
        empty.Text = total == 0 ? (chosen ? "尚未选择技能" : "全部技能均已选中") : "没有匹配技能";
        empty.Visible = visible.Length == 0;
        UpdateButtons();
    }
    void UpdateButtons()
    {
        add.Enabled = available.SelectedItems.Count > 0;
        remove.Enabled = selected.SelectedItems.Count > 0;
    }
}
