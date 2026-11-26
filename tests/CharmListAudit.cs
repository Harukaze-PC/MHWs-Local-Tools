using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class CharmListAudit
{
    static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly string[] CharmHeaders = {
        "护石名称", "类型", "稀有度", "技能 1", "技能 2", "技能 3",
        "装饰品孔位 1", "装饰品孔位 2", "装饰品孔位 3", "已收藏", "已装备"
    };

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    static Charm Sample(string name, string skill, int level)
    {
        return new Charm { name = name, kind = "appraised", rarity = 8,
            skills = new List<Skill> { new Skill { name = skill, level = level } },
            slots = new List<Socket>(), warnings = new List<string>() };
    }

    static IEnumerable<Control> ControlsIn(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var control in ControlsIn(child)) yield return control;
        }
    }

    static DataGridView Grid(CharmPreview preview)
    {
        return (DataGridView)preview.Controls.Find("previewGrid", true).Single();
    }

    static void CheckPreview(Inventory inventory, InventoryRow[] rows, InventoryRow[] recommended)
    {
        using (var preview = new CharmPreview(inventory, "不评分", false, false, false, rows))
        {
            var grid = Grid(preview);
            var search = (TextBox)preview.Controls.Find("previewSearch", true).Single();
            var details = (TextBox)preview.Controls.Find("previewDetails", true).Single();
            var state = grid.Columns["state"].Index;
            Check(grid.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText)
                .SequenceEqual(new[] { "护石名称", "技能", "装饰品孔位", "状态" }), "Unexpected preview columns");
            Check(!ControlsIn(preview).Any(control => control.Text.Contains("箱位") ||
                (control.AccessibleName ?? "").Contains("箱位")), "Box-position text remains in preview controls");
            Check(grid.RowCount == rows.Length, "Blank search should show all charms");
            search.Text = "  耳塞  ";
            Check(grid.RowCount == 1, "Skill search should find exactly one charm");
            Check("Alpha".Equals(grid.Rows[0].Cells["name"].Value), "Skill search showed a different charm");
            Check("已收藏、已装备".Equals(grid.Rows[0].Cells[state].Value), "Preview lost charm state");
            Check(ControlsIn(preview).OfType<Label>().Any(label => label.Text.StartsWith("Alpha　")), "Preview lost charm title");
            Check(details.Text.Contains("耳塞 Lv1") && details.Text.Contains("防具孔 Lv2"), "Preview lost skill or socket details");
            search.Text = "zULu";
            Check(grid.RowCount == 2, "Name search must remain case-insensitive");
            search.Text = "3";
            Check(grid.RowCount == 2, "A numeric query should match text in charm names, not stored positions");
            search.Text = "2";
            Check(grid.RowCount == 0, "A number absent from searchable text should not match a charm");
            Check(details.Text.Length == 0, "Details should clear when the search has no results");
            search.Clear();
            Check(grid.RowCount == rows.Length, "Clearing search should restore the full list");
        }

        using (var preview = new CharmPreview(inventory, Recommendations.Mode, true, true, true, recommended))
        {
            var grid = Grid(preview);
            var search = (TextBox)preview.Controls.Find("previewSearch", true).Single();
            Check(grid.Columns["score"] != null && grid.Columns["match"] != null, "Recommended preview lost optional columns");
            search.Text = "Audit style";
            Check(grid.RowCount == 2, "Recommended-playstyle search should still work");
            search.Text = "弓";
            Check(grid.RowCount == 2, "Recommended weapon search should still work");
        }
    }

    static void CheckWorksheet(string path, string[] optionalHeaders, string[] optionalValues, int alphaRow)
    {
        XElement sheet;
        using (var stream = File.OpenRead(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var entry = archive.GetEntry("xl/worksheets/sheet1.xml").Open())
            sheet = XElement.Load(entry);

        var rows = sheet.Element(Sheet + "sheetData").Elements(Sheet + "row").ToArray();
        var headers = optionalHeaders.Concat(CharmHeaders).ToArray();
        Check(rows.Length == 5, "Unexpected row count in " + path);
        Check(sheet.Element(Sheet + "cols").Elements(Sheet + "col").Count() == headers.Length, "Unexpected column widths in " + path);
        Check(rows[0].Elements(Sheet + "c").Select(cell => cell.Value).SequenceEqual(headers), "Unexpected headers in " + path);
        string range = "A1:" + (char)('A' + headers.Length - 1) + rows.Length;
        Check((string)sheet.Element(Sheet + "dimension").Attribute("ref") == range, "Unexpected dimension in " + path);
        Check((string)sheet.Element(Sheet + "autoFilter").Attribute("ref") == range, "Unexpected filter range in " + path);

        var alpha = rows[alphaRow - 1].Elements(Sheet + "c").ToDictionary(cell => (string)cell.Attribute("r"), cell => cell.Value);
        var values = optionalValues.Concat(new[] {
            "Alpha", "鉴定护石", "8", "攻击 Lv2", "耳塞 Lv1", null,
            "武器孔 Lv1", "防具孔 Lv2", null, "是", "是"
        }).ToArray();
        Check(alpha.Count == values.Count(value => value != null), "Unexpected populated cell count in " + path);
        for (int i = 0; i < values.Length; i++)
        {
            string address = (char)('A' + i) + alphaRow.ToString();
            string actual;
            alpha.TryGetValue(address, out actual);
            Check(actual == values[i], "Unexpected value in " + address + " of " + path + ": " + actual);
        }

        int nameColumn = optionalHeaders.Length;
        string nameCell = ((char)('A' + nameColumn)).ToString();
        var warningRow = rows.Skip(1).Single(row => row.Elements(Sheet + "c").Any(cell =>
            (string)cell.Attribute("r") == nameCell + (string)row.Attribute("r") && cell.Value == "Omega"));
        string warningNumber = (string)warningRow.Attribute("r");
        var warningCells = warningRow.Elements(Sheet + "c").ToDictionary(cell => (string)cell.Attribute("r"), cell => cell.Value);
        Check(warningCells[(char)('G' + nameColumn) + warningNumber] == "待核对", "Missing socket warning in " + path);
        if (optionalHeaders.Length > 0)
            Check(warningCells["A" + warningNumber] == "待核对", "Missing score warning in " + path);
    }

    [STAThread]
    static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "mhws-charm-list-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var alpha = Sample("Alpha", "攻击", 2);
            alpha.skills.Add(new Skill { name = "耳塞", level = 1 });
            alpha.slots.Add(new Socket { type = "weapon", level = 1 });
            alpha.slots.Add(new Socket { type = "armor", level = 2 });
            alpha.favorite = alpha.equipped = true;
            var zuluLow = Sample("Zulu3", "看破", 1);
            var zuluHigh = Sample("Zulu3", "攻击", 3);
            zuluHigh.slots.Add(new Socket { type = "armor", level = 3 });
            var warning = Sample("Omega", "攻击", 1);
            warning.warnings.Add("待核对样例");
            var zulus = new[] { zuluLow, zuluHigh }.OrderByDescending(charm => charm.skills[0].name, StringComparer.CurrentCulture).ToArray();
            var inventory = new Inventory { characterName = "Test", characterSlot = 1,
                talismans = new List<Charm> { zulus[0], warning, zulus[1], alpha } };
            var selected = new HashSet<string> { "攻击" };
            var targets = new[] { new BuildTarget { weapon = "弓", name = "Audit style", basis = "Fixture",
                skills = new Dictionary<string, int> { { "攻击", 3 } } } };
            var raw = InventoryRow.Create(inventory, null, null, false);
            var scored = InventoryRow.Create(inventory, selected, null, false);
            var recommended = InventoryRow.Create(inventory, null, targets, true);
            Check(raw.Select(row => row.Charm).SequenceEqual(new[] { alpha, warning, zulus[1], zulus[0] }), "Unexpected unscored sort order");
            Check(scored.Select(row => row.Charm).SequenceEqual(new[] { zuluHigh, alpha, zuluLow, warning }), "Unexpected scored sort order");
            CheckPreview(inventory, raw, recommended);

            Directory.CreateDirectory(folder);
            string rawPath = Path.Combine(folder, "raw.xlsx"), scoredPath = Path.Combine(folder, "scored.xlsx"),
                onePath = Path.Combine(folder, "one-weapon.xlsx"), allPath = Path.Combine(folder, "all-weapons.xlsx");
            ExcelExport.Save(rawPath, inventory);
            ExcelExport.Save(scoredPath, inventory, "自定义", selected);
            ExcelExport.SaveTargets(onePath, inventory, "弓", targets);
            ExcelExport.SaveTargets(allPath, inventory, BuildFit.AllWeapons, targets);
            CheckWorksheet(rawPath, new string[0], new string[0], 2);
            CheckWorksheet(scoredPath, new[] { "护石评分" }, new[] { "11" }, 3);
            CheckWorksheet(onePath, new[] { "护石评分", "最高分流派" }, new[] { "11", "Audit style" }, 3);
            CheckWorksheet(allPath, new[] { "护石评分", "最高分适配武器", "最高分流派示例" }, new[] { "11", "弓", "弓 · Audit style" }, 3);
            foreach (string path in new[] { rawPath, scoredPath, onePath, allPath }) File.Delete(path);
            Directory.Delete(folder);
            Console.WriteLine("PASS: charm preview, skill/name filters, sort, and all four Excel export layouts (synthetic charms only)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (Directory.Exists(folder)) Console.Error.WriteLine("Audit files: " + folder);
            return 1;
        }
    }
}
