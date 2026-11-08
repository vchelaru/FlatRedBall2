using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Pins found by the mutation audit of the chain flip PRs (plan/ae-test-audit-4.md): the Flip Diagonally
/// menu item and the Duplicate submenu items each do their own axis, and the diagonal chain flip carries
/// its label, its pose, its refreshes and the native tsx refusal like the other flips.
/// </summary>
[Collection("SequentialSingletons")]
public class ChainFlipAuditTests
{
    private static TreeMenuActions Actions(Action<ChainFlip> onDuplicateFlip, Action? onDuplicate = null) => new(
        Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: onDuplicate ?? (() => { }), Delete: () => { },
        Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: onDuplicateFlip);

    private static IReadOnlyList<TreeMenuItem> ChainMenu(TestServices ctx, AnimationChainSave chain, TreeMenuActions actions) =>
        TreeMenuPlanBuilder.Build(chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, actions);

    [Fact]
    public void TheFlipDiagonallyMenuItem_FlipsEveryFrameOfTheChain_AndOnlyDiagonally()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 2);   // frames: a click on an empty chain would pass for any action
        var items = ChainMenu(ctx, chain, Actions(_ => { }));

        items.Single(i => i.Header == "Flip Diagonally").OnClick!();

        Assert.All(chain.Frames, f => Assert.True(f.FlipDiagonal));
        Assert.All(chain.Frames, f => Assert.False(f.FlipHorizontal));
        Assert.All(chain.Frames, f => Assert.False(f.FlipVertical));
    }

    [Fact]
    public void TheDuplicateSubmenu_EachItemRequestsOnlyItsOwnAxis_AndOriginalRequestsNone()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var requested = new List<ChainFlip>();
        int plainDuplicates = 0;
        var items = ChainMenu(ctx, chain, Actions(requested.Add, () => plainDuplicates++));
        var submenu = items.Single(i => i.Header == "Duplicate").Children!;

        foreach (var header in new[] { "Flip Horizontal", "Flip Vertical", "Flip Diagonal" })
            submenu.Single(i => i.Header == header).OnClick!();
        submenu.Single(i => i.Header == "Original").OnClick!();

        Assert.Equal(new[] { ChainFlip.Horizontal, ChainFlip.Vertical, ChainFlip.Diagonal }, requested);
        Assert.Equal(1, plainDuplicates);
    }

    [Theory]
    [InlineData(1, "Flip Diagonal")]
    [InlineData(3, "Flip 3 Frames Diagonal")]
    public void FlipChainDiagonally_IsOneUndoEntry_NamedForTheAxisAndTheFrameCount(int frames, string label)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frames);

        ctx.AppCommands.FlipChainDiagonally(chain);

        Assert.Equal(label, Assert.Single(ctx.UndoManager.UndoHistory).Description);
    }

    [Fact]
    public void FlipChainDiagonally_RaisesFramesFlipped_WithTheDiagonalFlagInBothPoses()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        chain.Frames[0].RelativeX = 3;
        chain.Frames[0].RelativeY = 7;
        IReadOnlyList<FrameFlipChange>? raised = null;
        ctx.ApplicationEvents.FramesFlipped += changes => raised = changes;

        ctx.AppCommands.FlipChainDiagonally(chain);

        var change = Assert.Single(raised!);
        Assert.Equal(new FlipPose(false, false, false, 3f, 7f), change.Before);
        Assert.Equal(new FlipPose(false, false, true, 7f, 3f), change.After);
    }

    [Theory]
    [InlineData("Horizontally")]
    [InlineData("Vertically")]
    [InlineData("Diagonally")]
    public void FlippingAChain_RefreshesItsTreeNode_AndTheWireframe(string axis)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var refreshedChains = new List<AnimationChainSave>();
        int wireframeRefreshes = 0;
        ctx.AppCommands.RefreshChainNodeRequested += refreshedChains.Add;
        ctx.AppCommands.RefreshWireframeRequested += () => wireframeRefreshes++;

        switch (axis)
        {
            case "Horizontally": ctx.AppCommands.FlipChainHorizontally(chain); break;
            case "Vertically": ctx.AppCommands.FlipChainVertically(chain); break;
            default: ctx.AppCommands.FlipChainDiagonally(chain); break;
        }

        Assert.Contains(chain, refreshedChains);
        Assert.True(wireframeRefreshes > 0);
    }

    [Fact]
    public void FlippingTheCopyOfANativeTsxChain_IsRefused_SoItsDirectionWordsStayAsTheyWere()
    {
        using var dir = new TestHelpers.TempDir();
        var ctx = new TestServices();
        var path = Path.Combine(dir.Path, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="0"><animation><frame tileid="0" duration="200"/></animation></tile>
            </tileset>
            """);
        ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        chain.Name = "WalkRight";

        var copy = ctx.AppCommands.DuplicateChains([chain], ChainFlip.Horizontal).Single();

        Assert.Equal("WalkRight2", copy.Name);
    }
}
