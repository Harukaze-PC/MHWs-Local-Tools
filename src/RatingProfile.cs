using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class SkillPrice
{
    public string kind { get; set; }
    public int slot { get; set; }
    public string jewel { get; set; }
    public bool noJewel { get; set; }
}

internal static class Ratings
{
    internal const int WeaponWeight = 3;
    internal const string SkillRule = "只计算选中的技能。武器技能每级 3 分。防具技能每级按补 1 级所需的单技能珠孔级计分。";
    internal const string NoJewelRule = "属性变换按 ZY 示例计 3 分/级。本工具也将已核实无珠的属性吸收、属性异常耐性、饭团定为 3 分/级。";
    internal const string SlotRule = "每个武器 1 级孔计 3 分，防具孔按孔级分别计 1、2、3 分。";
    internal const string WeightSource = "https://www.bilibili.com/video/BV12gbYzdEnR/?t=170";
    static readonly Dictionary<string,SkillPrice> Prices =
        CharmFiles.Serializer().Deserialize<Dictionary<string,SkillPrice>>(CharmFiles.Resource("skillPrices"));
    internal static readonly string[] Skills = Recommendations.SelectableSkills
        .OrderBy(name => name, StringComparer.CurrentCulture).ToArray();

    internal static int PointsPerLevel(string skill)
    {
        SkillPrice price;
        if (!Prices.TryGetValue(skill, out price)) return 0;
        if (price.kind == "weapon") return WeaponWeight;
        if (price.kind != "armor") return 0;
        if (price.noJewel) return 3;
        return price.slot;
    }

    internal static string PriceLabel(string skill)
    {
        int points = PointsPerLevel(skill);
        return points > 0 ? points + " 分/级" : "未定价";
    }

    internal static string Evidence(string skill)
    {
        SkillPrice price;
        if (!Prices.TryGetValue(skill, out price)) return "待核对";
        if (price.kind == "weapon") return "武器技能固定权重";
        if (price.noJewel) return skill == "属性变换" ? "无装饰珠，按 ZY 示例" : "无装饰珠，本工具定价";
        return price.jewel;
    }

    internal static BuildMatch Score(Charm charm, Predicate<string> includes)
    {
        var terms = new List<string>();
        var unused = new List<string>();
        var unpriced = new List<string>();
        int skillPoints = 0;
        foreach (var skill in charm.skills)
        {
            if (!includes(skill.name)) { unused.Add(skill.name); continue; }
            int unit = PointsPerLevel(skill.name);
            if (unit == 0) { unpriced.Add(skill.name); continue; }
            int points = skill.level * unit;
            skillPoints += points;
            terms.Add(skill.name + " Lv" + skill.level + "×" + unit + "（" + points + "分）");
        }
        int total = skillPoints;
        foreach (var slot in charm.slots)
        {
            bool weapon = slot.type == "weapon";
            int points = weapon ? WeaponWeight : slot.level;
            total += points;
            terms.Add((weapon ? "武器" : "防具") + slot.level + "级孔 " + points + "分");
        }
        return new BuildMatch { Score = total, Innate = skillPoints, Terms = String.Join(" + ", terms),
            Unmatched = String.Join("、", unused), Unpriced = unpriced.Distinct().ToArray() };
    }
}
