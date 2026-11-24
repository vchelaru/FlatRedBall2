using AnimationEditor.Core;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Round trip of every construct Tiled's TMX/TSX reference allows in a tileset file (#2007), through
/// the editor's own load and save. Adding a construct is one <see cref="Row"/>. Each row runs every
/// <see cref="Op"/>; the saved XML tree must equal the original one, apart from whitespace, attribute
/// order and the normalisations in <see cref="Normalise"/>.
/// </summary>
public class TsxCorpusRoundTripTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    public enum Op
    {
        /// <summary>Open, save.</summary>
        Save,

        /// <summary>Open, change one frame's duration on the first animated tile, save. That tile is
        /// regenerated; the rest of the file is patched around it.</summary>
        EditSave,

        /// <summary><see cref="EditSave"/> on a file with no line breaks, as Tiled's "minimize output"
        /// writes it. The writer cannot patch it, so the whole file is regenerated.</summary>
        EditSaveOneLine,

        /// <summary>Open, save to a path that does not exist yet.</summary>
        SaveAs,
    }

    /// <param name="Xml">The whole file. Every row that opens has an animated tile to edit.</param>
    /// <param name="FileEncoding">Null is UTF-8 without a byte order mark.</param>
    /// <param name="TemplateXml">Written beside the file as <c>Template.tx</c>.</param>
    /// <param name="RefusedOnOneLine">The one-line form does not open (see <see cref="RefusedRows"/>).</param>
    private sealed record Row(
        string Name, string Xml, Encoding? FileEncoding = null, string? TemplateXml = null, bool RefusedOnOneLine = false);

    /// <summary>Rows that lose something today. <see cref="Issue"/> is the issue that fixes it. What each
    /// one loses is listed in <c>Fixtures/TsxCorpusKnownLosses.txt</c>.</summary>
    private sealed record Loss(string Issue, Op[] Ops, string[] Rows);

    private const string Animation =
        "<animation>\n   <frame tileid=\"0\" duration=\"200\"/>\n   <frame tileid=\"1\" duration=\"100\"/>\n  </animation>";

    /// <summary>A file laid out as Tiled writes it. Tile 0 always carries the animation.</summary>
    private static string Tsx(
        string root = "", string head = "", string image = "<image source=\"Heroes.png\" width=\"64\" height=\"64\"/>",
        string tile0Attributes = "", string tile0Before = "", string tile0After = "", string tiles = "", string tail = "",
        string prolog = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", string epilog = "",
        string size = "tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\"",
        string version = "version=\"1.10\" tiledversion=\"1.12.2\"")
    {
        static string Line(string indent, string content) => content.Length == 0 ? "" : $"{indent}{content}\n";
        return $"{prolog}<tileset {version} name=\"Heroes\" {size}{root}>\n"
            + Line(" ", head) + Line(" ", image)
            + $" <tile id=\"0\"{tile0Attributes}>\n" + Line("  ", tile0Before) + Line("  ", Animation) + Line("  ", tile0After) + " </tile>\n"
            + Line(" ", tiles) + Line(" ", tail) + "</tileset>\n" + epilog;
    }

    private static string Objects(string objects, string groupAttributes = " draworder=\"index\" id=\"2\"") =>
        $"<objectgroup{groupAttributes}>\n   {objects}\n  </objectgroup>";

    private static string Properties(string properties) => $"<properties>\n   {properties}\n  </properties>";

    private static string Wangsets(string wangset) => $"<wangsets>\n  {wangset}\n </wangsets>";

    private const string WangTile = "<wangtile tileid=\"0\" wangid=\"1,1,1,1,1,1,1,1\"/>";
    private const string WangColor = "<wangcolor name=\"Solid\" color=\"#ff0000\" tile=\"-1\" probability=\"1\"/>";

    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Files the editor opens.</summary>
    private static readonly Row[] Rows =
    [
        // <tileset>
        new("tileset attributes", Tsx(root: " class=\"Actors\" objectalignment=\"bottomleft\" tilerendersize=\"grid\" fillmode=\"preserve-aspect-fit\"")),
        new("tileset spacing and margin", Tsx(
            size: "tilewidth=\"16\" tileheight=\"16\" spacing=\"2\" margin=\"1\" tilecount=\"16\" columns=\"4\"",
            image: "<image source=\"Heroes.png\" width=\"72\" height=\"72\"/>")),
        new("tileset attributes at their defaults", Tsx(root: " class=\"\" spacing=\"0\" margin=\"0\" objectalignment=\"unspecified\" tilerendersize=\"tile\" fillmode=\"stretch\"")),
        new("tileset without versions (before 1.0)", Tsx(version: "")),
        new("tileset backgroundcolor", Tsx(root: " backgroundcolor=\"#ff336699\"")),
        new("tileset unknown attribute", Tsx(root: " futureattribute=\"kept\"")),
        new("editorsettings", Tsx(head: "<editorsettings>\n  <export target=\"Heroes.json\" format=\"json\"/>\n </editorsettings>")),
        new("tileoffset", Tsx(head: "<tileoffset x=\"2\" y=\"-3\"/>")),
        new("tileoffset unknown attribute", Tsx(head: "<tileoffset x=\"2\" y=\"-3\" future=\"1\"/>")),
        new("grid isometric", Tsx(head: "<grid orientation=\"isometric\" width=\"32\" height=\"16\"/>")),
        new("grid orthogonal", Tsx(head: "<grid orientation=\"orthogonal\" width=\"16\" height=\"16\"/>")),
        new("transformations", Tsx(head: "<transformations hflip=\"1\" vflip=\"0\" rotate=\"1\" preferuntransformed=\"1\"/>")),
        new("transformations unknown attribute", Tsx(head: "<transformations hflip=\"1\" vflip=\"0\" rotate=\"1\" preferuntransformed=\"1\" future=\"1\"/>")),
        new("terraintypes (before 1.5)", Tsx(head: "<terraintypes>\n  <terrain name=\"Grass\" tile=\"1\"/>\n </terraintypes>")),
        new("tile terrain (before 1.5)", Tsx(tile0Attributes: " terrain=\"0,0,,0\"")),

        // <image>
        new("image trans", Tsx(image: "<image source=\"Heroes.png\" trans=\"ff00ff\" width=\"64\" height=\"64\"/>")),
        new("image trans with hash", Tsx(image: "<image source=\"Heroes.png\" trans=\"#ff00ff\" width=\"64\" height=\"64\"/>")),
        new("image without size", Tsx(image: "<image source=\"Heroes.png\"/>")),
        new("image in another folder", Tsx(image: "<image source=\"../art/My Heroes.png\" width=\"64\" height=\"64\"/>")),
        new("image absolute path", Tsx(image: "<image source=\"C:/art/Heroes.png\" width=\"64\" height=\"64\"/>")),
        new("image deprecated id", Tsx(image: "<image id=\"3\" source=\"Heroes.png\" width=\"64\" height=\"64\"/>")),
        new("image unknown attribute", Tsx(image: "<image source=\"Heroes.png\" width=\"64\" height=\"64\" future=\"1\"/>")),

        // <properties>, on the edited tile and on the tileset
        new("tileset properties", Tsx(head: "<properties>\n  <property name=\"Author\" value=\"Vic\"/>\n  <property name=\"Notes\">line one\nline two</property>\n </properties>")),
        new("property simple types", Tsx(tile0Before: Properties(
            "<property name=\"S\" value=\"text\"/><property name=\"Empty\" value=\"\"/><property name=\"I\" type=\"int\" value=\"-7\"/>"
            + "<property name=\"B\" type=\"bool\" value=\"true\"/><property name=\"F\" type=\"float\" value=\"1.5\"/>"
            + "<property name=\"C\" type=\"color\" value=\"#ff00ff00\"/><property name=\"File\" type=\"file\" value=\"../a.png\"/>"
            + "<property name=\"O\" type=\"object\" value=\"3\"/>"))),
        new("property type string written out", Tsx(tile0Before: Properties("<property name=\"S\" type=\"string\" value=\"text\"/>"))),
        new("property color without alpha", Tsx(tile0Before: Properties("<property name=\"C\" type=\"color\" value=\"#00ff00\"/>"))),
        new("property color translucent", Tsx(
            head: "<properties>\n  <property name=\"C\" type=\"color\" value=\"#80ff0000\"/>\n </properties>",
            tile0Before: Properties("<property name=\"C\" type=\"color\" value=\"#80ff0000\"/>"))),
        new("property color empty", Tsx(tile0Before: Properties("<property name=\"C\" type=\"color\" value=\"\"/>"))),
        new("property float with many digits", Tsx(tile0Before: Properties("<property name=\"F\" type=\"float\" value=\"0.123456789012345\"/>"))),
        new("property large int", Tsx(tile0Before: Properties("<property name=\"I\" type=\"int\" value=\"2147483647\"/>"))),
        new("property multiline string", Tsx(tile0Before: Properties("<property name=\"Notes\">line one\nline two</property>"))),
        new("property string as element text", Tsx(tile0Before: Properties("<property name=\"Notes\">one line</property>"))),
        new("property string with tab and newline references", Tsx(tile0Before: Properties("<property name=\"Notes\" value=\"a&#9;b&#10;c\"/>"))),
        new("property class with members", Tsx(tile0Before: Properties(
            "<property name=\"Stats\" type=\"class\" propertytype=\"Stats\"><properties><property name=\"Hp\" type=\"int\" value=\"9\"/>"
            + "<property name=\"Inner\" type=\"class\" propertytype=\"Inner\"><properties><property name=\"Kind\" propertytype=\"Kind\" value=\"Big\"/></properties></property>"
            + "</properties></property>"))),
        new("property class without members", Tsx(tile0Before: Properties("<property name=\"Stats\" type=\"class\" propertytype=\"Stats\"/>"))),
        new("property class without members on the tileset", Tsx(head: "<properties>\n  <property name=\"Stats\" type=\"class\" propertytype=\"Stats\"/>\n </properties>")),
        new("property enum", Tsx(tile0Before: Properties(
            "<property name=\"Kind\" propertytype=\"Kind\" value=\"Big\"/><property name=\"Flags\" type=\"int\" propertytype=\"Flags\" value=\"5\"/>"))),
        new("property unknown attribute", Tsx(tile0Before: Properties("<property name=\"S\" value=\"text\" future=\"1\"/>"))),
        new("properties empty", Tsx(tile0Before: "<properties/>")),

        // <tile>
        new("tile type and probability", Tsx(tile0Attributes: " type=\"Grass\" probability=\"0.25\"", tiles: "<tile id=\"5\" type=\"Water\" probability=\"2\"/>")),
        new("tile class (1.9)", Tsx(tile0Attributes: " class=\"Grass\"", tiles: "<tile id=\"5\" class=\"Water\"/>")),
        new("tile probability zero", Tsx(tile0Attributes: " probability=\"0\"", tiles: "<tile id=\"5\" probability=\"0\"/>")),
        new("tile sub-rectangle (1.9)", Tsx(tile0Attributes: " x=\"1\" y=\"2\" width=\"8\" height=\"9\"")),
        new("tile unknown attribute", Tsx(tile0Attributes: " future=\"1\"")),
        new("tile unknown child", Tsx(tile0After: "<future a=\"1\">text</future>")),
        new("untouched tile unknown attribute and child", Tsx(tiles: "<tile id=\"5\" type=\"Water\" future=\"1\">\n  <future/>\n </tile>")),
        new("tile ids with gaps", Tsx(tiles: "<tile id=\"3\" type=\"A\"/>\n <tile id=\"9\" type=\"B\"/>\n <tile id=\"15\" type=\"C\"/>")),
        new("tiles in display order (1.7)", Tsx(tiles: "<tile id=\"9\" type=\"B\"/>\n <tile id=\"3\" type=\"A\"/>")),
        new("tile with only an id", Tsx(tiles: "<tile id=\"5\"/>")),
        new("tile with a name and other properties", Tsx(
            tile0Before: Properties("<property name=\"Name\" value=\"Walk\"/><property name=\"Other\" value=\"1\"/>"),
            tiles: "<tile id=\"5\">\n  <properties>\n   <property name=\"Keep\" value=\"1\"/>\n  </properties>\n </tile>")),
        new("tile with an empty Name property", Tsx(tile0Before: Properties("<property name=\"Name\" value=\"\"/>"))),
        new("tile with an int Name property", Tsx(tile0Before: Properties("<property name=\"Name\" type=\"int\" value=\"4\"/>"))),
        new("unanimated tile with a Name property", Tsx(tiles:
            "<tile id=\"5\" type=\"Chest\">\n  <properties>\n   <property name=\"Name\" value=\"Gold chest\"/>\n  </properties>\n </tile>")),
        new("unanimated tile with an int Name property", Tsx(tiles:
            "<tile id=\"5\" type=\"Chest\">\n  <properties>\n   <property name=\"Name\" type=\"int\" value=\"4\"/>\n  </properties>\n </tile>")),
        new("tile with an achx-push animation", Tsx(tiles:
            "<tile id=\"4\">\n  <properties>\n   <property name=\"achjSourceFile\" value=\"a.achx\"/>\n  </properties>\n  <animation>\n   <frame tileid=\"4\" duration=\"100\"/>\n  </animation>\n </tile>")),
        new("multi-tile animation", Tsx(
            tile0Before: Properties("<property name=\"Name\" value=\"Tall\"/>"),
            tiles: "<tile id=\"4\">\n  <properties>\n   <property name=\"ParentId\" type=\"int\" value=\"0\"/>\n  </properties>\n  <animation>\n   <frame tileid=\"4\" duration=\"200\"/>\n   <frame tileid=\"5\" duration=\"100\"/>\n  </animation>\n </tile>")),

        // <animation>
        new("second animation with other frame durations", Tsx(tiles:
            "<tile id=\"8\">\n  <animation>\n   <frame tileid=\"2\" duration=\"1\"/>\n   <frame tileid=\"3\" duration=\"33\"/>\n   <frame tileid=\"8\" duration=\"83\"/>\n   <frame tileid=\"9\" duration=\"1001\"/>\n   <frame tileid=\"9\" duration=\"0\"/>\n   <frame tileid=\"9\" duration=\"3600001\"/>\n  </animation>\n </tile>")),
        new("frame outside the image", Tsx(tiles:
            "<tile id=\"8\">\n  <animation>\n   <frame tileid=\"8\" duration=\"100\"/>\n   <frame tileid=\"99\" duration=\"100\"/>\n  </animation>\n </tile>")),
        new("animation empty", Tsx(tiles: "<tile id=\"8\" type=\"A\">\n  <animation/>\n </tile>")),
        new("animation unknown attribute", Tsx(tiles:
            "<tile id=\"8\">\n  <animation future=\"1\">\n   <frame tileid=\"8\" duration=\"100\" future=\"1\"/>\n  </animation>\n </tile>")),
        new("edited animation unknown frame attribute", Tsx().Replace("tileid=\"1\" duration=\"100\"", "tileid=\"1\" duration=\"100\" future=\"1\"")),

        // <objectgroup> and <object>
        new("objectgroup attributes", Tsx(tile0Before: Objects(
            "<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"/>",
            " draworder=\"topdown\" id=\"2\" name=\"Hit\" class=\"Box\" color=\"#ff0000\" offsetx=\"1.5\" offsety=\"-2\" opacity=\"0.5\" visible=\"0\" tintcolor=\"#80ff0000\" parallaxx=\"0.5\" parallaxy=\"2\""))),
        new("objectgroup tintcolor without alpha", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " draworder=\"index\" id=\"2\" tintcolor=\"#ff0000\""))),
        new("objectgroup properties", Tsx(tile0Before: Objects("<properties><property name=\"S\" value=\"a\"/></properties><object id=\"1\" x=\"1\" y=\"2\"/>"))),
        new("objectgroup empty", Tsx(tile0Before: "<objectgroup draworder=\"index\" id=\"2\"/>")),
        new("objectgroup without draworder", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " id=\"2\""))),
        new("objectgroup deprecated position and size", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " draworder=\"index\" id=\"2\" x=\"1\" y=\"2\" width=\"3\" height=\"4\""))),
        new("objectgroup unknown attribute", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " draworder=\"index\" id=\"2\" future=\"1\""))),
        new("object attributes", Tsx(tile0Before: Objects("<object id=\"1\" name=\"Hit\" type=\"Box\" x=\"1.25\" y=\"-2\" width=\"3\" height=\"4.5\" rotation=\"45\" visible=\"0\"/>"))),
        new("object class (1.9)", Tsx(tile0Before: Objects("<object id=\"1\" class=\"Box\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"/>"))),
        new("object opacity (1.12)", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\" opacity=\"0.5\"/>"))),
        new("object unknown attribute", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\" future=\"1\"/>"))),
        new("object many digits", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1.33333333333333\" y=\"2\" width=\"3\" height=\"4\"/>"))),
        new("object without id (before 0.11)", Tsx(tile0Before: Objects("<object x=\"1\" y=\"2\" width=\"3\" height=\"4\"/>"))),
        new("object properties", Tsx(tile0Before: Objects(
            "<object id=\"1\" x=\"1\" y=\"2\"><properties><property name=\"Kind\" propertytype=\"Kind\" value=\"Big\"/><property name=\"Other\" type=\"object\" value=\"1\"/></properties></object>"))),
        new("object ellipse", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"><ellipse/></object>"))),
        new("object point", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"><point/></object>"))),
        new("object polygon", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"><polygon points=\"0,0 4.5,-1 3,8\"/></object>"))),
        new("object polygon many digits", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"><polygon points=\"0,0 4.33333333333333,-1 3,8\"/></object>"))),
        new("object polyline", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"><polyline points=\"0,0 4,4\"/></object>"))),
        new("object text", Tsx(tile0Before: Objects(
            "<object id=\"1\" x=\"1\" y=\"2\" width=\"9\" height=\"9\"><text fontfamily=\"Arial\" pixelsize=\"12\" wrap=\"1\" color=\"#ff0000\" bold=\"1\" italic=\"1\" underline=\"1\" strikeout=\"1\" kerning=\"0\" halign=\"center\" valign=\"bottom\">Hi  there\n second &lt;line&gt; </text></object>"))),
        new("object text justified, translucent", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"9\" height=\"9\"><text color=\"#80ff0000\" halign=\"justify\">Hi</text></object>"))),
        new("object text defaults", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"9\" height=\"9\"><text>Hi</text></object>"))),
        new("object gid flipped", Tsx(tile0Before: Objects("<object id=\"1\" gid=\"2147483650\" x=\"1\" y=\"2\" width=\"16\" height=\"16\"/>"))),
        new("object template (1.1)", Tsx(tile0Before: Objects("<object id=\"1\" template=\"Template.tx\" x=\"1\" y=\"2\"/>")),
            TemplateXml: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<template>\n <object name=\"Hit\" type=\"Box\" width=\"5\" height=\"6\">\n  <properties>\n   <property name=\"Power\" type=\"int\" value=\"3\"/>\n  </properties>\n </object>\n</template>\n"),

        // <wangsets>
        new("wangset", Tsx(tail: Wangsets(
            "<wangset name=\"Ground\" class=\"Terrain\" type=\"corner\" tile=\"3\"><properties><property name=\"layer\" type=\"int\" value=\"2\"/></properties>"
            + "<wangcolor name=\"Grass\" class=\"Soft\" color=\"#00ff00\" tile=\"0\" probability=\"0.5\"><properties><property name=\"speed\" type=\"float\" value=\"1.5\"/></properties></wangcolor>"
            + $"{WangColor}{WangTile}<wangtile tileid=\"4\" wangid=\"0,1,0,2,0,1,0,254\"/></wangset>"))),
        new("wangsets sharing a name", Tsx(tail: Wangsets(
            $"<wangset name=\"Unnamed Set\" type=\"corner\" tile=\"-1\">{WangColor}{WangTile}</wangset>\n  <wangset name=\"Unnamed Set\" type=\"edge\" tile=\"-1\">{WangColor}{WangTile}</wangset>")), RefusedOnOneLine: true),
        new("wangset without type (before 1.5)", Tsx(tail: Wangsets($"<wangset name=\"Ground\" tile=\"-1\">{WangColor}{WangTile}</wangset>"))),
        new("wangset empty", Tsx(tail: Wangsets("<wangset name=\"Ground\" type=\"mixed\" tile=\"-1\"/>"))),
        new("wangsets empty", Tsx(tail: "<wangsets/>")),
        new("wangset unknown attribute and child", Tsx(tail: Wangsets($"<wangset name=\"Ground\" type=\"mixed\" tile=\"-1\" future=\"1\">{WangColor}{WangTile}<future/></wangset>"))),
        new("wangcolor unknown attribute", Tsx(tail: Wangsets($"<wangset name=\"Ground\" type=\"mixed\" tile=\"-1\"><wangcolor name=\"Solid\" color=\"#ff0000\" tile=\"-1\" probability=\"1\" future=\"1\"/>{WangTile}</wangset>"))),
        new("wangset corner and edge colors (before 1.5)", Tsx(tail: Wangsets(
            "<wangset name=\"Ground\" tile=\"-1\"><wangcornercolor name=\"A\" color=\"#ff0000\" tile=\"-1\" probability=\"1\"/><wangedgecolor name=\"B\" color=\"#00ff00\" tile=\"-1\" probability=\"1\"/><wangtile tileid=\"0\" wangid=\"1,1,1,1,1,1,1,1\"/></wangset>"))),
        new("wangtile flips (before 1.5)", Tsx(tail: Wangsets($"<wangset name=\"Ground\" tile=\"-1\">{WangColor}<wangtile tileid=\"0\" wangid=\"1,1,1,1,1,1,1,1\" hflip=\"1\" vflip=\"0\" dflip=\"1\"/></wangset>"))),

        // XML constructs and encodings
        new("UTF-8 byte order mark", Tsx(tile0Attributes: " type=\"Größe 日本\""), Utf8Bom),
        new("non-ASCII text", Tsx(tile0Attributes: " type=\"Größe 日本\"")),
        new("UTF-16 little endian", Tsx(tile0Attributes: " type=\"Größe 日本\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"), Encoding.Unicode),
        new("UTF-16 big endian", Tsx(tile0Attributes: " type=\"Größe 日本\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"), Encoding.BigEndianUnicode),
        new("ISO-8859-1", Tsx(tile0Attributes: " type=\"Größe\"", tiles: "<tile id=\"5\" type=\"Wäter\"/>", prolog: "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n"), Encoding.Latin1),
        new("CRLF line endings", Tsx(tiles: "<tile id=\"5\" type=\"Water\">\n  <properties>\n   <property name=\"Notes\">one\ntwo</property>\n  </properties>\n </tile>").Replace("\n", "\r\n")),
        new("tab indentation", Tsx(tiles: "<tile id=\"5\" type=\"Water\"/>").Replace("\n ", "\n\t")),
        new("entities and character references", Tsx(tile0Attributes: " type=\"&lt;a&gt; &amp; &quot;b&quot; &#x1F600;\"", tiles: "<tile id=\"5\" type=\"&lt;&amp;&apos;\"/>")),
        new("comment before the root", Tsx(prolog: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- top -->\n")),
        new("comment after the root", Tsx(epilog: "<!-- end -->\n")),
        new("comment before the first tile", Tsx(head: "<!-- head -->")),
        new("comment between tiles", Tsx(tiles: "<!-- mid -->\n <tile id=\"5\" type=\"Water\"/>")),
        new("comment after the last tile", Tsx(tail: "<!-- tail -->")),
        new("comment after the wangsets", Tsx(tail: Wangsets($"<wangset name=\"Ground\" type=\"mixed\" tile=\"-1\">{WangColor}{WangTile}</wangset>") + "\n <!-- tail -->")),
        new("comment inside the edited tile", Tsx(tile0After: "<!-- in -->")),
        new("comment inside an untouched tile", Tsx(tiles: "<tile id=\"5\" type=\"Water\">\n  <!-- in -->\n </tile>")),
        new("processing instruction", Tsx(prolog: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<?tool data?>\n", head: "<?tool more?>")),
        new("CDATA", Tsx(tile0Before: Properties("<property name=\"Notes\"><![CDATA[a < b]]></property>"))),
        new("unknown element before the tiles", Tsx(head: "<future a=\"1\"><deep/></future>")),
        new("unknown element between tiles", Tsx(tiles: "<future a=\"1\"/>\n <tile id=\"5\" type=\"Water\"/>")),
        new("unknown element after the last tile", Tsx(tail: "<future a=\"1\"/>")),
        new("unknown element after the wangsets", Tsx(tail: Wangsets($"<wangset name=\"Ground\" type=\"mixed\" tile=\"-1\">{WangColor}{WangTile}</wangset>") + "\n <future a=\"1\"/>")),
        new("prefixed namespace", Tsx(root: " xmlns:x=\"urn:x\" x:note=\"kept\"", head: "<x:future a=\"1\"/>")),
        new("default namespace", Tsx(root: " xmlns=\"urn:tiled\"")),
        new("standalone declaration", Tsx(prolog: "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n")),
        new("image after the tiles", Tsx(image: "", tail: "<image source=\"Heroes.png\" width=\"64\" height=\"64\"/>")),
    ];

    /// <summary>Files the editor refuses to open, so nothing in them can be lost.</summary>
    private static readonly Row[] RefusedRows =
    [
        new("image collection", Tsx(image: "", tile0Before: "<image source=\"a.png\" width=\"16\" height=\"16\"/>")),
        new("image collection with an embedded image", Tsx(image: "", tile0Before: "<image format=\"png\"><data encoding=\"base64\">iVBORw0KGgo=</data></image>")),
        new("sheet with one extra tile image", Tsx(tile0Before: "<image source=\"a.png\" width=\"16\" height=\"16\"/>")),
        new("no image", Tsx(image: "")),
        new("two tiles with one id", Tsx(tiles: "<tile id=\"5\" type=\"A\"/>\n <tile id=\"5\" type=\"B\"/>")),
        new("missing object template", Tsx(tile0Before: Objects("<object id=\"1\" template=\"Missing.tx\" x=\"1\" y=\"2\"/>"))),
        new("DOCTYPE with an entity", Tsx(tile0Attributes: " type=\"&e;\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE tileset [<!ENTITY e \"ext\">]>\n")),
        new("wangid in hex (before 1.5)", Tsx(tail: Wangsets($"<wangset name=\"Ground\" tile=\"-1\">{WangColor}<wangtile tileid=\"0\" wangid=\"0x01010101\"/></wangset>"))),
        new("tileset pointing at another file", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<tileset firstgid=\"1\" source=\"Other.tsx\"/>\n"),
        new("map file", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<map version=\"1.10\" orientation=\"orthogonal\" width=\"1\" height=\"1\" tilewidth=\"16\" tileheight=\"16\"/>\n"),
        new("image embedded data", Tsx(image: "<image format=\"png\" width=\"64\" height=\"64\">\n  <data encoding=\"base64\">iVBORw0KGgo=</data>\n </image>")),
        new("property list (1.12)", Tsx(tile0Before: Properties("<property name=\"L\" type=\"list\"><item type=\"int\" value=\"10\"/><item value=\"text\"/></property>"))),
        new("property list on the tileset (1.12)", Tsx(head: "<properties>\n  <property name=\"L\" type=\"list\">\n   <item type=\"int\" value=\"10\"/>\n  </property>\n </properties>")),
        new("property unknown type", Tsx(tile0Before: Properties("<property name=\"V\" type=\"vector\" value=\"1,2\"/>"))),
        new("properties unknown child", Tsx(tile0Before: Properties("<property name=\"S\" value=\"text\"/><future a=\"1\"/>"))),
        new("objectgroup without id (before 1.2)", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " draworder=\"index\""))),
        new("object unknown child", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"><future/></object>"))),
        new("object capsule (1.12)", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"><capsule/></object>"))),
        new("no XML declaration", Tsx(prolog: "")),
        new("two wangsets on one line", Tsx(tail: Wangsets($"<wangset name=\"A\" tile=\"-1\">{WangColor}{WangTile}</wangset><wangset name=\"B\" tile=\"-1\">{WangColor}{WangTile}</wangset>"))),
        new("truncated file", Tsx().Replace("</tileset>", "")),
    ];

    /// <summary>Differences a save is allowed to make. Applied to both trees, so a listed difference
    /// is invisible and anything else fails.</summary>
    private static void Normalise(XDocument document)
    {
        foreach (var element in document.Descendants().ToList())
        {
            var name = element.Name.LocalName;

            // Tiled itself omits an attribute that holds the format's default, so the writer may too.
            foreach (var attribute in element.Attributes().ToList())
                if (Defaults.TryGetValue((name, attribute.Name.LocalName), out var value) && attribute.Value == value)
                    attribute.Remove();

            // Tiled 1.9 wrote a tile's class as "class"; every other version reads and writes "type".
            if (name == "tile" && element.Attribute("class") is { } tileClass && element.Attribute("type") is null)
            {
                element.SetAttributeValue("type", tileClass.Value);
                tileClass.Remove();
            }

            // "#RRGGBB" and "#ffRRGGBB" are one color, and the '#' is optional. Tiled writes the short
            // form (a tintcolor without alpha, a trans without '#'); the writer spells the long one out.
            foreach (var color in ColorAttributes(element))
                color.Value = "#" + color.Value.TrimStart('#').ToLowerInvariant().PadLeft(8, 'f');

            // The writer adds the format DotTiled derives from the file extension. Tiled reads
            // "format" only for embedded images.
            if (name == "image" && element.Attribute("source") is not null)
                element.Attribute("format")?.Remove();

            // The format allows a string value as an attribute or as the element's text.
            if (name is "property" && element.Attribute("value") is null && !element.HasElements && element.Value.Length > 0)
            {
                var text = element.Value;
                element.RemoveNodes();
                element.SetAttributeValue("value", text);
            }

            // An empty container holds nothing.
            if (name is "properties" or "animation" or "wangsets" && !element.HasAttributes && !element.Nodes().Any())
                element.Remove();

            // A tileset holds at most one of each of these, so their order carries nothing. The
            // writer puts the image first; Tiled puts it last.
            if (name == "tileset")
                foreach (var single in new[] { "properties", "image", "transformations", "grid", "tileoffset" })
                    if (element.Elements().FirstOrDefault(e => e.Name.LocalName == single) is { } child)
                    {
                        child.Remove();
                        element.AddFirst(child);
                    }
        }
    }

    private static readonly Dictionary<(string Element, string Attribute), string> Defaults = new()
    {
        [("tileset", "class")] = "",
        [("tileset", "spacing")] = "0",
        [("tileset", "margin")] = "0",
        [("tileset", "objectalignment")] = "unspecified",
        [("tileset", "tilerendersize")] = "tile",
        [("tileset", "fillmode")] = "stretch",
        [("grid", "orientation")] = "orthogonal",
        [("property", "type")] = "string",
        [("objectgroup", "draworder")] = "topdown",
        [("object", "id")] = "0",
        [("text", "color")] = "#ff000000",
    };

    private static IEnumerable<XAttribute> ColorAttributes(XElement element) => element.Attributes().Where(a =>
        a.Value.Length > 0
        && (a.Name.LocalName is "color" or "tintcolor" or "trans" or "backgroundcolor"
            || (a.Name.LocalName == "value" && (string?)element.Attribute("type") == "color")));

    private static readonly Op[] EveryOp = [Op.Save, Op.EditSave, Op.EditSaveOneLine, Op.SaveAs];
    private static readonly Op[] EveryEdit = [Op.EditSave, Op.EditSaveOneLine, Op.SaveAs];
    private static readonly Op[] WholeFile = [Op.EditSaveOneLine, Op.SaveAs];

    private const string NameProperty = "#2014 A: a Name property the editor did not write is removed";
    private const string TileOrder = "#2013 B: tiles are sorted by id";
    private const string EditedTile = "#2013 C: an edited tile loses what DotTiled does not model";
    private const string TextIndent = "#2013 D: multi-line text in an edited tile gains a space per line";
    private const string AfterEditedTile = "#2013 E: what follows an edited tile is removed";
    private const string Rewrite = "#2013 F: rewriting the whole file loses what DotTiled does not model";
    private const string SaveAsLoss = "#2013 G: Save As loses backgroundcolor and the wangset type";
    private const string EncodingLoss = "#2015 H: a file that is not UTF-8 is corrupted";
    private const string DefaultNamespace = "#2013 I: a default namespace duplicates every tile";
    private const string ImagePath = "#2016 J: Save As to another folder breaks the image path";

    private static readonly Loss[] KnownLosses =
    [
        new(NameProperty, EveryOp,
        [
            "tile with an empty Name property", "tile with an int Name property",
            "unanimated tile with a Name property", "unanimated tile with an int Name property",
        ]),
        new(TileOrder, EveryOp, ["tiles in display order (1.7)"]),
        // A whole-file rewrite regenerates every tile, so it loses the same.
        new(EditedTile, EveryEdit,
        [
            "tile terrain (before 1.5)", "tile probability zero", "tile unknown attribute", "tile unknown child",
            "property unknown attribute", "property float with many digits", "edited animation unknown frame attribute",
            "objectgroup deprecated position and size", "objectgroup unknown attribute",
            "object class (1.9)", "object opacity (1.12)", "object unknown attribute", "object many digits",
            "object polygon many digits", "object template (1.1)", "comment inside the edited tile",
        ]),
        new(TextIndent, [Op.EditSave], ["object text"]),
        new(AfterEditedTile, EveryEdit,
        [
            "comment between tiles", "comment after the last tile",
            "unknown element between tiles", "unknown element after the last tile",
        ]),
        new(AfterEditedTile, [Op.EditSave], ["image after the tiles"]),
        new(Rewrite, WholeFile,
        [
            "editorsettings", "tileoffset unknown attribute", "transformations unknown attribute", "terraintypes (before 1.5)",
            "image deprecated id", "image unknown attribute", "untouched tile unknown attribute and child",
            "animation unknown attribute", "wangset unknown attribute and child", "wangcolor unknown attribute",
            "wangset corner and edge colors (before 1.5)", "wangtile flips (before 1.5)",
            "comment before the root", "comment after the root", "comment before the first tile", "comment after the wangsets",
            "comment inside an untouched tile", "processing instruction", "unknown element before the tiles",
            "unknown element after the wangsets", "prefixed namespace",
        ]),
        new(SaveAsLoss, [Op.SaveAs],
        [
            "tileset backgroundcolor", "tileset unknown attribute", "wangset", "wangset empty", "wangsets sharing a name",
        ]),
        new(EncodingLoss, [Op.Save, Op.EditSave], ["UTF-16 little endian", "UTF-16 big endian"]),
        new(EncodingLoss, EveryOp, ["ISO-8859-1"]),
        new(DefaultNamespace, EveryOp, ["default namespace"]),
    ];

    /// <param name="issue">Null for the cases that keep everything, else the pinned cases of one issue.</param>
    public static TheoryData<string, Op> Cases(string? issue)
    {
        var data = new TheoryData<string, Op>();
        foreach (var row in Rows)
            foreach (var op in EveryOp)
                if (!(row.RefusedOnOneLine && op == Op.EditSaveOneLine)
                    && KnownLosses.SingleOrDefault(l => l.Ops.Contains(op) && l.Rows.Contains(row.Name))?.Issue == issue)
                    data.Add(row.Name, op);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases), [null])]
    [MemberData(nameof(Cases), NameProperty, Skip = NameProperty)]
    [MemberData(nameof(Cases), TileOrder, Skip = TileOrder)]
    [MemberData(nameof(Cases), EditedTile, Skip = EditedTile)]
    [MemberData(nameof(Cases), TextIndent, Skip = TextIndent)]
    [MemberData(nameof(Cases), AfterEditedTile, Skip = AfterEditedTile)]
    [MemberData(nameof(Cases), Rewrite, Skip = Rewrite)]
    [MemberData(nameof(Cases), SaveAsLoss, Skip = SaveAsLoss)]
    [MemberData(nameof(Cases), EncodingLoss, Skip = EncodingLoss)]
    [MemberData(nameof(Cases), DefaultNamespace, Skip = DefaultNamespace)]
    public void SaveTsxProject_CorpusFile_KeepsEverything(string rowName, Op op) => RoundTripDiff(rowName, op).ShouldBe("");

    [Fact(Skip = ImagePath)]
    public void SaveTsxProject_SaveAsToAnotherFolder_ImageStillResolves() => ImageSourceAfterSaveAsElsewhere().ShouldBe("../Heroes.png");

    /// <summary>
    /// A pinned case must lose exactly what the fixture lists: no more, and no less. After a fix, remove
    /// the case from <see cref="KnownLosses"/> (and move a file that no longer opens to
    /// <see cref="RefusedRows"/>), then replace the fixture with the file the failure names.
    /// </summary>
    [Fact]
    public void KnownLosses_EveryPinnedCase_LosesWhatTheFixtureLists()
    {
        var actual = new List<string>();
        foreach (var row in KnownLosses.SelectMany(l => l.Rows).Distinct().OrderBy(name => name, StringComparer.Ordinal))
            foreach (var same in KnownLosses.Where(l => l.Rows.Contains(row)).SelectMany(l => l.Ops).GroupBy(op => DiffOrException(row, op)))
                actual.AddRange([$"## {row} [{string.Join(", ", same)}]", .. same.Key.Split('\n')]);
        actual.AddRange(["## Save As to another folder", ImageSourceAfterSaveAsElsewhere()]);
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TsxCorpusKnownLosses.txt");
        var actualPath = Path.ChangeExtension(fixture, ".actual.txt");
        File.WriteAllLines(actualPath, actual);

        Diff(File.ReadAllLines(fixture), [.. actual]).ShouldBe("", actualPath);
    }

    // The tree comparison ignores whitespace, so these compare the text.
    [Theory]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    [InlineData("\r\n", false)]
    public void SaveTsxProject_KeptFile_KeepsLineEndingsAndFinalNewline(string newline, bool finalNewline)
    {
        var xml = Tsx(
            tile0Before: Properties("<property name=\"S\" value=\"a\"/>"), tiles: "<tile id=\"5\" type=\"Water\"/>").Replace("\n", newline);
        var row = new Row("", finalNewline ? xml : xml.TrimEnd());
        var path = WriteFile(row, row.Xml);
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));

        pm.SaveTsxProject();
        var saved = File.ReadAllBytes(path);
        pm.AnimationChainListSave!.AnimationChains[0].Frames[0].FrameLength = 0.3f;
        pm.SaveTsxProject();
        var edited = File.ReadAllText(path);

        saved.ShouldBe(Encoding.UTF8.GetBytes(row.Xml));
        // The regenerated tile closes its empty elements with " />", Tiled with "/>".
        edited.Replace(" />", "/>").ShouldBe(row.Xml.Replace("duration=\"200\"", "duration=\"300\""));
    }

    private string DiffOrException(string rowName, Op op)
    {
        try
        {
            return RoundTripDiff(rowName, op);
        }
        catch (Exception exception)
        {
            return "throws " + exception.GetType().Name;
        }
    }

    /// <summary>What <paramref name="op"/> changes in the row's file, or "" when it keeps everything.</summary>
    private string RoundTripDiff(string rowName, Op op)
    {
        var row = Rows.Single(r => r.Name == rowName);
        var xml = op == Op.EditSaveOneLine ? Regex.Replace(row.Xml, @">\s+<", "><") : row.Xml;
        var path = WriteFile(row, xml);
        var expected = XDocument.Load(path);

        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        var savedPath = path;
        if (op is Op.EditSave or Op.EditSaveOneLine)
        {
            pm.AnimationChainListSave!.AnimationChains[0].Frames[0].FrameLength = 0.3f;
            // A satellite tile (ParentId) of a multi-tile animation shares its anchor's durations.
            foreach (var tile in expected.Descendants().Where(e => e.Name.LocalName == "tile"
                && ((string?)e.Attribute("id") == "0" || e.Descendants("property").Any(p => (string?)p.Attribute("name") == "ParentId"))))
                tile.Descendants().First(e => e.Name.LocalName == "frame").SetAttributeValue("duration", "300");
        }
        else if (op == Op.SaveAs)
            savedPath = Path.Combine(Path.GetDirectoryName(path)!, "Copy.tsx");
        pm.SaveTsxProject(savedPath);

        // DotTiled's reader is stricter than XDocument's, so a file it refuses is caught only here.
        new ProjectManager().LoadTsxProject(new FilePath(savedPath));
        return Diff(Dump(expected), Dump(XDocument.Load(savedPath)));
    }

    private string ImageSourceAfterSaveAsElsewhere()
    {
        var path = WriteFile(Rows[0], Rows[0].Xml);
        var copyPath = Path.Combine(Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path)!, "Copies")).FullName, "Copy.tsx");
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));

        pm.SaveTsxProject(copyPath);

        return (string)XDocument.Load(copyPath).Root!.Element("image")!.Attribute("source")!;
    }

    public static TheoryData<string> RefusedCases() => [.. RefusedRows.Select(r => r.Name)];

    [Theory]
    [MemberData(nameof(RefusedCases))]
    public void LoadTsxProject_FileItCannotKeep_IsRefused(string rowName)
    {
        var row = RefusedRows.Single(r => r.Name == rowName);
        var path = WriteFile(row, row.Xml);

        Should.Throw<Exception>(() => new ProjectManager().LoadTsxProject(new FilePath(path)));
    }

    private string WriteFile(Row row, string xml)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir.Path, Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(folder, "Heroes.tsx");
        File.WriteAllText(path, xml, row.FileEncoding ?? new UTF8Encoding(false));
        if (row.TemplateXml is not null)
            File.WriteAllText(Path.Combine(folder, "Template.tx"), row.TemplateXml);
        return path;
    }

    /// <summary>One line per node, attributes sorted, whitespace-only text left out.</summary>
    private static string[] Dump(XDocument source)
    {
        var document = new XDocument(source);
        Normalise(document);
        var lines = new List<string>();
        void Add(XNode node, int depth)
        {
            var indent = new string(' ', depth);
            switch (node)
            {
                case XElement element:
                    var attributes = element.Attributes().Where(a => !a.IsNamespaceDeclaration)
                        .OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => $" {a.Name}=\"{a.Value}\"");
                    lines.Add($"{indent}<{element.Name}{string.Concat(attributes)}>");
                    foreach (var child in element.Nodes())
                        Add(child, depth + 1);
                    break;
                // A CDATA section and plain text hold the same characters.
                case XText text when !string.IsNullOrWhiteSpace(text.Value):
                    lines.Add($"{indent}\"{text.Value.Replace("\n", "\\n")}\"");
                    break;
                case XComment comment:
                    lines.Add($"{indent}<!--{comment.Value}-->");
                    break;
                case XProcessingInstruction instruction:
                    lines.Add($"{indent}<?{instruction.Target} {instruction.Data}?>");
                    break;
            }
        }
        foreach (var node in document.Nodes())
            Add(node, 0);
        return [.. lines];
    }

    /// <summary>Line diff by longest common subsequence, so a moved or repeated line shows too.</summary>
    private static string Diff(string[] expected, string[] actual)
    {
        var common = new int[expected.Length + 1, actual.Length + 1];
        for (var i = expected.Length - 1; i >= 0; i--)
            for (var j = actual.Length - 1; j >= 0; j--)
                common[i, j] = expected[i] == actual[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);

        var lines = new List<string>();
        for (int i = 0, j = 0; i < expected.Length || j < actual.Length;)
        {
            if (i < expected.Length && j < actual.Length && expected[i] == actual[j])
                (i, j) = (i + 1, j + 1);
            else if (j == actual.Length || (i < expected.Length && common[i + 1, j] >= common[i, j + 1]))
                lines.Add("- " + expected[i++]);
            else
                lines.Add("+ " + actual[j++]);
        }
        return string.Join("\n", lines);
    }
}
