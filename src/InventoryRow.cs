using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class InventoryRow
{
    internal Charm Charm;
    internal int? Score;
    internal string ScoreDetail, BuildNames, Weapons;
    string matchNote;
    internal string Detail { get { return matchNote == null ? ScoreDetail : matchNote + ScoreDetail; } }

    internal static InventoryRow[] Create(Inventory inventory, HashSet<string> selected, BuildTarget[] builds, bool allWeapons)
    {
        return inventory.talismans.Select(charm => MakeRow(charm, selected, builds, allWeapons))
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Charm.name, StringComparer.CurrentCulture)
            .ThenBy(row => String.Join("、", row.Charm.skills.Select(skill => skill.name + " Lv" + skill.level)), StringComparer.CurrentCulture)
            .ToArray();
    }

    static InventoryRow MakeRow(Charm charm, HashSet<string> selected, BuildTarget[] builds, bool allWeapons)
    {
        var row = new InventoryRow { Charm = charm };
        if (selected == null && builds == null) return row;
        if (charm.warnings.Count > 0 ||
            charm.slots.Any(slot => !(slot.type == "weapon" && slot.level == 1 ||
                                      slot.type == "armor" && slot.level >= 1 && slot.level <= 3)))
        {
            row.ScoreDetail = "待核对：部分护石数据未识别";
            return row;
        }
        var best = builds == null ? Ratings.Score(charm, selected.Contains) : BuildFit.Best(charm, builds, allWeapons);
        if (best.Unpriced.Length == 0) row.Score = best.Score;
        row.matchNote = best.MatchNote;
        row.ScoreDetail = best.Detail;
        row.BuildNames = best.Names;
        row.Weapons = best.Weapons;
        return row;
    }

    internal static string SocketName(string type)
    {
        switch (type) {
            case "weapon": return "武器孔";
            case "armor": return "防具孔";
            case "universal": return "通用孔";
            default: return "未知类型孔";
        }
    }
}
