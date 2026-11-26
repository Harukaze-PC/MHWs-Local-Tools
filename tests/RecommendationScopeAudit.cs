using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class RecommendationScopeAudit
{
    const string Sword = "大剑", Bow = "弓";
    const string DrawStyle = "拔刀游击", ChargeStyle = "蓄力斩", ElementStyle = "属性蓄力与刚射";
    const string DrawSkill = "拔刀术【技】";
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    static T Field<T>(CharmDesk desk, string name) where T : class
    {
        return (T)typeof(CharmDesk).GetField(name, InstanceFields).GetValue(desk);
    }

    static BuildTarget[] Selected(CharmDesk desk)
    {
        return (BuildTarget[])typeof(CharmDesk).GetMethod("SelectedTargets", InstanceFields).Invoke(desk, null);
    }

    static void CheckAllWeaponScope(CharmDesk desk, ComboBox weapon, ComboBox style)
    {
        Check(weapon.Text == BuildFit.AllWeapons && style.Text == Recommendations.AllStyles,
            "All-weapons recommendation must show all styles");
        Check(!style.Enabled && style.Items.Cast<string>().SequenceEqual(new[] { Recommendations.AllStyles }),
            "All weapons must disable the style picker and offer only all styles");
        var targets = Selected(desk);
        Check(targets.Select(target => target.weapon).Distinct().Count() == BuildFit.Weapons.Length &&
            targets.SequenceEqual(Recommendations.ForScope(BuildFit.AllWeapons, Recommendations.AllStyles)),
            "All-weapons recommendation lost one or more targets");
    }

    static Rectangle BoundsOnScreen(Control control)
    {
        return control.Parent.RectangleToScreen(control.Bounds);
    }

    static void SendPickerWheel(ComboBox picker, int delta)
    {
        const int MouseWheel = 0x020a;
        var point = picker.PointToScreen(new Point(picker.Width / 2, picker.Height / 2));
        var coordinates = new IntPtr((point.X & 0xffff) | (point.Y << 16));
        SendMessage(picker.Handle, MouseWheel, new IntPtr(delta << 16), coordinates);
    }

    static void CheckClosedPickerWheel(ComboBox picker)
    {
        Check(!picker.DroppedDown && picker.Items.Count > 1, "Expected a closed picker with multiple choices");
        picker.Focus();
        int before = picker.SelectedIndex, selectionChanges = 0;
        EventHandler countChanges = (sender, args) => selectionChanges++;
        picker.SelectedIndexChanged += countChanges;
        try
        {
            SendPickerWheel(picker, -120);
            SendPickerWheel(picker, 120);
            Check(selectionChanges == 0 && picker.SelectedIndex == before,
                "Mouse wheel changed the selection in the closed picker " + picker.Name);
        }
        finally { picker.SelectedIndexChanged -= countChanges; }
    }

    static void CheckPickerWheelScrolling()
    {
        if (SystemInformation.MouseWheelScrollLines == 0) return;
        const int GetTopIndex = 0x015b;
        using (var form = new Form { ShowInTaskbar = false })
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(0, 700) };
            var row = new Panel { Width = 240, Height = 40 };
            var picker = new DropdownPicker { DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false, DropDownHeight = 100 };
            picker.Items.AddRange(Enumerable.Range(1, 20).Select(i => "Choice " + i).ToArray());
            picker.SelectedIndex = 0;
            form.Controls.Add(panel);
            panel.Controls.Add(row);
            row.Controls.Add(picker);
            form.Show();
            picker.Focus();
            Application.DoEvents();

            SendPickerWheel(picker, -120);
            Check(panel.AutoScrollPosition.Y < 0 && picker.SelectedIndex == 0,
                "Wheel over a closed picker should scroll the containing panel");

            panel.AutoScrollPosition = Point.Empty;
            picker.DroppedDown = true;
            int before = SendMessage(picker.Handle, GetTopIndex, IntPtr.Zero, IntPtr.Zero).ToInt32();
            SendPickerWheel(picker, -120);
            int after = SendMessage(picker.Handle, GetTopIndex, IntPtr.Zero, IntPtr.Zero).ToInt32();
            Check(picker.DroppedDown && after > before && picker.SelectedIndex == 0 &&
                panel.AutoScrollPosition == Point.Empty,
                "Wheel should scroll the open list without scrolling the containing panel");
            picker.DroppedDown = false;
        }
    }

    static void CheckVisibleBounds(Control control)
    {
        Check(control.Visible && control.Width > 0 && control.Height > 0, "Empty picker: " + control.Name);
        Rectangle bounds = BoundsOnScreen(control);
        for (Control container = control.Parent; container != null; container = container.Parent)
        {
            Rectangle client = container.RectangleToScreen(container.ClientRectangle);
            Check(client.Contains(bounds), "Clipped picker: " + control.Name + " " + bounds + " in " + client);
        }
    }

    static void CheckLayout(CharmDesk desk, ComboBox role, ComboBox mode, ComboBox weapon, ComboBox style)
    {
        desk.PerformLayout();
        Application.DoEvents();
        var save = Field<ComboBox>(desk, "saves");
        var browse = Field<Button>(desk, "browse");
        foreach (var picker in new[] { save, role, mode, weapon, style }) CheckVisibleBounds(picker);
        CheckVisibleBounds(browse);
        Rectangle saveBounds = BoundsOnScreen(save), browseBounds = BoundsOnScreen(browse),
            roleBounds = BoundsOnScreen(role), modeBounds = BoundsOnScreen(mode),
            weaponBounds = BoundsOnScreen(weapon), styleBounds = BoundsOnScreen(style);
        Check(saveBounds.Height == roleBounds.Height,
            "Steam save and role picker heights differ: " + saveBounds.Height + "/" + roleBounds.Height);
        Check(saveBounds.Right <= browseBounds.Left &&
            Math.Abs(saveBounds.Top + saveBounds.Bottom - browseBounds.Top - browseBounds.Bottom) <= 2,
            "Save picker and Browse button are not horizontally separated and vertically centered");
        Check(roleBounds.Bottom <= BoundsOnScreen(Field<TabControl>(desk, "toolTabs")).Top, "Tabs overlap the bottom of the role picker");
        Check(modeBounds.Height == weaponBounds.Height && modeBounds.Height == styleBounds.Height,
            "Mode/weapon/style picker heights differ: " + modeBounds.Height + "/" + weaponBounds.Height + "/" + styleBounds.Height);
        Check(modeBounds.Bottom <= weaponBounds.Top && weaponBounds.Top == styleBounds.Top && weaponBounds.Right <= styleBounds.Left,
            "Weapon/style pickers are not arranged left-to-right below the scoring mode");
    }

    static void WaitForHighlight(CharmDesk desk, string expected)
    {
        var label = Field<Label>(desk, "godCharmCount");
        for (int i = 0; i < 200; i++)
        {
            Application.DoEvents();
            if (label.Text == expected) return;
            Thread.Sleep(5);
        }
        Check(false, "Charm tier card did not update to " + expected + ", got " + label.Text);
    }

    static XElement ReadSheet(ZipArchive workbook, int sheet)
    {
        using (Stream stream = workbook.GetEntry("xl/worksheets/sheet" + sheet + ".xml").Open())
            return XElement.Load(stream);
    }

    static string Cell(XElement sheet, string address)
    {
        return sheet.Descendants(Sheet + "c").Single(cell => (string)cell.Attribute("r") == address).Value;
    }

    static void CheckExport(Inventory inventory, BuildTarget[] targets)
    {
        string path = Path.Combine(Path.GetTempPath(), "mhws-recommendation-audit-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            ExcelExport.SaveTargets(path, inventory, Sword, DrawStyle, targets);
            using (Stream stream = File.OpenRead(path))
            using (var workbook = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                XElement results = ReadSheet(workbook, 1), reference = ReadSheet(workbook, 2);
                Check(Cell(results, "A2") == "18" && Cell(results, "B2") == DrawStyle, "Export lost the selected style score/result");
                Check(Cell(reference, "A1").Contains(DrawStyle), "Export does not label the selected style");
                Check(reference.Descendants(Sheet + "row").Count() == 4 && Cell(reference, "A4") == Sword &&
                    Cell(reference, "B4") == DrawStyle, "Reference exports targets outside the selected style");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    static void CheckScopedEdits()
    {
        var edits = new RecommendedSkillEdits();
        var all = edits.ForScope(BuildFit.AllWeapons, Recommendations.AllStyles);
        var original = edits.ForScope(Sword, DrawStyle);
        Check(original.DefaultSkills.Contains(DrawSkill), "Test fixture is not a default draw-style skill");
        edits.Apply(Sword, DrawStyle, original.DefaultSkills.Where(skill => skill != DrawSkill), false);
        var edited = edits.ForScope(Sword, DrawStyle);
        Check(edited.IsEdited && !edited.DefaultSkills.Contains(DrawSkill), "Scoped skill edit did not apply");
        Check(!edits.ForScope(Sword, ChargeStyle).IsEdited && !edits.ForScope(Bow, Recommendations.AllStyles).IsEdited,
            "Scoped skill edit leaked to another build");
        var editedAll = edits.ForScope(BuildFit.AllWeapons, Recommendations.AllStyles);
        Check(editedAll.IsEdited && editedAll.Targets.Length == all.Targets.Length &&
            editedAll.Targets.Count(target => !all.Targets.Contains(target)) == 1,
            "All-weapons view should reflect exactly the edited style target");
        Check(original.DefaultSkills.Contains(DrawSkill), "Editing invalidated a previously captured scoring snapshot");
        edits.Apply(Sword, DrawStyle, original.DefaultSkills, true);
        Check(!edits.ForScope(Sword, DrawStyle).IsEdited && !edits.ForScope(BuildFit.AllWeapons, Recommendations.AllStyles).IsEdited,
            "Restoring style defaults did not remove the override");
    }

    [STAThread]
    static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CheckPickerWheelScrolling();
            Check(Recommendations.Mode == "按武器/流派推荐", "The old recommendation mode caption remains");
            CheckScopedEdits();
            var inventory = new Inventory { characterName = "Test Hunter", characterSlot = 1, talismans = new List<Charm> {
                new Charm { name = "Synthetic draw charm", kind = "appraised", rarity = 8,
                    skills = new List<Skill> { new Skill { name = DrawSkill, level = 3 } },
                    slots = Enumerable.Range(0, 3).Select(i => new Socket { type = "armor", level = 3 }).ToList(),
                    warnings = new List<string>() }
            } };

            using (var desk = new CharmDesk(false))
            {
                var mode = Field<ComboBox>(desk, "profiles");
                var weapon = Field<ComboBox>(desk, "weapons");
                var style = Field<ComboBox>(desk, "styles");
                var role = Field<ComboBox>(desk, "characters");
                Field<ComboBox>(desk, "saves").Items.Add(@"C:\Steam\userdata\123456789\2246340\remote\win64_save\data001Slot.bin");
                role.Items.Add(inventory);
                role.SelectedIndex = 0;
                desk.ShowInTaskbar = false;
                desk.Show();
                mode.SelectedItem = Recommendations.Mode;
                CheckAllWeaponScope(desk, weapon, style);
                CheckLayout(desk, role, mode, weapon, style);
                CheckClosedPickerWheel(weapon);
                desk.Size = desk.MinimumSize;
                CheckLayout(desk, role, mode, weapon, style);

                weapon.SelectedItem = Sword;
                Check(style.Enabled && style.Text == Recommendations.AllStyles &&
                    style.Items.Cast<string>().SequenceEqual(Recommendations.StylesForWeapon(Sword)) &&
                    style.Items.Contains(ChargeStyle) && style.Items.Contains(DrawStyle) &&
                    !style.Items.Contains(ElementStyle), "Selecting a weapon did not enable the correct style options");
                Check(Selected(desk).All(target => target.weapon == Sword), "Selected targets escaped the chosen weapon");
                CheckClosedPickerWheel(style);
                style.SelectedItem = ChargeStyle;
                var narrow = Selected(desk);
                Check(narrow.Length == 1 && narrow[0].Style == ChargeStyle && Score(inventory, narrow) == 9, "Scoring did not narrow to charge style");
                WaitForHighlight(desk, "0 枚");
                style.SelectedItem = DrawStyle;
                var draw = Selected(desk);
                Check(draw.Length == 1 && draw[0].Style == DrawStyle && Score(inventory, draw) == 18, "Scoring did not follow draw style");
                WaitForHighlight(desk, "1 枚");
                CheckExport(inventory, draw);

                weapon.SelectedItem = Bow;
                style.SelectedItem = ElementStyle;
                var elements = Selected(desk);
                Check(elements.Length == 5 && elements.All(target => target.weapon == Bow && target.Style == ElementStyle) &&
                    elements.Select(target => target.name).Distinct().Count() == 5, "Elemental variants should stay in one selectable style");
                weapon.SelectedItem = BuildFit.AllWeapons;
                CheckAllWeaponScope(desk, weapon, style);
                mode.SelectedItem = CharmDesk.NoRating;
                Check(Selected(desk) == null && !weapon.Visible && !style.Visible, "Scope controls should be hidden outside recommendation mode");
                using (Graphics graphics = desk.CreateGraphics())
                    Console.WriteLine("PASS: recommendation cascade, scope scoring/card/export/edits, picker geometry and wheel behavior at {0:0} DPI (synthetic only)", graphics.DpiY);
                desk.Close();
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static int? Score(Inventory inventory, BuildTarget[] targets)
    {
        return InventoryRow.Create(inventory, null, targets, false).Single().Score;
    }
}
