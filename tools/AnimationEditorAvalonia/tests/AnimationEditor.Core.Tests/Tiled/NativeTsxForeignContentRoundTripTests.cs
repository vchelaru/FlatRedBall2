using AnimationEditor.Core;
using AnimationEditor.Core.Tests;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #18 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): a real Tiled tileset
/// carries far more than animations -- root attributes, a background color, image transparency,
/// a grid, a tile offset, properties of every type, per-tile class/probability. Every one of
/// them must survive an ordinary edit-and-save of one animation, both in the normal
/// patch-in-place path and in the full-rewrite fallback the writer takes when it can't slice the
/// file line by line (e.g. a minified single-line tsx).
/// </summary>
public class NativeTsxForeignContentRoundTripTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // Everything Tiled 1.12 can put on a plain (non-collection, no wangsets/transformations/
    // object layers -- those are refused up front) tileset.
    private const string KitchenSinkXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" class="Actors" tilewidth="16" tileheight="16" tilecount="16" columns="4" objectalignment="bottomleft" tilerendersize="grid" fillmode="preserve-aspect-fit" backgroundcolor="#ff336699">
         <tileoffset x="2" y="-3"/>
         <grid orientation="isometric" width="32" height="16"/>
         <properties>
          <property name="Author" value="Vic"/>
          <property name="Speed" type="float" value="1.5"/>
          <property name="Tint" type="color" value="#ff00ff00"/>
          <property name="Sheet" type="file" value="../art/Heroes.png"/>
          <property name="Solid" type="bool" value="true"/>
          <property name="Tier" type="int" value="3"/>
         </properties>
         <image source="../art/Heroes.png" trans="ff00ff" width="64" height="64"/>
         <tile id="0" type="Grass" probability="0.25">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <tile id="5" type="Water" probability="2">
          <properties>
           <property name="Cost" type="int" value="7"/>
          </properties>
         </tile>
        </tileset>
        """;

    private string Write(string xml)
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, xml);
        return path;
    }

    private static void EditFirstFrameDurationAndSave(string path)
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        chain.Frames[0].FrameLength = 0.3f;
        Assert.Empty(pm.SaveTsxProject());
    }

    private static void AssertForeignContentSurvived(string path)
    {
        var raw = File.ReadAllText(path);
        var missing = new[]
        {
            "class=\"Actors\"", "objectalignment=\"bottomleft\"", "tilerendersize=\"grid\"",
            "fillmode=\"preserve-aspect-fit\"", "backgroundcolor=\"#ff336699\"",
            "orientation=\"isometric\"", "width=\"32\" height=\"16\"",
            "name=\"Author\" value=\"Vic\"", "type=\"float\" value=\"1.5\"", "type=\"color\" value=\"#ff00ff00\"",
            "type=\"file\" value=\"../art/Heroes.png\"", "type=\"bool\" value=\"true\"", "type=\"int\" value=\"3\"",
            "source=\"../art/Heroes.png\"",
            "type=\"Grass\"", "probability=\"0.25\"", "type=\"Water\"", "probability=\"2\"",
            "name=\"Cost\" type=\"int\" value=\"7\"",
        }.Where(expected => !raw.Contains(expected)).ToList();
        Assert.True(missing.Count == 0, "Missing: " + string.Join(" | ", missing));

        // Attributes the writer may legitimately reformat (trans gains a '#', self-closing tags
        // gain a space) are checked through the parsed model instead.
        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((2, -3), (reloaded.TileOffset.Value.X, reloaded.TileOffset.Value.Y));
        Assert.Equal(DotTiled.TiledColor.Parse("#ff00ff", null), reloaded.Image.Value.TransparentColor.Value);
        Assert.Equal([((uint)0, 300), ((uint)1, 200)], reloaded.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void EditAndSave_TiledFormattedFile_KeepsEveryForeignAttributeAndElement()
    {
        var path = Write(KitchenSinkXml);

        EditFirstFrameDurationAndSave(path);

        AssertForeignContentSurvived(path);
    }

    // Tiled 1.9 saved a tile's class as `class="..."` (1.10 went back to `type="..."`, which is
    // all DotTiled reads). An edited tile is regenerated from the model, so without a fixup the
    // class vanished from any 1.9-era animated tile the moment its animation was touched -- and a
    // class-only tile whose animation was removed was deleted outright as "empty".
    private const string Tiled19ClassXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.9" tiledversion="1.9.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0" class="Grass">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void EditAndSave_Tiled19ClassAttributeOnEditedTile_KeepsTheClass()
    {
        var path = Write(Tiled19ClassXml);

        EditFirstFrameDurationAndSave(path);

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal("Grass", reloaded.Tiles.Single(t => t.ID == 0).Type);
        Assert.Equal([((uint)0, 300), ((uint)1, 200)], reloaded.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void RemoveAnimationAndSave_Tiled19ClassOnlyTile_KeepsTheTile()
    {
        var path = Write(Tiled19ClassXml);
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        pm.AnimationChainListSave!.AnimationChains.Clear();

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var tile = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal("Grass", tile.Type);
        Assert.Empty(tile.Animation);
    }

    // Tiled always writes width/height on <image>, but a hand-written or tool-generated tsx may
    // not. The loader already falls back to columns*tilewidth by rows*tileheight; the save path
    // built its TilesetAnimationInfo with a null texture size and threw on the first UV->pixel
    // conversion ("Nullable object must have a value"), and since #1157 runs a mapping at open
    // to seed origin tracking, the file wouldn't even open.
    private const string ImageWithoutSizeXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void EditAndSave_ImageWithoutWidthAndHeight_OpensAndSavesUsingTheGridSize()
    {
        var path = Write(ImageWithoutSizeXml);

        EditFirstFrameDurationAndSave(path);

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)0, 300), ((uint)1, 200)], reloaded.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
    }

    // The same content with no line breaks: the patch writer can't slice it per tile, so this
    // exercises the full-rewrite fallback.
    [Fact]
    public void EditAndSave_MinifiedSingleLineFile_KeepsEveryForeignAttributeAndElement()
    {
        var minified = Regex.Replace(KitchenSinkXml, @">\s+<", "><");
        var path = Write(minified);

        EditFirstFrameDurationAndSave(path);

        AssertForeignContentSurvived(path);
    }
}
