using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>Finds the parts of an <c>.achx</c> text that reading it leaves out. Safe to use from several threads.</summary>
public sealed class AchxElementCheck
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    // By the element's path, such as "AnimationChainArraySave/AnimationChain/Frame/RelativeX".
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
                    if (pairs.Count > 0) return $"<{element.Name.LocalName}>";
                    if (FirstUnreadByProbing(element) is { } inside) return inside;
                }
            }
        }
        return null;
    }

    private string? FirstUnreadByProbing(XElement element)
    {
        if (!IsRead(element)) return $"<{element.Name.LocalName}>";
        if (UnreadAttribute(element) is { } unread) return unread;
        return element.Elements().Where(HasContent).Select(FirstUnreadByProbing).FirstOrDefault(inside => inside is not null);
    }

    private static string? UnreadAttribute(XElement element) =>
        element.Attributes().FirstOrDefault(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Xsi) is { } unread
            ? $"the attribute {unread.Name.LocalName}"
            : null;

    private static bool HasContent(XElement element) => element.HasElements || element.HasAttributes || element.Value.Length > 0;

    private bool IsRead(XElement element)
    {
        string path = string.Join('/', element.AncestorsAndSelf().Reverse().Select(part => part.Name));
        if (_isRead.TryGetValue(path, out bool known)) return known;
        bool isRead = ChangesWhatIsRead(element);
        // Other elements inside it may make a difference where these do not.
        return isRead || !element.HasElements ? _isRead[path] = isRead : isRead;
    }

    // An element is read when some value of it makes a difference, which an unreadable value does too, or the elements inside it do.
    private static bool ChangesWhatIsRead(XElement element)
    {
        string? without = Read(element, null);
        if (element.HasElements && Read(element.Document!.Root!) != without) return true;
        return without is null || new[] { "7", "true", "false", "x" }.Any(value => Read(element, value) != without);
    }

    /// <summary>The list read from the document of <paramref name="element"/> with that element's content replaced, or removed for null; null when it cannot be read.</summary>
    private static string? Read(XElement element, string? value)
    {
        var copy = new XElement(element.AncestorsAndSelf().Last());
        foreach (int index in element.AncestorsAndSelf().Reverse().Skip(1).Select(part => part.ElementsBeforeSelf().Count()))
            copy = copy.Elements().ElementAt(index);
        var root = copy.AncestorsAndSelf().Last();
        if (value is null) copy.Remove();
        else copy.ReplaceNodes(value);
        return Read(root);
    }

    private static string? Read(XElement root)
    {
        try
        {
            return AnimationChainListSave.FromString(root.ToString()).ToXmlString();
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        {
            return null;
        }
    }
}
