using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>The first thing in an animation file that its other format would not keep.</summary>
/// <param name="What">What it is: <c>&lt;ColorKey&gt;</c> in an <c>.achx</c>, <c>animationChains[0].name</c> in an <c>.achj</c>.</param>
/// <param name="Where">The animation and frame that hold it, for an <c>.achx</c> element that is read; null otherwise.</param>
public sealed record ConversionLoss(string What, string? Where = null)
{
    /// <summary>What the <c>.achj</c> of <paramref name="list"/>, read from <paramref name="source"/>, would not keep, or null.</summary>
    public static ConversionLoss? ToAchj(XDocument source, AnimationChainListSave list, AchxElementCheck check)
    {
        if (check.FirstUnread(source, list) is { } unread) return new ConversionLoss(unread);
        var readBack = AnimationChainListSave.FromJsonString(list.ToJsonString());
        return AnimationListComparison.FirstDifference(list, readBack) is { } where
            ? new ConversionLoss(AnimationListComparison.FirstDifferingElement(list, readBack), where)
            : null;
    }

    /// <summary>What the <c>.achx</c> of <paramref name="list"/>, read from the <c>.achj</c> text <paramref name="source"/>, would not keep, or null.</summary>
    public static ConversionLoss? ToAchx(string source, AnimationChainListSave list)
    {
        if (AchjPropertyCheck.FirstUnread((JsonObject)JsonNode.Parse(source)!, list) is { } unread) return new ConversionLoss(unread);

        // As the file would hold it: ToXmlString has other line breaks.
        using var file = new MemoryStream();
        list.Save(file);
        file.Position = 0;
        return AnimationListComparison.FirstDifferingProperty(list, AnimationChainListSave.FromStream(file)) is { } property
            ? new ConversionLoss(property)
            : null;
    }
}
