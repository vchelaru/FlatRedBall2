using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Writes the edits the editor makes to a <c>.tsx</c> (a tile's animation and its string and int properties) into
/// the file's own text. Everything else keeps its exact bytes, whether or not the editor knows what it is.
/// </summary>
public sealed class TsxWriter
{
    private readonly TsxSource _source;
    private readonly List<(int Start, int End, string Text)> _edits = [];

    private TsxWriter(TsxSource source) => _source = source;

    /// <summary>Writes the source's text, as <see cref="Render"/> changed it, to a path that may be another file's.</summary>
    /// <returns>What was written.</returns>
    internal static string Write(TsxSource source, string text, FilePath path)
    {
        text = RebasePaths(text, source.Path.GetDirectoryContainingThis(), path.GetDirectoryContainingThis());
        AtomicFile.Write(path.FullPath, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(source.HasByteOrderMark));
            writer.Write(text);
        });
        return text;
    }

    /// <summary>
    /// The source's text with every tile that differs between <paramref name="from"/> and <paramref name="to"/>
    /// changed to what <paramref name="to"/> holds. A tile is removed only when that leaves nothing but its id.
    /// </summary>
    internal static string Render(TsxSource source, Tileset from, Tileset to)
    {
        var writer = new TsxWriter(source);
        var fromTiles = from.Tiles.ToDictionary(tile => tile.ID);
        var toTiles = to.Tiles.ToDictionary(tile => tile.ID);
        var newTiles = new List<Tile>();
        foreach (var id in fromTiles.Keys.Union(toTiles.Keys))
        {
            var fromTile = fromTiles.GetValueOrDefault(id) ?? EmptyTile(id);
            var toTile = toTiles.GetValueOrDefault(id) ?? EmptyTile(id);
            if (HoldsTheSame(fromTile, toTile))
                continue;
            if (source.TilesById.TryGetValue(id, out var element))
                writer.EditTile(element, fromTile, toTile);
            else if (toTile.Properties.Count > 0 || toTile.Animation.Count > 0)
                newTiles.Add(toTile);
        }
        writer.InsertTiles(newTiles);
        return writer.ApplyEdits();
    }

    private static Tile EmptyTile(uint id) => new() { ID = id, Width = 0, Height = 0 };

    private static bool HoldsTheSame(Tile a, Tile b) =>
        a.Properties.Select(Describe).SequenceEqual(b.Properties.Select(Describe))
        && a.Animation.Select(Values).SequenceEqual(b.Animation.Select(Values));

    private static (uint TileId, int Duration) Values(Frame frame) => (frame.TileID, frame.Duration);

    /// <summary>A property's identity and its value. Only a string's and an int's value is ever written.</summary>
    private static ((string Name, Type? Type) Key, string Value) Describe(IProperty property) => property switch
    {
        StringProperty text => ((text.Name, typeof(string)), text.Value),
        IntProperty number => ((number.Name, typeof(int)), XmlConvert.ToString(number.Value)),
        _ => ((property.Name, null), ""),
    };

    private void EditTile(TsxElement tile, Tile from, Tile to)
    {
        var removed = new List<TsxElement>();
        var newProperties = EditProperties(tile, from, to, removed);
        var newAnimation = EditAnimation(tile, to, removed);

        if (newProperties.Length == 0 && newAnimation.Length == 0 && removed.Count > 0
            && IsBlank(tile.StartTagEnd, tile.EndTagStart, removed))
        {
            if (tile.Attributes.Keys.All(name => name == "id"))
                Remove(tile);
            else
                _edits.Add((tile.StartTagEnd - 1, tile.End, "/>"));
            return;
        }

        foreach (var child in removed)
            Remove(child);
        if (newProperties.Length > 0 && tile.Children.Count > 0)
            _edits.Add((tile.StartTagEnd, tile.StartTagEnd, newProperties));
        else
            newAnimation = newProperties + newAnimation;
        Append(tile, newAnimation);
    }

    /// <returns>The <c>&lt;properties&gt;</c> element to add, or "" when the tile has one already or needs none.</returns>
    private string EditProperties(TsxElement tile, Tile from, Tile to, List<TsxElement> removedFromTile)
    {
        var removed = new List<TsxElement>();
        var added = new List<IProperty>();
        var before = from.Properties.ToLookup(property => Describe(property).Key);
        var after = to.Properties.ToLookup(property => Describe(property).Key);
        var elements = TsxLoader.PropertyElements(tile).ToLookup(element => Describe(TsxLoader.ReadProperty(element)).Key);
        foreach (var key in before.Concat(after).Select(group => group.Key).Distinct())
        {
            var (old, wanted, existing) = (before[key].ToList(), after[key].ToList(), elements[key].ToList());
            for (var i = 0; i < wanted.Count; i++)
                if (i >= old.Count || Describe(old[i]) != Describe(wanted[i]))
                {
                    if (i < existing.Count)
                        SetPropertyValue(existing[i], Describe(wanted[i]).Value);
                    else
                        added.Add(wanted[i]);
                }
            removed.AddRange(existing.Take(old.Count).Skip(wanted.Count));
        }

        var properties = tile.Child("properties");
        if (properties is null)
            return added.Count == 0 ? "" : Block("properties", ChildLead(tile), added.Select(RenderProperty));
        if (added.Count == 0 && removed.Count > 0 && properties.Attributes.Count == 0
            && IsBlank(properties.StartTagEnd, properties.EndTagStart, removed))
        {
            removedFromTile.Add(properties);
            return "";
        }
        foreach (var property in removed)
            Remove(property);
        Append(properties, string.Concat(added.Select(property => ChildLead(properties) + RenderProperty(property))));
        return "";
    }

    /// <returns>The <c>&lt;animation&gt;</c> element to add, or "" when the tile has one already or needs none.</returns>
    private string EditAnimation(TsxElement tile, Tile to, List<TsxElement> removedFromTile)
    {
        var animation = tile.Child("animation");
        if (to.Animation.Count == 0)
        {
            if (animation is not null && animation.Children.Count > 0)
                removedFromTile.Add(animation);
            return "";
        }
        if (animation is null)
            return Block("animation", ChildLead(tile), to.Animation.Select(RenderFrame));

        var elements = TsxLoader.FrameElements(tile).ToList();
        var before = elements.Select(element => Values(TsxLoader.ReadFrame(element))).ToList();
        var after = to.Animation.Select(Values).ToList();
        var same = 0;
        while (same < before.Count && same < after.Count && before[same] == after[same])
            same++;
        var sameAtEnd = 0;
        while (sameAtEnd < before.Count - same && sameAtEnd < after.Count - same && before[^(sameAtEnd + 1)] == after[^(sameAtEnd + 1)])
            sameAtEnd++;

        var edited = Math.Min(before.Count, after.Count) - sameAtEnd;
        for (var i = same; i < edited; i++)
        {
            if (before[i].TileId != after[i].TileId)
                SetAttribute(elements[i], "tileid", XmlConvert.ToString(after[i].TileId));
            if (before[i].Duration != after[i].Duration)
                SetAttribute(elements[i], "duration", XmlConvert.ToString(after[i].Duration));
        }
        for (var i = edited; i < before.Count - sameAtEnd; i++)
            Remove(elements[i]);

        var lead = ChildLead(animation);
        var added = string.Concat(to.Animation.Take(after.Count - sameAtEnd).Skip(edited).Select(frame => lead + RenderFrame(frame)));
        if (added.Length > 0 && edited < elements.Count)
            _edits.Add((LeadStart(elements[edited]), LeadStart(elements[edited]), added));
        else
            Append(animation, added);
        return "";
    }

    private void InsertTiles(List<Tile> tiles)
    {
        var existing = _source.Root.ChildrenNamed("tile")
            .Select(element => (Id: XmlConvert.ToUInt32(element.Attribute("id")!), Element: element)).ToList();
        // Where the tiles start when there are none: Tiled writes only the wangsets after them.
        var end = existing.Count > 0 ? null : _source.Root.Child("wangsets");
        foreach (var group in tiles.OrderBy(tile => tile.ID)
            .GroupBy(tile => existing.Find(other => other.Id > tile.ID).Element ?? end))
        {
            var lead = ChildLead(_source.Root);
            var text = string.Concat(group.Select(tile => lead + RenderTile(tile, lead)));
            if (group.Key is { } next)
                _edits.Add((LeadStart(next), LeadStart(next), text));
            else if (existing.Count > 0)
                _edits.Add((existing[^1].Element.End, existing[^1].Element.End, text));
            else
                Append(_source.Root, text);
        }
    }

    private string RenderTile(Tile tile, string lead)
    {
        var childLead = Deeper(lead);
        var properties = tile.Properties.Count == 0 ? "" : Block("properties", childLead, tile.Properties.Select(RenderProperty));
        var animation = tile.Animation.Count == 0 ? "" : Block("animation", childLead, tile.Animation.Select(RenderFrame));
        return $"<tile id=\"{XmlConvert.ToString(tile.ID)}\">{properties}{animation}{lead}</tile>";
    }

    private string Block(string name, string lead, IEnumerable<string> children) =>
        $"{lead}<{name}>{string.Concat(children.Select(child => Deeper(lead) + child))}{lead}</{name}>";

    private static string RenderFrame(Frame frame) =>
        $"<frame tileid=\"{XmlConvert.ToString(frame.TileID)}\" duration=\"{XmlConvert.ToString(frame.Duration)}\"/>";

    private static string RenderProperty(IProperty property) => property switch
    {
        StringProperty text => $"<property name=\"{Escape(text.Name)}\" value=\"{Escape(text.Value)}\"/>",
        IntProperty number => $"<property name=\"{Escape(number.Name)}\" type=\"int\" value=\"{XmlConvert.ToString(number.Value)}\"/>",
        _ => throw new NotSupportedException($"TsxWriter cannot add the '{property.Type}' property \"{property.Name}\"."),
    };

    /// <exception cref="ArgumentException">A character XML cannot hold.</exception>
    private static string Escape(string value) => StorableText.Problem(value) is { } problem
        ? throw new InvalidDataException(problem)
        : new XAttribute("a", value).ToString()["a=\"".Length..^1].Replace("'", "&apos;");

    private void SetPropertyValue(TsxElement property, string value)
    {
        if (property.Attribute("value") is null && !property.IsEmpty)
            _edits.Add((property.StartTagEnd, property.EndTagStart, Escape(value)));
        else
            SetAttribute(property, "value", value);
    }

    private void SetAttribute(TsxElement element, string name, string value)
    {
        var text = _source.Text;
        var start = element.Start + 1 + element.Name.Length;
        while (element.Attribute(name) is not null)
        {
            while (char.IsWhiteSpace(text[start]))
                start++;
            var (valueStart, valueEnd) = ValueSpan(text, start);
            if (text.AsSpan(start, text.AsSpan(start).IndexOfAny("= \t\r\n")).SequenceEqual(name))
            {
                _edits.Add((valueStart, valueEnd, Escape(value)));
                return;
            }
            start = valueEnd + 1;
        }
        _edits.Add((start, start, $" {name}=\"{Escape(value)}\""));
    }

    /// <summary>What is between the quotes of the attribute whose name starts at <paramref name="nameStart"/>.</summary>
    private static (int Start, int End) ValueSpan(string text, int nameStart)
    {
        var quote = nameStart + text.AsSpan(nameStart).IndexOfAny('"', '\'');
        return (quote + 1, text.IndexOf(text[quote], quote + 1));
    }

    /// <summary>Adds text after everything inside the element.</summary>
    private void Append(TsxElement element, string text)
    {
        if (text.Length == 0)
            return;
        var source = _source.Text;
        if (element.IsEmpty)
        {
            var slash = element.StartTagEnd - "/>".Length;
            while (char.IsWhiteSpace(source[slash - 1]))
                slash--;
            _edits.Add((slash, element.End, $">{text}{Lead(element)}</{element.Name}>"));
            return;
        }
        var end = element.EndTagStart;
        while (end > element.StartTagEnd && char.IsWhiteSpace(source[end - 1]))
            end--;
        _edits.Add(end == element.StartTagEnd ? (end, element.EndTagStart, text + Lead(element)) : (end, end, text));
    }

    private void Remove(TsxElement element) => _edits.Add((LeadStart(element), element.End, ""));

    private bool IsBlank(int start, int end, IEnumerable<TsxElement> except)
    {
        foreach (var element in except.OrderBy(element => element.Start))
        {
            if (!_source.Text.AsSpan(start, element.Start - start).IsWhiteSpace())
                return false;
            start = element.End;
        }
        return _source.Text.AsSpan(start, end - start).IsWhiteSpace();
    }

    /// <summary>Where the line break and indentation before the element start; the element when it is not first on its line.</summary>
    private int LeadStart(TsxElement element)
    {
        var text = _source.Text;
        var start = element.Start;
        while (start > 0 && text[start - 1] is ' ' or '\t')
            start--;
        if (start == 0 || text[start - 1] is not ('\n' or '\r'))
            return element.Start;
        return start - (text[start - 1] == '\n' && start > 1 && text[start - 2] == '\r' ? 2 : 1);
    }

    private string Lead(TsxElement element) => _source.Text[LeadStart(element)..element.Start];

    private string ChildLead(TsxElement element) =>
        element.Children.Count > 0 ? Lead(element.Children[0]) : Deeper(Lead(element));

    /// <summary>The lead of a child: one more of the indentation the file uses for the children of the root.</summary>
    private string Deeper(string lead) =>
        lead.Length == 0 || _source.Root.Children.Count == 0 ? lead : lead + Lead(_source.Root.Children[0]).TrimStart('\r', '\n');

    private string ApplyEdits()
    {
        var result = new StringBuilder();
        var position = 0;
        foreach (var (start, end, text) in _edits.OrderBy(edit => edit.Start).ThenBy(edit => edit.End))
        {
            result.Append(_source.Text, position, start - position).Append(text);
            position = end;
        }
        return result.Append(_source.Text, position, _source.Text.Length - position).ToString();
    }

    /// <summary>
    /// Makes every path that Tiled resolves against the file's folder reach the same file from another folder.
    /// A path that cannot be relative to it (another drive) becomes absolute.
    /// </summary>
    private static string RebasePaths(string text, FilePath fromFolder, FilePath toFolder)
    {
        if (fromFolder == toFolder)
            return text;
        var result = new StringBuilder();
        var position = 0;
        foreach (var (element, type, name, value, start) in TsxSource.ReadAttributes(text))
        {
            var isPath = (element, name) is ("image", "source") or ("object", "template") or ("export", "target")
                || (element is "property" or "item" && name == "value" && type == "file");
            if (!isPath || TexturePathHelper.Rebase(value, fromFolder, toFolder) is not { } rebased || rebased == value)
                continue;
            var (valueStart, valueEnd) = ValueSpan(text, start);
            result.Append(text, position, valueStart - position).Append(Escape(rebased));
            position = valueEnd;
        }
        return result.Append(text, position, text.Length - position).ToString();
    }
}
