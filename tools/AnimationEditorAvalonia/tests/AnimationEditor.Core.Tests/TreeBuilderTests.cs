using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        Assert.Equal("Frame 1", root.Children[0].Header);
        Assert.Equal("Frame 2", root.Children[1].Header);
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
    public void BuildFrameHeader_WithTextureNameButNoExplicitName_ReturnsFrameIndex()
    {
        var frame = new AnimationFrameSave { TextureName = "sprites/player/walk1.png" };
        Assert.Equal("Frame 1", TreeBuilder.BuildFrameHeader(frame));
    }

    [Fact]
    public void BuildFrameHeader_WithNoTextureName_ReturnsFrameIndexLabel()
    {
        var frame = new AnimationFrameSave { TextureName = "" };
        Assert.Equal("Frame 1", TreeBuilder.BuildFrameHeader(frame, 0));
    }

    [Fact]
    public void BuildFrameHeader_NameOverridesTextureFilename()
    {
        // A user-assigned Name (HasCustomName=true) takes precedence over the
        // texture filename so that inline rename on a textured frame changes the
        // display label without touching the texture reference.
        var frame = new AnimationFrameSave
        {
            TextureName = "sprites/walk1.png",
            HasCustomName = true,
            Name = "Walk Start",
        };
        Assert.Equal("Walk Start", TreeBuilder.BuildFrameHeader(frame));
    }

    [Fact]
    public void BuildFrameNode_WithShapes_AddsShapeChildren()
    {
        var frame = new AnimationFrameSave
        {
            TextureName = "Tex.png",
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave!.Shapes.Add(
            new AARectSave { Name = "HitBox" });
        frame.ShapesSave!.Shapes.Add(
            new CircleSave { Name = "HurtCircle" });

        var node = TreeBuilder.BuildFrameNode(frame);

        Assert.Equal(2, node.Children.Count);
        Assert.Equal("HitBox",     node.Children[0].Header);
        Assert.Equal("HurtCircle", node.Children[1].Header);
    }

    [Fact]
    public void BuildFrameNode_SetsNodeFlags_ForFrameAndShapes()
    {
        var frame = new AnimationFrameSave
        {
            TextureName = "",
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave!.Shapes.Add(new AARectSave { Name = "Rect1" });
        frame.ShapesSave!.Shapes.Add(new CircleSave { Name = "Circle1" });

        var node = TreeBuilder.BuildFrameNode(frame, index: 1);

        Assert.True(node.IsFrameNode);
        Assert.Equal("Frame 2", node.Header);
        Assert.Equal(NodeKind.Frame, node.Kind);
        Assert.Equal(NodeKind.RectShape, node.Children[0].Kind);
        Assert.True(node.Children[0].IsRectNode);
        Assert.Equal(NodeKind.CircleShape, node.Children[1].Kind);
        Assert.True(node.Children[1].IsCircleNode);
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

    // ── SyncChainsInto ────────────────────────────────────────────────────────

    [Fact]
    public void SyncChainsInto_AddedChain_InsertsExpandedVm()
    {
        // Regression (#237): a pasted chain is new — it gets the default expanded
        // state, while the pre-existing collapsed chain keeps its state.
        var existing = new AnimationChainSave { Name = "Walk" };
        var roots = new ObservableCollection<TreeNodeVm> { TreeBuilder.BuildChainNode(existing) };
        roots[0].IsExpanded = false;  // user collapsed the existing chain
        var pasted = new AnimationChainSave { Name = "Walk2" };

        TreeBuilder.SyncChainsInto(roots, new[] { existing, pasted });

        Assert.Equal(2, roots.Count);
        Assert.False(roots[0].IsExpanded);   // pre-existing chain stays collapsed
        Assert.Same(pasted, roots[1].Data);
        Assert.True(roots[1].IsExpanded);    // new chain defaults to expanded
    }

    [Fact]
    public void SyncChainsInto_RemovedChain_DropsVm()
    {
        var chainA = new AnimationChainSave { Name = "Walk" };
        var chainB = new AnimationChainSave { Name = "Run" };
        var roots = new ObservableCollection<TreeNodeVm>
        {
            TreeBuilder.BuildChainNode(chainA),
            TreeBuilder.BuildChainNode(chainB),
        };

        TreeBuilder.SyncChainsInto(roots, new[] { chainB });

        Assert.Single(roots);
        Assert.Same(chainB, roots[0].Data);
    }

    [Fact]
    public void SyncChainsInto_Reorder_PreservesIsExpanded()
    {
        // Regression (#237): reordering chains must not re-expand a collapsed chain.
        var chainA = new AnimationChainSave { Name = "Walk" };
        var chainB = new AnimationChainSave { Name = "Run" };
        var roots = new ObservableCollection<TreeNodeVm>
        {
            TreeBuilder.BuildChainNode(chainA),
            TreeBuilder.BuildChainNode(chainB),
        };
        roots[0].IsExpanded = false;  // user collapsed "Walk"
        var vmA = roots[0];

        TreeBuilder.SyncChainsInto(roots, new[] { chainB, chainA });

        Assert.False(vmA.IsExpanded);
    }

    [Fact]
    public void SyncChainsInto_Reorder_ReusesExistingChainVm()
    {
        var chainA = new AnimationChainSave { Name = "Walk" };
        var chainB = new AnimationChainSave { Name = "Run" };
        var roots = new ObservableCollection<TreeNodeVm>
        {
            TreeBuilder.BuildChainNode(chainA),
            TreeBuilder.BuildChainNode(chainB),
        };
        var originalVmA = roots[0];
        var originalVmB = roots[1];

        TreeBuilder.SyncChainsInto(roots, new[] { chainB, chainA });

        Assert.Same(originalVmA, roots[1]);
        Assert.Same(originalVmB, roots[0]);
    }

    [Fact]
    public void SyncChainsInto_RetainedChain_RefreshesHeaderMetaAndSyncsFrames()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var roots = new ObservableCollection<TreeNodeVm> { TreeBuilder.BuildChainNode(chain) };

        // Mutate the data model the way a rename + add-frame would.
        chain.Name = "Walk Renamed";
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png" });

        TreeBuilder.SyncChainsInto(roots, new[] { chain });

        Assert.Equal("Walk Renamed", roots[0].Header);
        Assert.Equal("1 fr", roots[0].Meta);
        Assert.Single(roots[0].Children);
        Assert.Equal("Frame 1", roots[0].Children[0].Header);
    }

    // ── SyncFramesInto ────────────────────────────────────────────────────────

    [Fact]
    public void SyncFramesInto_Reorder_ReusesExistingFrameVm()
    {
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));

        var originalVmA = chainNode.Children[0];
        var originalVmB = chainNode.Children[1];

        // Reorder: put B first
        TreeBuilder.SyncFramesInto(chainNode, new[] { frameB, frameA });

        Assert.Same(originalVmA, chainNode.Children[1]);
        Assert.Same(originalVmB, chainNode.Children[0]);
    }

    [Fact]
    public void SyncFramesInto_Reorder_ChildOrderMatchesNewFrameOrder()
    {
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        var frameC = new AnimationFrameSave { TextureName = "c.png" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameC, 2));

        TreeBuilder.SyncFramesInto(chainNode, new[] { frameC, frameA, frameB });

        Assert.Same(frameC, chainNode.Children[0].Data);
        Assert.Same(frameA, chainNode.Children[1].Data);
        Assert.Same(frameB, chainNode.Children[2].Data);
    }

    [Fact]
    public void SyncFramesInto_Reorder_PreservesIsExpanded()
    {
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));

        chainNode.Children[0].IsExpanded = true;  // mark frameA VM expanded
        var vmA = chainNode.Children[0];

        TreeBuilder.SyncFramesInto(chainNode, new[] { frameB, frameA });

        Assert.True(vmA.IsExpanded);
    }

    [Fact]
    public void SyncFramesInto_AddedFrame_InsertsNewVm()
    {
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        var frameC = new AnimationFrameSave { TextureName = "c.png" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));

        TreeBuilder.SyncFramesInto(chainNode, new[] { frameA, frameC, frameB });

        Assert.Equal(3, chainNode.Children.Count);
        Assert.Same(frameC, chainNode.Children[1].Data);
    }

    [Fact]
    public void SyncFramesInto_RemovedFrame_RemovesVm()
    {
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        var frameC = new AnimationFrameSave { TextureName = "c.png" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameC, 2));

        TreeBuilder.SyncFramesInto(chainNode, new[] { frameA, frameC });

        Assert.Equal(2, chainNode.Children.Count);
        Assert.Same(frameA, chainNode.Children[0].Data);
        Assert.Same(frameC, chainNode.Children[1].Data);
    }

    [Fact]
    public void SyncFramesInto_Reorder_DynamicLabelsUpdateToReflectNewPosition()
    {
        // Auto-named frames display "Frame N" based on their current index.
        // After a reorder the label CHANGES to reflect the new position.
        var frameA = new AnimationFrameSave { TextureName = "" };
        var frameB = new AnimationFrameSave { TextureName = "" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));  // "Frame 1"
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));  // "Frame 2"

        // Move frameA to index 1 (swap order)
        TreeBuilder.SyncFramesInto(chainNode, new[] { frameB, frameA });

        // Both frames now have positional labels matching their new positions.
        Assert.Equal("Frame 1", chainNode.Children[0].Header);  // frameB is now at index 0
        Assert.Equal("Frame 2", chainNode.Children[1].Header);  // frameA is now at index 1
    }

    [Fact]
    public void SyncFramesInto_MoveThirdToTop_DynamicLabelsRenumber()
    {
        // Dynamic labels renumber after reorder — the frame moved to index 0 now shows "Frame 1".
        var frameA = new AnimationFrameSave { TextureName = "" };
        var frameB = new AnimationFrameSave { TextureName = "" };
        var frameC = new AnimationFrameSave { TextureName = "" };
        var chainNode = new TreeNodeVm();
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameA, 0));  // "Frame 1"
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameB, 1));  // "Frame 2"
        chainNode.Children.Add(TreeBuilder.BuildFrameNode(frameC, 2));  // "Frame 3"

        TreeBuilder.SyncFramesInto(chainNode, new[] { frameC, frameA, frameB });

        Assert.Equal("Frame 1", chainNode.Children[0].Header);  // frameC at index 0
        Assert.Equal("Frame 2", chainNode.Children[1].Header);  // frameA at index 1
        Assert.Equal("Frame 3", chainNode.Children[2].Header);  // frameB at index 2
    }

    [Fact]
    public void BuildFrameNode_AutoNamed_DoesNotPersistNameToModel()
    {
        // Dynamic-label design: auto-named frames display "Frame N" at render time;
        // the Name field is intentionally left empty so reorder updates the label.
        var frame = new AnimationFrameSave { TextureName = "" };

        TreeBuilder.BuildFrameNode(frame, 2);  // position 2 → display "Frame 3", but Name stays empty

        Assert.False(frame.HasCustomName);
        Assert.Equal(string.Empty, frame.Name);
    }

    [Fact]
    public void BuildFrameNode_AutoNamed_TexturedFrameDoesNotPersistName()
    {
        // Textured frames are also auto-named dynamically — Name must not be set.
        var frame = new AnimationFrameSave { TextureName = "sprites/walk1.png" };

        TreeBuilder.BuildFrameNode(frame, 0);

        Assert.False(frame.HasCustomName);
        Assert.Equal(string.Empty, frame.Name);
    }

    [Fact]
    public void BuildFrameNode_CustomNamed_PreservesNameAndFlag()
    {
        // A frame with HasCustomName=true keeps its Name through BuildFrameNode.
        var frame = new AnimationFrameSave { HasCustomName = true, Name = "Jump Frame" };

        TreeBuilder.BuildFrameNode(frame, 0);

        Assert.True(frame.HasCustomName);
        Assert.Equal("Jump Frame", frame.Name);
    }

    [Fact]
    public void BuildTree_CustomNamedFramesReordered_PreservesCustomLabels()
    {
        // Custom-named frames (HasCustomName=true) keep their display name regardless
        // of position — the label is sticky, not positional.
        var frameA = new AnimationFrameSave { HasCustomName = true, Name = "Idle" };
        var frameB = new AnimationFrameSave { HasCustomName = true, Name = "Walk" };
        var frameC = new AnimationFrameSave { HasCustomName = true, Name = "Run" };

        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Anim" };
        chain.Frames.Add(frameC);  // reordered: C first
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        acls.AnimationChains.Add(chain);

        var nodes = TreeBuilder.BuildTree(acls);

        Assert.Equal("Run",  nodes[0].Children[0].Header);
        Assert.Equal("Idle", nodes[0].Children[1].Header);
        Assert.Equal("Walk", nodes[0].Children[2].Header);
    }

    // ── ExpandAncestorsOf ─────────────────────────────────────────────────────

    [Fact]
    public void ExpandAncestorsOf_FrameTarget_ExpandsParentChainButNotTheFrame()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        var frame = new AnimationFrameSave { TextureName = "a.png" };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        var roots = TreeBuilder.BuildTree(acls);
        foreach (var r in roots) TreeNodeVm.SetExpandedRecursive(r, false);

        TreeBuilder.ExpandAncestorsOf(roots, frame);

        Assert.True(roots[0].IsExpanded);                 // parent chain revealed
        Assert.False(roots[0].Children[0].IsExpanded);    // target frame itself not expanded
    }

    [Fact]
    public void ExpandAncestorsOf_ShapeTarget_ExpandsParentFrameAndChain()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        var circle = new CircleSave { Radius = 4 };
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(circle);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        var roots = TreeBuilder.BuildTree(acls);
        foreach (var r in roots) TreeNodeVm.SetExpandedRecursive(r, false);

        TreeBuilder.ExpandAncestorsOf(roots, circle);

        Assert.True(roots[0].IsExpanded);                 // chain
        Assert.True(roots[0].Children[0].IsExpanded);     // parent frame
    }
}

// ── SyncShapesInto tests ──────────────────────────────────────────────────────

public class TreeBuilderSyncShapesTests
{
    private static TreeNodeVm FrameNodeWithRect(out AARectSave rect)
    {
        rect = new AARectSave { Name = "HitBox" };
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        return TreeBuilder.BuildFrameNode(frame);
    }

    private static TreeNodeVm FrameNodeWithCircle(out CircleSave circle)
    {
        circle = new CircleSave { Name = "Hurt" };
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(circle);
        return TreeBuilder.BuildFrameNode(frame);
    }

    [Fact]
    public void SyncShapesInto_ExistingRectVm_IsPreservedByReference()
    {
        var frameNode = FrameNodeWithRect(out var rect);
        var originalVm = frameNode.Children[0];
        var ss = new ShapesSave();
        ss.Shapes.Add(rect);

        TreeBuilder.SyncShapesInto(frameNode, ss);

        Assert.Same(originalVm, frameNode.Children[0]);
    }

    [Fact]
    public void SyncShapesInto_ExistingCircleVm_IsPreservedByReference()
    {
        var frameNode = FrameNodeWithCircle(out var circle);
        var originalVm = frameNode.Children[0];
        var ss = new ShapesSave();
        ss.Shapes.Add(circle);

        TreeBuilder.SyncShapesInto(frameNode, ss);

        Assert.Same(originalVm, frameNode.Children[0]);
    }

    [Fact]
    public void SyncShapesInto_RectNameChange_UpdatesHeader()
    {
        var frameNode = FrameNodeWithRect(out var rect);
        rect.Name = "NewName";
        var ss = new ShapesSave();
        ss.Shapes.Add(rect);

        TreeBuilder.SyncShapesInto(frameNode, ss);

        Assert.Equal("NewName", frameNode.Children[0].Header);
    }

    [Fact]
    public void SyncShapesInto_CircleNameChange_UpdatesHeader()
    {
        var frameNode = FrameNodeWithCircle(out var circle);
        circle.Name = "NewCircle";
        var ss = new ShapesSave();
        ss.Shapes.Add(circle);

        TreeBuilder.SyncShapesInto(frameNode, ss);

        Assert.Equal("NewCircle", frameNode.Children[0].Header);
    }

    [Fact]
    public void SyncShapesInto_AddedRect_IsInserted()
    {
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        var frameNode = TreeBuilder.BuildFrameNode(frame);
        var newRect = new AARectSave { Name = "New" };
        frame.ShapesSave.Shapes.Add(newRect);

        TreeBuilder.SyncShapesInto(frameNode, frame.ShapesSave);

        Assert.Single(frameNode.Children);
        Assert.Same(newRect, frameNode.Children[0].Data);
    }

    [Fact]
    public void SyncShapesInto_RemovedRect_IsRemoved()
    {
        var frameNode = FrameNodeWithRect(out _);
        Assert.Single(frameNode.Children);

        TreeBuilder.SyncShapesInto(frameNode, new ShapesSave());

        Assert.Empty(frameNode.Children);
    }

    [Fact]
    public void SyncShapesInto_NullShapesSave_ClearsAllShapeChildren()
    {
        var frameNode = FrameNodeWithRect(out _);
        Assert.Single(frameNode.Children);

        TreeBuilder.SyncShapesInto(frameNode, null);

        Assert.Empty(frameNode.Children);
    }

    [Fact]
    public void SyncShapesInto_PreservesRectsBeforeCirclesOrder()
    {
        var rect   = new AARectSave  { Name = "R" };
        var circle = new CircleSave  { Name = "C" };
        var frame  = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);
        var frameNode  = TreeBuilder.BuildFrameNode(frame);
        var originalRectVm   = frameNode.Children[0];
        var originalCircleVm = frameNode.Children[1];

        TreeBuilder.SyncShapesInto(frameNode, frame.ShapesSave);

        Assert.Same(originalRectVm,   frameNode.Children[0]);
        Assert.Same(originalCircleVm, frameNode.Children[1]);
    }
}

// ── SyncFramesInto tests ──────────────────────────────────────────────────────

public class TreeBuilderSyncFramesTests
{
    [Fact]
    public void SyncFramesInto_ExistingFrameVm_IsPreservedByReference()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "walk1.png" };
        chain.Frames.Add(frame);
        var chainNode = TreeBuilder.BuildChainNode(chain);
        var originalVm = chainNode.Children[0];

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        Assert.Same(originalVm, chainNode.Children[0]);
    }

    [Fact]
    public void SyncFramesInto_ReorderedFrames_VmsAreReordered()
    {
        var chain  = new AnimationChainSave { Name = "Walk" };
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        var chainNode = TreeBuilder.BuildChainNode(chain);
        var vmA = chainNode.Children[0];
        var vmB = chainNode.Children[1];

        chain.Frames.Clear();
        chain.Frames.Add(frameB);
        chain.Frames.Add(frameA);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        Assert.Same(vmB, chainNode.Children[0]);
        Assert.Same(vmA, chainNode.Children[1]);
    }

    [Fact]
    public void SyncFramesInto_IsExpanded_PreservedAfterReorder()
    {
        var chain  = new AnimationChainSave { Name = "Walk" };
        var frameA = new AnimationFrameSave { TextureName = "a.png" };
        var frameB = new AnimationFrameSave { TextureName = "b.png" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        var chainNode = TreeBuilder.BuildChainNode(chain);
        chainNode.Children[0].IsExpanded = true;

        chain.Frames.Clear();
        chain.Frames.Add(frameB);
        chain.Frames.Add(frameA);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        // frameA moved to index 1 and must still be expanded
        Assert.False(chainNode.Children[0].IsExpanded);
        Assert.True(chainNode.Children[1].IsExpanded);
    }

    [Fact]
    public void SyncFramesInto_NewFrame_IsAdded()
    {
        var chain     = new AnimationChainSave { Name = "Walk" };
        var chainNode = TreeBuilder.BuildChainNode(chain);
        var newFrame  = new AnimationFrameSave { TextureName = "new.png" };
        chain.Frames.Add(newFrame);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        Assert.Single(chainNode.Children);
        Assert.Same(newFrame, chainNode.Children[0].Data);
    }

    [Fact]
    public void SyncFramesInto_RemovedFrame_IsRemoved()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "walk1.png" };
        chain.Frames.Add(frame);
        var chainNode = TreeBuilder.BuildChainNode(chain);
        chain.Frames.Clear();

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        Assert.Empty(chainNode.Children);
    }

    [Fact]
    public void SyncFramesInto_RecursesSyncShapesInto_ExistingFrameNode()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var rect  = new AARectSave { Name = "Hit" };
        var frame = new AnimationFrameSave { TextureName = "w1.png", ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        chain.Frames.Add(frame);
        var chainNode     = TreeBuilder.BuildChainNode(chain);
        var frameVm       = chainNode.Children[0];
        var originalRectVm = frameVm.Children[0];

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        Assert.Same(frameVm,        chainNode.Children[0]);
        Assert.Same(originalRectVm, chainNode.Children[0].Children[0]);
    }

    [Fact]
    public void SyncFramesInto_FrameHeader_UpdatesAfterIndexChange()
    {
        var chain  = new AnimationChainSave { Name = "Walk" };
        var frameA = new AnimationFrameSave { TextureName = "" };
        var frameB = new AnimationFrameSave { TextureName = "" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        var chainNode = TreeBuilder.BuildChainNode(chain);
        Assert.Equal("Frame 1", chainNode.Children[0].Header);
        Assert.Equal("Frame 2", chainNode.Children[1].Header);

        chain.Frames.Clear();
        chain.Frames.Add(frameB);
        chain.Frames.Add(frameA);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        // After swap, labels update dynamically to reflect the new position —
        // auto-named frames always show "Frame N" for their current index.
        Assert.Equal("Frame 1", chainNode.Children[0].Header);
        Assert.Equal("Frame 2", chainNode.Children[1].Header);
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
        frame.ShapesSave!.Shapes.Add(rect);
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
        frame.ShapesSave!.Shapes.Add(circle);
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
