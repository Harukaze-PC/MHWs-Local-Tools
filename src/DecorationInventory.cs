using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal class DecorationInfo
{
    public string name { get; set; }
    public string kind { get; set; }
    public int slot { get; set; }
    public Skill[] skills { get; set; }
}

internal sealed class DecorationRow : DecorationInfo
{
    internal int id, freeCount, mountedCount;
    internal int totalCount { get { return checked(freeCount + mountedCount); } }
    internal string TypeLabel { get { return kind == "weapon" ? "武器" : (kind == "armor" ? "防具" : "待核对"); } }
    internal string SkillsLabel { get { return skills == null ? "" : string.Join("、", skills.Select(s => s.name + " Lv" + s.level)); } }
}

internal static class DecorationInventory
{
    static readonly Dictionary<int, DecorationInfo> Catalog = CharmFiles.Serializer()
        .Deserialize<Dictionary<string, DecorationInfo>>(CharmFiles.Resource("decorations"))
        .ToDictionary(entry => int.Parse(entry.Key, CultureInfo.InvariantCulture), entry => entry.Value);

    internal static int CatalogCount { get { return Catalog.Count; } }

    static DecorationRow MakeRow(int id, DecorationInfo info, int freeCount, int mountedCount)
    {
        return new DecorationRow {
            id = id, name = info == null ? "未知装饰珠 #" + id : info.name,
            kind = info == null ? null : info.kind, slot = info == null ? 0 : info.slot,
            skills = info == null ? null : info.skills, freeCount = freeCount, mountedCount = mountedCount
        };
    }

    internal static List<DecorationRow> IncludingUnowned(IEnumerable<DecorationRow> owned)
    {
        var rows = owned.ToList();
        var ownedIds = new HashSet<int>(rows.Select(row => row.id));
        foreach (var entry in Catalog)
            if (!ownedIds.Contains(entry.Key))
                rows.Add(MakeRow(entry.Key, entry.Value, 0, 0));
        return rows;
    }

    internal static List<DecorationRow> ForCharacter(OfflineCharacter character)
    {
        var free = character.DecorationCounts;
        var mounted = character.MountedDecorationCounts;
        var rows = new List<DecorationRow>();
        foreach (int id in free.Keys.Union(mounted.Keys))
        {
            int freeCount, mountedCount;
            free.TryGetValue(id, out freeCount);
            mounted.TryGetValue(id, out mountedCount);
            if (freeCount == 0 && mountedCount == 0) continue;
            DecorationInfo info;
            Catalog.TryGetValue(id, out info);
            rows.Add(MakeRow(id, info, freeCount, mountedCount));
        }
        return rows;
    }
}
