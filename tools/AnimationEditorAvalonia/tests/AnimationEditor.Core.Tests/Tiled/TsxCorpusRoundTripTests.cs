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
/// <see cref="Op"/>; the saved bytes must equal the original ones, apart from the edited duration.
/// </summary>
public class TsxCorpusRoundTripTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    public enum Op
    {
        /// <summary>Open, save.</summary>
        Save,

        /// <summary>Open, change one frame's duration on the first animated tile, save.</summary>
        EditSave,

        /// <summary><see cref="EditSave"/> on a file with no line breaks, as Tiled's "minimize output" writes it.</summary>
        EditSaveOneLine,

        /// <summary>Open, save to a path that does not exist yet.</summary>
        SaveAs,
    }

    /// <param name="Xml">The whole file. Every row that opens has an animated tile to edit.</param>
    /// <param name="FileEncoding">Null is UTF-8 without a byte order mark.</param>
    /// <param name="TemplateXml">Written beside the file as <c>Template.tx</c>.</param>
    private sealed record Row(
        string Name, string Xml, Encoding? FileEncoding = null, string? TemplateXml = null);

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
            $"<wangset name=\"Unnamed Set\" type=\"corner\" tile=\"-1\">{WangColor}{WangTile}</wangset>\n  <wangset name=\"Unnamed Set\" type=\"edge\" tile=\"-1\">{WangColor}{WangTile}</wangset>"))),
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
        new("attributes in single quotes", Tsx(tile0Attributes: " type='a \"b\"'").Replace("tileid=\"1\" duration=\"100\"", "duration = '100'  tileid='1'")),
        new("spaces inside tags", Tsx(tiles: "<tile id=\"5\"  type=\"Water\"   />\n <tile id=\"6\" ></tile >")),
        new("mixed line endings", Tsx(tiles: "<tile id=\"5\" type=\"A\"/>\r\n <tile id=\"6\" type=\"B\"/>")),

        // Constructs DotTiled cannot read.
        new("missing object template", Tsx(tile0Before: Objects("<object id=\"1\" template=\"Missing.tx\" x=\"1\" y=\"2\"/>"))),
        new("wangid in hex (before 1.5)", Tsx(tail: Wangsets($"<wangset name=\"Ground\" tile=\"-1\">{WangColor}<wangtile tileid=\"0\" wangid=\"0x01010101\"/></wangset>"))),
        new("property list (1.12)", Tsx(tile0Before: Properties("<property name=\"L\" type=\"list\"><item type=\"int\" value=\"10\"/><item value=\"text\"/></property>"))),
        new("property list on the tileset (1.12)", Tsx(head: "<properties>\n  <property name=\"L\" type=\"list\">\n   <item type=\"int\" value=\"10\"/>\n  </property>\n </properties>")),
        new("property unknown type", Tsx(tile0Before: Properties("<property name=\"V\" type=\"vector\" value=\"1,2\"/>"))),
        new("properties unknown child", Tsx(tile0Before: Properties("<property name=\"S\" value=\"text\"/><future a=\"1\"/>"))),
        new("objectgroup without id (before 1.2)", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\"/>", " draworder=\"index\""))),
        new("object unknown child", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"><future/></object>"))),
        new("object capsule (1.12)", Tsx(tile0Before: Objects("<object id=\"1\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"><capsule/></object>"))),
        new("no XML declaration", Tsx(prolog: "")),
        new("two wangsets on one line", Tsx(tail: Wangsets($"<wangset name=\"A\" tile=\"-1\">{WangColor}{WangTile}</wangset><wangset name=\"B\" tile=\"-1\">{WangColor}{WangTile}</wangset>"))),
    ];

    /// <summary>Files the editor refuses to open, so nothing in them can be lost.</summary>
    private static readonly Row[] RefusedRows =
    [
        new("image collection", Tsx(image: "", tile0Before: "<image source=\"a.png\" width=\"16\" height=\"16\"/>")),
        new("image collection with an embedded image", Tsx(image: "", tile0Before: "<image format=\"png\"><data encoding=\"base64\">iVBORw0KGgo=</data></image>")),
        new("sheet with one extra tile image", Tsx(tile0Before: "<image source=\"a.png\" width=\"16\" height=\"16\"/>")),
        new("no image", Tsx(image: "")),
        new("two tiles with one id", Tsx(tiles: "<tile id=\"5\" type=\"A\"/>\n <tile id=\"5\" type=\"B\"/>")),
        new("DOCTYPE with an entity", Tsx(tile0Attributes: " type=\"&e;\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE tileset [<!ENTITY e \"ext\">]>\n")),
        new("tileset pointing at another file", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<tileset firstgid=\"1\" source=\"Other.tsx\"/>\n"),
        new("map file", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<map version=\"1.10\" orientation=\"orthogonal\" width=\"1\" height=\"1\" tilewidth=\"16\" tileheight=\"16\"/>\n"),
        new("image embedded data", Tsx(image: "<image format=\"png\" width=\"64\" height=\"64\">\n  <data encoding=\"base64\">iVBORw0KGgo=</data>\n </image>")),
        new("UTF-16 little endian", Tsx(tile0Attributes: " type=\"Größe 日本\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"), Encoding.Unicode),
        new("UTF-16 big endian", Tsx(tile0Attributes: " type=\"Größe 日本\"", prolog: "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"), Encoding.BigEndianUnicode),
        new("ISO-8859-1", Tsx(tile0Attributes: " type=\"Größe\"", tiles: "<tile id=\"5\" type=\"Wäter\"/>", prolog: "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n"), Encoding.Latin1),
        new("truncated file", Tsx().Replace("</tileset>", "")),
    ];

    public static TheoryData<string, Op> Cases()
    {
        var data = new TheoryData<string, Op>();
        foreach (var row in Rows)
            foreach (var op in Enum.GetValues<Op>())
                data.Add(row.Name, op);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SaveTsxProject_CorpusFile_ChangesOnlyTheEditedDuration(string rowName, Op op)
    {
        var row = Rows.Single(r => r.Name == rowName);
        var xml = op == Op.EditSaveOneLine ? Regex.Replace(row.Xml, @">\s+<", "><") : row.Xml;
        var path = WriteFile(row, xml);
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        var savedPath = op == Op.SaveAs ? Path.Combine(Path.GetDirectoryName(path)!, "Copy.tsx") : path;
        if (op is Op.EditSave or Op.EditSaveOneLine)
        {
            pm.AnimationChainListSave!.AnimationChains[0].Frames[0].FrameLength = 0.3f;
            // Every row's first animated tile, and the satellites that share its durations, hold the only 200s.
            xml = xml.Replace("duration=\"200\"", "duration=\"300\"");
        }

        pm.SaveTsxProject(savedPath);

        File.ReadAllBytes(savedPath).ShouldBe(Bytes(row, xml));
        new ProjectManager().LoadTsxProject(new FilePath(savedPath));
    }

    /// <summary>A copy of every <c>.tsx</c> in the repository that the editor opens.</summary>
    [Fact]
    public void SaveTsxProject_RepositoryFileWithNoEdit_KeepsEveryByte()
    {
        var opened = 0;
        foreach (var original in RepositoryFiles())
        {
            var path = Path.Combine(_dir.Path, Guid.NewGuid().ToString("N") + ".tsx");
            File.Copy(original, path);
            var pm = new ProjectManager();
            try
            {
                pm.LoadTsxProject(new FilePath(path));
            }
            catch (NotSupportedException)
            {
                continue;
            }
            opened++;

            pm.SaveTsxProject();

            File.ReadAllBytes(path).ShouldBe(File.ReadAllBytes(original), original);
        }
        opened.ShouldBeGreaterThan(5);
    }

    private static IEnumerable<string> RepositoryFiles()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(root.FullName, ".claude", "templates")))
            root = root.Parent!;
        return new[] { ".claude/templates", "samples", "templates", "tests", "tools/AnimationEditorAvalonia/tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root.FullName, folder), "*.tsx", SearchOption.AllDirectories))
            .Where(file => !file.Split('/', '\\').Any(part => part is "bin" or "obj"));
    }

    [Fact]
    public void SaveTsxProject_SaveAsToAnotherFolder_ImageStillResolves() => ImageSourceAfterSaveAsElsewhere().ShouldBe("../Heroes.png");

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
        File.WriteAllBytes(path, Bytes(row, xml));
        if (row.TemplateXml is not null)
            File.WriteAllText(Path.Combine(folder, "Template.tx"), row.TemplateXml);
        return path;
    }

    private static byte[] Bytes(Row row, string xml)
    {
        var encoding = row.FileEncoding ?? new UTF8Encoding(false);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)];
    }
}
