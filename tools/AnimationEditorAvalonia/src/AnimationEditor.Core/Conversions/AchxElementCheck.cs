using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>Finds the parts of an <c>.achx</c> text that an <c>.achj</c> does not keep. Safe to use from several threads.</summary>
public sealed class AchxElementCheck
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    // The kinds of element found to be read or not, see KindOf.
    private readonly ConcurrentDictionary<string, bool> _isRead = new();

    /// <summary>Whether <paramref name="source"/> has XML comments, which no save keeps.</summary>
    public static bool HasComments(XDocument source) => source.DescendantNodes().OfType<XComment>().Any();

    /// <summary>
    /// Every element, attribute and text of <paramref name="source"/> that <paramref name="list"/>, read from it, does not hold or
    /// only carries. An element without any content holds nothing to lose.
    /// </summary>
    public IReadOnlyList<ConversionDrop> Drops(XDocument source, AnimationChainListSave list)
    {
        var drops = new List<ConversionDrop>();
        AddDrops(source.Root!, XElement.Parse(list.ToXmlString()), default, drops);
        if (drops.Count == 0) return []; // so that two plans without any are equal
        return drops;
    }

    private void AddDrops(XElement source, XElement written, ConversionPlace place, List<ConversionDrop> drops)
    {
        AddAttributes(source, place, drops);
        // What is read from such an element is written in the place of its siblings, which are not read then.
        bool replacesOthers = source.Elements().Any(element =>
            element.HasElements && !AnimationChainListSave.IsOnlyCarried(element) && !written.Elements(element.Name).Any() && IsRead(element));
        foreach (var group in source.Elements().GroupBy(element => element.Name))
        {
            var pairs = written.Elements(group.Key).ToList();
            int number = 0;
            foreach (var element in group)
            {
                number++;
                var inside = PlaceOf(element, number, place);
                if (pairs.Count == 0)
                    // A default, left out, or written in another element, when it is read at all.
                    AddDropsByProbing(element, inside, drops);
                else if (number <= pairs.Count && !replacesOthers && !AnimationChainListSave.IsOnlyCarried(element))
                    AddDrops(element, pairs[number - 1], inside, drops);
                else
                    AddIfContent(element, inside, drops);
            }
        }
    }

    private void AddDropsByProbing(XElement element, ConversionPlace place, List<ConversionDrop> drops)
    {
        if (!HasContent(element)) return;
        if (AnimationChainListSave.IsOnlyCarried(element) || !IsRead(element))
        {
            AddIfContent(element, place, drops);
            return;
        }
        AddAttributes(element, place, drops);
        if (element.HasElements && element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
            drops.Add(new ConversionDrop($"the text in {Named(element)}", place));
        foreach (var inside in element.Elements()) AddDropsByProbing(inside, place, drops);
    }

    private static void AddIfContent(XElement element, ConversionPlace place, List<ConversionDrop> drops)
    {
        if (HasContent(element)) drops.Add(new ConversionDrop(Named(element), place));
    }

    private static void AddAttributes(XElement element, ConversionPlace place, List<ConversionDrop> drops) =>
        drops.AddRange(UnreadAttributes(element).Select(attribute => new ConversionDrop($"the attribute {attribute.Name.LocalName}", place)));

    internal static ConversionPlace PlaceOf(XElement element, int number, ConversionPlace outer) => element.Name.LocalName switch
    {
        "AnimationChain" when !outer.IsInAnimation => new ConversionPlace(number, (string?)element.Element("Name") ?? "", 0),
        "Frame" when outer is { IsInAnimation: true, IsInFrame: false } => outer with { Frame = number },
        _ => outer,
    };

    internal static string Named(XElement element) => $"<{element.Name.LocalName}>";

    private static IEnumerable<XAttribute> UnreadAttributes(XElement element) =>
        element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Xsi);

    private static bool HasContent(XElement element) => element.HasElements || element.Value.Length > 0 || UnreadAttributes(element).Any();

    private bool IsRead(XElement element)
    {
        string kind = KindOf(element);
        if (_isRead.TryGetValue(kind, out bool known)) return known;
        // Whether leaving elements out makes a difference depends on what they hold, so that it makes none is only known of these.
        string exactKind = element.HasElements ? $"{kind} {string.Join(' ', element.Descendants().Select(part => part.Name).Distinct())}" : kind;
        if (_isRead.ContainsKey(exactKind)) return false;
        bool isRead = ChangesWhatIsRead(element);
        _isRead[isRead ? kind : exactKind] = isRead;
        return isRead;
    }

    // The path, such as "AnimationChainArraySave/AnimationChain/Frame/RelativeX". The first of a name is read where a repeat may
    // not be; when the second is, they are items of a list. Elements inside are read where text may not be.
    private static string KindOf(XElement element) =>
        string.Join('/', element.AncestorsAndSelf().Reverse().Select(part => part.Name))
        + (element.ElementsBeforeSelf(element.Name).Any() ? "#" : "") + (element.HasElements ? "/" : "");

    // Elements are read when leaving them out makes a difference, a text when one text makes a difference from another (unreadable is one).
    private static bool ChangesWhatIsRead(XElement element)
    {
        if (element.HasElements) return Read(element, _ => { }) != Read(element, copy => copy.Remove());
        string? number = Read(element, copy => copy.SetValue("7"));
        return Read(element, copy => copy.SetValue("true")) != number || Read(element, copy => copy.SetValue("x")) != number;
    }

    /// <summary>What is read from a document with a changed copy of <paramref name="element"/> in it; null when it cannot be read.</summary>
    private static string? Read(XElement element, Action<XElement> change)
    {
        // Around it, the first element of each name is enough, and quick to read.
        XElement copy = new(element), outer = copy;
        for (var part = element; part.Parent is { } parent; part = parent)
        {
            var names = new HashSet<XName>();
            var inner = outer;
            outer = new XElement(parent.Name, parent.Attributes(),
                parent.Elements().Where(other => names.Add(other.Name) | other == part).Select(other => other == part ? inner : other));
        }
        change(copy);
        try
        {
            return AnimationChainListSave.FromString(outer.ToString()).ToXmlString();
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        {
            return null;
        }
    }
}
