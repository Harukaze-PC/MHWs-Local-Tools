using System;
using System.Collections.Generic;
using System.Linq;

internal static class SkillCatalog
{
    // Category describes how a hunter uses the skill; kind is its in-game equipment type.
    sealed class Group
    {
        public string category { get; set; }
        public string kind { get; set; }
        public string[] names { get; set; }
    }

    static readonly Group[] Groups = CharmFiles.Serializer().Deserialize<Group[]>(CharmFiles.Resource("skillCategories"));
    static readonly Dictionary<string, Group> ByName = Groups
        .SelectMany(group => group.names.Select(name => new { name, group }))
        .ToDictionary(entry => entry.name, entry => entry.group, StringComparer.Ordinal);

    internal static readonly string[] Skills = Recommendations.SelectableSkills
        .OrderBy(name => name, StringComparer.CurrentCulture).ToArray();
    internal static readonly string[] AllSkills = CharmFiles.Serializer()
        .Deserialize<Dictionary<string, string>>(CharmFiles.Resource("skills")).Values
        .Union(Skills).OrderBy(name => name, StringComparer.CurrentCulture).ToArray();
    internal static readonly string[] Categories = Groups.Select(group => group.category)
        .Concat(AllSkills.Where(name => !ByName.ContainsKey(name)).Select(name => "待分类"))
        .Distinct().ToArray();

    internal static string Category(string name)
    {
        Group group;
        return ByName.TryGetValue(name, out group) ? group.category : "待分类";
    }

    internal static string Kind(string name)
    {
        Group group;
        return ByName.TryGetValue(name, out group) ? group.kind : "待核对";
    }
}
