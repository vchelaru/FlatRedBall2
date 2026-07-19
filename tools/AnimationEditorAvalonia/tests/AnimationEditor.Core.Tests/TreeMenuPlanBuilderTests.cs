using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreeMenuPlanBuilderTests
{
    private static TreeMenuActions NoOpActions() => new(
        Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: () => { }, Delete: () => { },
        Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: (_, _) => { });

    private static int IndexOf(IReadOnlyList<TreeMenuItem> items, string header) =>
        items.ToList().FindIndex(i => i.Header == header);

    [Fact]
    public void Build_ChainNode_DuplicateIsSubmenuWithThreeChildren()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run");

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        var duplicate = items.Single(i => i.Header == "Duplicate");
        Assert.Equal(new[] { "Original", "Flip Horizontal", "Flip Vertical" }, duplicate.Children!.Select(c => c.Header));
    }

    [Fact]
    public void Build_ChainNode_MultipleChains_FirstChain_HasMoveDownButNotMoveUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        TestHelpers.MakeChain(ctx.Acls, "Run");

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.True(IndexOf(items, "v  Move Down") >= 0);
        Assert.True(IndexOf(items, "vv Move To Bottom") >= 0);
        Assert.Equal(-1, IndexOf(items, "^  Move Up"));
        Assert.Equal(-1, IndexOf(items, "^^ Move To Top"));
    }

    [Fact]
    public void Build_ChainNode_SingleChain_HasHostSlotsAndCopyPasteRenameDeleteInOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(-1, IndexOf(items, "^  Move Up")); // single chain: no reorder items
        Assert.Equal(TreeMenuHostSlot.AdjustFrameTime, items[0].HostSlot);
        int copy = IndexOf(items, "Copy");
        Assert.True(copy >= 0);
        Assert.Equal(copy + 1, IndexOf(items, "Cut"));
        Assert.Equal(copy + 2, IndexOf(items, "Paste"));
        Assert.Contains(items, i => i.HostSlot == TreeMenuHostSlot.AddMultipleFrames);
        Assert.Contains(items, i => i.HostSlot == TreeMenuHostSlot.AdjustOffsets);
        Assert.True(IndexOf(items, "Rename…") >= 0);
        Assert.True(IndexOf(items, "Delete Animation") >= 0);
        Assert.Equal("Sort Animations Alphabetically", items[^1].Header);
    }

    [Fact]
    public void Build_CircleNode_HasCopyPasteDuplicateRename_AndNoMatchFrameSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave!.Shapes.Add(circle);

        var items = TreeMenuPlanBuilder.Build(
            circle, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        int copy = IndexOf(items, "Copy");
        Assert.True(copy >= 0);
        Assert.Equal(copy + 1, IndexOf(items, "Cut"));
        Assert.Equal(copy + 2, IndexOf(items, "Paste"));
        Assert.Equal(copy + 3, IndexOf(items, "Duplicate"));
        Assert.True(IndexOf(items, "Rename…") >= 0);
        Assert.True(IndexOf(items, "Delete Circle") >= 0);
        Assert.Equal(-1, IndexOf(items, "Match Frame Size"));
    }

    [Fact]
    public void Build_EmptySelection_OnlyAddAnimationThenSort()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        var items = TreeMenuPlanBuilder.Build(
            null, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(3, items.Count);
        Assert.Equal("Add Animation", items[0].Header);
        Assert.True(items[1].IsSeparator);
        Assert.Equal("Sort Animations Alphabetically", items[2].Header);
    }

    [Fact]
    public void Build_FrameNode_HasAddShapeItemsAndViewTextureHostSlotAndDeleteFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame("run.png");
        chain.Frames.Add(frame);

        var items = TreeMenuPlanBuilder.Build(
            frame, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.True(IndexOf(items, "Add AxisAlignedRectangle") >= 0);
        Assert.True(IndexOf(items, "Add Circle") >= 0);
        Assert.Contains(items, i => i.HostSlot == TreeMenuHostSlot.ViewTextureInExplorer);
        Assert.True(IndexOf(items, "Delete Frame") >= 0);
    }

    [Fact]
    public void Build_FrameNode_SingleFrameInChain_NoReorderItems()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame("run.png");
        chain.Frames.Add(frame);

        var items = TreeMenuPlanBuilder.Build(
            frame, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(-1, IndexOf(items, "^  Move Up"));
        Assert.Equal(-1, IndexOf(items, "v  Move Down"));
    }

    [Fact]
    public void Build_RectNode_FirstOfTwoShapes_ShowsMoveDownButNotMoveUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        var rect = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);

        var items = TreeMenuPlanBuilder.Build(
            rect, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.True(IndexOf(items, "v  Move Down") >= 0);
        Assert.True(IndexOf(items, "vv Move To Bottom") >= 0);
        Assert.Equal(-1, IndexOf(items, "^  Move Up"));
        Assert.Equal(-1, IndexOf(items, "^^ Move To Top"));
    }

    [Fact]
    public void Build_RectNode_HasMatchFrameSizeCopyPasteDuplicateRenameAndDelete()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        var rect = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);

        var items = TreeMenuPlanBuilder.Build(
            rect, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        int copy = IndexOf(items, "Copy");
        Assert.True(copy >= 0);
        Assert.Equal(copy + 1, IndexOf(items, "Cut"));
        Assert.Equal(copy + 2, IndexOf(items, "Paste"));
        Assert.Equal(copy + 3, IndexOf(items, "Duplicate"));
        Assert.True(IndexOf(items, "Match Frame Size") >= 0);
        Assert.True(IndexOf(items, "Rename…") >= 0);
        Assert.True(IndexOf(items, "Delete Rectangle") >= 0);
    }
}
