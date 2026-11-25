using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>Compares two animation lists field by field, as the <c>.achx</c> format writes them.</summary>
public static class AnimationListComparison
{
    /// <summary>Where <paramref name="other"/> first differs from <paramref name="original"/> ("animation 'Walk', frame 2"), or null when it does not.</summary>
    public static string? FirstDifference(AnimationChainListSave original, AnimationChainListSave other)
    {
        XElement from = XElement.Parse(original.ToXmlString()), to = XElement.Parse(other.ToXmlString());
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
}
