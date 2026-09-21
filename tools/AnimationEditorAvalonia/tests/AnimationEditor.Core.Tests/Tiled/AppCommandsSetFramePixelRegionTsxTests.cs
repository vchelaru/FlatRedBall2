using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #15 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): the wireframe's
/// single-frame handle drag propagates a resize to its chain's sibling frames (<see
/// cref="AnimationEditor.Core.Tiled.FrameFootprintSync"/>), but typing a Width/Height into the
/// inspector goes through <see cref="IAppCommands.SetFramePixelRegion"/>, which didn't -- leaving
/// the chain with mismatched footprints so the save skipped it with a warning.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsSetFramePixelRegionTsxTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles: one chain of two single-tile frames at tiles 0 and 1.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="4" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string LoadTsx()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        return path;
    }

    [Fact]
    public void SetFramePixelRegion_WidthAndHeightInNativeTsxProject_PropagatesSizeToSiblingFrames()
    {
        var path = LoadTsx();
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        _ctx.AppCommands.SetFramePixelRegion([frame0], pixelX: null, pixelY: null, pixelW: 32, pixelH: 32, bmpW: 64, bmpH: 64);

        Assert.Equal(0.5f, frame0.RightCoordinate, precision: 4);
        // Frame 1 keeps its own origin (tile 4: col 0, row 1) and takes the same 2x2-tile size.
        Assert.Equal(0f, frame1.LeftCoordinate, precision: 4);
        Assert.Equal(0.25f, frame1.TopCoordinate, precision: 4);
        Assert.Equal(0.5f, frame1.RightCoordinate, precision: 4);
        Assert.Equal(0.75f, frame1.BottomCoordinate, precision: 4);

        // The autosave that just ran must have written a consistent 2x2 group, not skipped it.
        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([0u, 1u, 4u, 5u], reloaded.Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID));
    }

    [Fact]
    public void SetFramePixelRegion_WidthInNativeTsxProject_UndoRestoresSiblingFrameToo()
    {
        LoadTsx();
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        var frame1 = chain.Frames[1];

        _ctx.AppCommands.SetFramePixelRegion([chain.Frames[0]], pixelX: null, pixelY: null, pixelW: 32, pixelH: null, bmpW: 64, bmpH: 64);
        Assert.Equal(0.5f, frame1.RightCoordinate, precision: 4);

        _ctx.UndoManager.Undo();

        Assert.Equal(0.25f, frame1.RightCoordinate, precision: 4);
    }

    // Moving a frame (X/Y only) never changes the footprint, so its siblings stay where they are.
    [Fact]
    public void SetFramePixelRegion_XOnlyInNativeTsxProject_DoesNotTouchSiblingFrames()
    {
        LoadTsx();
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        var frame1 = chain.Frames[1];

        _ctx.AppCommands.SetFramePixelRegion([chain.Frames[0]], pixelX: 32, pixelY: null, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);

        Assert.Equal(0f, frame1.LeftCoordinate, precision: 4);
        Assert.Equal(0.25f, frame1.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetFramePixelRegion_WidthInPlainAchxProject_DoesNotPropagateToSiblingFrames()
    {
        var chain = new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = "Walk" };
        var frame0 = TestHelpers.MakeFrame();
        var frame1 = TestHelpers.MakeFrame();
        frame0.RightCoordinate = 0.25f;
        frame1.RightCoordinate = 0.25f;
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);
        _ctx.Acls.AnimationChains.Add(chain);

        _ctx.AppCommands.SetFramePixelRegion([frame0], pixelX: null, pixelY: null, pixelW: 32, pixelH: null, bmpW: 64, bmpH: 64);

        Assert.Equal(0.5f, frame0.RightCoordinate, precision: 4);
        Assert.Equal(0.25f, frame1.RightCoordinate, precision: 4);
    }
}
