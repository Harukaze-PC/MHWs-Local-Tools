using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BuildTarget
{
    public string weapon { get; set; }
    public string name { get; set; }
    public string basis { get; set; }
    public Dictionary<string,int> skills { get; set; }
    // Priorities order the skills shown for a preset. The score uses membership only.
    internal Dictionary<string,int> displayPriority;

    internal int DisplayPriority(string skill)
    {
        int rank;
        return displayPriority != null && displayPriority.TryGetValue(skill, out rank) ? rank : 1;
    }

}

internal sealed class BuildMatch
{
    internal int Score, Innate;
    internal string Weapons, Names, MatchNote, Terms, Unmatched;
    internal string[] Unpriced;

    internal string Detail
    {
        get
        {
            string sum = Terms.Length == 0 ? "0分" : Terms + " = " + Score + "分";
            if (Unpriced.Length > 0) return "已知小计：" + sum + "；未定价：" + String.Join("、", Unpriced);
            return sum + (Unmatched.Length == 0 ? "" : "；未命中技能：" + Unmatched);
        }
    }
}

internal static class BuildFit
{
    internal const string AllWeapons = "全部武器";
    internal static readonly string[] Weapons = {
        "大剑","太刀","片手剑","双剑","大锤","狩猎笛","长枪","铳枪","斩斧","盾斧","操虫棍","轻弩炮","重弩炮","弓"
    };

    // Attribute variants often tie. Keep every matching element visible on one line.
    static string DisplayNames(IEnumerable<string> names)
    {
        var groups = new List<string>();
        var attributes = new Dictionary<string,List<string>>();
        foreach (var name in names)
        {
            var parts = name.Split('·');
            int i = Array.FindIndex(parts, part => part.Length == 1 && "火水雷冰龙".Contains(part));
            if (i < 0) { groups.Add(name); continue; }
            string element = parts[i];
            parts[i] = "{0}";
            string key = String.Join("·", parts);
            if (!attributes.ContainsKey(key)) { groups.Add(key); attributes[key] = new List<string>(); }
            attributes[key].Add(element);
        }
        return String.Join("、", groups.Select(key => attributes.ContainsKey(key)
            ? String.Format(key, String.Join("/", attributes[key])) : key));
    }

    internal static BuildMatch Evaluate(Charm item, BuildTarget build)
    {
        var match = Ratings.Score(item, build.skills.ContainsKey);
        match.Weapons = build.weapon;
        match.Names = build.name;
        return match;
    }

    internal static BuildMatch Best(Charm item, BuildTarget[] builds, bool allWeapons = false)
    {
        var matches = builds.Select(b => Evaluate(item, b)).OrderByDescending(m => m.Score).ToList();
        var best = matches.First();
        // A candidate with an unpriced match could outrank the highest known subtotal.
        var missing = matches.SelectMany(m => m.Unpriced).Distinct().ToArray();
        if (missing.Length > 0)
        {
            best.Unpriced = missing;
            best.Names = "待核对";
            best.Weapons = "待核对";
        }
        else if (best.Innate == 0)
        {
            best.Names = "无技能命中";
            best.Weapons = best.Score > 0 ? "不限武器" : "无";
        }
        else
        {
            var tied = matches.Where(m => m.Score == best.Score).ToList();
            if (allWeapons)
            {
                string example = best.Weapons + " · " + best.Names;
                best.Weapons = String.Join("、", Weapons.Where(w => tied.Any(m => m.Weapons == w)));
                best.Names = example;
                if (tied.Count > 1) best.MatchNote = tied.Count + " 个玩法目标同分。以「" + example + "」为例：";
            }
            else if (tied.Count > 1)
            {
                best.MatchNote = "以「" + best.Names + "」为例：";
                best.Names = DisplayNames(tied.Select(m => m.Names));
            }
        }
        return best;
    }
}
