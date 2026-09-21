using AnimationEditor.Core.Tests;
using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #20 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): the achx-push path
/// (<see cref="TiledTilesetSyncRunner"/>) writes into a tsx the user also edits in Tiled, so it is
/// the path most likely to meet a real, fully-decorated Tiled file. Same kitchen sink as
/// <see cref="NativeTsxForeignContentRoundTripTests"/>, plus a hand-authored animation and a
/// Tiled 1.9 class-only tile that the push must never touch, through push, re-push after the
/// chain was removed, and the minified full-rewrite fallback.
/// </summary>
public class AchxPushForeignContentRoundTripTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private const string KitchenSinkXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.9" tiledversion="1.9.2" name="Heroes" class="Actors" tilewidth="16" tileheight="16" tilecount="16" columns="4" objectalignment="bottomleft" backgroundcolor="#ff336699">
         <tileoffset x="2" y="-3"/>
         <properties>
          <property name="Author" value="Vic"/>
          <property name="Tint" type="color" value="#ff00ff00"/>
         </properties>
         <image source="Heroes.png" trans="ff00ff" width="64" height="64"/>
         <tile id="5" class="Water" probability="2">
          <animation>
           <frame tileid="5" duration="150"/>
           <frame tileid="6" duration="150"/>
          </animation>
         </tile>
         <tile id="9" class="Grass"/>
        </tileset>
        """;

    private string WriteTsx(string xml)
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, xml);
        return path;
    }

    private static AnimationChainListSave AchjWithWalkAtTiles0And1()
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16 });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, LeftCoordinate = 16, TopCoordinate = 0, RightCoordinate = 32, BottomCoordinate = 16 });
        save.AnimationChains.Add(chain);
        // A second chain lands on tile 9, the class-only tile: the push may claim it, and the
        // later removal must give it back with its class intact rather than deleting it.
        var idle = new AnimationChainSave { Name = "Idle" };
        idle.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, LeftCoordinate = 16, TopCoordinate = 32, RightCoordinate = 32, BottomCoordinate = 48 });
        save.AnimationChains.Add(idle);
        return save;
    }

    private void PushThenRemoveAndPushAgain(string tsxPath)
    {
        var achxPath = Path.Combine(_dir.Path, "Hero.achx");
        var first = TiledTilesetSyncRunner.SyncAll(AchjWithWalkAtTiles0And1(), achxPath, [tsxPath]);
        Assert.True(first[0].Success, first[0].Error?.ToString());
        var afterPush = TsxLoader.LoadTileset(tsxPath);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], afterPush.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
        var claimedIdle = afterPush.Tiles.Single(t => t.ID == 9);
        Assert.Equal([((uint)9, 100)], claimedIdle.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Grass", claimedIdle.Type);
        AssertForeignContentSurvived(tsxPath);

        var second = TiledTilesetSyncRunner.SyncAll(new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }, achxPath, [tsxPath]);
        Assert.True(second[0].Success, second[0].Error?.ToString());
        var afterRemoval = TsxLoader.LoadTileset(tsxPath);
        Assert.DoesNotContain(afterRemoval.Tiles, t => t.ID == 0);
        Assert.Empty(afterRemoval.Tiles.Single(t => t.ID == 9).Animation);
        AssertForeignContentSurvived(tsxPath);
    }

    private static void AssertForeignContentSurvived(string path)
    {
        var raw = File.ReadAllText(path);
        var missing = new[]
        {
            "class=\"Actors\"", "objectalignment=\"bottomleft\"", "backgroundcolor=\"#ff336699\"",
            "name=\"Author\" value=\"Vic\"", "type=\"color\" value=\"#ff00ff00\"", "probability=\"2\"",
        }.Where(expected => !raw.Contains(expected)).ToList();
        Assert.True(missing.Count == 0, "Missing: " + string.Join(" | ", missing));

        // TsxLoader, not the raw DotTiled loader: an untouched 1.9 tile keeps its original
        // `class="..."` text verbatim in the patch path, which only TsxLoader reads.
        var reloaded = TsxLoader.LoadTileset(path);
        Assert.Equal((2, -3), (reloaded.TileOffset.Value.X, reloaded.TileOffset.Value.Y));
        // The hand-authored animation and the class-only tile are not the push's to touch.
        var water = reloaded.Tiles.Single(t => t.ID == 5);
        Assert.Equal("Water", water.Type);
        Assert.Equal([((uint)5, 150), ((uint)6, 150)], water.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Grass", reloaded.Tiles.Single(t => t.ID == 9).Type);
    }

    [Fact]
    public void SyncAll_TiledFormattedFile_KeepsForeignContentAndHandAuthoredTilesThroughPushAndRemoval()
    {
        PushThenRemoveAndPushAgain(WriteTsx(KitchenSinkXml));
    }

    [Fact]
    public void SyncAll_MinifiedSingleLineFile_KeepsForeignContentAndHandAuthoredTilesThroughPushAndRemoval()
    {
        PushThenRemoveAndPushAgain(WriteTsx(Regex.Replace(KitchenSinkXml, @">\s+<", "><")));
    }
}
