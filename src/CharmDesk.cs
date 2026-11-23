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

internal sealed class SkillSelector : AppForm
{
    readonly TextBox search = new TextBox();
    readonly CheckedListBox list = new CheckedListBox();
    readonly Label count = new Label();
    readonly Button apply = new Button();
    readonly string[] available;
    readonly string[] recommendedDefaults;
    bool rebuilding;
    internal readonly HashSet<string> SelectedSkills = new HashSet<string>();
    internal bool RestoredDefaults { get; private set; }

    internal SkillSelector(string name, IEnumerable<string> options)
        : this(name, options, null, null) { }

    internal SkillSelector(string name, IEnumerable<string> options, IEnumerable<string> selected, IEnumerable<string> defaults)
    {
        bool editing = defaults != null;
        recommendedDefaults = editing ? defaults.ToArray() : null;
        if (selected != null) SelectedSkills.UnionWith(selected);
        available = options.OrderByDescending(SelectedSkills.Contains)
            .ThenBy(skill => skill, StringComparer.CurrentCulture).ToArray();
        Text = (editing ? "编辑技能 · " : "追加技能 · ") + name;
        Font = new Font("Microsoft YaHei UI", 10); StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(470, 460); MinimumSize = new Size(400, 340);
        BackColor = Color.FromArgb(247, 247, 241);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = editing ? 5 : 4 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int row = 0;
        if (editing)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            string scope = name == BuildFit.AllWeapons ? "全部武器" : "「" + name + "」";
            grid.Controls.Add(new Label { Text = "修改范围：" + scope + "的推荐玩法。取消勾选可移除技能，额外追加的技能会加入该范围内的每种玩法。",
                Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 0, 3, 8) }, 0, row++);
        }
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(grid);
        grid.Controls.Add(new Label { Text = "搜索技能", AutoSize = true }, 0, row++);
        search.AccessibleName = "搜索技能"; search.Dock = DockStyle.Top; grid.Controls.Add(search, 0, row++);

        list.Dock = DockStyle.Fill; list.CheckOnClick = true; list.IntegralHeight = false;
        list.AccessibleName = editing ? "参与评分的技能" : "可追加技能";
        list.Format += (s,e) => {
            string skill = (string)e.ListItem;
            e.Value = skill + "    " + Ratings.PriceLabel(skill);
        };
        list.FormattingEnabled = true; grid.Controls.Add(list, 0, row++);
        var footer = new TableLayoutPanel { ColumnCount = editing ? 4 : 3, AutoSize = true, Dock = DockStyle.Fill };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 1; i < footer.ColumnCount; i++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        count.AutoSize = true; count.Anchor = AnchorStyles.Left; footer.Controls.Add(count, 0, 0);
        int buttonColumn = 1;
        if (editing)
        {
            var reset = new Button { Name = "restoreRecommendation", Text = "恢复推荐", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            reset.Click += (s,e) => {
                SelectedSkills.Clear();
                SelectedSkills.UnionWith(recommendedDefaults);
                RestoredDefaults = true;
                Populate();
            };
            footer.Controls.Add(reset, buttonColumn++, 0);
        }
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        apply.Text = editing ? "应用" : "追加"; apply.AutoSize = true; apply.DialogResult = DialogResult.OK;
        apply.BackColor = Color.FromArgb(30, 92, 74); apply.ForeColor = Color.White;
        apply.FlatStyle = FlatStyle.Flat; footer.Controls.Add(cancel, buttonColumn++, 0); footer.Controls.Add(apply, buttonColumn, 0);
        grid.Controls.Add(footer, 0, row); AcceptButton = apply; CancelButton = cancel;
        list.ItemCheck += (s,e) => {
            if (rebuilding) return;
            string skill = (string)list.Items[e.Index];
            if (e.NewValue == CheckState.Checked) SelectedSkills.Add(skill);
            else SelectedSkills.Remove(skill);
            UpdateCount();
        };
        search.TextChanged += (s,e) => Populate();
        Populate();
    }
    void UpdateCount()
    {
        count.Text = (list.Items.Count == 0 ? "没有匹配技能　·　" : "") +
            (recommendedDefaults != null ? "已选 " : "待追加 ") + SelectedSkills.Count + " 项";
        apply.Enabled = recommendedDefaults != null || SelectedSkills.Count > 0;
    }
    void Populate()
    {
        string query = search.Text.Trim();
        rebuilding = true; list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (string skill in available)
            {
                if (skill.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    list.Items.Add(skill, SelectedSkills.Contains(skill));
            }
        }
        finally { list.EndUpdate(); rebuilding = false; }
        UpdateCount();
    }
}

internal sealed class Charm
{
    public int boxIndex { get; set; }
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
                var charm = new Charm { boxIndex = item.BoxIndex + 1, favorite = item.Favorite, equipped = item.Equipped,
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
        Text = inventory.DisplayName + (inventory.showSlot ? "（栏位 " + inventory.characterSlot + "）" : "") + " · 未持有装饰珠";
        Font = new Font("Microsoft YaHei UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(247, 247, 241); ForeColor = Color.FromArgb(34, 51, 45);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        MinimumSize = new Size(470, 290); ClientSize = new Size(660, 420);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(layout);

        if (missing.Length == 0)
        {
            layout.Controls.Add(new Label { Text = "当前角色已持有图鉴中的全部 " + DecorationInventory.CatalogCount + " 种装饰珠。",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        }
        else
        {
            var bar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0) };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            const string prompt = "搜索珠名或技能";
            bar.Controls.Add(new Label { Text = prompt, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            var search = new TextBox { AccessibleName = prompt, Dock = DockStyle.Fill };
            var count = new Label { AutoSize = true, Anchor = AnchorStyles.Right };
            bar.Controls.Add(search, 1, 0); bar.Controls.Add(count, 2, 0); layout.Controls.Add(bar, 0, 0);

            var grid = new DataGridView { AccessibleName = "未持有装饰珠清单", Dock = DockStyle.Fill,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 92, 74);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(212, 234, 222);
            grid.DefaultCellStyle.SelectionForeColor = ForeColor; grid.RowTemplate.Height = Font.Height + 9;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 246);
            grid.GridColor = Color.FromArgb(218, 227, 222); grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            string[] labels = { "装饰珠名称", "类型", "所需孔级", "技能" };
            int[] widths = { 32, 12, 15, 41 };
            for (int i = 0; i < labels.Length; i++)
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = labels[i], FillWeight = widths[i] });
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
        layout.Controls.Add(close, 0, 2); CancelButton = close;
    }
}

internal sealed partial class CharmDesk : AppForm
{
    // Scrollbars are added after WinForms lays out children. Reserve the vertical
    // scrollbar's width on the first pass so wrapped content always fits the viewport.
    static Rectangle ScrollWidth(ScrollableControl panel, Rectangle area)
    {
        int reserved = panel.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth;
        area.Width = Math.Max(0, panel.ClientSize.Width - panel.Padding.Horizontal - reserved);
        return area;
    }
    sealed class SkillFlowPanel : FlowLayoutPanel
    {
        public override Rectangle DisplayRectangle { get { return ScrollWidth(this, base.DisplayRectangle); } }
    }
    sealed class ContentTable : TableLayoutPanel
    {
        public override Rectangle DisplayRectangle { get { return ScrollWidth(this, base.DisplayRectangle); } }
    }
    internal const string NoRating = "不评分", CustomRating = "自定义选择";
    readonly ComboBox saves = new ComboBox(), characters = new ComboBox();
    string characterSelectionPath;
    int? rememberedCharacterSlot;
    readonly ComboBox profiles = new ComboBox();
    readonly Label skillPreview = new Label();
    readonly HashSet<string> custom = new HashSet<string>();
    readonly RecommendedSkillEdits recommendedEdits = new RecommendedSkillEdits();
    readonly FlowLayoutPanel selectedSkillsPanel = new SkillFlowPanel();
    SkillSelection recommendedSelection;
    readonly ComboBox weapons = new ComboBox();
    readonly Label weaponLabel = new Label { Text = "武器", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Button adjust = new Button();
    readonly Label status = new Label();
    readonly TabControl toolTabs = new TabControl();
    readonly Label charmSummary = new Label(), decorationSummary = new Label();
    const int GodCharmScore = 17, SuperCharmScore = 19;
    readonly Label godCharmCount = new Label(), superCharmCount = new Label();
    readonly Label decorationHeadline = new Label(), decorationMessage = new Label();
    readonly TableLayoutPanel decorationCard = new TableLayoutPanel();
    readonly Panel decorationMeter = new Panel();
    double decorationProgress;
    readonly Button read = new Button(), preview = new Button(), export = new Button(), browse = new Button();
    readonly Button missingDecorations = new Button(), exportDecorations = new Button();
    static readonly Color green = Color.FromArgb(30, 92, 74);
    bool busy, writingFile, closeAfterExport;
    CharmDesk()
    {
        Text = AppName; Font = new Font("Microsoft YaHei UI", 10);
        BackColor = Color.FromArgb(247, 247, 241); ForeColor = Color.FromArgb(34, 51, 45);
        ClientSize = new Size(760, 598); MinimumSize = new Size(600, 470); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 14, 20, 12) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); page.Controls.Add(header, 0, 0);
        Add(header, new Label { Text = "Steam 存档", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        var source = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true };
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        saves.Dock = DockStyle.Fill; saves.DropDownStyle = ComboBoxStyle.DropDownList; source.Controls.Add(saves, 0, 0);
        SetButton(browse, "选择文件…", Choose, false); source.Controls.Add(browse, 1, 0); Add(header, source);
        var readRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        readRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); readRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        SetButton(read, "读取存档", async () => await LoadSave(), true);
        read.Anchor = AnchorStyles.Left; readRow.Controls.Add(read, 0, 0);
        readRow.Controls.Add(new Label {
            Text = "右键 Steam 快捷方式，选择「打开文件所在的位置」。\r\n" +
                @"进入 userdata\你的数字账号文件夹\2246340\remote\win64_save，选择 data001Slot.bin。",
            AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(9, 2, 3, 2),
            ForeColor = Color.FromArgb(69, 85, 78)
        }, 1, 0);
        Add(header, readRow);
        Add(header, new Label { Text = "角色清单", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 9, 3, 3) });
        characters.Dock = DockStyle.Top; characters.DropDownStyle = ComboBoxStyle.DropDownList; Add(header, characters);
        status.AutoSize = true; status.Dock = DockStyle.Top; status.UseMnemonic = false;
        status.Visible = false; Add(header, status);

        toolTabs.AccessibleName = "工具选择"; toolTabs.Dock = DockStyle.Fill;
        toolTabs.Margin = new Padding(0, 8, 0, 0); page.Controls.Add(toolTabs, 0, 1);
        var charmTab = new TabPage("护石工具") { Padding = new Padding(8), BackColor = BackColor };
        var decorationTab = new TabPage("装饰珠工具") { Padding = new Padding(8), BackColor = BackColor };
        toolTabs.TabPages.AddRange(new[] { charmTab, decorationTab });
        toolTabs.SelectedIndex = 0;
        var charmLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        charmLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        charmLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        charmLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        charmTab.Controls.Add(charmLayout);
        charmLayout.Controls.Add(BuildCharmTierCards(), 0, 0);
        var grid = new ContentTable { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); charmLayout.Controls.Add(grid, 0, 1);
        charmSummary.AutoSize = true; charmSummary.Margin = new Padding(3, 4, 3, 8); Add(grid, charmSummary);
        BuildDecorationCard(decorationTab);
        const string profileLabel = "评分方式（选择有效技能）";
        var ratingHeader = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        ratingHeader.Controls.Add(new Label { Text = profileLabel, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(Font, FontStyle.Bold) });
        var help = new Button { Name = "ratingHelp", AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left };
        SetButton(help, RatingHelp.Caption, () => {
            using (var dialog = new RatingHelp())
                dialog.ShowDialog(this);
        }, false);
        help.Padding = new Padding(4, 0, 4, 0);
        ratingHeader.Controls.Add(help);
        Add(grid, ratingHeader);
        var prefs = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Top, AutoSize = true };
        prefs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        prefs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        prefs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        profiles.AccessibleName = profileLabel;
        profiles.DropDownStyle = ComboBoxStyle.DropDownList; profiles.Dock = DockStyle.Top;
        profiles.Items.Add(NoRating); profiles.Items.Add(Recommendations.Mode); profiles.Items.Add(CustomRating);
        prefs.Controls.Add(profiles, 0, 0);
        weapons.DropDownStyle = ComboBoxStyle.DropDownList;
        WeaponThumbnails.ConfigureWeaponPicker(weapons);
        weapons.MinimumSize = new Size(145, 0);
        weapons.Items.Add(BuildFit.AllWeapons);
        weapons.Items.AddRange(BuildFit.Weapons);
        weapons.Dock = DockStyle.Top;
        weapons.AccessibleName = "参考武器";
        prefs.Controls.Add(weaponLabel, 1, 0);
        prefs.Controls.Add(weapons, 2, 0); Add(grid, prefs);
        var skillHeader = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true };
        skillHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        skillHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        skillPreview.AutoSize = true; skillPreview.Anchor = AnchorStyles.Left;
        skillHeader.Controls.Add(skillPreview, 0, 0);
        SetButton(adjust, "追加技能", AdjustSkills, false);
        adjust.Name = "addSkills"; skillHeader.Controls.Add(adjust, 1, 0); Add(grid, skillHeader);
        selectedSkillsPanel.Name = "selectedSkillsPanel"; selectedSkillsPanel.AccessibleName = "已选技能";
        selectedSkillsPanel.Dock = DockStyle.Top; selectedSkillsPanel.AutoScroll = true;
        selectedSkillsPanel.Padding = new Padding(4); selectedSkillsPanel.BackColor = Color.White;
        selectedSkillsPanel.BorderStyle = BorderStyle.FixedSingle;
        Add(grid, selectedSkillsPanel);
        profiles.SelectedIndexChanged += (s,e) => RefreshRating();
        weapons.SelectedIndexChanged += (s,e) => RefreshRecommendedSelection();
        profiles.SelectedIndex = 0;
        weapons.SelectedIndex = 0;
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0, 6, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
        AddActionColumn(buttons, 0, preview, missingDecorations);
        AddActionColumn(buttons, 1, export, exportDecorations);
        toolTabs.SelectedIndexChanged += (s,e) => UpdateToolButtons();
        UpdateToolButtons();
        string[] detected;
        try { detected = CharmFiles.FindSaves(); } catch { detected = new string[0]; }
        saves.Items.AddRange(detected); if (detected.Length > 0) saves.SelectedIndex = 0;
        saves.SelectedIndexChanged += async (s,e) => await LoadSave();
        characters.SelectedIndexChanged += (s,e) => { SetStatus(""); UpdateCharacterSummary(); UpdateState(); };
        Shown += async (s,e) => await LoadSave();
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
            AccessibleName = "推荐玩法护石分档", AutoSize = true, ColumnCount = 2, RowCount = 1,
            Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 4)
        };
        charmTiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charmTiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charmTiers.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        charmTiers.Controls.Add(BuildCharmTier("神护石", GodCharmScore + "～" + (SuperCharmScore - 1) + " 分",
            godCharmCount, Color.FromArgb(231, 240, 232), green, new Padding(0, 0, 4, 0)), 0, 0);
        charmTiers.Controls.Add(BuildCharmTier("超神护石", SuperCharmScore + " 分及以上",
            superCharmCount, Color.FromArgb(246, 238, 216), Color.FromArgb(108, 68, 18), new Padding(4, 0, 0, 0)), 1, 0);
        return charmTiers;
    }

    TableLayoutPanel BuildCharmTier(string title, string criterion, Label count, Color background, Color accent, Padding margin)
    {
        var card = new TableLayoutPanel {
            ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = margin,
            Padding = new Padding(12, 5, 12, 6), BackColor = background
        };
        Add(card, new Label {
            Text = title + "　·　" + criterion, AutoSize = true,
            Margin = Padding.Empty, ForeColor = accent, Font = new Font(Font, FontStyle.Bold)
        });
        count.AccessibleName = title + "数量";
        count.AutoSize = true;
        count.Font = new Font(Font.FontFamily, 20, FontStyle.Bold);
        count.ForeColor = accent;
        count.Margin = Padding.Empty;
        Add(card, count);
        return card;
    }

    async void UpdateCharmHighlights()
    {
        var inventory = characters.SelectedItem as Inventory;
        if (inventory == null)
        {
            godCharmCount.Text = superCharmCount.Text = "—";
            charmSummary.Text = "";
            return;
        }

        UpdateCharmSummary(inventory);
        var selection = recommendedSelection;
        bool allWeapons = (string)weapons.SelectedItem == BuildFit.AllWeapons;
        godCharmCount.Text = superCharmCount.Text = "…";
        InventoryRow[] rows = null;
        try
        {
            rows = await Task.Run(() => InventoryRow.Create(inventory, null, selection.Targets, allWeapons));
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
        }
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
        decorationCard.Padding = new Padding(18, 8, 18, 8);
        decorationCard.BackColor = Color.FromArgb(231, 240, 232);
        Add(decorationCard, new Label {
            Text = "装饰珠图鉴", AutoSize = true, ForeColor = green,
            Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty
        });

        decorationHeadline.AutoSize = true;
        decorationHeadline.ForeColor = green;
        decorationHeadline.Font = new Font(Font.FontFamily, 21, FontStyle.Bold);
        decorationHeadline.Margin = new Padding(0, 2, 0, 0);
        Add(decorationCard, decorationHeadline);

        decorationMeter.AccessibleRole = AccessibleRole.ProgressBar;
        decorationMeter.AccessibleName = "装饰珠收集进度";
        decorationMeter.Dock = DockStyle.Top;
        decorationMeter.Height = 7;
        decorationMeter.Margin = new Padding(0, 6, 0, 6);
        decorationMeter.BackColor = Color.FromArgb(203, 223, 208);
        decorationMeter.Paint += (s,e) => {
            int width = (int)Math.Round(decorationMeter.ClientSize.Width * decorationProgress);
            using (var brush = new SolidBrush(green))
                e.Graphics.FillRectangle(brush, 0, 0, width, decorationMeter.ClientSize.Height);
        };
        Add(decorationCard, decorationMeter);

        decorationSummary.AutoSize = true;
        decorationSummary.Margin = Padding.Empty;
        Add(decorationCard, decorationSummary);
        decorationCard.Visible = false;
        page.Controls.Add(decorationCard);

        decorationMessage.AutoSize = true;
        decorationMessage.Dock = DockStyle.Top;
        decorationMessage.Padding = new Padding(4, 7, 4, 0);
        decorationMessage.Visible = false;
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
            decorationCard.Visible = decorationMessage.Visible = false;
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
    SkillSelection Preset { get { return Profile == Recommendations.Mode ? recommendedSelection : null; } }
    void RefreshRecommendedSelection()
    {
        recommendedSelection = recommendedEdits.ForWeapon((string)weapons.SelectedItem);
        RefreshRating();
        UpdateCharmHighlights();
    }
    void RefreshRating()
    {
        string mode = Profile;
        bool hasPreset = mode == Recommendations.Mode;
        weaponLabel.Visible = weapons.Visible = hasPreset;
        adjust.Text = hasPreset ? "编辑技能" : "追加技能";
        adjust.Visible = hasPreset || mode == CustomRating;
        selectedSkillsPanel.Visible = hasPreset || mode == CustomRating;
        selectedSkillsPanel.AccessibleName = hasPreset ? "推荐玩法的有效技能" : "已选技能";
        DisplaySkills();
    }
    void DisplaySkills()
    {
        var preset = Preset;
        selectedSkillsPanel.SuspendLayout();
        try
        {
            foreach (Control old in selectedSkillsPanel.Controls.Cast<Control>().ToArray()) old.Dispose();
            selectedSkillsPanel.AutoScrollPosition = Point.Empty;
            if (preset != null)
            {
                foreach (string name in preset.DefaultSkills)
                    selectedSkillsPanel.Controls.Add(new Label {
                        Text = name, AutoSize = true, UseMnemonic = false, Padding = new Padding(4, 2, 4, 2),
                        Margin = new Padding(2), BackColor = BackColor, ForeColor = ForeColor
                    });
            }
            else if (Profile == CustomRating)
            {
                foreach (string name in custom.OrderBy(skill => skill, StringComparer.CurrentCulture))
                {
                    var chip = new Button { Text = name + " ×", Tag = name, AccessibleName = "移除" + name,
                        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat,
                        BackColor = Color.White, ForeColor = ForeColor, Margin = new Padding(2), Padding = new Padding(4, 0, 4, 0) };
                    chip.FlatAppearance.BorderColor = Color.FromArgb(194, 203, 197);
                    chip.Click += RemoveSkill; selectedSkillsPanel.Controls.Add(chip);
                }
            }
        }
        finally { selectedSkillsPanel.ResumeLayout(true); }
        UpdateSkillSummary();
    }
    void RemoveSkill(object sender, EventArgs args)
    {
        if (Profile != CustomRating) return;
        var chip = (Button)sender;
        custom.Remove((string)chip.Tag);
        chip.Dispose();
        UpdateSkillSummary();
    }
    void UpdateSkillSummary()
    {
        var preset = Preset;
        int count = preset == null ? custom.Count : preset.DefaultSkills.Length;
        if (Profile == NoRating)
            skillPreview.Text = "按装备箱顺序导出。";
        else
        {
            skillPreview.Text = selectedSkillsPanel.AccessibleName + "（" + count + "）"
                + (preset != null && preset.IsEdited ? " · 已编辑" : "");
            if (count == 0)
                selectedSkillsPanel.Controls.Add(new Label { Text = preset == null ? "点击「追加技能」选择参与评分的技能。" : "当前仅计算孔位分，可点「编辑技能」添加技能。", AutoSize = true, Margin = new Padding(5) });
            int rows = count < 3 ? 1 : (count <= 6 ? 2 : 3);
            var chip = selectedSkillsPanel.Controls[0];
            int rowHeight = chip.GetPreferredSize(Size.Empty).Height + chip.Margin.Vertical;
            selectedSkillsPanel.Height = rows * rowHeight + selectedSkillsPanel.Padding.Vertical + 2 * SystemInformation.BorderSize.Height;
        }
        UpdateState();
    }
    void AdjustSkills()
    {
        var preset = Preset;
        if (preset != null)
        {
            string weapon = (string)weapons.SelectedItem;
            var defaults = Recommendations.ForWeapon(weapon).SelectMany(target => target.skills.Keys).Distinct();
            using (var picker = new SkillSelector(weapon, Ratings.Skills, preset.DefaultSkills, defaults))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                recommendedEdits.Apply(weapon, picker.SelectedSkills, picker.RestoredDefaults);
                RefreshRecommendedSelection();
            }
            return;
        }
        if (Profile != CustomRating) return;
        using (var picker = new SkillSelector(Profile, Ratings.Skills.Except(custom)))
        {
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            custom.UnionWith(picker.SelectedSkills);
            DisplaySkills();
        }
    }
    BuildTarget[] SelectedTargets()
    {
        var preset = Preset;
        return preset == null ? null : preset.Targets;
    }
    void SetButton(Button button, string text, Action action, bool primary)
    {
        button.Text = text; button.AutoSize = true; button.FlatStyle = FlatStyle.Flat; button.Padding = new Padding(8, 3, 8, 3);
        button.BackColor = primary ? green : Color.White; button.ForeColor = primary ? Color.White : ForeColor;
        button.FlatAppearance.BorderColor = primary ? green : Color.FromArgb(175, 188, 181);
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
            SetStatus("正在读取装备箱…");
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
        toolTabs.Enabled = profiles.Enabled = weapons.Enabled = selectedSkillsPanel.Enabled = ready;
        adjust.Enabled = ready && (Preset != null || (Profile == CustomRating && custom.Count < Ratings.Skills.Length));
    }
    async Task ShowPreview()
    {
        var inventory = (Inventory)characters.SelectedItem;
        string mode = Profile, weapon = (string)weapons.SelectedItem;
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
            string label = recommended ? mode + " · " + weapon : mode;
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
                (!decorationsOnly && Profile == Recommendations.Mode ? (string)weapons.SelectedItem + "推荐-" : "") + DateTime.Now.ToString("yyyyMMdd-HHmmss")
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string mode = Profile, weapon = (string)weapons.SelectedItem, path = dialog.FileName;
            var selected = new HashSet<string>(custom);
            var builds = SelectedTargets();
            writingFile = true;
            UpdateState();
            SetStatus("正在生成 Excel…");
            try
            {
                await Task.Run(() => {
                    if (decorationsOnly) ExcelExport.SaveDecorations(path, inventory);
                    else if (builds != null) ExcelExport.SaveTargets(path, inventory, weapon, builds);
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
