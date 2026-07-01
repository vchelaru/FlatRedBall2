using System.Collections.Generic;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Pure tests for the ANIMATIONS tree search/filter predicate (issue #517).
public class TreeFilterTests
{
    private static readonly string[] Chains = { "walkLeft", "slowWalk", "Idle", "RunRight" };

    // ── Query-change path (can shrink) — FilterChainNames / MatchesFilter ──────

    [Fact]
    public void FilterChainNames_EmptyQuery_ReturnsAll()
    {
        var result = TreeBuilder.FilterChainNames(Chains, "");
        Assert.Equal(Chains, result);
    }

    [Fact]
    public void FilterChainNames_WhitespaceQuery_ReturnsAll()
    {
        var result = TreeBuilder.FilterChainNames(Chains, "   ");
        Assert.Equal(Chains, result);
    }

    [Fact]
    public void FilterChainNames_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var result = TreeBuilder.FilterChainNames(Chains, "WALK");
        Assert.Equal(new List<string> { "walkLeft", "slowWalk" }, result);
    }

    [Fact]
    public void FilterChainNames_MidStringSubstring_Matches()
    {
        // "walk" appears mid-string in "slowWalk", not just as a prefix.
        var result = TreeBuilder.FilterChainNames(Chains, "walk");
        Assert.Equal(new List<string> { "walkLeft", "slowWalk" }, result);
    }

    [Fact]
    public void FilterChainNames_NoMatch_ReturnsEmpty()
    {
        var result = TreeBuilder.FilterChainNames(Chains, "zzz");
        Assert.Empty(result);
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
}
