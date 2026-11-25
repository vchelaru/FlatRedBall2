using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>Finds the parts of an <c>.achx</c> text that reading it leaves out. Safe to use from several threads.</summary>
public sealed class AchxElementCheck
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    // The kinds of element found to be read, see KindOf.
    private readonly ConcurrentDictionary<string, bool> _isRead = new();

    /// <summary>Whether <paramref name="source"/> has XML comments, which no save keeps.</summary>
    public static bool HasComments(XDocument source) => source.DescendantNodes().OfType<XComment>().Any();

    /// <summary>
    /// The first element or attribute of <paramref name="source"/> that <paramref name="list"/>, read from it, does not hold:
    /// "&lt;Custom&gt;", or null. An element without any content holds nothing to lose.
    /// </summary>
    public string? FirstUnread(XDocument source, AnimationChainListSave list) =>
        FirstUnread(source.Root!, XElement.Parse(list.ToXmlString()));

    private string? FirstUnread(XElement source, XElement written)
    {
        if (UnreadAttribute(source) is { } unread) return unread;
        foreach (var group in source.Elements().GroupBy(element => element.Name))
        {
            var pairs = written.Elements(group.Key).ToList();
            int i = 0;
            foreach (var element in group)
            {
                // What is written back is compared inside. What is not is a default, left out, or in another element, when it is read at all.
                if (i < pairs.Count)
                {
                    if (FirstUnread(element, pairs[i++]) is { } inside) return inside;
                }
                else if (HasContent(element))
                {
                    if (pairs.Count > 0) return Named(element);
                    if (FirstUnreadByProbing(element) is { } inside) return inside;
                }
            }
        }
        // Elements read from another place are written in the place of these, so these were not compared with their own.
        return source.Elements().Any(element => element.HasElements && !written.Elements(element.Name).Any())
            && source.Elements().FirstOrDefault(element => HasContent(element) && written.Elements(element.Name).Any()) is { } replaced
                ? Named(replaced)
                : null;
    }

    private string? FirstUnreadByProbing(XElement element)
    {
        if (!IsRead(element)) return Named(element);
        if (UnreadAttribute(element) is { } unread) return unread;
        if (element.HasElements && element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value))) return $"the text in {Named(element)}";
        return element.Elements().Where(HasContent).Select(FirstUnreadByProbing).FirstOrDefault(inside => inside is not null);
    }

    private static string Named(XElement element) => $"<{element.Name.LocalName}>";

    private static string? UnreadAttribute(XElement element) =>
        element.Attributes().FirstOrDefault(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Xsi) is { } unread
            ? $"the attribute {unread.Name.LocalName}"
            : null;

    private static bool HasContent(XElement element) => element.HasElements || element.HasAttributes || element.Value.Length > 0;

    private bool IsRead(XElement element)
    {
        string kind = KindOf(element);
        if (_isRead.ContainsKey(kind)) return true;
        // One that is not read ends the check of its file, so only the others are asked about again.
        if (!ChangesWhatIsRead(element)) return false;
        return _isRead[kind] = true;
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
