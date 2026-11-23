using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static class ExcelExport
{
    static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Content = "http://schemas.openxmlformats.org/package/2006/content-types";
    const string OfficeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.";

    internal static void Save(string path, Inventory inventory)
    {
        Save(path, inventory, null, null);
    }

    internal static void Save(string path, Inventory inventory, string profile, HashSet<string> selected)
    {
        SaveCore(path, inventory, profile, selected, null);
    }

    internal static void SaveRecommended(string path, Inventory inventory, string weapon)
    {
        SaveTargets(path, inventory, weapon, Recommendations.ForWeapon(weapon));
    }

    internal static void SaveTargets(string path, Inventory inventory, string weapon, BuildTarget[] targets)
    {
        if (targets == null || targets.Length == 0) throw new ArgumentException("没有找到该武器的评分目标。");
        SaveCore(path, inventory, weapon, null, targets);
    }

    internal static void SaveDecorations(string path, Inventory inventory)
    {
        if (!String.IsNullOrEmpty(inventory.decorationError))
            throw new InvalidDataException(inventory.decorationError);
        if (inventory.decorations == null)
            throw new InvalidDataException("尚未读取该角色的装饰珠清单。");
        SaveWorkbook(path, inventory.exportedAt, "装饰珠清单", DecorationSheet(inventory));
    }

    static void SaveCore(string path, Inventory inventory, string profile, HashSet<string> selected, BuildTarget[] builds)
    {
        string referenceName = null;
        XElement referenceSheet = null;
        if (builds != null) {
            referenceName = "玩法参考";
            referenceSheet = RecommendedSheet(profile, builds);
        } else if (selected != null) {
            referenceName = "评分说明";
            referenceSheet = SettingsSheet(profile, selected);
        }
        string sheetName = inventory.NameForExport(@"\/:?*[]".ToCharArray(), 29) + "护石";
        var mainSheet = Worksheet(inventory, selected, builds, builds != null && profile == BuildFit.AllWeapons);
        SaveWorkbook(path, inventory.exportedAt, sheetName, mainSheet, referenceName, referenceSheet);
    }

    static void SaveWorkbook(string path, string exportedAt, string sheetName, XElement mainSheet,
        string referenceName = null, XElement referenceSheet = null)
    {
        string destination = Path.GetFullPath(path);
        string folder = Path.GetDirectoryName(destination);
        Directory.CreateDirectory(folder);
        string temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteXml(zip, "[Content_Types].xml", new XElement(Content + "Types",
                    new XElement(Content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(Content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    PartType("/xl/workbook.xml", OfficeType + "sheet.main+xml"),
                    PartType("/xl/styles.xml", OfficeType + "styles+xml"),
                    PartType("/xl/worksheets/sheet1.xml", OfficeType + "worksheet+xml"),
                    referenceName != null ? PartType("/xl/worksheets/sheet2.xml", OfficeType + "worksheet+xml") : null,
                    PartType("/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml")));
                WriteXml(zip, "_rels/.rels", new XElement(Relationships + "Relationships",
                    Relationship("rId1", OfficeRel.NamespaceName + "/officeDocument", "xl/workbook.xml"),
                    Relationship("rId2", Relationships.NamespaceName + "/metadata/core-properties", "docProps/core.xml")));
                WriteXml(zip, "xl/workbook.xml", new XElement(Sheet + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", OfficeRel.NamespaceName),
                    new XElement(Sheet + "sheets", SheetEntry(sheetName, 1, "rId1"),
                        referenceName != null ? SheetEntry(referenceName, 2, "rId3") : null)));
                WriteXml(zip, "xl/_rels/workbook.xml.rels", new XElement(Relationships + "Relationships",
                    Relationship("rId1", OfficeRel.NamespaceName + "/worksheet", "worksheets/sheet1.xml"),
                    Relationship("rId2", OfficeRel.NamespaceName + "/styles", "styles.xml"),
                    referenceName != null ? Relationship("rId3", OfficeRel.NamespaceName + "/worksheet", "worksheets/sheet2.xml") : null));
                WriteXml(zip, "docProps/core.xml", CoreProperties(sheetName, exportedAt));
                WriteXml(zip, "xl/styles.xml", XElement.Parse(Styles));
                WriteXml(zip, "xl/worksheets/sheet1.xml", mainSheet);
                if (referenceSheet != null) WriteXml(zip, "xl/worksheets/sheet2.xml", referenceSheet);
            }
            if (File.Exists(destination)) File.Replace(temp, destination, null);
            else File.Move(temp, destination);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    static XElement SheetEntry(string name, int id, string relation)
    {
        return new XElement(Sheet + "sheet", new XAttribute("name", name), new XAttribute("sheetId", id),
            new XAttribute(OfficeRel + "id", relation));
    }

    static XElement DecorationSheet(Inventory inventory)
    {
        var headers = new[] { "装饰珠名称", "类型", "所需孔级", "技能", "空闲数量", "已镶嵌数量", "总数量", "持有状态" };
        var widths = new[] { 30, 12, 12, 62, 13, 15, 13, 15 };
        var rows = new List<XElement>();
        var all = DecorationInventory.IncludingUnowned(inventory.decorations);
        var ordered = all.OrderBy(d => d.totalCount > 0)
            .ThenBy(d => d.kind == "weapon" ? 0 : (d.kind == "armor" ? 1 : 2))
            .ThenBy(d => d.slot).ThenBy(d => d.name, StringComparer.Ordinal).ThenBy(d => d.id);
        foreach (var item in ordered)
        {
            int n = rows.Count + 4;
            string name = String.IsNullOrEmpty(item.name) ? "未识别装饰珠 #" + item.id : item.name;
            var row = Row(n, 25);
            row.Add(TextCell("A" + n, name, 2), TextCell("B" + n, item.TypeLabel, 2),
                item.slot > 0 ? NumberCell("C" + n, item.slot) : null, TextCell("D" + n, item.SkillsLabel, 2),
                NumberCell("E" + n, item.freeCount),
                NumberCell("F" + n, item.mountedCount), NumberCell("G" + n, item.totalCount),
                TextCell("H" + n, item.totalCount == 0 ? "未持有" : "已持有", 2));
            rows.Add(row);
        }
        int catalog = DecorationInventory.CatalogCount;
        int missing = all.Count(d => d.totalCount == 0);
        long free = inventory.decorations.Sum(d => (long)d.freeCount);
        long mounted = inventory.decorations.Sum(d => (long)d.mountedCount);
        string guide = String.Format("图鉴共 {0} 种，已持有 {1} 种，未持有 {2} 种。共 {3} 颗（空闲 {4}，已镶嵌 {5}）。",
            catalog, catalog - missing, missing, free + mounted, free, mounted);
        if (all.Count > catalog)
            guide += String.Format("另有 {0} 种图鉴外装饰珠。", all.Count - catalog);
        return ReferenceSheet(inventory.DisplayName + " · 装饰珠清单", guide, headers, widths, rows);
    }

    static XElement SettingsSheet(string profile, HashSet<string> selected)
    {
        var data = new XElement(Sheet + "sheetData");
        string[,] settings = {
            { "评分偏好", String.IsNullOrWhiteSpace(profile) ? "自定义" : profile },
            { "技能分", Ratings.SkillRule },
            { "无珠技能", Ratings.NoJewelRule },
            { "孔位分", Ratings.SlotRule },
            { "护石评分", "技能分与孔位分相加。含参与评分的未定价技能时，护石评分显示「待核对」。" },
            { "权重参考", "这是一只ZY《护石机制分析&刷取建议》" }
        };
        int n = 0;
        for (int i = 0; i < settings.GetLength(0); i++)
        {
            n++;
            bool heading = i == 0;
            var row = Row(n, i == 0 ? 25 : 45);
            row.Add(TextCell("A" + n, settings[i, 0], heading ? 1 : 2),
                TextCell("B" + n, settings[i, 1], heading ? 1 : 2));
            data.Add(row);
        }
        n++;
        var header = Row(n, 25);
        header.Add(TextCell("A" + n, "参与评分的技能", 1), TextCell("B" + n, "每级分值", 1),
            TextCell("C" + n, "计分依据", 1));
        data.Add(header);
        foreach (string skill in selected.OrderBy(s => s, StringComparer.Ordinal))
        {
            n++;
            var row = Row(n, 21);
            int points = Ratings.PointsPerLevel(skill);
            row.Add(TextCell("A" + n, skill, 2),
                points > 0 ? NumberCell("B" + n, points) : TextCell("B" + n, "未定价", 2),
                TextCell("C" + n, Ratings.Evidence(skill), 2));
            data.Add(row);
        }
        return new XElement(Sheet + "worksheet",
            new XElement(Sheet + "cols", Column(1, 26), Column(2, 78), Column(3, 32)), data);
    }

    static XElement ReferenceSheet(string titleText, string guideText, string[] headers, int[] widths, List<XElement> rows)
    {
        var data = new XElement(Sheet + "sheetData");
        var title = Row(1, 29);
        title.Add(TextCell("A1", titleText, 1));
        data.Add(title);
        var guide = Row(2, 27);
        guide.Add(TextCell("A2", guideText, 2));
        data.Add(guide);
        var columns = new XElement(Sheet + "cols");
        var header = Row(3, 26);
        for (int i = 0; i < headers.Length; i++)
        {
            columns.Add(Column(i + 1, widths[i]));
            header.Add(TextCell((char)('A' + i) + "3", headers[i], 1));
        }
        data.Add(header);
        data.Add(rows);
        char lastCol = (char)('A' + headers.Length - 1);
        string lastCell = lastCol.ToString() + (3 + rows.Count);
        return new XElement(Sheet + "worksheet",
            new XElement(Sheet + "dimension", new XAttribute("ref", "A1:" + lastCell)),
            new XElement(Sheet + "sheetViews", new XElement(Sheet + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(Sheet + "pane", new XAttribute("ySplit", 3), new XAttribute("topLeftCell", "A4"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
            columns, data, new XElement(Sheet + "autoFilter", new XAttribute("ref", "A3:" + lastCell)),
            new XElement(Sheet + "mergeCells", new XAttribute("count", 2),
                new XElement(Sheet + "mergeCell", new XAttribute("ref", "A1:" + lastCol + "1")),
                new XElement(Sheet + "mergeCell", new XAttribute("ref", "A2:" + lastCol + "2"))));
    }

    static XElement ReferenceRow(int n, string[] values, int[] widths, int style)
    {
        int lines = 1;
        for (int i = 0; i < values.Length; i++)
        {
            // Reserve roughly two character widths for a CJK glyph.
            int available = Math.Max(1, (widths[i] - 3) / 2);
            int needed = values[i].Split('\n').Sum(line => Math.Max(1, (line.Length + available - 1) / available));
            lines = Math.Max(lines, needed);
        }
        var row = Row(n, Math.Min(409, lines * 18 + 7));
        for (int i = 0; i < values.Length; i++)
            row.Add(TextCell((char)('A' + i) + n.ToString(), values[i], style));
        return row;
    }

    static XElement RecommendedSheet(string weapon, BuildTarget[] targets)
    {
        var headers = new[] { "武器", "推荐玩法", "玩法说明", "重点技能", "其他有效技能" };
        var widths = new[] { 14, 27, 56, 44, 62 };
        var rows = new List<XElement>();
        foreach (var target in targets)
        {
            var ordered = target.skills.Keys.OrderByDescending(target.DisplayPriority)
                .ThenBy(skill => skill, StringComparer.Ordinal).ToArray();
            string[] values = { target.weapon, target.name, target.basis,
                String.Join("、", ordered.Where(skill => target.DisplayPriority(skill) >= 2)),
                String.Join("、", ordered.Where(skill => target.DisplayPriority(skill) < 2)) };
            rows.Add(ReferenceRow(rows.Count + 4, values, widths, rows.Count % 2 == 0 ? 2 : 3));
        }
        return ReferenceSheet(weapon + " · 玩法参考", "每行是一种玩法。「玩法说明」介绍默认打法，两列技能列出本次评分使用的技能。玩家额外追加的技能列在「其他有效技能」中。",
            headers, widths, rows);
    }

    static XElement Column(int index, int width)
    {
        return new XElement(Sheet + "col", new XAttribute("min", index), new XAttribute("max", index),
            new XAttribute("width", width), new XAttribute("customWidth", 1));
    }

    static XElement PartType(string name, string type)
    {
        return new XElement(Content + "Override", new XAttribute("PartName", name), new XAttribute("ContentType", type));
    }
    static XElement Relationship(string id, string type, string target)
    {
        return new XElement(Relationships + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target));
    }
    static void WriteXml(ZipArchive zip, string entryName, XElement root)
    {
        using (var entry = zip.CreateEntry(entryName).Open())
        using (var writer = XmlWriter.Create(entry, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) }))
            root.Save(writer);
    }
    static XElement CoreProperties(string title, string timestamp)
    {
        XNamespace core = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace terms = "http://purl.org/dc/terms/";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        DateTimeOffset date;
        var root = new XElement(core + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "dcterms", terms.NamespaceName), new XElement(dc + "title", title));
        if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            root.Add(new XElement(terms + "created", new XAttribute(xsi + "type", "dcterms:W3CDTF"), XmlConvert.ToString(date.UtcDateTime, XmlDateTimeSerializationMode.Utc)));
        return root;
    }


    static XElement Worksheet(Inventory inventory, HashSet<string> selected, BuildTarget[] builds, bool allWeapons)
    {
        var headers = new List<string> { "箱位", "护石名称", "类型", "稀有度", "技能 1", "技能 2", "技能 3", "装饰品孔位 1", "装饰品孔位 2", "装饰品孔位 3", "已收藏", "已装备" };
        var widths = new List<int> { 9, 25, 13, 10, 22, 22, 22, 18, 18, 18, 10, 10 };
        bool scored = selected != null || builds != null;
        if (scored)
        {
            if (builds != null)
            {
                headers.Insert(0, allWeapons ? "最高分玩法示例" : "最高分玩法");
                widths.Insert(0, 48);
            }
            if (allWeapons) { headers.Insert(0, "最高分适配武器"); widths.Insert(0, 38); }
            headers.Insert(0, "护石评分");
            widths.Insert(0, 15);
        }
        var columns = new XElement(Sheet + "cols");
        var data = new XElement(Sheet + "sheetData");
        var header = Row(1, 26);
        for (int i = 0; i < headers.Count; i++)
        {
            columns.Add(Column(i + 1, widths[i]));
            header.Add(TextCell((char)('A' + i) + "1", headers[i], 1));
        }
        data.Add(header);
        int n = 1;
        int shift = headers.IndexOf("箱位");
        foreach (var entry in InventoryRow.Create(inventory, selected, builds, allWeapons))
        {
            var charm = entry.Charm;
            n++;
            int lines = 1;
            if (builds != null && entry.BuildNames != null) lines = Math.Max(lines, (entry.BuildNames.Length + 20) / 21);
            if (allWeapons && entry.Weapons != null) lines = Math.Max(lines, (entry.Weapons.Length + 15) / 16);
            var row = Row(n, Math.Min(409, lines * 17 + 6));
            Func<char, string> address = col => ((char)(col + shift)).ToString() + n;
            if (scored) row.Add(entry.Score.HasValue ? NumberCell("A" + n, entry.Score.Value) : TextCell("A" + n, "待核对", 2));
            if (allWeapons) row.Add(TextCell("B" + n, entry.Weapons ?? "待核对", 2));
            if (builds != null) row.Add(TextCell((allWeapons ? "C" : "B") + n, entry.BuildNames ?? "待核对", 2));
            row.Add(NumberCell(address('A'), charm.boxIndex), TextCell(address('B'), charm.name, 2),
                TextCell(address('C'), charm.kind == "appraised" ? "鉴定护石" : "制作护石", 2),
                charm.rarity.HasValue ? NumberCell(address('D'), charm.rarity.Value) : TextCell(address('D'), "未知", 2),
                charm.skills.Select((skill, index) => TextCell(address((char)('E' + index)), skill.name + " Lv" + skill.level, 2)),
                charm.slots.Select((slot, index) => TextCell(address((char)('H' + index)), InventoryRow.SocketName(slot.type) + " Lv" + slot.level, 2)),
                charm.slots.Count == 0 && charm.warnings.Count > 0 ? TextCell(address('H'), "待核对", 2) : null,
                TextCell(address('K'), charm.favorite ? "是" : "否", 2),
                TextCell(address('L'), charm.equipped ? "是" : "否", 2));
            data.Add(row);
        }
        string range = "A1:" + (char)('A' + headers.Count - 1) + n;
        return new XElement(Sheet + "worksheet",
            new XElement(Sheet + "dimension", new XAttribute("ref", range)),
            new XElement(Sheet + "sheetViews", new XElement(Sheet + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(Sheet + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")),
                new XElement(Sheet + "selection", new XAttribute("pane", "bottomLeft")))),
            new XElement(Sheet + "sheetFormatPr", new XAttribute("defaultRowHeight", 18)),
            columns, data, new XElement(Sheet + "autoFilter", new XAttribute("ref", range)));
    }
    static XElement Row(int n, int height)
    {
        return new XElement(Sheet + "row", new XAttribute("r", n), new XAttribute("ht", height), new XAttribute("customHeight", 1));
    }
    static XElement TextCell(string address, string value, int style)
    {
        return new XElement(Sheet + "c", new XAttribute("r", address), new XAttribute("s", style), new XAttribute("t", "inlineStr"),
            new XElement(Sheet + "is", new XElement(Sheet + "t", value)));
    }
    static XElement NumberCell(string address, int value)
    {
        return new XElement(Sheet + "c", new XAttribute("r", address), new XAttribute("s", 2), new XElement(Sheet + "v", value));
    }

    const string Styles = @"<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>
  <fonts count='2'>
    <font><sz val='11'/><name val='Microsoft YaHei'/></font>
    <font><b/><sz val='11'/><color rgb='FFFFFFFF'/><name val='Microsoft YaHei'/></font>
  </fonts>
  <fills count='4'>
    <fill><patternFill patternType='none'/></fill>
    <fill><patternFill patternType='gray125'/></fill>
    <fill><patternFill patternType='solid'><fgColor rgb='FF205C4A'/><bgColor indexed='64'/></patternFill></fill>
    <fill><patternFill patternType='solid'><fgColor rgb='FFEDF5F1'/><bgColor indexed='64'/></patternFill></fill>
  </fills>
  <borders count='1'><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>
  <cellXfs count='4'>
    <xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/>
    <xf numFmtId='0' fontId='1' fillId='2' borderId='0' xfId='0' applyFont='1' applyFill='1' applyAlignment='1'><alignment vertical='center'/></xf>
    <xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0' applyAlignment='1'><alignment vertical='center' wrapText='1'/></xf>
    <xf numFmtId='0' fontId='0' fillId='3' borderId='0' xfId='0' applyFill='1' applyAlignment='1'><alignment vertical='center' wrapText='1'/></xf>
  </cellXfs>
  <cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles>
</styleSheet>";
}
