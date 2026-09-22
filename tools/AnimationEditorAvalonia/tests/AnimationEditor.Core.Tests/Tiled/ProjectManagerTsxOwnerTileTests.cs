using AnimationEditor.Core;
using DotTiled;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Issue #1182: a chain's owner tile (which physical Tiled tile carries its &lt;animation&gt;
/// block) was only ever hidden save-time bookkeeping (<c>ProjectManager._tsxEntryTileIdsByChain</c>).
/// These tests cover the explicit, user-facing surface: reading the current owner
/// (<see cref="ProjectManager.GetTsxOwnerTileId"/>), setting it (<see
/// cref="ProjectManager.TrySetTsxOwnerTileId"/>), and reading a frame's own tile id (<see
/// cref="ProjectManager.ComputeFrameTileId"/>).
/// </summary>
public class ProjectManagerTsxOwnerTileTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, 8 rows (tilecount=32, image 64x128). "RiseUp" is owned by tile 9,
    // but frame 0 is tile 8 -- the "owner not first frame" hand-authored pattern from issue #1182's
    // real repro. "Idle" is a second, unrelated chain owned by tile 20. Tile 16 is a blank, unused
    // cell (no <tile> element at all).
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="32" columns="4">
         <image source="Heroes.png" width="64" height="128"/>
         <tile id="9">
          <properties>
           <property name="Name" value="RiseUp"/>
          </properties>
          <animation>
           <frame tileid="8" duration="300"/>
           <frame tileid="12" duration="300"/>
          </animation>
         </tile>
         <tile id="20">
          <properties>
           <property name="Name" value="Idle"/>
          </properties>
          <animation>
           <frame tileid="20" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private static Tileset Disk(string path) => DotTiled.Serialization.Loader.Default().LoadTileset(path);

    [Fact]
    public void GetTsxOwnerTileId_LoadedChain_ReturnsOwnerFromFile_EvenThoughItIsNotFrameZero()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(TsxFixtureXml, "Heroes.tsx")));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");

        Assert.Equal((uint)9, pm.GetTsxOwnerTileId(chain));
    }

    [Fact]
    public void ComputeFrameTileId_LoadedChainFrame_ReturnsTheFramesOwnTileId()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(TsxFixtureXml, "Heroes.tsx")));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");

        // Frame 0 is tile 8 -- distinct from the chain's owner tile (9), exactly the mismatch
        // issue #1182 asks the Inspector to make visible.
        Assert.Equal((uint)8, pm.ComputeFrameTileId(chain.Frames[0]));
        Assert.Equal((uint)12, pm.ComputeFrameTileId(chain.Frames[1]));
    }

    [Fact]
    public void ComputeFrameTileId_NotATsxProject_ReturnsNull()
    {
        var pm = new ProjectManager();
        var frame = new FlatRedBall2.AnimationEditorCommon.AnimationFrameSave();

        Assert.Null(pm.ComputeFrameTileId(frame));
    }

    [Fact]
    public void TrySetTsxOwnerTileId_ValidUnclaimedTile_ReturnsNullAndSaveMovesTheAnimation()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(TsxFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");

        var error = pm.TrySetTsxOwnerTileId(chain, 16);

        Assert.Null(error);
        Assert.Equal((uint)16, pm.GetTsxOwnerTileId(chain));

        pm.SaveTsxProject();
        var reloaded = Disk(path);

        var newOwner = reloaded.Tiles.Single(t => t.ID == 16);
        Assert.Equal([((uint)8, 300), ((uint)12, 300)], newOwner.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("RiseUp", newOwner.Properties.OfType<StringProperty>().Single(p => p.Name == "Name").Value);

        // The old owner tile must be cleared, not left as an orphaned second copy.
        var oldOwner = reloaded.Tiles.SingleOrDefault(t => t.ID == 9);
        Assert.True(oldOwner is null || oldOwner.Animation.Count == 0);
    }

    [Fact]
    public void TrySetTsxOwnerTileId_TileAlreadyOwnedByAnotherChain_ReturnsErrorAndDoesNotMutate()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(TsxFixtureXml, "Heroes.tsx")));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");

        var error = pm.TrySetTsxOwnerTileId(chain, 20); // "Idle"'s own owner tile

        Assert.NotNull(error);
        Assert.Contains("Idle", error);
        Assert.Equal((uint)9, pm.GetTsxOwnerTileId(chain)); // unchanged
    }

    [Fact]
    public void TrySetTsxOwnerTileId_TileIdPastTileCount_ReturnsErrorAndDoesNotMutate()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(TsxFixtureXml, "Heroes.tsx")));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");

        var error = pm.TrySetTsxOwnerTileId(chain, 999);

        Assert.NotNull(error);
        Assert.Equal((uint)9, pm.GetTsxOwnerTileId(chain));
    }

    [Fact]
    public void TrySetTsxOwnerTileId_NotATsxProject_ReturnsError()
    {
        var pm = new ProjectManager();
        var chain = new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = "Foo" };

        Assert.NotNull(pm.TrySetTsxOwnerTileId(chain, 0));
    }
}
