using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>Picks names for new and copied sequences that no existing one has.</summary>
public static class NameGenerator
{
    /// <summary>
    /// <paramref name="baseName"/> when free, else the first free "<paramref name="baseName"/> 2", "... 3", and so on.
    /// Free ignores case, and a gap left by a deletion is filled.
    /// </summary>
    public static string Unique(IEnumerable<string> usedNames, string baseName)
    {
        var used = usedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return used.Contains(baseName) ? Numbered(used, baseName) : baseName;
    }

    /// <summary>
    /// The name for a copy of <paramref name="name"/>: the name itself when free, else its first free numbered form.
    /// A copy of "Idle 2" is "Idle 3", not "Idle 2 2": a trailing number counts as the copy number.
    /// </summary>
    public static string ForCopy(IEnumerable<string> usedNames, string name)
    {
        var used = usedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return used.Contains(name) ? Numbered(used, WithoutNumber(name)) : name;
    }

    private static string Numbered(HashSet<string> used, string baseName)
    {
        int suffix = 2;
        while (used.Contains($"{baseName} {suffix}")) suffix++;
        return $"{baseName} {suffix}";
    }

    // "Idle 2" is "Idle"; a name with nothing before its number, like "7", stays whole.
    private static string WithoutNumber(string name)
    {
        int space = name.LastIndexOf(' ');
        if (space <= 0 || space == name.Length - 1) return name;
        return name.AsSpan(space + 1).ToString().All(char.IsAsciiDigit) ? name[..space].TrimEnd() : name;
    }
}
