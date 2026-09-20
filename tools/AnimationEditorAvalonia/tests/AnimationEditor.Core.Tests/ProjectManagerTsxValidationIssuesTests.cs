using AnimationEditor.Core;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerTsxValidationIssuesTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles. Tile 8 is the anchor of a 2-tile group; tile 9's second frame
    // (14) is hand-edited out of lockstep with the anchor's second frame (12), which should
    // be column 1 of that row (13), not 14.
    private const string InconsistentGroupFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="14" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    // 4 columns, 16x16 tiles. Anchor is tile 9, whose own id isn't its own frame-0 tile (frame 0
    // is tile 8) -- the "owner not first frame" pattern that requires SaveTsxProject to consult
    // the _tsxEntryTileIdsByChain hint instead of recomputing from frame geometry. Satellite tile
    // 10's second frame (14) is hand-edited out of lockstep with the anchor's second frame (12);
    // in lockstep it should be tile 13 (anchor's row+1, same column offset as the satellite).
    private const string OwnerNotFirstFrameWithBrokenSatelliteFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="9">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="10">
          <properties>
           <property name="ParentId" type="int" value="9"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="14" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    private const string ConsistentFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
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

    [Fact]
    public void GetChainNamesWithTsxIssues_InconsistentGroup_ReturnsAnchorChainName()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(InconsistentGroupFixtureXml, "Heroes.tsx")));

        var issueChainNames = pm.GetChainNamesWithTsxIssues();

        Assert.Contains("ID:8", issueChainNames);
    }

    [Fact]
    public void GetChainNamesWithTsxIssues_ConsistentProject_ReturnsEmpty()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(ConsistentFixtureXml, "Heroes.tsx")));

        var issueChainNames = pm.GetChainNamesWithTsxIssues();

        Assert.Empty(issueChainNames);
    }

    [Fact]
    public void GetChainNamesWithTsxIssues_NotATsxProject_ReturnsEmpty()
    {
        var pm = new ProjectManager();

        Assert.Empty(pm.GetChainNamesWithTsxIssues());
    }

    // GetChainNamesWithTsxIssues correlates a validator issue's anchor tile id against the entry
    // tile id MultiTileToTiledAnimationMapper.Map computes for each chain, using the SAME
    // _tsxEntryTileIdsByChain/_tsxSatelliteTileIdsByChain hints SaveTsxProject uses. This pins that
    // the two public methods agree in practice (not just that the underlying mapper is
    // deterministic given identical inputs): the flagged chain name must be the one whose anchor
    // tile SaveTsxProject actually keeps writing to (tile 9, not the frame-0-derived tile 8), and a
    // real save must both keep that tile assignment and fix the lockstep violation the issue named.
    [Fact]
    public void GetChainNamesWithTsxIssues_ThenSaveTsxProject_AgreeOnEntryTileIdForFlaggedChain()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameWithBrokenSatelliteFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var issueChainNames = pm.GetChainNamesWithTsxIssues();
        Assert.Equal(["ID:9"], issueChainNames);

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);

        // Anchor must still be tile 9, not relocated to its frame-0 tile (8) -- the same id
        // GetChainNamesWithTsxIssues correlated the issue against.
        var anchorTile = reloaded.Tiles.Single(t => t.ID == 9);
        Assert.Equal([((uint)8, 150), ((uint)12, 150)], anchorTile.Animation.Select(f => (f.TileID, f.Duration)));

        // Satellite must still be tile 10 (not relocated), with its out-of-lockstep frame
        // corrected to match the anchor (13, not the hand-edited 14).
        var satelliteTile = reloaded.Tiles.Single(t => t.ID == 10);
        Assert.Equal([((uint)9, 150), ((uint)13, 150)], satelliteTile.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // After SaveTsxProject fixes the lockstep violation, GetChainNamesWithTsxIssues must no longer
    // flag the chain -- checked both on the same ProjectManager instance (Apply mutates the
    // in-memory tileset directly) and against a fresh load of the file it wrote, so a disagreement
    // in either direction (stale in-memory state or a save that doesn't actually fix the file)
    // would fail this test.
    [Fact]
    public void GetChainNamesWithTsxIssues_AfterSaveFixesLockstep_ReturnsEmpty()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameWithBrokenSatelliteFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));
        Assert.NotEmpty(pm.GetChainNamesWithTsxIssues());

        pm.SaveTsxProject();

        Assert.Empty(pm.GetChainNamesWithTsxIssues());

        var pm2 = new ProjectManager();
        pm2.LoadTsxProject(new FilePath(path));
        Assert.Empty(pm2.GetChainNamesWithTsxIssues());
    }

    // NativeTsxAnimationSync.Apply always overwrites a satellite's on-disk frames to match its
    // anchor -- so if SaveTsxProject runs Apply against the *live* _tsxTileset before TsxWriter.Write
    // is attempted, a write failure (bad target path here; a future disk/permissions failure in
    // general) would leave the in-memory tileset already "fixed" even though nothing was actually
    // persisted, and a subsequent GetChainNamesWithTsxIssues() call would wrongly report no issues.
    [Fact]
    public void GetChainNamesWithTsxIssues_AfterSaveFails_StillReflectsUnsavedOnDiskState()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(InconsistentGroupFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));
        Assert.Equal(["ID:8"], pm.GetChainNamesWithTsxIssues());

        var badPath = Path.Combine(_dir.Path, "does-not-exist", "Heroes.tsx");
        Assert.ThrowsAny<Exception>(() => pm.SaveTsxProject(targetPath: badPath));

        Assert.Equal(["ID:8"], pm.GetChainNamesWithTsxIssues());
        var onDisk = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var satellite = onDisk.Tiles.Single(t => t.ID == 9);
        Assert.Equal([((uint)9, 150), ((uint)14, 150)], satellite.Animation.Select(f => (f.TileID, f.Duration)));
    }
}
