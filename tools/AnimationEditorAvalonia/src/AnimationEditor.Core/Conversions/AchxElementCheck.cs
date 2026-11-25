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

    /// <summary>
    /// The first element or attribute of <paramref name="source"/> that <paramref name="list"/>, read from it, does not hold:
    /// "&lt;Custom&gt;", or null. An element without any content holds nothing to lose.
    /// </summary>
    public string? FirstUnread(XDocument source, AnimationChainListSave list) =>
        FirstUnread(source.Root!, XElement.Parse(list.ToXmlString()));

    private string? FirstUnread(XElement source, XElement written)
    {
        if (source.Attributes().FirstOrDefault(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Xsi) is { } unread)
            return $"the attribute {unread.Name.LocalName}";
        foreach (var group in source.Elements().GroupBy(element => element.Name))
        {
            var pairs = written.Elements(group.Key).ToList();
            int i = 0;
            foreach (var element in group)
            {
                // What is written back is compared inside. What is not is a default, left out, when it is read at all.
                if (i < pairs.Count)
                {
                    if (FirstUnread(element, pairs[i++]) is { } inside) return inside;
                }
                else if (HasContent(element) && (pairs.Count > 0 || !IsRead(element)))
                {
                    return $"<{element.Name.LocalName}>";
                }
            }
        }
        return null;
    }

    private static bool HasContent(XElement element) => element.HasElements || element.HasAttributes || element.Value.Length > 0;

    private bool IsRead(XElement element) => _isRead.GetOrAdd(
        string.Join('/', element.AncestorsAndSelf().Reverse().Select(part => part.Name)), _ => ChangesWhatIsRead(element));

    // An element is read when some value of it makes a difference, which an unreadable value does too.
    private static bool ChangesWhatIsRead(XElement element)
    {
        string? without = Read(element, null);
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
