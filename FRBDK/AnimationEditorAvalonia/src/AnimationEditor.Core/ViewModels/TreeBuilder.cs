using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Builds <see cref="TreeNodeVm"/> trees from animation data objects and routes
/// tree-selection events to <see cref="SelectedState"/>.
/// All methods are pure (no Avalonia references).
/// </summary>
public static class TreeBuilder
{
    // ── Tree construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a full tree from an <see cref="AnimationChainListSave"/>.
    /// Chain nodes whose names appear in <paramref name="expandedChainNames"/> get
    /// <see cref="TreeNodeVm.IsExpanded"/> = <c>true</c>.
    /// When <paramref name="expandedChainNames"/> is <c>null</c> every chain defaults
    /// to expanded (preserves existing behaviour when no saved state is available).
    /// </summary>
    public static List<TreeNodeVm> BuildTree(
        AnimationChainListSave acls,
        IEnumerable<string>? expandedChainNames = null)
    {
        var expanded = expandedChainNames is null
            ? null
            : new HashSet<string>(expandedChainNames, System.StringComparer.Ordinal);

        var result = new List<TreeNodeVm>(acls.AnimationChains.Count);
        foreach (var chain in acls.AnimationChains)
        {
            var node = BuildChainNode(chain);
            node.IsExpanded = expanded is null || expanded.Contains(chain.Name);
            result.Add(node);
        }
        return result;
    }

    /// <summary>Builds a single chain node with all its frame children.</summary>
    public static TreeNodeVm BuildChainNode(AnimationChainSave chain)
    {
        var node = new TreeNodeVm { Header = chain.Name, Data = chain, IsExpanded = true, IsChainNode = true };
        foreach (var frame in chain.Frames)
            node.Children.Add(BuildFrameNode(frame));
        return node;
    }

    /// <summary>
    /// Builds a single frame node with any shape children from
    /// <see cref="AnimationFrameSave.ShapeCollectionSave"/>.
    /// </summary>
    public static TreeNodeVm BuildFrameNode(AnimationFrameSave frame)
    {
        var node = new TreeNodeVm { Header = BuildFrameHeader(frame), Data = frame };
        if (frame.ShapeCollectionSave is not null)
        {
            foreach (var r in frame.ShapeCollectionSave.AxisAlignedRectangleSaves)
                node.Children.Add(new TreeNodeVm { Header = r.Name, Data = r });
            foreach (var c in frame.ShapeCollectionSave.CircleSaves)
                node.Children.Add(new TreeNodeVm { Header = c.Name, Data = c });
        }
        return node;
    }

    /// <summary>Returns the display label for a frame node.</summary>
    public static string BuildFrameHeader(AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName))
            return "<UNTEXTURED>";
        return Path.GetFileName(frame.TextureName);
    }

    // ── Expand-state persistence ──────────────────────────────────────────────

    /// <summary>
    /// Returns the names of chain nodes that currently have
    /// <see cref="TreeNodeVm.IsExpanded"/> = <c>true</c>, for persistence in
    /// <c>AESettingsSave.ExpandedNodes</c>.
    /// </summary>
    public static IEnumerable<string> GetExpandedChainNames(IEnumerable<TreeNodeVm> roots) =>
        roots
            .Where(n => n.Data is AnimationChainSave && n.IsExpanded)
            .Select(n => ((AnimationChainSave)n.Data!).Name);

    // ── Selection routing ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a selected data object to the appropriate
    /// <see cref="ISelectedState"/> property.
    /// Returns <c>true</c> when the data object matched a known type.
    /// <para>
    /// Only applies the selection when the data object is reachable from
    /// <paramref name="acls"/>'s current chain list, preventing stale tree
    /// nodes from overwriting live selection state.
    /// </para>
    /// </summary>
    public static bool RouteNodeSelection(object? data, ISelectedState selectedState, AnimationChainListSave? acls)
    {
        if (data is null) return false;
        switch (data)
        {
            case AnimationChainSave chain:
                if (acls is null || !acls.AnimationChains.Contains(chain))
                    return true; // stale — recognised type, but don't corrupt state
                // Re-assign even if chain is the same object when a frame is selected,
                // so that SelectedChain setter clears SelectedFrame and fires SelectionChanged.
                if (selectedState.SelectedChain != chain || selectedState.SelectedFrame != null)
                    selectedState.SelectedChain = chain;
                return true;
            case AnimationFrameSave frame:
            {
                if (acls is null || !acls.AnimationChains.Any(c => c.Frames.Contains(frame)))
                    return true; // stale — recognised type, but don't corrupt state
                // Re-assign even when the same frame is already selected if a shape is
                // selected, so that SelectedFrame.set clears SelectedCircle/SelectedRectangle.
                if (selectedState.SelectedFrame != frame || selectedState.SelectedShape != null)
                    selectedState.SelectedFrame = frame;
                return true;
            }
            case AxisAlignedRectangleSave rect:
                if (selectedState.SelectedRectangle != rect)
                    selectedState.SelectedRectangle = rect;
                return true;
            case CircleSave circle:
                if (selectedState.SelectedCircle != circle)
                    selectedState.SelectedCircle = circle;
                return true;
            default:
                return false;
        }
    }

    // ── Node search ───────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively searches <paramref name="roots"/> and returns the first node
    /// whose <see cref="TreeNodeVm.Data"/> is the same reference as
    /// <paramref name="target"/>.  Returns <c>null</c> when not found.
    /// </summary>
    public static TreeNodeVm? FindNodeForData(IEnumerable<TreeNodeVm> roots, object target)
    {
        foreach (var node in roots)
        {
            if (ReferenceEquals(node.Data, target)) return node;
            var found = FindNodeForData(node.Children, target);
            if (found is not null) return found;
        }
        return null;
    }
}
