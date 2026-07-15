using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Pure tests for the ANIMATIONS tree search/filter (issue #517). These exercise the same
// functions production runs: ApplyQueryFilter on keystroke, ComputeVisibleAfterModelChange
// on model mutation.
public class TreeFilterTests
{
    private static readonly string[] Names = { "walkLeft", "slowWalk", "Idle", "RunRight" };

    // Builds the root chain nodes exactly as the tree holds them: Header == chain name,
    // Data == the chain (the guard ApplyQueryFilter keys on).
    private static List<TreeNodeVm> ChainNodes(params string[] names) =>
        names.Select(n => new TreeNodeVm
        {
            Header = n,
            Data = new AnimationChainSave { Name = n },
            IsChainNode = true,
        }).ToList();

    private static List<string> VisibleNames(IEnumerable<TreeNodeVm> roots) =>
        roots.Where(n => n.PinnedVisible).Select(n => n.Header).ToList();

    // Builds a chain node with one frame holding one AARectSave shape child, matching the
    // shape produced by TreeBuilder.BuildChainNode (chain -> frame -> shape), for shape-search tests.
    private static TreeNodeVm ChainWithShapeNode(string chainName, string shapeName)
    {
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = shapeName });
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(frame);
        return TreeBuilder.BuildChainNode(chain);
    }

    // ── Query-change path (can shrink) — ApplyQueryFilter ─────────────────────

    [Fact]
    public void ApplyQueryFilter_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var roots = ChainNodes(Names);
        TreeBuilder.ApplyQueryFilter(roots, "WALK");
        Assert.Equal(new List<string> { "walkLeft", "slowWalk" }, VisibleNames(roots));
    }

    [Fact]
    public void ApplyQueryFilter_EmptyQuery_AllVisible()
    {
        var roots = ChainNodes(Names);
        TreeBuilder.ApplyQueryFilter(roots, "");
        Assert.Equal(Names, VisibleNames(roots));
    }

    // Guards against a naive "match everything on blank query" shape search forcing every
    // chain/frame open the moment the search box is cleared.
    [Fact]
    public void ApplyQueryFilter_EmptyQuery_DoesNotExpandCollapsedNodes()
    {
        var chainNode = ChainWithShapeNode("Idle", "HitBox");
        chainNode.IsExpanded = false;
        chainNode.Children[0].IsExpanded = false; // frame node
        var roots = new List<TreeNodeVm> { chainNode };

        TreeBuilder.ApplyQueryFilter(roots, "");

        Assert.False(chainNode.IsExpanded);
        Assert.False(chainNode.Children[0].IsExpanded);
    }

    [Fact]
    public void ApplyQueryFilter_MidStringSubstring_Matches()
    {
        // "walk" appears mid-string in "slowWalk", not just as a prefix.
        var roots = ChainNodes(Names);
        TreeBuilder.ApplyQueryFilter(roots, "walk");
        Assert.Equal(new List<string> { "walkLeft", "slowWalk" }, VisibleNames(roots));
    }

    [Fact]
    public void ApplyQueryFilter_NoMatch_AllHidden()
    {
        var roots = ChainNodes(Names);
        TreeBuilder.ApplyQueryFilter(roots, "zzz");
        Assert.Empty(VisibleNames(roots));
    }

    // The guard: a non-chain node (e.g. a frame) is never toggled by the query filter.
    [Fact]
    public void ApplyQueryFilter_NonChainNode_LeftVisible()
    {
        var frameNode = new TreeNodeVm { Header = "Frame 1", Data = new AnimationFrameSave(), IsFrameNode = true };
        var roots = new List<TreeNodeVm> { frameNode };

        TreeBuilder.ApplyQueryFilter(roots, "walk"); // frame header doesn't match

        Assert.True(frameNode.PinnedVisible); // untouched, stays visible
    }

    // Shape names (#726): a query matching a shape inside a frame reveals the owning chain
    // even when the chain's own name doesn't match.
    [Fact]
    public void ApplyQueryFilter_ShapeNameMatch_ChainBecomesVisible()
    {
        var chainNode = ChainWithShapeNode("Idle", "HitBox");
        var roots = new List<TreeNodeVm> { chainNode };

        TreeBuilder.ApplyQueryFilter(roots, "hit");

        Assert.True(chainNode.PinnedVisible);
    }

    // A shape-level match must also expand its owning chain/frame — otherwise the matched
    // row stays hidden behind a collapsed chain/frame even though PinnedVisible is true.
    [Fact]
    public void ApplyQueryFilter_ShapeNameMatch_ExpandsOwningChainAndFrame()
    {
        var chainNode = ChainWithShapeNode("Idle", "HitBox");
        chainNode.IsExpanded = false;
        chainNode.Children[0].IsExpanded = false; // frame node
        var roots = new List<TreeNodeVm> { chainNode };

        TreeBuilder.ApplyQueryFilter(roots, "hit");

        Assert.True(chainNode.IsExpanded);
        Assert.True(chainNode.Children[0].IsExpanded);
    }

    [Fact]
    public void ApplyQueryFilter_SurroundingWhitespace_TrimsAndMatches()
    {
        var roots = ChainNodes("walkLeft", "Idle");
        TreeBuilder.ApplyQueryFilter(roots, "  walk  "); // trimmed to "walk"
        Assert.Equal(new List<string> { "walkLeft" }, VisibleNames(roots));
    }

    [Fact]
    public void ApplyQueryFilter_WhitespaceQuery_AllVisible()
    {
        var roots = ChainNodes(Names);
        TreeBuilder.ApplyQueryFilter(roots, "   ");
        Assert.Equal(Names, VisibleNames(roots));
    }

    // ── Sticky model-change path (grow-only) — ComputeVisibleAfterModelChange ──
    //
    // While the query is unchanged, a model mutation must never HIDE a chain that
    // was already visible, and must SHOW a chain that just became relevant
    // (newly matches, brand-new, or undo-restored). It must not leak previously
    // hidden non-matching chains back in.

    // Brand-new chain (e.g. just created) appears even though its name doesn't match.
    [Fact]
    public void ComputeVisibleAfterModelChange_BrandNewChain_IsVisible()
    {
        var newChain = new AnimationChainSave { Name = "NewAnimation" };
        var current = new List<AnimationChainSave> { newChain };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave> { newChain });

        Assert.Contains(newChain, visible);
    }

    // Two distinct chains share the name "Idle"; only the one that was visible stays so —
    // reference identity, not name, drives the grow-only keep.
    [Fact]
    public void ComputeVisibleAfterModelChange_DuplicateNames_ReferenceIdentityKeepsRightOne()
    {
        var visibleIdle = new AnimationChainSave { Name = "Idle" };
        var hiddenIdle = new AnimationChainSave { Name = "Idle" };
        var current = new List<AnimationChainSave> { visibleIdle, hiddenIdle };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave> { visibleIdle },
            currentChains: current,
            query: "walk", // neither name matches
            brandNew: new List<AnimationChainSave>());

        Assert.Contains(visibleIdle, visible);
        Assert.DoesNotContain(hiddenIdle, visible);
    }

    [Fact]
    public void ComputeVisibleAfterModelChange_EmptyQuery_AllVisible()
    {
        var a = new AnimationChainSave { Name = "walkLeft" };
        var b = new AnimationChainSave { Name = "Idle" };
        var current = new List<AnimationChainSave> { a, b };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "",
            brandNew: new List<AnimationChainSave>());

        Assert.Contains(a, visible);
        Assert.Contains(b, visible);
    }

    // A hidden, non-matching chain must not reappear on an unrelated model change.
    [Fact]
    public void ComputeVisibleAfterModelChange_HiddenNonMatching_StaysHidden()
    {
        var idle = new AnimationChainSave { Name = "Idle" };
        var current = new List<AnimationChainSave> { idle };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave>());

        Assert.DoesNotContain(idle, visible);
    }

    // Renaming a hidden chain so it now matches the query makes it appear.
    [Fact]
    public void ComputeVisibleAfterModelChange_NewlyMatchingRename_BecomesVisible()
    {
        var chain = new AnimationChainSave { Name = "walkIdle" }; // renamed from "Idle"
        var current = new List<AnimationChainSave> { chain };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave>());

        Assert.Contains(chain, visible);
    }

    // A chain that was visible but has since been removed from the model must not survive
    // in the result (result is scoped to currentChains).
    [Fact]
    public void ComputeVisibleAfterModelChange_PreviouslyVisibleButRemoved_NotInResult()
    {
        var kept = new AnimationChainSave { Name = "walkLeft" };
        var deleted = new AnimationChainSave { Name = "walkRight" };
        var current = new List<AnimationChainSave> { kept }; // deleted no longer present

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave> { kept, deleted },
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave>());

        Assert.DoesNotContain(deleted, visible);
        Assert.Contains(kept, visible);
    }

    // The chain being edited stays visible even after it's renamed out of the filter.
    [Fact]
    public void ComputeVisibleAfterModelChange_RenamedOutOfFilter_StaysVisible()
    {
        var chain = new AnimationChainSave { Name = "Idle" }; // was "walkLeft", now renamed out
        var current = new List<AnimationChainSave> { chain };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave> { chain }, // was visible
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave>());

        Assert.Contains(chain, visible);
    }

    // An undo-restored chain (brand-new to the current tree) reappears.
    [Fact]
    public void ComputeVisibleAfterModelChange_UndoRestoredChain_IsVisible()
    {
        var restored = new AnimationChainSave { Name = "Idle" };
        var current = new List<AnimationChainSave> { restored };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "walk",
            brandNew: new List<AnimationChainSave> { restored }); // restored == new to the tree

        Assert.Contains(restored, visible);
    }

    [Fact]
    public void ComputeVisibleAfterModelChange_WhitespaceQuery_AllVisible()
    {
        var a = new AnimationChainSave { Name = "walkLeft" };
        var b = new AnimationChainSave { Name = "Idle" };
        var current = new List<AnimationChainSave> { a, b };

        var visible = TreeBuilder.ComputeVisibleAfterModelChange(
            previouslyVisible: new List<AnimationChainSave>(),
            currentChains: current,
            query: "   ",
            brandNew: new List<AnimationChainSave>());

        Assert.Contains(a, visible);
        Assert.Contains(b, visible);
    }
}
