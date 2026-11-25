using AnimationEditor.Core.Paths;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace AnimationEditor.Core.Tiled;

/// <summary>One element of a <see cref="TsxSource"/>, with its place in the text.</summary>
/// <param name="Name">As written, so a prefixed element never matches one of Tiled's names.</param>
/// <param name="Start">Index of the start tag's <c>&lt;</c>.</param>
internal sealed record TsxElement(string Name, int Start)
{
    /// <summary>Index after the start tag's <c>&gt;</c>.</summary>
    public required int StartTagEnd { get; init; }

    /// <summary>Index of the end tag's <c>&lt;</c>; <see cref="StartTagEnd"/> when <see cref="IsEmpty"/>.</summary>
    public required int EndTagStart { get; set; }

    /// <summary>Index after the element.</summary>
    public required int End { get; set; }

    /// <summary>Written as <c>&lt;a/&gt;</c>.</summary>
    public required bool IsEmpty { get; init; }

    public required IReadOnlyDictionary<string, string> Attributes { get; init; }

    /// <summary>The text directly inside a <c>&lt;property&gt;</c>, with references decoded.</summary>
    public string Text { get; set; } = "";

    public List<TsxElement> Children { get; } = [];

    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);

    public TsxElement? Child(string name) => Children.Find(child => child.Name == name);

    public IEnumerable<TsxElement> ChildrenNamed(string name) => Children.Where(child => child.Name == name);
}

/// <summary>
/// The text of a <c>.tsx</c> and where its elements are. <see cref="TsxWriter"/> changes spans of this text, so
/// everything the editor does not edit keeps its exact bytes.
/// </summary>
public sealed class TsxSource
{
    /// <summary>The elements whose children the editor reads.</summary>
    private static readonly HashSet<string> Containers = ["tileset", "tile", "properties", "animation", "image"];

    private static readonly char[] LineEnds = ['\r', '\n'];

    /// <summary>Without the byte order mark.</summary>
    public string Text { get; }

    public bool HasByteOrderMark { get; }

    /// <summary>The file the text came from. Its relative paths are relative to that file's folder.</summary>
    public FilePath Path { get; }

    internal TsxElement Root { get; }

    internal IReadOnlyDictionary<uint, TsxElement> TilesById { get; }

    private TsxSource(FilePath path, string text, bool hasByteOrderMark)
    {
        (Path, Text, HasByteOrderMark) = (path, text, hasByteOrderMark);
        Root = ReadElements(text);

        var tiles = new Dictionary<uint, TsxElement>();
        foreach (var tile in Root.ChildrenNamed("tile"))
            if (!tiles.TryAdd(TsxLoader.Required(tile, "id", XmlConvert.ToUInt32), tile))
                throw new InvalidOperationException(
                    $"The tileset has more than one tile with id {tile.Attribute("id")}, which isn't valid Tiled data.");
        TilesById = tiles;
    }

    /// <exception cref="NotSupportedException">The file is not UTF-8.</exception>
    /// <exception cref="XmlException">The file is not well-formed, or has a DOCTYPE.</exception>
    public static TsxSource Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!TsxCompatibilityChecker.CheckEncoding(bytes, out var blockingReason))
            throw new NotSupportedException(blockingReason);
        var hasByteOrderMark = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(bytes.AsSpan(hasByteOrderMark ? Encoding.UTF8.Preamble.Length : 0));
        return new TsxSource(new FilePath(path), text, hasByteOrderMark);
    }

    /// <summary>This file after a save replaced its text.</summary>
    internal TsxSource WithText(string text) => new(Path, text, HasByteOrderMark);

    /// <summary>Every attribute in the text, with the index of its name.</summary>
    internal static IEnumerable<(string Element, string? ElementType, string Name, string Value, int Start)> ReadAttributes(string text)
    {
        var lineStarts = LineStarts(text);
        using var reader = XmlReader.Create(new StringReader(text));
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;
            var (element, type) = (reader.Name, reader.GetAttribute("type"));
            while (reader.MoveToNextAttribute())
                yield return (element, type, reader.Name, reader.Value, Offset(reader, lineStarts));
        }
    }

    private static TsxElement ReadElements(string text)
    {
        var lineStarts = LineStarts(text);
        using var reader = XmlReader.Create(new StringReader(text));
        TsxElement? root = null;
        var open = new Stack<TsxElement>();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                // A child of an element that is not a container is not pushed, so the depths differ inside it.
                if (root is not null && (open.Count != reader.Depth || !Containers.Contains(open.Peek().Name)))
                    continue;

                var start = Offset(reader, lineStarts) - 1;
                var startTagEnd = StartTagEnd(text, start);
                var (name, isEmpty) = (reader.Name, reader.IsEmptyElement);
                var attributes = new Dictionary<string, string>(reader.AttributeCount);
                while (reader.MoveToNextAttribute())
                    attributes[reader.Name] = reader.Value;
                var element = new TsxElement(name, start)
                {
                    StartTagEnd = startTagEnd, EndTagStart = startTagEnd, End = startTagEnd, IsEmpty = isEmpty, Attributes = attributes,
                };
                if (root is null)
                    root = element;
                else
                    open.Peek().Children.Add(element);
                if (!isEmpty)
                    open.Push(element);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && open.Count == reader.Depth + 1)
            {
                var element = open.Pop();
                element.EndTagStart = Offset(reader, lineStarts) - "</".Length;
                element.End = text.IndexOf('>', element.EndTagStart) + 1;
            }
            else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA && open.Count == reader.Depth
                && open.Peek() is { Name: "property" } property)
                property.Text += reader.Value;
        }
        return root!;
    }

    /// <summary>The index of the name the reader is on.</summary>
    private static int Offset(XmlReader reader, List<int> lineStarts)
    {
        var info = (IXmlLineInfo)reader;
        return lineStarts[info.LineNumber - 1] + info.LinePosition - 1;
    }

    /// <summary>Line starts as <see cref="XmlReader"/> counts them: CRLF, LF and CR each end a line.</summary>
    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = text.IndexOfAny(LineEnds); i >= 0; i = text.IndexOfAny(LineEnds, i))
        {
            i += text.AsSpan(i).StartsWith("\r\n") ? 2 : 1;
            starts.Add(i);
        }
        return starts;
    }

    private static int StartTagEnd(string text, int start)
    {
        var i = start;
        while (text[i] != '>')
            i = text[i] is '"' or '\'' ? text.IndexOf(text[i], i + 1) + 1 : i + 1;
        return i + 1;
    }
}
