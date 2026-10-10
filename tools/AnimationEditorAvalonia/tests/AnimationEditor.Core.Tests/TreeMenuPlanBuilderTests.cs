using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
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
    public void Build_LockedChainNode_OffersNoWayToAddFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run");
        chain.IsLocked = true;

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.DoesNotContain(items, i => i.Header == "Add Frame");
        Assert.DoesNotContain(items, i => i.HostSlot == TreeMenuHostSlot.AddMultipleFrames);
    }

    [Fact]
    public void Build_FrameNode_AddShapeItemsCarryMatchingIcons()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 1).Frames[0];

        var items = TreeMenuPlanBuilder.Build(
            frame, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(TreeMenuIcon.Rectangle, items.Single(i => i.Header == "Add AxisAlignedRectangle").Icon);
        Assert.Equal(TreeMenuIcon.Circle, items.Single(i => i.Header == "Add Circle").Icon);
        Assert.Equal(TreeMenuIcon.Polygon, items.Single(i => i.Header == "Add Polygon").Icon);
    }

    [Fact]
    public void Build_ChainNode_CommonItemsCarryIcons()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        TestHelpers.MakeChain(ctx.Acls, "Run");

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(TreeMenuIcon.MoveDown, items.Single(i => i.Header == "Move Down").Icon);
        Assert.Equal(TreeMenuIcon.Frame, items.Single(i => i.Header == "Add Frame").Icon);
        Assert.Equal(TreeMenuIcon.Copy, items.Single(i => i.Header == "Copy").Icon);
        Assert.Equal(TreeMenuIcon.Delete, items.Single(i => i.Header == "Delete Animation").Icon);
    }

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

        Assert.True(IndexOf(items, "Move Down") >= 0);
        Assert.True(IndexOf(items, "Move To Bottom") >= 0);
        Assert.Equal(-1, IndexOf(items, "Move Up"));
        Assert.Equal(-1, IndexOf(items, "Move To Top"));
    }

    [Fact]
    public void Build_ChainNode_SingleChain_HasHostSlotsAndCopyPasteRenameDeleteInOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(-1, IndexOf(items, "Move Up")); // single chain: no reorder items
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
        frame.ShapesSave!.Add(circle);

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

        Assert.Equal(-1, IndexOf(items, "Move Up"));
        Assert.Equal(-1, IndexOf(items, "Move Down"));
    }

    [Fact]
    public void Build_RectNode_OnlyRectAmongOtherTypes_ShowsNoMoveItems()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        var rect = new AARectSave { Name = "Rect" };
        frame.ShapesSave!.Add(rect);
        frame.ShapesSave.Add(new CircleSave { Name = "Circle" });

        var items = TreeMenuPlanBuilder.Build(
            rect, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.Equal(-1, IndexOf(items, "Move Down"));
        Assert.Equal(-1, IndexOf(items, "Move Up"));
    }

    [Fact]
    public void Build_RectNode_FirstOfTwoShapes_ShowsMoveDownButNotMoveUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        var rect = new AARectSave { Name = "Rect" };
        var circle = new AARectSave { Name = "Rect2" };
        frame.ShapesSave!.Add(rect);
        frame.ShapesSave.Add(circle);

        var items = TreeMenuPlanBuilder.Build(
            rect, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());

        Assert.True(IndexOf(items, "Move Down") >= 0);
        Assert.True(IndexOf(items, "Move To Bottom") >= 0);
        Assert.Equal(-1, IndexOf(items, "Move Up"));
        Assert.Equal(-1, IndexOf(items, "Move To Top"));
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
        frame.ShapesSave!.Add(rect);
        frame.ShapesSave.Add(circle);

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

    // ── Match Frame Size on a multi-selection (issue #567) ────────────────────

    [Fact]
    public void Build_RectNode_MatchFrameSizeClick_OnMultiSelection_MatchesEveryRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 40f;
        frame.RelativeY = -20f;
        chain.Frames.Add(frame);
        var r0 = new AARectSave { Name = "R0", X = 1f, Y = 1f };
        var r1 = new AARectSave { Name = "R1", X = 2f, Y = 2f };
        frame.ShapesSave!.Add(r0);
        frame.ShapesSave.Add(r1);
        ctx.SelectedState.SelectedNodes = new List<object> { r0, r1 };

        // Menu built for r0 (the right-clicked node) — Click must still act on the whole
        // preserved multi-selection (r0 and r1), not just r0.
        var items = TreeMenuPlanBuilder.Build(
            r0, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());
        items.Single(i => i.Header == "Match Frame Size").OnClick!();

        Assert.Equal(40f, r0.X);
        Assert.Equal(-20f, r0.Y);
        Assert.Equal(40f, r1.X);
        Assert.Equal(-20f, r1.Y);
    }

    [Fact]
    public void Build_RectNode_MatchFrameSizeClick_OnMultiSelectionSpanningFrames_MatchesEachToItsOwnFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frameA = TestHelpers.MakeFrame("a.png");
        var frameB = TestHelpers.MakeFrame("b.png");
        frameA.RelativeX = 5f;  frameA.RelativeY = 6f;
        frameB.RelativeX = 50f; frameB.RelativeY = 60f;
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        var rectInA = new AARectSave { Name = "InA", X = 1f, Y = 1f };
        var rectInB = new AARectSave { Name = "InB", X = 2f, Y = 2f };
        frameA.ShapesSave!.Add(rectInA);
        frameB.ShapesSave!.Add(rectInB);
        ctx.SelectedState.SelectedNodes = new List<object> { rectInA, rectInB };

        var items = TreeMenuPlanBuilder.Build(
            rectInA, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());
        items.Single(i => i.Header == "Match Frame Size").OnClick!();

        // Each rectangle matches its OWN frame, not frameA (the node the menu was built for).
        Assert.Equal(5f, rectInA.X);
        Assert.Equal(6f, rectInA.Y);
        Assert.Equal(50f, rectInB.X);
        Assert.Equal(60f, rectInB.Y);
    }

    private static IReadOnlyList<TreeMenuItem> BuildFor(
        TestServices ctx, object node, params object[] selection)
    {
        ctx.SelectedState.SelectedNodes = selection.ToList();
        return TreeMenuPlanBuilder.Build(
            node, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, NoOpActions());
    }

    [Fact]
    public void Build_FrameNode_SingleSelection_KeepsSingularLabels()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 2).Frames[0];

        var items = BuildFor(ctx, frame, frame);

        Assert.Contains(items, i => i.Header == "Delete Frame");
        Assert.Contains(items, i => i.Header == "Copy");
        Assert.Contains(items, i => i.Header == "Duplicate");
    }

    [Fact]
    public void Build_FrameNode_MultiSelection_LabelsCarryCount()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frames = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 3).Frames;

        var items = BuildFor(ctx, frames[0], frames[0], frames[1], frames[2]);

        Assert.Contains(items, i => i.Header == "Delete 3 Frames");
        Assert.Contains(items, i => i.Header == "Copy 3 Frames");
        Assert.Contains(items, i => i.Header == "Cut 3 Frames");
        Assert.Contains(items, i => i.Header == "Duplicate 3 Frames");
        Assert.Contains(items, i => i.Header == "Paste");
    }

    [Fact]
    public void Build_ChainNode_MultiSelection_CountsAnimationsAndHidesSingleNodeTransforms()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var run = TestHelpers.MakeChain(ctx.Acls, "Run");

        var items = BuildFor(ctx, walk, walk, run);

        Assert.Contains(items, i => i.Header == "Delete 2 Animations");
        Assert.Contains(items, i => i.Header == "Copy 2 Animations");
        Assert.Contains(items, i => i.Header == "Duplicate 2 Animations" && i.Children is not null);
        // These only touch the right-clicked chain, so a multi-selection must not offer them.
        Assert.DoesNotContain(items, i => i.Header is "Flip Horizontally" or "Flip Vertically" or "Invert Frame Order");
        Assert.DoesNotContain(items, i => i.Header == "Copy Qualified Name");
    }

    [Fact]
    public void Build_ChainNode_SingleSelection_KeepsSingleNodeTransforms()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");

        var items = BuildFor(ctx, walk, walk);

        Assert.Contains(items, i => i.Header == "Delete Animation");
        Assert.Contains(items, i => i.Header == "Flip Horizontally");
        Assert.Contains(items, i => i.Header == "Invert Frame Order");
    }

    [Fact]
    public void Build_ShapeNode_MultiSelection_NamesTypeWhenUniformAndShapesWhenMixed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 1).Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        var circle = new CircleSave { Name = "C" };
        frame.ShapesSave!.Add(rectA);
        frame.ShapesSave.Add(rectB);
        frame.ShapesSave.Add(circle);

        var uniform = BuildFor(ctx, rectA, rectA, rectB);
        var mixed = BuildFor(ctx, rectA, rectA, circle);

        Assert.Contains(uniform, i => i.Header == "Delete 2 Rectangles");
        Assert.Contains(mixed, i => i.Header == "Delete 2 Shapes");
        Assert.Contains(mixed, i => i.Header == "Copy 2 Shapes");
        Assert.Contains(uniform, i => i.Header == "Match Frame Size");
    }

    [Fact]
    public void Build_MixedKindSelection_DeleteSaysItems_AndCopyStaysBare()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 2);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "R" };
        frame.ShapesSave!.Add(rect);

        var items = BuildFor(ctx, frame, frame, rect, chain.Frames[1]);

        Assert.Contains(items, i => i.Header == "Delete 3 Items");
        // A mixed selection can't be copied (SelectionCopyContext refuses), so no count is promised.
        Assert.Contains(items, i => i.Header == "Copy");
        Assert.Contains(items, i => i.Header == "Duplicate");
    }
}
