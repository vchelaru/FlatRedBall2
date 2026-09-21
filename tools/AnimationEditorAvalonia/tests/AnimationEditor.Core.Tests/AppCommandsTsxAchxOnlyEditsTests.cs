using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #1147 pass #22: a native tsx project can't store shapes, flips, sprite offsets, color, or a
/// non-looping chain (<see cref="Tiled.TsxLossyDataCheck"/> warns on save). Pass #17 added the
/// warning; this closes the entry points, at the command level so every host (tree menu, inspector,
/// keyboard, browser) is covered, and in the shared tree-menu plan so the items aren't offered.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsTsxAchxOnlyEditsTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private AnimationChainSave OpenTsx()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
    }

    [Fact]
    public void AddCircleAndAddAxisAlignedRectangle_InNativeTsxProject_DoNothing()
    {
        var chain = OpenTsx();
        var frame = chain.Frames[0];

        _ctx.AppCommands.AddCircle(frame);
        _ctx.AppCommands.AddAxisAlignedRectangle(frame);

        Assert.True(frame.ShapesSave is null || frame.ShapesSave.Shapes.Count == 0);
        Assert.False(_ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetChainLoopAndFlipChain_InNativeTsxProject_DoNothing()
    {
        var chain = OpenTsx();

        _ctx.AppCommands.SetChainLoop(chain, false);
        _ctx.AppCommands.FlipChainHorizontally(chain);

        Assert.True(chain.Loop);
        Assert.All(chain.Frames, f => Assert.False(f.FlipHorizontal));
        Assert.False(_ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void DuplicateChains_FlippedInNativeTsxProject_CopiesWithoutTheFlip()
    {
        var chain = OpenTsx();

        var copy = _ctx.AppCommands.DuplicateChains([chain], flipH: true).Single();

        Assert.All(copy.Frames, f => Assert.False(f.FlipHorizontal));
    }

    [Fact]
    public void TreeMenuPlan_InNativeTsxProject_OffersNoShapeFlipOrOffsetItems()
    {
        var chain = OpenTsx();
        var actions = new TreeMenuActions(
            Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: () => { }, Delete: () => { },
            Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: (_, _) => { });

        var frameItems = TreeMenuPlanBuilder.Build(chain.Frames[0], _ctx.AppCommands, _ctx.SelectedState, _ctx.ObjectFinder, _ctx.ProjectManager, actions);
        var chainItems = TreeMenuPlanBuilder.Build(chain, _ctx.AppCommands, _ctx.SelectedState, _ctx.ObjectFinder, _ctx.ProjectManager, actions);

        Assert.DoesNotContain(frameItems, i => i.Header is "Add AxisAlignedRectangle" or "Add Circle");
        Assert.DoesNotContain(chainItems, i => i.Header is "Flip Horizontally" or "Flip Vertically");
        Assert.DoesNotContain(chainItems, i => i.HostSlot == TreeMenuHostSlot.AdjustOffsets);
        var duplicate = chainItems.Single(i => i.Header == "Duplicate");
        Assert.Null(duplicate.Children);
    }
}
