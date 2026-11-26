using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class SkillPickerAudit
{
    const string Attack = "攻击", Eye = "看破", Earplugs = "耳塞";
    const uint WmVScroll = 0x115;
    const int ScrollToBottom = 7;

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    static void CheckCatalog()
    {
        var names = CharmFiles.Serializer().Deserialize<Dictionary<string, string>>(CharmFiles.Resource("skills"));
        var prices = CharmFiles.Serializer().Deserialize<Dictionary<string, SkillPrice>>(CharmFiles.Resource("skillPrices"));
        Check(new HashSet<string>(SkillCatalog.AllSkills).SetEquals(names.Values), "The reference should classify every decoded skill");
        Check(new HashSet<string>(SkillCatalog.Skills).SetEquals(Recommendations.SelectableSkills), "The picker must support exactly the scoring skills");
        Check(new HashSet<string>(SkillCatalog.Skills).SetEquals(prices.Keys), "Missing priced skills or extra unpriced skills in picker");
        foreach (string name in SkillCatalog.AllSkills)
            Check(SkillCatalog.Categories.Contains(SkillCatalog.Category(name)), "No known category for " + name);
        Check(SkillCatalog.AllSkills.Length == SkillCatalog.AllSkills.Distinct().Count(), "Duplicate reference skills");
        Check(SkillCatalog.Skills.Length == SkillCatalog.Skills.Distinct().Count(), "Duplicate scoring skills");
        Check(SkillCatalog.Categories.Length == SkillCatalog.Categories.Distinct().Count(), "Duplicate categories");
        Check(!SkillCatalog.Categories.Contains("待分类"), "Some decoded skills still lack a category");
        using (var help = new RatingHelp())
        {
            var grid = Find<DataGridView>(help, "skillPrices");
            Check(grid.RowCount == SkillCatalog.AllSkills.Length, "Reference view is missing catalog entries");
            string referenceOnly = SkillCatalog.AllSkills.Except(SkillCatalog.Skills).First();
            Find<ComboBox>(help, "priceCategory").SelectedItem = SkillCatalog.Category(referenceOnly);
            Find<TextBox>(help, "priceSearch").Text = referenceOnly;
            var row = grid.Rows.Cast<DataGridViewRow>().Single(entry => (string)entry.Cells["skill"].Value == referenceOnly);
            Check((string)row.Cells["category"].Value == SkillCatalog.Category(referenceOnly) &&
                (string)row.Cells["kind"].Value == SkillCatalog.Kind(referenceOnly) &&
                (string)row.Cells["jewel"].Value == "仅供查阅", "Reference-only skill has incorrect category or scoring status");
        }
    }

    static T Find<T>(Control parent, string name) where T : Control
    {
        return (T)parent.Controls.Find(name, true).Single();
    }

    static string[] Items(ListBox list)
    {
        return list.Items.Cast<string>().ToArray();
    }

    static void SelectNames(ListBox list, params string[] names)
    {
        list.ClearSelected();
        foreach (string name in names)
        {
            int index = list.Items.IndexOf(name);
            Check(index >= 0, name + " should be present in " + list.Name);
            list.SetSelected(index, true);
        }
    }

    static void Click(SkillPicker picker, string buttonName)
    {
        var button = Find<Button>(picker, buttonName);
        Check(button.Visible && button.Enabled, "Expected available action " + buttonName);
        button.PerformClick();
    }

    static DialogResult Drive(SkillPicker picker, Action act)
    {
        Exception failure = null;
        picker.ShowInTaskbar = false;
        picker.Shown += (sender, args) => picker.BeginInvoke(new Action(() => {
            try {
                act();
                Check(picker.DialogResult != DialogResult.None, "Final picker action did not close the dialog");
            }
            catch (Exception error) { failure = error; picker.Close(); }
        }));
        DialogResult result = picker.ShowDialog();
        if (failure != null) throw new Exception("Dialog audit failed", failure);
        return result;
    }

    static void CheckGeometry(SkillPicker picker)
    {
        picker.PerformLayout();
        Application.DoEvents();
        var controls = new[] { "availableSkills", "selectedSkills", "availableSearch", "selectedSearch",
            "skillCategory", "addSkills", "removeSkills", "skillChanges", "applySkills", "cancelSkills" };
        foreach (string name in controls)
        {
            var control = Find<Control>(picker, name);
            var bounds = control.Parent.RectangleToScreen(control.Bounds);
            Check(control.Visible && bounds.Width > 0 && bounds.Height > 0, "Empty control " + name);
            for (Control parent = control.Parent; parent != null; parent = parent.Parent)
                Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds), "Clipped control " + name);
        }
    }

    static void CheckFilterAndTransfer()
    {
        var initial = new HashSet<string> { Earplugs };
        using (var picker = new SkillPicker("自定义选择", SkillCatalog.Skills, initial, null))
        {
            var result = Drive(picker, () => {
                CheckGeometry(picker);
                picker.Size = picker.MinimumSize;
                CheckGeometry(picker);
                var available = Find<ListBox>(picker, "availableSkills");
                var selected = Find<ListBox>(picker, "selectedSkills");
                var search = Find<TextBox>(picker, "availableSearch");
                var selectedSearch = Find<TextBox>(picker, "selectedSearch");
                var category = Find<ComboBox>(picker, "skillCategory");
                var changes = Find<TextBox>(picker, "skillChanges");
                Check(Items(selected).SequenceEqual(new[] { Earplugs }), "Unexpected opening selection");
                Check(!Items(available).Contains(Earplugs), "A selected skill appears in the available list");
                Check(Items(available).Length + Items(selected).Length == SkillCatalog.Skills.Length, "Unfiltered skill count mismatch");
                string group = SkillCatalog.Category(Attack);
                category.SelectedItem = group;
                Check(Items(available).Contains(Attack) && Items(available).All(name => SkillCatalog.Category(name) == group),
                    "Category filter includes other categories");
                search.Text = Attack;
                Check(Items(available).Contains(Attack) && Items(available).All(name => name.Contains(Attack)), "Search/category intersection failed");
                SelectNames(available, Attack);
                Check(picker.SelectedSkills.SetEquals(initial), "Highlight alone changes the draft");
                Click(picker, "addSkills");
                Check(changes.Text.Contains(Attack) && picker.SelectedSkills.SetEquals(new[] { Attack, Earplugs }), "Addition not named or not staged");
                category.SelectedIndex = 0;
                search.Clear();
                SelectNames(available, Eye);
                Click(picker, "addSkills");
                selectedSearch.Text = Earplugs;
                Check(Items(selected).SequenceEqual(new[] { Earplugs }), "Selected search does not narrow review");
                SelectNames(selected, Earplugs);
                Click(picker, "removeSkills");
                Check(picker.SelectedSkills.SetEquals(new[] { Attack, Eye }), "Removing a filtered skill changed another selection");
                Check(changes.Text.Contains(Earplugs) && changes.Text.Contains(Attack) && changes.Text.Contains(Eye), "Missing pending skill names");
                Check(initial.SetEquals(new[] { Earplugs }), "Draft changed caller state before apply");
                selectedSearch.Clear();
                Click(picker, "revertSkillChanges");
                Check(picker.SelectedSkills.SetEquals(initial), "Revert did not recover initial selection");
                SelectNames(available, Attack, Eye);
                Click(picker, "addSkills");
                SelectNames(selected, Eye, Earplugs);
                Click(picker, "removeSkills");
                Check(picker.SelectedSkills.SetEquals(new[] { Attack }), "Batch add/remove lost the surviving skill");
                Click(picker, "applySkills");
            });
            Check(result == DialogResult.OK && picker.SelectedSkills.SetEquals(new[] { Attack }), "Apply lost the exact final set");
        }
    }

    static void CheckRestoreAndCancel()
    {
        var initial = new HashSet<string> { Attack, Eye };
        var defaults = new[] { Eye, Earplugs };
        using (var picker = new SkillPicker("弓 · 全部流派", SkillCatalog.Skills, initial, defaults))
        {
            var result = Drive(picker, () => {
                Click(picker, "restoreRecommendation");
                Check(picker.RestoredDefaults && picker.SelectedSkills.SetEquals(defaults), "Wrong default restoration");
                var changes = Find<TextBox>(picker, "skillChanges").Text;
                Check(changes.Contains(Earplugs) && changes.Contains(Attack), "Restored changes are not named");
                Click(picker, "cancelSkills");
            });
            Check(result == DialogResult.Cancel && initial.SetEquals(new[] { Attack, Eye }), "Cancelled edits escaped the dialog");
        }
    }

    static void CheckRemoveAll()
    {
        using (var picker = new SkillPicker("自定义选择", SkillCatalog.Skills, new[] { Earplugs }, null))
        {
            var result = Drive(picker, () => {
                SelectNames(Find<ListBox>(picker, "selectedSkills"), Earplugs);
                Click(picker, "removeSkills");
                Find<TextBox>(picker, "availableSearch").Text = "AuditNonexistentSkill";
                Check(Items(Find<ListBox>(picker, "availableSkills")).Length == 0, "Empty search yielded a skill");
                Check(!Find<Button>(picker, "addSkills").Enabled && !Find<Button>(picker, "removeSkills").Enabled, "Empty lists still enable transfer");
                Click(picker, "applySkills");
            });
            Check(result == DialogResult.OK && picker.SelectedSkills.Count == 0, "Empty custom selection was not applied");
        }
    }

    static void Edit(CharmDesk desk, Action<SkillPicker> act)
    {
        Exception failure = null;
        bool opened = false;
        var button = Find<Button>(desk, "editSkills");
        Check(button.Visible && button.Enabled, "Main window did not enable the skill editor");
        desk.BeginInvoke(new Action(() => {
            var picker = Application.OpenForms.OfType<SkillPicker>().SingleOrDefault();
            try {
                Check(picker != null && picker.Owner == desk, "Editor did not open from main window");
                opened = true;
                act(picker);
                Check(picker.DialogResult != DialogResult.None, "Editor did not finish");
            }
            catch (Exception error) { failure = error; if (picker != null) picker.Close(); }
        }));
        button.PerformClick();
        if (failure != null) throw new Exception("Main editor audit failed", failure);
        Check(opened, "Main window did not open an editor");
    }

    static void CheckMainWindow()
    {
        using (var desk = new CharmDesk(false))
        {
            desk.ShowInTaskbar = false;
            desk.Show();
            var mode = Find<ComboBox>(desk, "profiles");
            var summary = Find<Control>(desk, "selectedSkillsPanel");
            mode.SelectedItem = CharmDesk.CustomRating;
            Edit(desk, picker => {
                SelectNames(Find<ListBox>(picker, "availableSkills"), Earplugs);
                Click(picker, "addSkills");
                Click(picker, "applySkills");
            });
            Check(SummaryText(summary).Contains(Earplugs), "Custom summary did not show applied skill");
            string beforeCancel = SummaryText(summary);
            Edit(desk, picker => {
                SelectNames(Find<ListBox>(picker, "selectedSkills"), Earplugs);
                Click(picker, "removeSkills");
                Click(picker, "cancelSkills");
            });
            Check(SummaryText(summary) == beforeCancel, "Cancelled custom edit altered main-window summary");
            mode.SelectedItem = Recommendations.Mode;
            Find<ComboBox>(desk, "weapons").SelectedItem = "大剑";
            Find<ComboBox>(desk, "styles").SelectedItem = "拔刀游击";
            const string draw = "拔刀术【技】";
            Check(SummaryText(summary).Contains(draw), "Unexpected default style");
            Edit(desk, picker => {
                SelectNames(Find<ListBox>(picker, "selectedSkills"), draw);
                Click(picker, "removeSkills");
                Click(picker, "applySkills");
            });
            Check(!SummaryText(summary).Contains(draw), "Recommended summary kept a removed skill");
            Edit(desk, picker => {
                Click(picker, "restoreRecommendation");
                Click(picker, "applySkills");
            });
            Check(SummaryText(summary).Contains(draw), "Restoring style defaults did not restore the original main-window summary");
            mode.SelectedItem = CharmDesk.CustomRating;
            Check(SummaryText(summary) == beforeCancel, "The custom selection changed while editing a recommendation");
        }
    }

    static string SummaryText(Control summary)
    {
        return summary.Text;
    }

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    static void CheckSummaryGrouping(Control summary, IEnumerable<string> expected)
    {
        string[] names = expected.ToArray();
        string text = SummaryText(summary);
        int position = -1;
        foreach (string category in SkillCatalog.Categories.Where(category => names.Any(name => SkillCatalog.Category(name) == category)))
        {
            int next = text.IndexOf(category, position + 1, StringComparison.Ordinal);
            Check(next > position, "Summary omitted or reordered category " + category);
            position = next;
        }
        foreach (string name in names)
            Check(text.Contains(name), "Summary omitted selected skill " + name);

        var selectable = new HashSet<string>(SkillCatalog.Skills, StringComparer.Ordinal);
        var labels = Descendants(summary).OfType<Label>().ToArray();
        var actual = labels.Where(label =>
            label.AccessibleName != null && selectable.Contains(label.AccessibleName)).ToArray();
        Check(actual.Length == names.Length && new HashSet<string>(actual.Select(label => label.AccessibleName)).SetEquals(names),
            "Visual summary has " + actual.Length + " skill labels for " + names.Length + " selected skills");
        var placed = new List<Rectangle>();
        foreach (var label in actual)
        {
            Check(label.Text == label.AccessibleName, "A skill label does not show its whole name: " + label.AccessibleName);
            var bounds = summary.RectangleToClient(label.Parent.RectangleToScreen(label.Bounds));
            Check(bounds.Width > 0 && bounds.Height > 0 && summary.ClientRectangle.Contains(bounds),
                "A skill label overflows the summary: " + label.Text + " " + bounds);
            Check(!placed.Any(previous => previous.IntersectsWith(bounds)), "Overlapping skill label: " + label.Text);
            placed.Add(bounds);
        }

        var groups = names.ToLookup(SkillCatalog.Category);
        var headings = SkillCatalog.Categories.Where(groups.Contains).Select(category =>
            labels.Single(label => label.AccessibleName == category)).ToArray();
        for (int i = 0; i < headings.Length; i++)
        {
            var heading = headings[i];
            var frame = BoundsIn(summary, heading);
            int end = i + 1 < headings.Length ? BoundsIn(summary, headings[i + 1]).Top : summary.Height;
            var expectedCount = groups[heading.Text].Count();
            var counts = labels.Where(label => {
                var count = BoundsIn(summary, label);
                return label.Text == expectedCount + " 项" && count.Left >= frame.Right && count.Top == frame.Top;
            }).ToArray();
            Check(counts.Length == 1, "Missing skill count for " + heading.Text);
            int namesLeft = BoundsIn(summary, counts[0]).Right;
            foreach (var skill in groups[heading.Text])
            {
                var chip = BoundsIn(summary, actual.Single(label => label.AccessibleName == skill));
                Check(chip.Top >= frame.Top && chip.Bottom <= end && chip.Left >= namesLeft,
                    "Skill appeared in the wrong section: " + skill);
            }
        }
    }

    static Rectangle BoundsIn(Control parent, Control child)
    {
        return parent.RectangleToClient(child.Parent.RectangleToScreen(child.Bounds));
    }

    static void CheckSummaryScrolling(CharmDesk desk, Control summary, string[] skills)
    {
        Check(summary.TabStop && summary.CanSelect && summary.ContextMenuStrip != null && summary.ContextMenuStrip.Items.Cast<ToolStripItem>()
            .Any(item => item.Text.Contains("复制")), "The summary offers no accessible copy command");
        int wideHeight = summary.Height;
        desk.Width = desk.MinimumSize.Width;
        desk.PerformLayout();
        Application.DoEvents();
        Check(summary.Height >= wideHeight, "Narrow width hid part of the summary instead of wrapping it");
        CheckSummaryGrouping(summary, skills);

        var last = Descendants(summary).OfType<Label>().Where(label => skills.Contains(label.AccessibleName))
            .OrderBy(label => label.Parent.RectangleToScreen(label.Bounds).Bottom).Last();
        ScrollableControl viewport = null;
        for (Control container = summary.Parent; container != null && viewport == null; container = container.Parent)
        {
            var scroll = container as ScrollableControl;
            if (scroll != null && scroll.AutoScroll) viewport = scroll;
        }
        Check(viewport != null && viewport.VerticalScroll.Visible, "The long summary is not reachable via the page scrollbar");
        Check(!(summary is ScrollableControl) || !((ScrollableControl)summary).AutoScroll, "Skill summary created a nested scrollbar");
        // Use the same scroll-to-bottom message that the native page scrollbar handles.
        SendMessage(viewport.Handle, WmVScroll, (IntPtr)ScrollToBottom, IntPtr.Zero);
        Application.DoEvents();
        var visible = viewport.RectangleToScreen(viewport.ClientRectangle);
        var tail = last.Parent.RectangleToScreen(last.Bounds);
        Check(visible.Contains(tail), "Cannot scroll to the last skill label: " + last.Text + " " + tail + " in " + visible +
            "; scrollbar position " + viewport.AutoScrollPosition + " with max/large change " +
            viewport.VerticalScroll.Maximum + "/" + viewport.VerticalScroll.LargeChange +
            "; scroll min size " + viewport.AutoScrollMinSize + "; content bounds " + summary.Parent.Bounds +
            ", summary bounds " + summary.Bounds);
        viewport.AutoScrollPosition = Point.Empty;
        Application.DoEvents();
    }

    static void CheckLongSummaryAndEmptyStates()
    {
        using (var desk = new CharmDesk(false))
        {
            desk.ShowInTaskbar = false;
            desk.Show();
            var mode = Find<ComboBox>(desk, "profiles");
            var summary = Find<Control>(desk, "selectedSkillsPanel");
            Check(!summary.Visible, "Unscored mode unexpectedly shows a skill summary");
            mode.SelectedItem = Recommendations.Mode;
            var all = new RecommendedSkillEdits().ForScope(BuildFit.AllWeapons, Recommendations.AllStyles).DefaultSkills;
            Check(all.Length > 40, "The all-weapons summary fixture no longer exercises a long list");
            CheckSummaryGrouping(summary, all);
            CheckSummaryScrolling(desk, summary, all);

            mode.SelectedItem = CharmDesk.CustomRating;
            Check(summary.Visible && SummaryText(summary).Contains("编辑技能"), "Empty custom summary offers no action");
            Edit(desk, picker => {
                SelectNames(Find<ListBox>(picker, "availableSkills"), Attack, Earplugs);
                Click(picker, "addSkills");
                Click(picker, "applySkills");
            });
            CheckSummaryGrouping(summary, new[] { Attack, Earplugs });
            Edit(desk, picker => {
                SelectNames(Find<ListBox>(picker, "selectedSkills"), Attack, Earplugs);
                Click(picker, "removeSkills");
                Click(picker, "applySkills");
            });
            Check(SummaryText(summary).Contains("编辑技能") && !SummaryText(summary).Contains(Earplugs), "Removing every custom skill left a stale summary");

            mode.SelectedItem = Recommendations.Mode;
            Find<ComboBox>(desk, "weapons").SelectedItem = "大剑";
            Find<ComboBox>(desk, "styles").SelectedItem = "拔刀游击";
            Edit(desk, picker => {
                var selected = Find<ListBox>(picker, "selectedSkills");
                SelectNames(selected, Items(selected));
                Click(picker, "removeSkills");
                Click(picker, "applySkills");
            });
            Check(SummaryText(summary).Contains("孔位") && !SummaryText(summary).Contains("拔刀术【技】"), "Empty recommendation should explain that only socket scores apply");
            mode.SelectedItem = CharmDesk.NoRating;
            Check(!summary.Visible, "Unscored mode did not hide the skill summary again");
        }
    }

    [STAThread]
    static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CheckCatalog();
            CheckFilterAndTransfer();
            CheckRestoreAndCancel();
            CheckRemoveAll();
            CheckMainWindow();
            CheckLongSummaryAndEmptyStates();
            Console.WriteLine("PASS: skill catalog, picker geometry, filters, staged transfers, restore/cancel/apply, grouped main-window skills, long summary scrolling and empty states");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
