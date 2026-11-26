using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class SkillSelection
{
    internal readonly BuildTarget[] Targets;
    internal readonly string[] DefaultSkills;
    internal readonly bool IsEdited;

    internal SkillSelection(BuildTarget[] targets, bool isEdited = false)
    {
        Targets = targets;
        IsEdited = isEdited;
        DefaultSkills = targets.SelectMany(build => build.skills.Keys.Select(skill => new {
                skill, priority = build.DisplayPriority(skill)
            }))
            .GroupBy(entry => entry.skill)
            .OrderByDescending(group => group.Max(entry => entry.priority))
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.CurrentCulture)
            .Select(group => group.Key).ToArray();
    }
}

internal sealed class RecommendedSkillEdits
{
    // The weapon and all-weapon views refer to the same built-in targets.
    // Replace the edited target as a whole so previews can keep an immutable snapshot.
    readonly Dictionary<BuildTarget, BuildTarget> overrides = new Dictionary<BuildTarget, BuildTarget>();

    BuildTarget Current(BuildTarget original)
    {
        BuildTarget edited;
        return overrides.TryGetValue(original, out edited) ? edited : original;
    }

    internal SkillSelection ForScope(string weapon, string style)
    {
        var originals = Recommendations.ForScope(weapon, style);
        return new SkillSelection(originals.Select(Current).ToArray(), originals.Any(overrides.ContainsKey));
    }

    internal void Apply(string weapon, string style, IEnumerable<string> selected, bool restoreDefaults)
    {
        var originals = Recommendations.ForScope(weapon, style);
        var baseline = restoreDefaults ? originals : originals.Select(Current).ToArray();
        var desired = new HashSet<string>(selected);
        var additions = desired.Except(baseline.SelectMany(target => target.skills.Keys)).ToArray();
        var nativeSkills = new HashSet<string>(originals.SelectMany(target => target.skills.Keys));

        for (int i = 0; i < originals.Length; i++)
        {
            var original = originals[i];
            var skills = new HashSet<string>(baseline[i].skills.Keys.Where(desired.Contains));
            // Use the exact default targets when restoring a skill, including its element.
            // A skill outside this scope's presets is new to every target in the scope.
            skills.UnionWith(additions.Where(skill => !nativeSkills.Contains(skill) || original.skills.ContainsKey(skill)));

            if (skills.SetEquals(original.skills.Keys)) overrides.Remove(original);
            else if (!skills.SetEquals(Current(original).skills.Keys))
                overrides[original] = new BuildTarget {
                    weapon = original.weapon, Style = original.Style, name = original.name, basis = original.basis,
                    displayPriority = original.displayPriority,
                    skills = skills.ToDictionary(skill => skill, Recommendations.SkillCap)
                };
        }
    }
}
