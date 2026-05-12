using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

// ── Pure tests (no singleton state) ──────────────────────────────────────────

public class TreeBuilderPureTests
{
    // ── BuildTree ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildTree_EmptyAcls_ReturnsEmptyList()
    {
        var acls = new AnimationChainListSave();
        var result = TreeBuilder.BuildTree(acls);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildTree_SingleChain_CreatesOneRootNode()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        acls.AnimationChains.Add(chain);

        var result = TreeBuilder.BuildTree(acls);

        Assert.Single(result);
        Assert.Equal("Walk", result[0].Header);
        Assert.Same(chain, result[0].Data);
    }

    [Fact]
    public void BuildTree_ChainWithFrames_CreatesChildNodes()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sprites/run1.png" });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sprites/run2.png" });
        acls.AnimationChains.Add(chain);

        var root = TreeBuilder.BuildTree(acls)[0];

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("run1.png", root.Children[0].Header);
        Assert.Equal("run2.png", root.Children[1].Header);
    }

    [Fact]
    public void BuildTree_WithNullExpandedNames_AllNodesDefaultExpanded()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "A" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "B" });

        var result = TreeBuilder.BuildTree(acls, expandedChainNames: null);

        Assert.All(result, n => Assert.True(n.IsExpanded));
    }

    [Fact]
    public void BuildTree_WithExpandedNames_OnlyListedNodesAreExpanded()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });

        var result = TreeBuilder.BuildTree(acls, expandedChainNames: new[] { "Walk", "Idle" });

        Assert.True(result.First(n => n.Header == "Walk").IsExpanded);
        Assert.False(result.First(n => n.Header == "Run").IsExpanded);
        Assert.True(result.First(n => n.Header == "Idle").IsExpanded);
    }

    [Fact]
    public void BuildTree_WithEmptyExpandedNamesList_AllNodesCollapsed()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });

        var result = TreeBuilder.BuildTree(acls, expandedChainNames: Enumerable.Empty<string>());

        Assert.False(result[0].IsExpanded);
    }

    // ── BuildChainNode ────────────────────────────────────────────────────────

    [Fact]
    public void BuildChainNode_SetsHeaderAndData()
    {
        var chain = new AnimationChainSave { Name = "Jump" };
        var node = TreeBuilder.BuildChainNode(chain);
        Assert.Equal("Jump", node.Header);
        Assert.Same(chain, node.Data);
    }

    [Fact]
    public void BuildChainNode_DefaultsIsExpandedTrue()
    {
        var chain = new AnimationChainSave { Name = "Jump" };
        var node = TreeBuilder.BuildChainNode(chain);
        Assert.True(node.IsExpanded);
    }

    [Fact]
    public void BuildChainNode_SetsIsChainNodeTrue()
    {
        var chain = new AnimationChainSave { Name = "Jump" };
        var node = TreeBuilder.BuildChainNode(chain);
        Assert.True(node.IsChainNode);
    }

    [Fact]
    public void BuildFrameNode_HasIsChainNodeFalse()
    {
        var frame = new AnimationFrameSave { TextureName = "run1.png" };
        var node = TreeBuilder.BuildFrameNode(frame);
        Assert.False(node.IsChainNode);
    }

    [Fact]
    public void BuildTree_ChainNodes_HaveIsChainNodeTrue()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });

        var result = TreeBuilder.BuildTree(acls);

        Assert.All(result, n => Assert.True(n.IsChainNode));
    }

    // ── BuildFrameNode & BuildFrameHeader ─────────────────────────────────────

    [Fact]
    public void BuildFrameHeader_WithTexturePath_ReturnsFileNameOnly()
    {
        var frame = new AnimationFrameSave { TextureName = "sprites/player/walk1.png" };
        Assert.Equal("walk1.png", TreeBuilder.BuildFrameHeader(frame));
    }

    [Fact]
    public void BuildFrameHeader_WithNoTextureName_ReturnsUntexturedLabel()
    {
        var frame = new AnimationFrameSave { TextureName = "" };
        Assert.Equal("<UNTEXTURED>", TreeBuilder.BuildFrameHeader(frame));
    }

    [Fact]
    public void BuildFrameNode_WithShapes_AddsShapeChildren()
    {
        var frame = new AnimationFrameSave
        {
            TextureName = "Tex.png",
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave!.AARectSaves.Add(
            new AARectSave { Name = "HitBox" });
        frame.ShapesSave!.CircleSaves.Add(
            new CircleSave { Name = "HurtCircle" });

        var node = TreeBuilder.BuildFrameNode(frame);

        Assert.Equal(2, node.Children.Count);
        Assert.Equal("HitBox",     node.Children[0].Header);
        Assert.Equal("HurtCircle", node.Children[1].Header);
    }

    // ── GetExpandedChainNames ─────────────────────────────────────────────────

    [Fact]
    public void GetExpandedChainNames_ReturnsNamesOfExpandedChainNodes()
    {
        var chainA = new AnimationChainSave { Name = "A" };
        var chainB = new AnimationChainSave { Name = "B" };
        var nodes = new List<TreeNodeVm>
        {
            new TreeNodeVm { Data = chainA, IsExpanded = true  },
            new TreeNodeVm { Data = chainB, IsExpanded = false },
        };

        var names = TreeBuilder.GetExpandedChainNames(nodes).ToList();

        Assert.Single(names);
        Assert.Equal("A", names[0]);
    }

    [Fact]
    public void GetExpandedChainNames_IgnoresExpandedFrameNodes()
    {
        var frame = new AnimationFrameSave { TextureName = "Tex.png" };
        var nodes = new List<TreeNodeVm>
        {
            new TreeNodeVm { Data = frame, IsExpanded = true }
        };

        var names = TreeBuilder.GetExpandedChainNames(nodes).ToList();

        Assert.Empty(names);
    }

    // ── FindNodeForData ───────────────────────────────────────────────────────

    [Fact]
    public void FindNodeForData_FindsRootLevelNode()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var node  = new TreeNodeVm { Data = chain };
        var roots = new List<TreeNodeVm> { node };

        Assert.Same(node, TreeBuilder.FindNodeForData(roots, chain));
    }

    [Fact]
    public void FindNodeForData_FindsNestedFrameNode()
    {
        var frame     = new AnimationFrameSave { TextureName = "Tex.png" };
        var frameNode = new TreeNodeVm { Data = frame };
        var chainNode = new TreeNodeVm { Data = new AnimationChainSave { Name = "Walk" } };
        chainNode.Children.Add(frameNode);

        Assert.Same(frameNode, TreeBuilder.FindNodeForData(new[] { chainNode }, frame));
    }

    [Fact]
    public void FindNodeForData_ReturnsNull_WhenNotFound()
    {
        var roots = new List<TreeNodeVm>
        {
            new TreeNodeVm { Data = new AnimationChainSave { Name = "X" } }
        };
        var unrelated = new AnimationChainSave { Name = "Y" };

        Assert.Null(TreeBuilder.FindNodeForData(roots, unrelated));
    }
}

// ── Singleton-touching tests (RouteNodeSelection) ─────────────────────────────

[Collection("SequentialSingletons")]
public class TreeBuilderSingletonTests
{
    [Fact]
    public void RouteNodeSelection_ChainNode_SetsSelectedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = new AnimationChainSave { Name = "Walk" };
        acls.AnimationChains.Add(chain);
        var vm = new TreeNodeVm { Data = chain };

        var handled = TreeBuilder.RouteNodeSelection(vm.Data, ctx.SelectedState, acls);

        Assert.True(handled);
        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void RouteNodeSelection_FrameNode_SetsSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", frameCount: 1);
        var frame = chain.Frames[0];
        var vm    = new TreeNodeVm { Data = frame };

        var handled = TreeBuilder.RouteNodeSelection(vm.Data, ctx.SelectedState, acls);

        Assert.True(handled);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void RouteNodeSelection_RectangleNode_SetsSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var rect  = new AARectSave { Name = "HitBox" };
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave!.AARectSaves.Add(rect);
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        var vm = new TreeNodeVm { Data = rect };

        var handled = TreeBuilder.RouteNodeSelection(vm.Data, ctx.SelectedState, acls);

        Assert.True(handled);
        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void RouteNodeSelection_CircleNode_SetsSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var circle = new CircleSave { Name = "HurtCircle" };
        var frame  = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave!.CircleSaves.Add(circle);
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        var vm = new TreeNodeVm { Data = circle };

        var handled = TreeBuilder.RouteNodeSelection(vm.Data, ctx.SelectedState, acls);

        Assert.True(handled);
        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void RouteNodeSelection_NullData_ReturnsFalse()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var vm = new TreeNodeVm { Data = null };

        var handled = TreeBuilder.RouteNodeSelection(vm.Data, ctx.SelectedState, acls);

        Assert.False(handled);
    }

    /// <summary>
    /// Regression: clicking the parent frame after a shape is selected must clear the shape.
    /// Without the fix, the guard `if (SelectedFrame != frame)` short-circuits and never
    /// clears SelectedCircle.
    /// </summary>
    [Fact]
    public void RouteNodeSelection_FrameAlreadySelected_WithCircle_ClearsCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", frameCount: 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "C" };

        // Select the frame first, then a shape (SelectedCircle.set does not clear SelectedFrame)
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;
        Assert.Same(circle, ctx.SelectedState.SelectedCircle);

        // Re-click the same frame node — must clear the circle
        TreeBuilder.RouteNodeSelection(frame, ctx.SelectedState, acls);

        Assert.Null(ctx.SelectedState.SelectedCircle);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void RouteNodeSelection_FrameAlreadySelected_WithRect_ClearsRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle", frameCount: 1);
        var frame = chain.Frames[0];
        var rect  = new AARectSave { Name = "HitBox" };

        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;
        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);

        TreeBuilder.RouteNodeSelection(frame, ctx.SelectedState, acls);

        Assert.Null(ctx.SelectedState.SelectedRectangle);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void RouteNodeSelection_ChainAfterFrame_ClearsSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];

        // Select the frame first.
        TreeBuilder.RouteNodeSelection(frame, ctx.SelectedState, acls);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);

        // Now select the parent chain — SelectedFrame must be cleared.
        TreeBuilder.RouteNodeSelection(chain, ctx.SelectedState, acls);

        Assert.Null(ctx.SelectedState.SelectedFrame);
        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }
}
