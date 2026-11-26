using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class SkillSummary : Panel
{
    sealed class Section
    {
        internal Label Heading, Count;
        internal Label[] Skills;
    }

    static readonly Font categoryFont = new Font(DeskTheme.Body, FontStyle.Bold);
    readonly List<Section> sections = new List<Section>();
    readonly ContextMenuStrip copyMenu = new ContextMenuStrip();
    Label empty;
    int layoutWidth;

    internal SkillSummary()
    {
        Name = "selectedSkillsPanel";
        AccessibleName = "已选技能";
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = DeskTheme.Surface;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 4, 0, 0);
        ContextMenuStrip = copyMenu;
        var copyItem = copyMenu.Items.Add("复制技能名", null, (s,e) => {
            var label = copyMenu.SourceControl as Label;
            if (label != null) CopyText(label.Tag as string ?? label.Text);
        });
        copyMenu.Items.Add("复制全部技能", null, (s,e) => CopyText(Text));
        copyMenu.Opening += (s,e) => {
            var label = copyMenu.SourceControl as Label;
            copyItem.Visible = label != null && label != empty;
            copyItem.Text = label != null && label.Tag is string ? "复制此分类" : "复制技能名";
        };
    }

    void CopyText(string value)
    {
        try { Clipboard.SetText(value); }
        catch (ExternalException) {
            MessageBox.Show(this, "剪贴板正被占用，请稍后再复制。", "复制技能", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    internal void SetSkills(IEnumerable<string> skills, string emptyMessage)
    {
        var lookup = skills.ToLookup(SkillCatalog.Category);
        var categories = SkillCatalog.Categories.Concat(lookup.Select(group => group.Key))
            .Distinct().Where(lookup.Contains).ToArray();
        var lines = categories.Select(name => name + "　" + String.Join("、", lookup[name])).ToArray();
        string summary = lines.Length == 0 ? emptyMessage : String.Join(Environment.NewLine, lines);
        if (Text == summary) return;

        SuspendLayout();
        try {
            sections.Clear();
            empty = null;
            while (Controls.Count != 0) Controls[Controls.Count - 1].Dispose();
            Text = summary;
            for (int i = 0; i < categories.Length; i++) {
                string name = categories[i];
                var heading = MakeLabel(name, categoryFont, DeskTheme.Forest);
                var labels = lookup[name].Select(skill => {
                    var label = MakeLabel(skill, DeskTheme.Body, DeskTheme.Ink);
                    label.BackColor = DeskTheme.Canvas;
                    label.Padding = new Padding(Math.Max(5, Font.Height / 3), 0, Math.Max(5, Font.Height / 3), 0);
                    return label;
                }).ToArray();
                var count = MakeLabel(labels.Length + " 项", DeskTheme.Caption, DeskTheme.Muted);
                heading.Tag = count.Tag = lines[i];
                sections.Add(new Section { Heading = heading, Count = count, Skills = labels });
            }
            if (sections.Count == 0) empty = MakeLabel(emptyMessage, DeskTheme.Body, DeskTheme.Muted);
            ArrangeSections();
        } finally { ResumeLayout(); }
    }

    Label MakeLabel(string text, Font font, Color ink)
    {
        var label = new Label { Text = text, AccessibleName = text, UseMnemonic = false, Font = font,
            ForeColor = ink, TextAlign = ContentAlignment.MiddleLeft, ContextMenuStrip = copyMenu };
        label.Click += (s,e) => Focus();
        Controls.Add(label);
        return label;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (sections != null && ClientSize.Width != layoutWidth) ArrangeSections();
    }

    void ArrangeSections()
    {
        int width = layoutWidth = ClientSize.Width;
        if (width <= 0) return;
        int gap = Math.Max(4, Font.Height / 4), inset = 2 * gap;
        int rowPad = gap + gap / 2;
        int y = 0;
        if (empty != null) {
            empty.Bounds = new Rectangle(inset, inset, Math.Max(1, width - 2 * inset), empty.PreferredHeight);
            y = empty.Bottom + inset;
        }
        if (sections.Count > 0) {
            int headingWidth = sections.Max(section => section.Heading.PreferredWidth);
            int countWidth = sections.Max(section => section.Count.PreferredWidth);
            int countLeft = inset + headingWidth + gap;
            int namesLeft = countLeft + countWidth + 3 * gap;
            int pillHeight = Font.Height + gap + 2;
            foreach (var section in sections) {
                y += rowPad;
                section.Heading.SetBounds(inset, y, headingWidth, pillHeight);
                section.Count.SetBounds(countLeft, y, countWidth, pillHeight);
                int x = namesLeft, line = y;
                foreach (var skill in section.Skills) {
                    int w = skill.PreferredWidth;
                    if (x > namesLeft && x + w > width - inset) { x = namesLeft; line += pillHeight + gap; }
                    skill.SetBounds(x, line, w, pillHeight);
                    x += w + gap + gap / 2;
                }
                y = line + pillHeight + rowPad;
            }
        }
        Height = Math.Max(1, y);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int gap = Math.Max(4, Font.Height / 4);
        using (var line = new Pen(DeskTheme.Tint))
            foreach (var section in sections) {
                int y = section.Heading.Top - gap - gap / 2;
                e.Graphics.DrawLine(line, 0, y, Width - 1, y);
            }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.C) && Text.Length > 0) { CopyText(Text); return true; }
        for (Control parent = Parent; parent != null; parent = parent.Parent) {
            var page = parent as ScrollableControl;
            if (page == null || !page.AutoScroll || !page.VerticalScroll.Visible) continue;
            int top = -page.AutoScrollPosition.Y;
            switch (keyData) {
                case Keys.Up: top -= Font.Height; break;
                case Keys.Down: top += Font.Height; break;
                case Keys.PageUp: top -= page.ClientSize.Height; break;
                case Keys.PageDown: top += page.ClientSize.Height; break;
                case Keys.Home: top = 0; break;
                case Keys.End: top = page.VerticalScroll.Maximum; break;
                default: return base.ProcessCmdKey(ref msg, keyData);
            }
            page.AutoScrollPosition = new Point(0, Math.Max(0, top));
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) copyMenu.Dispose();
        base.Dispose(disposing);
    }
}
