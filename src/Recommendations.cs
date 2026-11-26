using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class WeaponStyle
{
    public string weapon { get; set; }
    public string name { get; set; }
    public string reason { get; set; }
    public bool elemental { get; set; }
    public Dictionary<string,int> skills { get; set; }
}

internal sealed class RecommendationData
{
    public Dictionary<string,int> caps { get; set; }
    public WeaponStyle[] profiles { get; set; }
}

internal static class Recommendations
{
    internal const string Mode = "按武器/流派推荐", AllStyles = "全部流派";
    static readonly RecommendationData Data = CharmFiles.Serializer().Deserialize<RecommendationData>(CharmFiles.Resource("recommendations"));
    static readonly BuildTarget[] Targets = BuildTargets().ToArray();

    static IEnumerable<BuildTarget> BuildTargets()
    {
        foreach (var style in Data.profiles)
        {
            var elements = style.elemental ? new[] { "火", "水", "雷", "冰", "龙" } : new[] { "" };
            foreach (string element in elements)
            {
                var priority = new Dictionary<string,int>(style.skills);
                if (element.Length > 0) priority[element + "属性攻击强化"] = 3;
                yield return new BuildTarget {
                    weapon = style.weapon, Style = style.name, name = style.name + (element.Length == 0 ? "" : "·" + element),
                    basis = style.reason, displayPriority = priority,
                    skills = priority.Keys.ToDictionary(key => key, key => Data.caps[key])
                };
            }
        }
    }

    internal static BuildTarget[] ForWeapon(string weapon)
    {
        return ForScope(weapon, AllStyles);
    }

    internal static BuildTarget[] ForScope(string weapon, string style)
    {
        if (weapon == BuildFit.AllWeapons) return Targets;
        return Targets.Where(target => target.weapon == weapon &&
            (style == AllStyles || target.Style == style)).ToArray();
    }

    internal static string[] StylesForWeapon(string weapon)
    {
        if (weapon == BuildFit.AllWeapons) return new[] { AllStyles };
        return new[] { AllStyles }.Concat(ForWeapon(weapon).Select(target => target.Style).Distinct()).ToArray();
    }

    internal static string ScopeLabel(string weapon, string style)
    {
        return weapon + " · " + style;
    }

    internal static IEnumerable<string> SelectableSkills { get { return Data.caps.Keys; } }
    internal static int SkillCap(string skill) { return Data.caps[skill]; }
}
