using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>What converting an animation file to its other format does not keep.</summary>
/// <param name="Drops">What it leaves out. The file still converts.</param>
/// <param name="Difference">What the AnimationEditor reads and would come out different, or null. Such a file is not converted.</param>
public sealed record ConversionLoss(IReadOnlyList<ConversionDrop> Drops, ConversionDifference? Difference)
{
    /// <summary>What the <c>.achj</c> of <paramref name="list"/>, read from <paramref name="source"/>, does not keep.</summary>
    public static ConversionLoss ToAchj(XDocument source, AnimationChainListSave list, AchxElementCheck check)
    {
        var readBack = AnimationChainListSave.FromJsonString(list.ToJsonString());
        return new ConversionLoss(check.Drops(source, list), AnimationListComparison.FirstDifference(list, readBack) is { } where
            ? new ConversionDifference(AnimationListComparison.FirstDifferingElement(list, readBack), where)
            : null);
    }

    /// <summary>What saving <paramref name="list"/> as an <c>.achj</c> leaves out: what it only carries from an <c>.achx</c>.</summary>
    public static IReadOnlyList<ConversionDrop> OfSavingAsAchj(AnimationChainListSave list)
    {
        try
        {
            return new AchxElementCheck().Drops(XDocument.Parse(list.ToXmlString()), list);
        }
        catch (InvalidDataException) // it holds a text that no .achx can, so it carries nothing from one
        {
            return [];
        }
    }

    /// <summary>What the <c>.achx</c> of <paramref name="list"/>, read from the <c>.achj</c> text <paramref name="source"/>, does not keep.</summary>
    public static ConversionLoss ToAchx(string source, AnimationChainListSave list)
    {
        // As the file would hold it: ToXmlString has other line breaks.
        using var file = new MemoryStream();
        list.Save(file);
        file.Position = 0;
        return new ConversionLoss(AchjPropertyCheck.Drops((JsonObject)JsonNode.Parse(source)!, list),
            AnimationListComparison.FirstDifferingProperty(list, AnimationChainListSave.FromStream(file)) is { } property
                ? new ConversionDifference(property)
                : null);
    }
}

/// <summary>The first thing the AnimationEditor reads that would come out different.</summary>
/// <param name="What">What it is: <c>&lt;RelativeX&gt;</c> in an <c>.achx</c>, <c>animationChains[0].name</c> in an <c>.achj</c>.</param>
/// <param name="Where">The animation and frame that hold it, for an <c>.achx</c>; null for an <c>.achj</c>.</param>
public sealed record ConversionDifference(string What, string? Where = null);
