using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle(CharmDesk.AppName)]
[assembly: AssemblyProduct(CharmDesk.AppName)]

internal sealed class Skill { public string name { get; set; } public int level { get; set; } }
internal sealed class Socket { public string type { get; set; } public int level { get; set; } }
internal sealed class Crafted { public string name { get; set; } public int rarity { get; set; } public Skill[] skills { get; set; } }

internal class AppForm : Form
{
    const int WmSetIcon = 0x80, ImageIcon = 1, LoadShared = 0x8000;
    // The C# compiler stores /win32icon as the executable's application icon group (32512).
    static readonly IntPtr IconGroup = new IntPtr(32512);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadImage(IntPtr module, IntPtr name, uint type, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr which, IntPtr icon);

    protected override void CreateHandle()
    {
        base.CreateHandle();
        SetWindowIcon(0, SystemInformation.SmallIconSize);
        SetWindowIcon(1, SystemInformation.IconSize);
    }

    void SetWindowIcon(int which, Size size)
    {
        var module = Marshal.GetHINSTANCE(typeof(AppForm).Module);
        var icon = LoadImage(module, IconGroup, ImageIcon, size.Width, size.Height, LoadShared);
        if (icon != IntPtr.Zero) SendMessage(Handle, WmSetIcon, new IntPtr(which), icon);
    }
}

internal sealed class Charm
{
    public string kind { get; set; }
    public string name { get; set; }
    public int? rarity { get; set; }
    public List<Skill> skills { get; set; }
    public List<Socket> slots { get; set; }
    public bool favorite { get; set; }
    public bool equipped { get; set; }
    public List<string> warnings { get; set; }
}
internal sealed class Inventory
{
    public string exportedAt { get; set; }
    public int characterSlot { get; set; }
    public string characterName { get; set; }
    public List<Charm> talismans { get; set; }
    internal List<DecorationRow> decorations;
    internal string decorationError;
    internal bool showSlot;
    internal string DisplayName { get { return String.IsNullOrWhiteSpace(characterName) ? "角色 " + characterSlot : characterName; } }
    public override string ToString()
    {
        return DisplayName + (showSlot ? "（栏位 " + characterSlot + "）" : "");
    }

    internal string NameForExport(char[] invalid, int maxLength)
    {
        string value = (characterName ?? "").Trim();
        if (value.Length > maxLength)
            value = value.Substring(0, Char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength);
        var safe = new string(value.Select(c => Char.IsControl(c) || c == '\ufffe' || c == '\uffff' ||
            Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).TrimStart('\'');
        return String.IsNullOrWhiteSpace(safe) ? "角色" + characterSlot : safe;
    }
    internal string FileLabel
    {
        get {
            string name = NameForExport(Path.GetInvalidFileNameChars(), 64);
            return String.IsNullOrWhiteSpace(characterName) ? name : name + "-栏位" + characterSlot;
        }
    }
}

internal static class CharmFiles
{
    internal static string Resource(string name)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
    }
    internal static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }; }
    internal static string[] FindSaves()
    {
        var roots = new List<string>();
        foreach (var key in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
        {
            try
            {
                var path = Registry.GetValue(key, "SteamPath", null) as string ?? Registry.GetValue(key, "InstallPath", null) as string;
                if (!String.IsNullOrEmpty(path)) roots.Add(path);
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        roots.Add(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Steam"));
        var files = new List<string>();
        foreach (var root in roots)
        {
            var users = Path.Combine(root, "userdata");
            if (!Directory.Exists(users)) continue;
            foreach (var user in Directory.GetDirectories(users))
            {
                var path = Path.GetFullPath(Path.Combine(user, @"2246340\remote\win64_save\data001Slot.bin"));
                if (File.Exists(path)) files.Add(path);
            }
        }
        return files.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
    }
    internal static ulong Owner(string path)
    {
        var folder = new FileInfo(path).Directory;
        while (folder != null)
        {
            if (folder.Name == "2246340" && folder.Parent != null)
            {
                uint account;
                if (UInt32.TryParse(folder.Parent.Name, out account)) return 76561197960265728UL + account;
            }
            folder = folder.Parent;
        }
        throw new InvalidDataException("请选择 Steam 的 userdata 目录内属于本机账号的 data001Slot.bin。");
    }
    internal static List<Inventory> Read(string path)
    {
        if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("存档超出支持的文件大小。");
        var bytes = File.ReadAllBytes(path);
        var source = OfflineReader.Read(bytes, Owner(path));
        var serializer = Serializer();
        var skillNames = serializer.Deserialize<Dictionary<string,string>>(Resource("skills"));
        var crafted = serializer.Deserialize<Dictionary<string,Crafted>>(Resource("crafted"));
        var names = new[] { "不明护石", "史传护石", "秘史护石", "盛世护石" };
        var time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz",CultureInfo.InvariantCulture);
        var result = new List<Inventory>();
        foreach (var character in source)
        {
            var inv = new Inventory { characterSlot = character.Slot, characterName = character.Name, exportedAt = time, talismans = new List<Charm>() };
            inv.decorationError = character.DecorationError;
            if (String.IsNullOrEmpty(inv.decorationError))
                inv.decorations = DecorationInventory.ForCharacter(character);
            foreach (var item in character.Talismans)
            {
                var charm = new Charm { favorite = item.Favorite, equipped = item.Equipped,
                    skills = new List<Skill>(), slots = new List<Socket>(), warnings = new List<string>() };
                if (item.FreeVal0 >= 189 && item.FreeVal0 <= 192)
                {
                    int rank = item.FreeVal0 - 189;
                    charm.kind = "appraised"; charm.rarity = 5 + rank; charm.name = names[rank];
                    var values = item.Packed;
                    if (values == null || values.Length < 4) charm.warnings.Add("此护石缺少技能与孔位数据");
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int value = values[i];
                            if (value == 0) continue;
                            string name;
                            if (value < 1000) { charm.warnings.Add("未识别的技能字段 " + value); continue; }
                            int id = value % 1000;
                            if (!skillNames.TryGetValue(id.ToString(), out name)) { name = "技能 #" + id; charm.warnings.Add("该技能的中文名称待核对"); }
                            charm.skills.Add(new Skill { name = name, level = value / 1000 });
                        }
                        int packed = values[3];
                        if (packed < 0 || packed > 9999) charm.warnings.Add("未识别的孔位字段 " + packed);
                        else
                        {
                            int[] levels = { packed / 1000, packed / 100 % 10, packed / 10 % 10 };
                            for (int i = 0; i < levels.Length; i++)
                                if (levels[i] > 0) charm.slots.Add(new Socket { level = levels[i], type = i == 0 && packed % 10 != 0 ? "weapon" : "armor" });
                        }
                    }
                }
                else
                {
                    charm.kind = "crafted";
                    Crafted known;
                    if (crafted.TryGetValue(item.FreeVal0 + "/" + item.FreeVal1, out known))
                    { charm.name = known.name; charm.rarity = known.rarity; charm.skills.AddRange(known.skills); }
                    else
                    { charm.name = "制作护石 #" + item.FreeVal0 + "（阶段 " + item.FreeVal1 + "）"; charm.warnings.Add("名称、技能及稀有度待核对"); }
                }
                inv.talismans.Add(charm);
            }
            result.Add(inv);
        }
        foreach (var group in result.GroupBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            foreach (var inv in group) inv.showSlot = !String.IsNullOrWhiteSpace(inv.characterName);
        return result;
    }

}

internal sealed class MissingDecorations : AppForm
{
    internal MissingDecorations(Inventory inventory)
    {
        if (!String.IsNullOrEmpty(inventory.decorationError))
            throw new InvalidDataException(inventory.decorationError);
        if (inventory.decorations == null)
            throw new InvalidDataException("尚未读取该角色的装饰珠清单。");

        var missing = DecorationInventory.IncludingUnowned(inventory.decorations).Where(d => d.totalCount == 0)
            .OrderBy(d => d.kind == "weapon" ? 0 : 1).ThenBy(d => d.slot)
            .ThenBy(d => d.name, StringComparer.Ordinal).ThenBy(d => d.id).ToArray();
        string character = inventory.ToString();
        Text = character + " · 未持有装饰珠";
        Font = DeskTheme.Body; AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DeskTheme.Canvas; ForeColor = DeskTheme.Ink;
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        MinimumSize = new Size(470, 290); ClientSize = new Size(720, 465);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(layout);

        if (missing.Length == 0)
        {
            layout.Controls.Add(ListHeader.Create("未持有装饰珠", character, null, null, null), 0, 0);
            layout.Controls.Add(new Label { Text = "当前角色已持有图鉴中的全部 " + DecorationInventory.CatalogCount + " 种装饰珠。",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty,
                BackColor = DeskTheme.Surface, ForeColor = DeskTheme.Forest, Font = DeskTheme.Heading }, 0, 1);
        }
        else
        {
            var search = new TextBox(); var count = new Label();
            layout.Controls.Add(ListHeader.Create("未持有装饰珠", character, "搜索珠名或技能", search, count), 0, 0);

            var grid = new DataGridView { AccessibleName = "未持有装饰珠清单", Dock = DockStyle.Fill,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                MultiSelect = false, RowHeadersVisible = false, Margin = Padding.Empty, Font = Font,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            DeskTheme.StyleGrid(grid);
            string[] labels = { "装饰珠名称", "类型", "所需孔级", "技能" };
            int[] widths = { 32, 12, 15, 41 };
            int[] minimumWidths = { 110, 55, 78, 124 };
            for (int i = 0; i < labels.Length; i++)
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = labels[i], FillWeight = widths[i], MinimumWidth = minimumWidths[i] });
            grid.Columns[2].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            layout.Controls.Add(grid, 0, 1);
            Action filter = () => {
                string query = search.Text.Trim();
                grid.Rows.Clear();
                foreach (var item in missing)
                {
                    if (item.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.SkillsLabel.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        grid.Rows.Add(item.name, item.TypeLabel, item.slot, item.SkillsLabel);
                }
                count.Text = query.Length == 0 ? "未持有 " + missing.Length + " 种" :
                    "匹配 " + grid.Rows.Count + " / " + missing.Length + " 种";
            };
            search.TextChanged += (s,e) => filter(); filter();
        }
        var close = new Button { Text = "关闭", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel };
        DeskTheme.StyleButton(close); close.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(close, 0, 2); CancelButton = close;
    }
}

internal sealed partial class CharmDesk : AppForm
{
    internal const string NoRating = "不评分", CustomRating = "自定义选择";
    readonly ComboBox saves = new DropdownPicker(), characters = new DropdownPicker();
    readonly SaveLocationGuide saveLocationGuide = new SaveLocationGuide();
    string characterSelectionPath;
    int? rememberedCharacterSlot;
    readonly ComboBox profiles = new DropdownPicker();
    readonly Label skillPreview = new Label();
    readonly HashSet<string> custom = new HashSet<string>();
    readonly RecommendedSkillEdits recommendedEdits = new RecommendedSkillEdits();
    readonly SkillSummary selectedSkillsPanel = new SkillSummary();
    SkillSelection recommendedSelection;
    readonly ComboBox weapons = new DropdownPicker(), styles = new DropdownPicker();
    readonly TableLayoutPanel recommendationScope = new TableLayoutPanel();
    readonly Label weaponLabel = new Label { Text = "武器", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Label styleLabel = new Label { Text = "流派", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Button adjust = new Button();
    readonly Label status = new Label();
    readonly TabControl toolTabs = new DeskTabs();
    readonly Label charmSummary = new Label(), decorationSummary = new Label();
    const int GodCharmScore = 17, SuperCharmScore = 19;
    readonly Label godCharmCount = new Label(), superCharmCount = new Label();
    readonly Label decorationHeadline = new Label(), decorationMessage = new Label();
    readonly TableLayoutPanel decorationCard = new TableLayoutPanel();
    readonly Panel decorationMeter = new Panel();
    double decorationProgress;
    readonly Button read = new Button(), preview = new Button(), export = new Button(), browse = new Button();
    readonly Button missingDecorations = new Button(), exportDecorations = new Button();
    bool busy, writingFile, closeAfterExport;
    internal CharmDesk(bool detectSaves = true)
    {
        Text = AppName; Font = DeskTheme.Body;
        BackColor = DeskTheme.Canvas; ForeColor = DeskTheme.Ink;
        ClientSize = new Size(828, 644); MinimumSize = new Size(600, 620); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18, 16, 18, 12) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);
        var header = new TableLayoutPanel {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 12),
            BackColor = DeskTheme.SaveHeader,
            ForeColor = DeskTheme.Forest
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); page.Controls.Add(header, 0, 0);
        var sourceHeading = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty };
        sourceHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourceHeading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourceHeading.Controls.Add(new Label {
            Text = "Steam 存档", Font = DeskTheme.Heading, AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty
        }, 0, 0);
        var locationHelp = new LinkLabel {
            Name = "saveLocationHelp", Text = "在哪里找存档？", AccessibleName = "查看 Steam 存档位置说明", AutoSize = true,
            Anchor = AnchorStyles.Right, Margin = Padding.Empty, LinkColor = DeskTheme.Forest,
            ActiveLinkColor = DeskTheme.Forest, VisitedLinkColor = DeskTheme.Forest,
            LinkBehavior = LinkBehavior.AlwaysUnderline, Font = DeskTheme.Caption
        };
        locationHelp.LinkClicked += (s,e) => saveLocationGuide.ShowBy(locationHelp);
        saveLocationGuide.Closed += (s,e) => {
            if (e.CloseReason == ToolStripDropDownCloseReason.Keyboard ||
                e.CloseReason == ToolStripDropDownCloseReason.CloseCalled) locationHelp.Focus();
        };
        Disposed += (s,e) => saveLocationGuide.Dispose();
        sourceHeading.Controls.Add(locationHelp, 1, 0); Add(header, sourceHeading);
        var source = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 6, 0, 8) };
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        saves.Name = "saves"; saves.AccessibleName = "Steam 存档";
        saves.Anchor = AnchorStyles.Left | AnchorStyles.Right; saves.Margin = Padding.Empty;
        saves.ForeColor = DeskTheme.Ink; saves.BackColor = DeskTheme.Surface;
        saves.DropDownStyle = ComboBoxStyle.DropDownList;
        WeaponThumbnails.ConfigureTextPicker(saves);
        source.Controls.Add(saves, 0, 0);
        SetButton(browse, "选择文件…", Choose, false);
        browse.Anchor = AnchorStyles.Left; browse.Margin = new Padding(8, 0, 0, 0);
        source.Controls.Add(browse, 1, 0);
        SetButton(read, "读取存档", async () => await LoadSave(), true);
        read.Anchor = AnchorStyles.Left; read.Margin = new Padding(8, 0, 0, 0);
        source.Controls.Add(read, 2, 0); Add(header, source);
        var roleRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        roleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); roleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        roleRow.Controls.Add(new Label {
            Text = "角色清单", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0)
        }, 0, 0);
        characters.Name = "characters"; characters.AccessibleName = "角色清单";
        characters.Dock = DockStyle.Top; characters.Margin = Padding.Empty; characters.DropDownStyle = ComboBoxStyle.DropDownList;
        characters.ForeColor = DeskTheme.Ink; characters.BackColor = DeskTheme.Surface;
        WeaponThumbnails.ConfigureTextPicker(characters); roleRow.Controls.Add(characters, 1, 0); Add(header, roleRow);
        status.AutoSize = true; status.Dock = DockStyle.Top; status.UseMnemonic = false; status.Font = DeskTheme.Caption;
        status.Margin = new Padding(0, 6, 0, 0);
        status.Visible = false; Add(header, status);

        toolTabs.AccessibleName = "工具选择"; toolTabs.Dock = DockStyle.Fill;
        toolTabs.Margin = new Padding(0, 12, 0, 0); page.Controls.Add(toolTabs, 0, 1);
        var charmTab = new TabPage("护石工具") { Padding = new Padding(14, 13, 14, 8), BackColor = DeskTheme.Surface };
        var decorationTab = new TabPage("装饰珠工具") { Padding = new Padding(14, 13, 14, 8), BackColor = DeskTheme.Surface };
        toolTabs.TabPages.AddRange(new[] { charmTab, decorationTab });
        toolTabs.SelectedIndex = 0;
        var charmLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        charmLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        charmLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        charmLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        charmTab.Controls.Add(charmLayout);
        charmLayout.Controls.Add(BuildCharmTierCards(), 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        charmLayout.Controls.Add(scroll, 0, 1);
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); scroll.Controls.Add(grid);
        // Wrapping skills can change the grid height during a scrollbar layout pass.
        // Publish the final height after that pass so Windows updates the entire scroll range.
        bool scrollUpdatePending = false;
        grid.SizeChanged += (s,e) => {
            if (!scroll.IsHandleCreated) { scroll.AutoScrollMinSize = new Size(0, grid.Height); return; }
            if (scrollUpdatePending) return;
            scrollUpdatePending = true;
            scroll.BeginInvoke((Action)(() => {
                scrollUpdatePending = false;
                if (scroll.IsDisposed) return;
                scroll.AutoScrollMinSize = new Size(0, grid.Height);
                scroll.PerformLayout();
            }));
        };
        charmSummary.AutoSize = true; charmSummary.Margin = new Padding(0, 3, 0, 8);
        charmSummary.ForeColor = DeskTheme.Muted; Add(grid, charmSummary);
        BuildDecorationCard(decorationTab);
        const string profileLabel = "评分方式（选择有效技能）";
        var ratingHeader = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty };
        ratingHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ratingHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ratingHeader.Controls.Add(new Label { Text = "评分方式", AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty, Font = DeskTheme.Heading }, 0, 0);
        var help = new Button { Name = "ratingHelp", AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Right };
        SetButton(help, RatingHelp.Caption, () => {
            using (var dialog = new RatingHelp())
                dialog.ShowDialog(this);
        }, false);
        help.Font = DeskTheme.Caption; help.Padding = new Padding(6, 0, 6, 0); help.Margin = Padding.Empty;
        ratingHeader.Controls.Add(help, 1, 0);
        Add(grid, ratingHeader);
        profiles.Name = "profiles"; profiles.AccessibleName = profileLabel;
        profiles.DropDownStyle = ComboBoxStyle.DropDownList; profiles.Dock = DockStyle.Top; profiles.Margin = new Padding(0, 4, 0, 6);
        WeaponThumbnails.ConfigureTextPicker(profiles);
        profiles.Items.Add(NoRating); profiles.Items.Add(Recommendations.Mode); profiles.Items.Add(CustomRating);
        Add(grid, profiles);

        recommendationScope.ColumnCount = 4; recommendationScope.Dock = DockStyle.Top;
        recommendationScope.AutoSize = true; recommendationScope.Margin = Padding.Empty;
        recommendationScope.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        recommendationScope.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        recommendationScope.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        recommendationScope.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        weapons.DropDownStyle = ComboBoxStyle.DropDownList;
        WeaponThumbnails.ConfigureWeaponPicker(weapons);
        weapons.Items.Add(BuildFit.AllWeapons);
        weapons.Items.AddRange(BuildFit.Weapons);
        weapons.Dock = DockStyle.Top; weapons.Name = "weapons"; weapons.AccessibleName = "推荐武器";
        styles.DropDownStyle = ComboBoxStyle.DropDownList; styles.Dock = DockStyle.Top;
        styles.Name = "styles"; styles.AccessibleName = "推荐流派";
        WeaponThumbnails.ConfigureTextPicker(styles);
        recommendationScope.Controls.Add(weaponLabel, 0, 0);
        recommendationScope.Controls.Add(weapons, 1, 0);
        recommendationScope.Controls.Add(styleLabel, 2, 0);
        recommendationScope.Controls.Add(styles, 3, 0);
        weaponLabel.Margin = new Padding(0, 0, 8, 0); styleLabel.Margin = new Padding(14, 0, 8, 0);
        weapons.Margin = styles.Margin = Padding.Empty;
        Add(grid, recommendationScope);
        var skillHeader = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        skillHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        skillHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        skillPreview.AutoSize = true; skillPreview.Anchor = AnchorStyles.Left; skillPreview.Margin = Padding.Empty; skillPreview.ForeColor = DeskTheme.Muted;
        skillHeader.Controls.Add(skillPreview, 0, 0);
        SetButton(adjust, "编辑技能", AdjustSkills, false);
        adjust.Name = "editSkills"; adjust.Margin = Padding.Empty; skillHeader.Controls.Add(adjust, 1, 0); Add(grid, skillHeader);
        Add(grid, selectedSkillsPanel);
        weapons.SelectedIndex = 0;
        PopulateStyles();
        profiles.SelectedIndex = 0;
        profiles.SelectedIndexChanged += (s,e) => RefreshRating();
        weapons.SelectedIndexChanged += (s,e) => { PopulateStyles(); RefreshRecommendedSelection(); };
        styles.SelectedIndexChanged += (s,e) => { if (!rebuildingStyles) RefreshRecommendedSelection(); };
        RefreshRecommendedSelection();
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0, 7, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        page.Controls.Add(buttons, 0, 2);
        SetButton(preview, "查看护石", async () => {
            try { await ShowPreview(); }
            catch (Exception error) { if (!IsDisposed) MessageBox.Show(this, error.Message, Text); }
        }, true);
        SetButton(export, "导出护石 Excel", async () => await SaveExcel(false), false);
        SetButton(missingDecorations, "查看未持有装饰珠", () => {
            using (var dialog = new MissingDecorations((Inventory)characters.SelectedItem)) dialog.ShowDialog(this);
        }, true);
        SetButton(exportDecorations, "导出装饰珠 Excel", async () => await SaveExcel(true), false);
        AddActionColumn(buttons, 1, export, exportDecorations);
        AddActionColumn(buttons, 2, preview, missingDecorations);
        toolTabs.SelectedIndexChanged += (s,e) => UpdateToolButtons();
        UpdateToolButtons();
        characters.SelectedIndexChanged += (s,e) => { SetStatus(""); UpdateCharacterSummary(); UpdateState(); };
        HandleCreated += (s,e) => { if (characters.SelectedItem != null) UpdateCharmHighlights(); };
        if (detectSaves)
        {
            string[] detected;
            try { detected = CharmFiles.FindSaves(); } catch { detected = new string[0]; }
            saves.Items.AddRange(detected); if (detected.Length > 0) saves.SelectedIndex = 0;
            Shown += async (s,e) => await LoadSave();
        }
        saves.SelectedIndexChanged += async (s,e) => await LoadSave();
        FormClosing += (s,e) => {
            if (!writingFile || e.CloseReason != CloseReason.UserClosing) return;
            closeAfterExport = e.Cancel = true;
            SetStatus("正在写入 Excel，保存完成后将关闭窗口。");
        };
    }
    static void Add(TableLayoutPanel grid, Control control)
    { int row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); grid.Controls.Add(control, 0, row); }

    static void AddActionColumn(TableLayoutPanel buttons, int column, params Button[] actions)
    {
        var slot = new Panel { Anchor = AnchorStyles.Left, Margin = Padding.Empty, TabIndex = column };
        buttons.Controls.Add(slot, column, 0);
        slot.Controls.AddRange(actions);
        // All tools share the same column positions, regardless of button caption width.
        int width = actions.Max(button => button.GetPreferredSize(Size.Empty).Width);
        int height = actions.Max(button => button.GetPreferredSize(Size.Empty).Height);
        slot.Size = new Size(width + 6, height + 6);
        foreach (var button in actions)
        {
            button.AutoSize = false;
            button.Bounds = new Rectangle(3, 3, width, height);
        }
    }

    TableLayoutPanel BuildCharmTierCards()
    {
        var charmTiers = new TableLayoutPanel {
            AccessibleName = "武器与流派推荐护石分档", AutoSize = true, ColumnCount = 2, RowCount = 1,
            Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 4)
        };
        charmTiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charmTiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charmTiers.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        charmTiers.Controls.Add(BuildCharmTier("神护石", GodCharmScore + "～" + (SuperCharmScore - 1) + " 分",
            godCharmCount, DeskTheme.Tint, DeskTheme.Forest, new Padding(0, 0, 5, 0)), 0, 0);
        charmTiers.Controls.Add(BuildCharmTier("超神护石", SuperCharmScore + " 分及以上",
            superCharmCount, DeskTheme.GoldTint, DeskTheme.Gold, new Padding(5, 0, 0, 0)), 1, 0);
        return charmTiers;
    }

    TableLayoutPanel BuildCharmTier(string title, string criterion, Label count, Color background, Color accent, Padding margin)
    {
        var card = new TableLayoutPanel {
            ColumnCount = 2, RowCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = margin,
            Padding = new Padding(14, 9, 14, 9), BackColor = background
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.Controls.Add(new Label { Text = title, AutoSize = true, Margin = Padding.Empty, ForeColor = accent, Font = DeskTheme.Heading }, 0, 0);
        card.Controls.Add(new Label { Text = criterion, AutoSize = true, Margin = new Padding(0, 2, 0, 0), ForeColor = accent, Font = DeskTheme.Caption }, 0, 1);
        count.AccessibleName = title + "数量";
        count.AutoSize = true;
        count.Anchor = AnchorStyles.Right;
        count.Font = DeskTheme.Stat;
        count.ForeColor = accent;
        count.Margin = new Padding(5, 0, 0, 0);
        card.Controls.Add(count, 1, 0); card.SetRowSpan(count, 2);
        return card;
    }

    void UpdateCharmHighlights()
    {
        var inventory = characters.SelectedItem as Inventory;
        if (inventory == null)
        {
            godCharmCount.Text = superCharmCount.Text = "—";
            charmSummary.Text = "";
            return;
        }

        UpdateCharmSummary(inventory);
        if (!IsHandleCreated) return;
        var selection = recommendedSelection;
        bool allWeapons = SelectedWeapon == BuildFit.AllWeapons;
        godCharmCount.Text = superCharmCount.Text = "…";
        Task.Run(() => {
            InventoryRow[] rows = null;
            try { rows = InventoryRow.Create(inventory, null, selection.Targets, allWeapons); }
            catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
            // The window can close while scoring a previously selected scope.
            try { BeginInvoke((Action)(() => DisplayCharmHighlights(inventory, selection, rows))); }
            catch (InvalidOperationException error) { System.Diagnostics.Debug.WriteLine(error); }
        });
    }

    void DisplayCharmHighlights(Inventory inventory, SkillSelection selection, InventoryRow[] rows)
    {
        if (IsDisposed || Disposing || !ReferenceEquals(inventory, characters.SelectedItem) ||
            !ReferenceEquals(selection, recommendedSelection)) return;
        if (rows == null)
        {
            godCharmCount.Text = superCharmCount.Text = "—";
            UpdateCharmSummary(inventory, "护石分档暂时无法计算");
            return;
        }

        godCharmCount.Text = rows.Count(row => row.Score >= GodCharmScore && row.Score < SuperCharmScore) + " 枚";
        superCharmCount.Text = rows.Count(row => row.Score >= SuperCharmScore) + " 枚";
        int unrated = rows.Count(row => !row.Score.HasValue);
        if (unrated > 0) UpdateCharmSummary(inventory, "待核对 " + unrated + " 枚");
    }

    void BuildDecorationCard(TabPage page)
    {
        decorationCard.ColumnCount = 1;
        decorationCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        decorationCard.Dock = DockStyle.Top;
        decorationCard.AutoSize = true;
        decorationCard.Padding = new Padding(20, 17, 20, 18);
        decorationCard.BackColor = DeskTheme.Tint;
        Add(decorationCard, new Label {
            Text = "装饰珠图鉴", AutoSize = true, ForeColor = DeskTheme.Forest,
            Font = DeskTheme.Heading, Margin = Padding.Empty
        });

        decorationHeadline.AutoSize = true;
        decorationHeadline.ForeColor = DeskTheme.Forest;
        decorationHeadline.Font = DeskTheme.Stat;
        decorationHeadline.Margin = new Padding(0, 7, 0, 0);
        Add(decorationCard, decorationHeadline);

        decorationMeter.AccessibleRole = AccessibleRole.ProgressBar;
        decorationMeter.AccessibleName = "装饰珠收集进度";
        decorationMeter.Dock = DockStyle.Top;
        decorationMeter.Height = 8;
        decorationMeter.Margin = new Padding(0, 10, 0, 10);
        decorationMeter.BackColor = SystemInformation.HighContrast ? SystemColors.Window : DeskTheme.Border;
        decorationMeter.Paint += (s,e) => {
            int width = (int)Math.Round(decorationMeter.ClientSize.Width * decorationProgress);
            using (var brush = new SolidBrush(DeskTheme.Accent))
                e.Graphics.FillRectangle(brush, 0, 0, width, decorationMeter.ClientSize.Height);
        };
        Add(decorationCard, decorationMeter);

        decorationSummary.AutoSize = true; decorationSummary.ForeColor = DeskTheme.Forest;
        decorationSummary.Margin = Padding.Empty;
        Add(decorationCard, decorationSummary);
        decorationCard.Visible = false;
        page.Controls.Add(decorationCard);

        decorationMessage.AutoSize = true;
        decorationMessage.Dock = DockStyle.Top;
        decorationMessage.Padding = new Padding(16, 14, 16, 14);
        decorationMessage.ForeColor = DeskTheme.Forest; decorationMessage.BackColor = DeskTheme.Tint;
        decorationMessage.Text = "读取存档并选择角色后，可查看装饰珠收集进度。";
        page.Controls.Add(decorationMessage);
    }

    void UpdateToolButtons()
    {
        bool charms = toolTabs.SelectedIndex <= 0;
        preview.Visible = export.Visible = charms;
        missingDecorations.Visible = exportDecorations.Visible = toolTabs.SelectedIndex == 1;
    }
    void SetStatus(string message)
    {
        status.Text = message;
        status.Visible = message.Length > 0;
    }
    void UpdateCharacterSummary()
    {
        var inv = characters.SelectedItem as Inventory;
        UpdateCharmHighlights();
        if (inv == null)
        {
            decorationCard.Visible = false;
            decorationMessage.Text = "读取存档并选择角色后，可查看装饰珠收集进度。";
            decorationMessage.Visible = true;
            return;
        }
        int total = DecorationInventory.CatalogCount;
        if (inv.decorations == null || total == 0)
        {
            decorationCard.Visible = false;
            decorationMessage.Text = total == 0 ? "装饰珠图鉴暂不可用。" :
                inv.decorationError ?? "尚未读取该角色的装饰珠清单。";
            decorationMessage.Visible = true;
            return;
        }
        int missing = DecorationInventory.IncludingUnowned(inv.decorations.Where(row => row.totalCount > 0))
            .Count(row => row.totalCount == 0);
        decorationProgress = (double)(total - missing) / total;
        double percent = missing == 0 ? 100 : Math.Min(99.9, decorationProgress * 100);
        string progressText = percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        decorationHeadline.Text = "已收集 " + progressText + " 的装饰珠";
        decorationMeter.AccessibleDescription = progressText;
        decorationMeter.Invalidate();
        decorationSummary.Text = String.Format("图鉴共 {0} 种　·　已持有 {1} 种　·　未持有 {2} 种",
            total, total - missing, missing);
        decorationMessage.Visible = false;
        decorationCard.Visible = true;
    }
    void UpdateCharmSummary(Inventory inventory, string ratingStatus = "")
    {
        int appraised = inventory.talismans.Count(c => c.kind == "appraised");
        charmSummary.Text = String.Format("共 {0} 枚　·　鉴定 {1} 枚　·　制作 {2} 枚　·　收藏 {3} 枚",
            inventory.talismans.Count, appraised, inventory.talismans.Count - appraised,
            inventory.talismans.Count(c => c.favorite)) + (ratingStatus.Length > 0 ? "　·　" + ratingStatus : "");
    }
    string Profile { get { return profiles.SelectedItem as string ?? NoRating; } }
    string SelectedWeapon { get { return (string)weapons.SelectedItem ?? BuildFit.AllWeapons; } }
    string SelectedStyle { get { return (string)styles.SelectedItem ?? Recommendations.AllStyles; } }
    SkillSelection Preset { get { return Profile == Recommendations.Mode ? recommendedSelection : null; } }
    bool rebuildingStyles;
    void PopulateStyles()
    {
        rebuildingStyles = true;
        styles.BeginUpdate();
        try
        {
            styles.Items.Clear();
            styles.Items.AddRange(Recommendations.StylesForWeapon(SelectedWeapon));
            styles.SelectedIndex = 0;
        }
        finally { styles.EndUpdate(); rebuildingStyles = false; }
    }
    void RefreshRecommendedSelection()
    {
        recommendedSelection = recommendedEdits.ForScope(SelectedWeapon, SelectedStyle);
        RefreshRating();
        UpdateCharmHighlights();
    }
    void RefreshRating()
    {
        string mode = Profile;
        bool hasPreset = mode == Recommendations.Mode;
        recommendationScope.Visible = hasPreset;
        adjust.Visible = hasPreset || mode == CustomRating;
        selectedSkillsPanel.Visible = hasPreset || mode == CustomRating;
        selectedSkillsPanel.AccessibleName = hasPreset ? "所选武器与流派的有效技能" : "已选技能";
        DisplaySkills();
    }
    void DisplaySkills()
    {
        if (Profile == NoRating)
        {
            skillPreview.Font = DeskTheme.Body;
            skillPreview.ForeColor = DeskTheme.Muted;
            skillPreview.Text = "仅列出护石，不计算分数。";
            UpdateState();
            return;
        }
        var preset = Preset;
        var names = preset == null ? custom.OrderBy(name => name, StringComparer.CurrentCulture).ToArray() : preset.DefaultSkills;
        skillPreview.Font = DeskTheme.Heading;
        skillPreview.ForeColor = DeskTheme.Forest;
        skillPreview.Text = (preset == null ? "已选技能" : "有效技能") + "  ·  " + names.Length + " 项"
            + (preset != null && preset.IsEdited ? " · 已编辑" : "");
        selectedSkillsPanel.SetSkills(names, preset == null ? "点击「编辑技能」选择参与评分的技能。" : "当前仅计算孔位分。");
        UpdateState();
    }

    void AdjustSkills()
    {
        bool recommended = Profile == Recommendations.Mode;
        if (!recommended && Profile != CustomRating) return;
        string weapon = SelectedWeapon, style = SelectedStyle;
        string scope = recommended ? Recommendations.ScopeLabel(weapon, style) : CustomRating;
        var initial = recommended ? recommendedSelection.DefaultSkills : custom.ToArray();
        var defaults = recommended ? Recommendations.ForScope(weapon, style)
            .SelectMany(target => target.skills.Keys).Distinct().ToArray() : null;
        using (var picker = new SkillPicker(scope, SkillCatalog.Skills, initial, defaults))
        {
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            if (recommended)
            {
                recommendedEdits.Apply(weapon, style, picker.SelectedSkills, picker.RestoredDefaults);
                RefreshRecommendedSelection();
            }
            else
            {
                custom.Clear();
                custom.UnionWith(picker.SelectedSkills);
                DisplaySkills();
            }
        }
    }
    BuildTarget[] SelectedTargets()
    {
        var preset = Preset;
        return preset == null ? null : preset.Targets;
    }
    void SetButton(Button button, string text, Action action, bool primary)
    {
        button.Text = text; button.AutoSize = true; button.Padding = new Padding(10, 3, 10, 3);
        DeskTheme.StyleButton(button, primary);
        button.Click += (s,e) => { try { action(); } catch (Exception error) { MessageBox.Show(this, error.Message, Text); } };
    }
    void Choose()
    {
        using (var box = new OpenFileDialog { Title = "选择 Steam userdata 下的荒野角色存档", Filter = "荒野角色存档|data001Slot.bin" })
        {
            if (box.ShowDialog(this) != DialogResult.OK) return;
            if (!saves.Items.Contains(box.FileName)) saves.Items.Add(box.FileName);
            saves.SelectedItem = box.FileName;
        }
    }
    async Task LoadSave()
    {
        string path = saves.SelectedItem as string;
        if (busy) return;
        var current = characters.SelectedItem as Inventory;
        if (!String.Equals(path, characterSelectionPath, StringComparison.OrdinalIgnoreCase))
            rememberedCharacterSlot = null;
        else if (current != null)
            rememberedCharacterSlot = current.characterSlot;
        characterSelectionPath = path;
        busy = true; characters.Items.Clear(); UpdateCharacterSummary(); UpdateState();
        try
        {
            if (path == null) { SetStatus("尚未找到本机 Steam 存档，请点「选择文件」。"); return; }
            SetStatus("正在读取护石和装饰珠…");
            var list = await Task.Run(() => CharmFiles.Read(path));
            if (list.Count == 0) {
                rememberedCharacterSlot = null;
                SetStatus("这个存档里没有已建立的角色。"); return;
            }
            var next = list.FirstOrDefault(item => item.characterSlot == rememberedCharacterSlot) ?? list[0];
            characters.Items.AddRange(list.ToArray());
            characters.SelectedItem = next;
        }
        catch (Exception e) { SetStatus("读取失败：" + e.Message); }
        finally { busy = false; UpdateState(); }
    }
    void UpdateState()
    {
        var inv = characters.SelectedItem as Inventory;
        bool ready = !busy && !writingFile;
        preview.Enabled = export.Enabled = ready && inv != null && (Profile != CustomRating || custom.Count > 0);
        missingDecorations.Enabled = exportDecorations.Enabled = ready && inv != null;
        saves.Enabled = browse.Enabled = characters.Enabled = ready; read.Enabled = ready && saves.SelectedIndex >= 0;
        toolTabs.Enabled = profiles.Enabled = recommendationScope.Enabled = selectedSkillsPanel.Enabled = ready;
        styles.Enabled = ready && SelectedWeapon != BuildFit.AllWeapons;
        adjust.Enabled = ready && (Preset != null || Profile == CustomRating);
    }
    async Task ShowPreview()
    {
        var inventory = (Inventory)characters.SelectedItem;
        string mode = Profile, weapon = SelectedWeapon, style = SelectedStyle;
        bool recommended = mode == Recommendations.Mode;
        var selected = mode == CustomRating ? new HashSet<string>(custom) : null;
        var builds = SelectedTargets();
        bool allWeapons = recommended && weapon == BuildFit.AllWeapons;
        busy = true;
        UpdateState();
        SetStatus("正在整理护石清单…");
        try
        {
            var rows = await Task.Run(() => InventoryRow.Create(inventory, selected, builds, allWeapons));
            if (IsDisposed || Disposing) return;
            SetStatus("已读取 " + rows.Length + " 枚护石。");
            string label = recommended ? Recommendations.ScopeLabel(weapon, style) : mode;
            using (var window = new CharmPreview(inventory, label, mode != NoRating, recommended, allWeapons, rows))
                window.ShowDialog(this);
        }
        finally
        {
            busy = false;
            if (!IsDisposed && !Disposing) { SetStatus(""); UpdateState(); }
        }
    }

    async Task SaveExcel(bool decorationsOnly)
    {
        try { await SaveInventoryExcel(decorationsOnly); }
        catch (Exception error) { closeAfterExport = false; if (!IsDisposed) MessageBox.Show(this, error.Message, Text); }
    }
    async Task SaveInventoryExcel(bool decorationsOnly)
    {
        var inventory = (Inventory)characters.SelectedItem;
        if (decorationsOnly && !String.IsNullOrEmpty(inventory.decorationError))
            throw new InvalidDataException(inventory.decorationError);
        string content = decorationsOnly ? "装饰珠" : "护石";
        using (var dialog = new SaveFileDialog {
            Title = "保存" + content + " Excel 清单", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx", AddExtension = true,
            FileName = "荒野" + content + "-" + inventory.FileLabel + "-" +
                (!decorationsOnly && Profile == Recommendations.Mode ? SelectedWeapon + "-" + SelectedStyle + "-" : "") + DateTime.Now.ToString("yyyyMMdd-HHmmss")
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string mode = Profile, weapon = SelectedWeapon, style = SelectedStyle, path = dialog.FileName;
            var selected = new HashSet<string>(custom);
            var builds = SelectedTargets();
            writingFile = true;
            UpdateState();
            SetStatus("正在生成 Excel…");
            try
            {
                await Task.Run(() => {
                    if (decorationsOnly) ExcelExport.SaveDecorations(path, inventory);
                    else if (builds != null) ExcelExport.SaveTargets(path, inventory, weapon, style, builds);
                    else if (mode == CustomRating) ExcelExport.Save(path, inventory, CustomRating, selected);
                    else ExcelExport.Save(path, inventory);
                });
            }
            finally { writingFile = false; SetStatus(""); UpdateState(); }
            SetStatus("已保存：" + path);
            if (closeAfterExport) Close();
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--export")
        {
            foreach (var inv in CharmFiles.Read(args[1]))
            {
                ExcelExport.Save(Path.Combine(args[2], "我的护石-" + inv.FileLabel + ".xlsx"), inv);
            }
            return 0;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new CharmDesk()); return 0;
    }
}
