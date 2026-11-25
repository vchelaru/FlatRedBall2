using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>
/// Compares two animation lists field by field, as the <c>.achx</c> format writes them. What an <c>.achx</c> only carries
/// (<see cref="AchxElementCheck.IsOnlyCarried"/>) is not compared.
/// </summary>
public static class AnimationListComparison
{
    /// <summary>Where <paramref name="other"/> first differs from <paramref name="original"/> ("animation 'Walk', frame 2"), or null when it does not.</summary>
    public static string? FirstDifference(AnimationChainListSave original, AnimationChainListSave other)
    {
        XElement from = Fields(original), to = Fields(other);
        if (XNode.DeepEquals(from, to)) return null;

        var (chains, otherChains) = (from.Elements("AnimationChain").ToList(), to.Elements("AnimationChain").ToList());
        if (chains.Count != otherChains.Count) return $"{otherChains.Count} animations instead of {chains.Count}";
        for (int i = 0; i < chains.Count; i++)
        {
            if (XNode.DeepEquals(chains[i], otherChains[i])) continue;
            string chain = $"animation '{original.AnimationChains[i].Name}'";
            var (frames, otherFrames) = (chains[i].Elements("Frame").ToList(), otherChains[i].Elements("Frame").ToList());
            if (frames.Count != otherFrames.Count) return $"{chain}, {otherFrames.Count} frames instead of {frames.Count}";
            int frame = Enumerable.Range(0, frames.Count).FirstOrDefault(index => !XNode.DeepEquals(frames[index], otherFrames[index]), -1);
            return frame < 0 ? chain : $"{chain}, frame {frame + 1}";
        }
        return "the settings of the file";
    }

    /// <summary>The first element that differs where <see cref="FirstDifference"/> finds a difference: "&lt;ColorKey&gt;".</summary>
    internal static string FirstDifferingElement(AnimationChainListSave original, AnimationChainListSave other) =>
        $"<{FirstDiffering(Fields(original), Fields(other)).Name.LocalName}>";

    private static XElement FirstDiffering(XElement from, XElement to)
    {
        foreach (var group in from.Elements().GroupBy(element => element.Name))
        {
            var pairs = to.Elements(group.Key).ToList();
            int i = 0;
            foreach (var element in group)
            {
                if (i == pairs.Count) return element;
                var pair = pairs[i++];
                if (!XNode.DeepEquals(element, pair)) return FirstDiffering(element, pair);
            }
        }
        return from;
    }

    /// <summary>
    /// The path of the first property, as the <c>.achj</c> format writes them, that differs ("animationChains[0].name"), or
    /// null when none does. Unlike an <c>.achx</c>, an <c>.achj</c> keeps every text as it is.
    /// </summary>
    internal static string? FirstDifferingProperty(AnimationChainListSave original, AnimationChainListSave other) =>
        FirstDiffering(JsonNode.Parse(original.ToJsonString())!, JsonNode.Parse(other.ToJsonString()))?.GetPath()[2..];

    private static JsonNode? FirstDiffering(JsonNode from, JsonNode? to)
    {
        if (JsonNode.DeepEquals(from, to)) return null;
        var inside = (from, to) switch
        {
            (JsonObject properties, JsonObject others) => properties.Where(property => property.Value is not null)
                .Select(property => FirstDiffering(property.Value!, others[property.Key])),
            (JsonArray items, JsonArray others) when items.Count == others.Count => items.Zip(others, (item, pair) => FirstDiffering(item!, pair)),
            _ => [],
        };
        return inside.FirstOrDefault(node => node is not null) ?? from;
    }

    // An .achx writes an empty text as an empty element or as none, whichever it was read as, and both mean the same. So does
    // a polygon's name before its points or after.
    private static XElement Fields(AnimationChainListSave list)
    {
        var root = XElement.Parse(list.ToXmlString());
        root.Descendants().Where(AchxElementCheck.IsOnlyCarried).Remove();
        foreach (var name in root.Descendants("PolygonSave").Elements("Name").ToList())
        {
            var polygon = name.Parent!;
            name.Remove();
            polygon.AddFirst(name);
        }
        root.Descendants().Where(element => element is { HasElements: false, Value.Length: 0 }).Remove();
        return root;
    }
}
