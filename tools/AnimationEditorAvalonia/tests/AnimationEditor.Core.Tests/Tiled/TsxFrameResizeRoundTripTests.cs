using AnimationEditor.Core;
using AnimationEditor.Core.Tests;
using AnimationEditor.Core.Tiled;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Every edge a user can grab on a native-tsx chain's frames (left/top/right/bottom), grown and
/// shrunk, across the three ways a chain's entry tile can relate to its frame-0 geometry: derived
/// by our own save, loaded from a file whose owner tile is frame 0's own top-left cell, and loaded
/// from a file whose owner tile is deliberately unrelated to the frames. Each case saves, then
/// reloads the written file through <see cref="ProjectManager.LoadTsxProject"/> and checks the
/// chain comes back with exactly the edited rects and the expected owner tile -- a round trip is
/// the only assertion that catches an anchor/satellite/ParentId set that is internally
/// inconsistent (e.g. a satellite the loader can't attach to its anchor).
/// </summary>
public class TsxFrameResizeRoundTripTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, 12 rows (tilecount=48, image 64x192). Every chain below starts as a
    // 2x2 footprint: frame 0 at cols 1..3 rows 1..3 (origin tile 5), frame 1 at cols 1..3 rows
    // 4..6 (origin tile 17) -- one free tile of room on every side for the grow cases.
    private const string TilesetHeader = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="48" columns="4">
         <image source="Heroes.png" width="64" height="192"/>
        """;

    private const string EmptyFixtureXml = TilesetHeader + """

        </tileset>
        """;

    // Owner tile 5 is frame 0's own top-left cell; satellites sit at their physical cells.
    private const string OwnerIsOriginFixtureXml = TilesetHeader + """

         <tile id="5">
          <animation>
           <frame tileid="5" duration="100"/>
           <frame tileid="17" duration="100"/>
          </animation>
         </tile>
         <tile id="6">
          <properties>
           <property name="ParentId" type="int" value="5"/>
          </properties>
          <animation>
           <frame tileid="6" duration="100"/>
           <frame tileid="18" duration="100"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="5"/>
          </properties>
          <animation>
           <frame tileid="9" duration="100"/>
           <frame tileid="21" duration="100"/>
          </animation>
         </tile>
         <tile id="10">
          <properties>
           <property name="ParentId" type="int" value="5"/>
          </properties>
          <animation>
           <frame tileid="10" duration="100"/>
           <frame tileid="22" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Owner tile 36 (row 9, col 0) has nothing to do with the frames it animates (rows 1..6);
    // its satellites sit next to the OWNER (37, 40, 41), not next to the frames -- the
    // hand-authored pattern TiledAnimationToAchjMapper reads satellites by.
    private const string OwnerUnrelatedFixtureXml = TilesetHeader + """

         <tile id="36">
          <animation>
           <frame tileid="5" duration="100"/>
           <frame tileid="17" duration="100"/>
          </animation>
         </tile>
         <tile id="37">
          <properties>
           <property name="ParentId" type="int" value="36"/>
          </properties>
          <animation>
           <frame tileid="6" duration="100"/>
           <frame tileid="18" duration="100"/>
          </animation>
         </tile>
         <tile id="40">
          <properties>
           <property name="ParentId" type="int" value="36"/>
          </properties>
          <animation>
           <frame tileid="9" duration="100"/>
           <frame tileid="21" duration="100"/>
          </animation>
         </tile>
         <tile id="41">
          <properties>
           <property name="ParentId" type="int" value="36"/>
          </properties>
          <animation>
           <frame tileid="10" duration="100"/>
           <frame tileid="22" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteFixture(string xml)
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, xml);
        return path;
    }

    private static void SetGridRect(AnimationFrameSave frame, int colStart, int colEnd, int rowStart, int rowEnd)
    {
        const float tileX = 16f / 64f;
        const float tileY = 16f / 192f;
        frame.LeftCoordinate = colStart * tileX;
        frame.RightCoordinate = colEnd * tileX;
        frame.TopCoordinate = rowStart * tileY;
        frame.BottomCoordinate = rowEnd * tileY;
    }

    /// <summary>Applies one edge move (in whole tiles) to both frames of the 2x2 base geometry.</summary>
    private static void Resize(AnimationChainSave chain, int dLeft, int dTop, int dRight, int dBottom)
    {
        SetGridRect(chain.Frames[0], 1 + dLeft, 3 + dRight, 1 + dTop, 3 + dBottom);
        SetGridRect(chain.Frames[1], 1 + dLeft, 3 + dRight, 4 + dTop, 6 + dBottom);
    }

    private static AnimationChainSave NewBaseChain(string name)
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f });
        Resize(chain, 0, 0, 0, 0);
        return chain;
    }

    private static void AssertRoundTrip(string path, AnimationChainSave edited, uint expectedEntryTileId)
    {
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var anchors = tileset.Tiles
            .Where(t => t.Animation.Count > 0
                && !t.Properties.Any(p => p.Name == "ParentId")
                && (t.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == "Name")?.Value ?? $"ID:{t.ID}") == edited.Name)
            .ToList();
        Assert.Equal([expectedEntryTileId], anchors.Select(t => t.ID));

        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        var reloaded = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == edited.Name);
        Assert.Equal(edited.Frames.Count, reloaded.Frames.Count);
        for (var i = 0; i < edited.Frames.Count; i++)
        {
            Assert.Equal(edited.Frames[i].LeftCoordinate, reloaded.Frames[i].LeftCoordinate, 4);
            Assert.Equal(edited.Frames[i].TopCoordinate, reloaded.Frames[i].TopCoordinate, 4);
            Assert.Equal(edited.Frames[i].RightCoordinate, reloaded.Frames[i].RightCoordinate, 4);
            Assert.Equal(edited.Frames[i].BottomCoordinate, reloaded.Frames[i].BottomCoordinate, 4);
        }
    }

    /// <summary>Every way a user can drag the frame rect, as (dLeft, dTop, dRight, dBottom) in
    /// whole tiles, and the owner tile an origin-following chain ends up on. Edges, corners
    /// (two edges at once), and whole-rect moves (all four edges by the same amount). Only a
    /// left/top change moves the origin cell; right/bottom-only changes leave the owner at 5.</summary>
    public static TheoryData<string, int, int, int, int, uint> EdgeMoves => new()
    {
        { "grow left", -1, 0, 0, 0, 4u },
        { "shrink left", 1, 0, 0, 0, 6u },
        { "grow top", 0, -1, 0, 0, 1u },
        { "shrink top", 0, 1, 0, 0, 9u },
        { "grow right", 0, 0, 1, 0, 5u },
        { "shrink right", 0, 0, -1, 0, 5u },
        { "grow bottom", 0, 0, 0, 1, 5u },
        { "shrink bottom", 0, 0, 0, -1, 5u },
        { "grow top-left corner", -1, -1, 0, 0, 0u },
        { "shrink top-left corner", 1, 1, 0, 0, 10u },
        { "grow top-right corner", 0, -1, 1, 0, 1u },
        { "shrink top-right corner", 0, 1, -1, 0, 9u },
        { "grow bottom-left corner", -1, 0, 0, 1, 4u },
        { "shrink bottom-left corner", 1, 0, 0, -1, 6u },
        { "grow bottom-right corner", 0, 0, 1, 1, 5u },
        { "shrink bottom-right corner", 0, 0, -1, -1, 5u },
        { "move left", -1, 0, -1, 0, 4u },
        { "move right", 1, 0, 1, 0, 6u },
        { "move up", 0, -1, 0, -1, 1u },
        { "move down", 0, 1, 0, 1, 9u },
        { "move down-right", 1, 1, 1, 1, 10u },
    };

    /// <summary><see cref="EdgeMoves"/> without the owner column, for the chain whose owner
    /// never moves.</summary>
    public static TheoryData<string, int, int, int, int> EdgeMovesOnly
    {
        get
        {
            var data = new TheoryData<string, int, int, int, int>();
            foreach (var row in EdgeMoves)
                data.Add((string)row[0], (int)row[1], (int)row[2], (int)row[3], (int)row[4]);
            return data;
        }
    }

    // Left/top moves relocate frame 0's origin cell, so the owner follows it; right/bottom never
    // move the origin, so the owner stays at 5.
    [Theory]
    [MemberData(nameof(EdgeMoves))]
    public void Resize_AutoDerivedChain_RoundTripsWithOwnerAtFrameZeroOrigin(string _, int dLeft, int dTop, int dRight, int dBottom, uint expectedEntryTileId)
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5 derived from frame 0

        Resize(chain, dLeft, dTop, dRight, dBottom);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId);
    }

    // A file whose owner tile is frame 0's own origin cell is the shape our own saves write, so it
    // follows the origin exactly like an auto-derived chain -- even though the file, not us,
    // authored the hint.
    [Theory]
    [MemberData(nameof(EdgeMoves))]
    public void Resize_LoadedChainWithOwnerAtOrigin_RoundTripsWithOwnerAtFrameZeroOrigin(string _, int dLeft, int dTop, int dRight, int dBottom, uint expectedEntryTileId)
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerIsOriginFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single();

        Resize(chain, dLeft, dTop, dRight, dBottom);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId);
    }

    // An owner tile unrelated to the frames is a deliberate hand-authored choice: no resize ever
    // moves it, and any satellite a grow adds must sit next to the OWNER (where the loader looks
    // for it), not at the frame's physical cell.
    [Theory]
    [MemberData(nameof(EdgeMovesOnly))]
    public void Resize_LoadedChainWithUnrelatedOwner_RoundTripsWithOwnerUnchanged(string _, int dLeft, int dTop, int dRight, int dBottom)
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerUnrelatedFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single();

        Resize(chain, dLeft, dTop, dRight, dBottom);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId: 36);
    }

    // Tiles carry the user's own Tiled data too (custom properties, a class, collision shapes).
    // The tile an owner vacates keeps all of it (only the animation and our tracking properties
    // go), and the tile it lands on keeps its own alongside the new animation.
    [Fact]
    public void Resize_ShrinkLeftTransfersOwner_VacatedAndClaimedTilesKeepTheirOwnProperties()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(TilesetHeader + """

             <tile id="5" type="Grass">
              <properties>
               <property name="Cost" type="int" value="3"/>
              </properties>
             </tile>
             <tile id="6">
              <properties>
               <property name="Cost" type="int" value="7"/>
              </properties>
             </tile>
             <tile id="9">
              <properties>
               <property name="Cost" type="int" value="2"/>
              </properties>
             </tile>
            </tileset>
            """);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5, satellites 6, 9, 10

        Resize(chain, dLeft: 1, dTop: 0, dRight: 0, dBottom: 0);
        pm.SaveTsxProject();

        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var vacated = tileset.Tiles.Single(t => t.ID == 5);
        Assert.Empty(vacated.Animation);
        Assert.Equal("Grass", vacated.Type);
        Assert.Equal(["Cost"], vacated.Properties.Select(p => p.Name));
        Assert.Equal(3, vacated.GetProperty<IntProperty>("Cost").Value);

        var claimed = tileset.Tiles.Single(t => t.ID == 6);
        Assert.Equal([((uint)6, 100), ((uint)18, 100)], claimed.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal(7, claimed.GetProperty<IntProperty>("Cost").Value);
        Assert.Equal("Hero", claimed.GetProperty<StringProperty>("Name").Value);

        // The vacated satellite loses its animation and ParentId, nothing else.
        var vacatedSatellite = tileset.Tiles.Single(t => t.ID == 9);
        Assert.Empty(vacatedSatellite.Animation);
        Assert.Equal(["Cost"], vacatedSatellite.Properties.Select(p => p.Name));
        Assert.Equal(2, vacatedSatellite.GetProperty<IntProperty>("Cost").Value);
    }

    // A transfer must be reversible: a resize followed by its Undo (the rect put back exactly)
    // lands the owner and every satellite back on the tiles they started on.
    [Fact]
    public void Resize_GrowLeftThenRevert_OwnerReturnsToOriginalTile()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5

        Resize(chain, dLeft: -1, dTop: 0, dRight: 0, dBottom: 0);
        pm.SaveTsxProject(); // owner tile 4
        Resize(chain, 0, 0, 0, 0);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId: 5);
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 4);
        Assert.Equal([5u, 6u, 9u, 10u], tileset.Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID));
    }

    // Half-way through a drag only frame 0 has the new rect (or a command resized one frame
    // without its siblings). That save can't map the chain, so it must leave the file and the
    // owner alone and report it; the transfer happens on the save where the siblings match.
    [Fact]
    public void Resize_OnlyFrameZeroGrownLeft_WarnsAndKeepsOwnerUntilSiblingsMatch()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5

        SetGridRect(chain.Frames[0], colStart: 0, colEnd: 3, rowStart: 1, rowEnd: 3);
        var warnings = pm.SaveTsxProject();

        Assert.Contains("Hero", Assert.Single(warnings));
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)5, 100), ((uint)17, 100)], tileset.Tiles.Single(t => t.ID == 5).Animation.Select(f => (f.TileID, f.Duration)));
        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 4);

        SetGridRect(chain.Frames[1], colStart: 0, colEnd: 3, rowStart: 4, rowEnd: 6);
        Assert.Empty(pm.SaveTsxProject());

        AssertRoundTrip(path, chain, expectedEntryTileId: 4);
    }

    // The tile the origin moves onto can already be another chain's owner. Two animations can't
    // share a tile, so the save refuses (same as any other geometry collision) and the file keeps
    // its pre-edit content instead of one chain silently swallowing the other.
    [Fact]
    public void Resize_GrowLeftOntoAnotherChainsOwnerTile_ThrowsAndLeavesFileUntouched()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var hero = NewBaseChain("Hero");
        var other = new AnimationChainSave { Name = "Other" };
        other.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f });
        other.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f });
        SetGridRect(other.Frames[0], colStart: 0, colEnd: 1, rowStart: 1, rowEnd: 2); // tile 4
        SetGridRect(other.Frames[1], colStart: 0, colEnd: 1, rowStart: 4, rowEnd: 5); // tile 16
        pm.AnimationChainListSave!.AnimationChains.Add(hero);
        pm.AnimationChainListSave.AnimationChains.Add(other);
        pm.SaveTsxProject(); // Hero owns 5, Other owns 4

        Resize(hero, dLeft: -1, dTop: 0, dRight: 0, dBottom: 0); // Hero's origin now wants 4

        var ex = Assert.Throws<InvalidOperationException>(() => pm.SaveTsxProject());
        Assert.Contains("Hero", ex.Message);
        Assert.Contains("Other", ex.Message);
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)5, 100), ((uint)17, 100)], tileset.Tiles.Single(t => t.ID == 5).Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal([((uint)4, 100), ((uint)16, 100)], tileset.Tiles.Single(t => t.ID == 4).Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Resizing one chain never touches another chain's tiles.
    [Fact]
    public void Resize_OneOfTwoChains_OtherChainKeepsItsTiles()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var hero = NewBaseChain("Hero");
        var other = new AnimationChainSave { Name = "Other" };
        other.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f });
        SetGridRect(other.Frames[0], colStart: 0, colEnd: 2, rowStart: 8, rowEnd: 9); // tiles 32, 33
        pm.AnimationChainListSave!.AnimationChains.Add(hero);
        pm.AnimationChainListSave.AnimationChains.Add(other);
        pm.SaveTsxProject();

        Resize(hero, dLeft: 1, dTop: 0, dRight: 0, dBottom: 0);
        pm.SaveTsxProject();

        AssertRoundTrip(path, hero, expectedEntryTileId: 6);
        AssertRoundTrip(path, other, expectedEntryTileId: 32);
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)33, 100)], tileset.Tiles.Single(t => t.ID == 33).Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Moving a frame other than frame 0 changes what the tiles animate, not which tiles own it.
    [Fact]
    public void Resize_MoveOnlyFrameOne_OwnerStays()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5

        SetGridRect(chain.Frames[1], colStart: 0, colEnd: 2, rowStart: 7, rowEnd: 9); // origin 28
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId: 5);
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)5, 100), ((uint)28, 100)], tileset.Tiles.Single(t => t.ID == 5).Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Deleting every frame parks the owner as a dormant hint; Undo re-inserts the same frame
    // objects and revives it. The revived owner is still frame 0's cell, so it must still follow
    // that cell on a later resize instead of having forgotten where it came from.
    [Fact]
    public void Resize_AfterAllFramesDeletedAndRestored_OwnerStillFollowsOrigin()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5

        var frames = chain.Frames.ToArray();
        chain.Frames.Clear();
        pm.SaveTsxProject();
        chain.Frames.AddRange(frames);
        pm.SaveTsxProject(); // revived on tile 5

        Resize(chain, dLeft: 1, dTop: 0, dRight: 0, dBottom: 0);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId: 6);
    }

    // Switching tabs parks the whole tsx state (TabEditorCache) and restores it later; the
    // origin tracking must survive that round trip like every other hint does.
    [Fact]
    public void Resize_AfterTsxStateCapturedAndRestored_OwnerStillFollowsOrigin()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(path));
        var chain = NewBaseChain("Hero");
        var acls = pm.AnimationChainListSave!;
        acls.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // owner tile 5

        var parked = pm.CaptureTsxState();
        var otherPath = Path.Combine(_dir.Path, "Other.tsx");
        File.WriteAllText(otherPath, EmptyFixtureXml);
        pm.LoadTsxProject(new FilePath(otherPath));
        pm.AnimationChainListSave = acls;
        pm.FileName = path;
        pm.RestoreTsxState(parked);

        Resize(chain, dLeft: 1, dTop: 0, dRight: 0, dBottom: 0);
        pm.SaveTsxProject();

        AssertRoundTrip(path, chain, expectedEntryTileId: 6);
    }
}
