using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>One thing in an animation file that its other format does not keep, because the AnimationEditor does not read it or only carries it.</summary>
/// <param name="What">What it is: <c>&lt;ColorKey&gt;</c> in an <c>.achx</c>, <c>"custom"</c> in an <c>.achj</c>.</param>
public sealed record ConversionDrop(string What, ConversionPlace Place)
{
    private const int MaxNamesShown = 3;

    /// <summary>One sentence for each kind of thing, saying how often and where it is: "&lt;ColorKey&gt; in 2 animations ('Idle' and 'Walk') is not kept".</summary>
    public static IReadOnlyList<string> Describe(IEnumerable<ConversionDrop> drops) =>
        drops.GroupBy(drop => (drop.What, drop.Place.IsInAnimation, drop.Place.IsInFrame))
            .Select(group => $"{group.Key.What}{Where(group.Select(drop => drop.Place).Distinct().ToList())} is not kept")
            .ToList();

    private static string Where(IReadOnlyList<ConversionPlace> places)
    {
        var first = places[0];
        if (!first.IsInAnimation) return "";
        if (places.Count == 1) return $" in animation '{first.Name}'{(first.IsInFrame ? $", frame {first.Frame}" : "")}";
        var names = places.DistinctBy(place => place.Animation).Select(place => $"'{place.Name}'").ToList();
        string frames = first.IsInFrame ? $"{places.Count} frames of " : "";
        return names.Count == 1 ? $" in {frames}animation {names[0]}" : $" in {frames}{names.Count} animations ({Shown(names)})";
    }

    private static string Shown(IReadOnlyList<string> names) => names.Count > MaxNamesShown
        ? $"{string.Join(", ", names.Take(MaxNamesShown))} and {names.Count - MaxNamesShown} more"
        : $"{string.Join(", ", names.SkipLast(1))} and {names[^1]}";
}

/// <summary>Where in an animation file something is: in the file itself, in one animation, or in one frame of it.</summary>
/// <param name="Animation">Which animation, from 1; 0 for the file itself.</param>
/// <param name="Frame">Which frame of it, from 1; 0 for the animation itself.</param>
public readonly record struct ConversionPlace(int Animation, string Name, int Frame)
{
    public bool IsInAnimation => Animation > 0;
    public bool IsInFrame => Frame > 0;
}
