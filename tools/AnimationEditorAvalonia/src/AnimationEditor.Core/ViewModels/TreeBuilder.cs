using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FlatRedBall2.Animation.Content;

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
        var node = new TreeNodeVm
        {
            Header = chain.Name,
            Data = chain,
            IsExpanded = true,
            IsChainNode = true,
            Kind = NodeKind.Chain,
            Meta = $"{chain.Frames.Count} fr",
        };
        for (int i = 0; i < chain.Frames.Count; i++)
            node.Children.Add(BuildFrameNode(chain.Frames[i], i));
        return node;
    }

    /// <summary>
    /// Builds a single frame node with any shape children from
    /// <see cref="AnimationFrameSave.ShapesSave"/>.
    /// Auto-named frames (those without a user-set name) are NOT given a
    /// persisted name — they display a dynamic position-based label ("Frame N")
    /// computed at display time. Custom-named frames (<see cref="AnimationFrameSave.HasCustomName"/>
    /// = <c>true</c>) keep their name regardless of position.
    /// </summary>
    public static TreeNodeVm BuildFrameNode(AnimationFrameSave frame, int index = 0)
    {
        var node = new TreeNodeVm
        {
            Header = BuildFrameHeader(frame, index),
            Data = frame,
            Kind = NodeKind.Frame,
            IsFrameNode = true,
            Meta = $"{frame.FrameLength:0.00}s",
        };
        if (frame.ShapesSave is not null)
        {
            foreach (var shape in frame.ShapesSave!.Shapes)
            {
                switch (shape)
                {
                    case AARectSave r:
                        node.Children.Add(new TreeNodeVm
                        {
                            Header = r.Name,
                            Data = r,
                            Kind = NodeKind.RectShape,
                            IsRectNode = true,
                        });
                        break;
                    case CircleSave c:
                        node.Children.Add(new TreeNodeVm
                        {
                            Header = c.Name,
                            Data = c,
                            Kind = NodeKind.CircleShape,
                            IsCircleNode = true,
                        });
                        break;
                }
            }
        }
        return node;
    }

    /// <summary>
    /// Returns the display label for a frame node.
    /// When <see cref="AnimationFrameSave.HasCustomName"/> is <c>true</c>, the
    /// user-assigned <see cref="AnimationFrameSave.Name"/> is returned as-is.
    /// Otherwise a dynamic position-based label "Frame N" is returned based on
    /// <paramref name="index"/>, so labels update automatically on reorder.
    /// </summary>
    public static string BuildFrameHeader(AnimationFrameSave frame, int index = 0)
    {
        if (frame.HasCustomName && !string.IsNullOrEmpty(frame.Name))
            return frame.Name;
        return $"Frame {index + 1}";
    }

    // ── Diff-update helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Diff-updates the shape children of <paramref name="frameNode"/> to match
    /// <paramref name="shapesSave"/> without replacing existing VMs.
    /// <para>
    /// VMs whose <see cref="TreeNodeVm.Data"/> still appears in
    /// <paramref name="shapesSave"/> are reused and their
    /// <see cref="TreeNodeVm.Header"/> is resynced (so a renamed shape is reflected).
    /// VMs for removed shapes are deleted; new VMs are created for added shapes.
    /// Order matches the insertion order of <see cref="ShapesSave.Shapes"/>.
    /// </para>
    /// </summary>
    public static void SyncShapesInto(TreeNodeVm frameNode, ShapesSave? shapesSave)
    {
            var shapes = shapesSave?.Shapes ?? new System.Collections.Generic.List<object>();

            // Remove shape VMs that no longer exist in the data list.
            for (int i = frameNode.Children.Count - 1; i >= 0; i--)
            {
                var child = frameNode.Children[i];
                bool keep = child.Data is AARectSave || child.Data is CircleSave;
                if (keep) keep = shapes.Contains(child.Data!);
                if (!keep) frameNode.Children.RemoveAt(i);
            }

            // Ensure every desired shape has a VM at the correct index.
            int pos = 0;
            foreach (var shape in shapes)
            {
                switch (shape)
                {
                    case AARectSave r:
                    {
                        var vm = frameNode.Children.FirstOrDefault(n => ReferenceEquals(n.Data, r));
                        if (vm is null)
                        {
                            vm = new TreeNodeVm { Header = r.Name, Data = r, Kind = NodeKind.RectShape, IsRectNode = true };
                            frameNode.Children.Insert(pos, vm);
                        }
                        else
                        {
                            vm.Header = r.Name;
                            int cur = frameNode.Children.IndexOf(vm);
                            if (cur != pos) { frameNode.Children.RemoveAt(cur); frameNode.Children.Insert(pos, vm); }
                        }
                        pos++;
                        break;
                    }
                    case CircleSave c:
                    {
                        var vm = frameNode.Children.FirstOrDefault(n => ReferenceEquals(n.Data, c));
                        if (vm is null)
                        {
                            vm = new TreeNodeVm { Header = c.Name, Data = c, Kind = NodeKind.CircleShape, IsCircleNode = true };
                            frameNode.Children.Insert(pos, vm);
                        }
                        else
                        {
                            vm.Header = c.Name;
                            int cur = frameNode.Children.IndexOf(vm);
                            if (cur != pos) { frameNode.Children.RemoveAt(cur); frameNode.Children.Insert(pos, vm); }
                        }
                        pos++;
                        break;
                    }
                }
            }
    }

    /// <summary>
    /// Diff-updates the frame children of <paramref name="chainNode"/> to match
    /// <paramref name="frames"/> without replacing existing <see cref="TreeNodeVm"/>
    /// instances.  Retained VMs keep their <see cref="TreeNodeVm.IsExpanded"/> state
    /// (and Avalonia's TreeView keeps them in its <c>SelectedItems</c> because selection
    /// uses object identity).  Headers and Meta are always refreshed.
    /// <para>
    /// Dynamic frames (those with <see cref="AnimationFrameSave.HasCustomName"/> = <c>false</c>)
    /// display "Frame N" based on their current position — labels update automatically on reorder.
    /// Custom-named frames (<see cref="AnimationFrameSave.HasCustomName"/> = <c>true</c>) always
    /// display their user-assigned <see cref="AnimationFrameSave.Name"/> regardless of position.
    /// </para>
    /// </summary>
    public static void SyncFramesInto(TreeNodeVm chainNode, IList<AnimationFrameSave> frames)
    {
        var children = chainNode.Children;

        // Build a lookup of existing VMs keyed by frame reference.
        var existingByFrame = new Dictionary<AnimationFrameSave, TreeNodeVm>(
            ReferenceEqualityComparer.Instance);
        foreach (var child in children)
            if (child.Data is AnimationFrameSave f)
                existingByFrame[f] = child;

        // Step 1: Remove VMs whose frame is no longer in the list.
        var frameSet = new HashSet<AnimationFrameSave>(frames, ReferenceEqualityComparer.Instance);
        for (int i = children.Count - 1; i >= 0; i--)
            if (children[i].Data is AnimationFrameSave f && !frameSet.Contains(f))
                children.RemoveAt(i);

        // Step 2: Append new VMs for frames not yet represented (step 3 reorders them).
        for (int i = 0; i < frames.Count; i++)
            if (!existingByFrame.ContainsKey(frames[i]))
                children.Add(BuildFrameNode(frames[i], i));

        // Step 3: Reorder children to match the target frame order, then refresh
        //         Header and Meta.  Dynamic frames get positional labels; custom-named
        //         frames keep their user-assigned name regardless of position.
        for (int i = 0; i < frames.Count; i++)
        {
            var target = frames[i];
            if (!ReferenceEquals(children[i].Data, target))
            {
                for (int j = i + 1; j < children.Count; j++)
                    if (ReferenceEquals(children[j].Data, target))
                    {
                        // Use RemoveAt+Insert instead of Move to avoid triggering
                        // Avalonia's Move-action CollectionChanged path, which can
                        // cause SelectionChanged to fire and corrupt tree state.
                        var nodeVm = children[j];
                        children.RemoveAt(j);
                        children.Insert(i, nodeVm);
                        break;
                    }
            }
            children[i].Header = BuildFrameHeader(target, i);
            children[i].Meta   = $"{target.FrameLength:0.00}s";
            SyncShapesInto(children[i], target.ShapesSave);
        }
    }

    /// <summary>
    /// Diff-updates the root chain nodes in <paramref name="roots"/> to match
    /// <paramref name="chains"/> without replacing existing <see cref="TreeNodeVm"/>
    /// instances.  Retained chain VMs keep their <see cref="TreeNodeVm.IsExpanded"/>
    /// state (and Avalonia's TreeView keeps them selected, since selection uses object
    /// identity); their <see cref="TreeNodeVm.Header"/> and <see cref="TreeNodeVm.Meta"/>
    /// are resynced and their frame children are diff-updated via
    /// <see cref="SyncFramesInto"/>.  New chains get a freshly-built node (expanded by
    /// default, per <see cref="BuildChainNode"/>); VMs for removed chains are deleted.
    /// <para>
    /// This is the chain-level counterpart of <see cref="SyncFramesInto"/>: it lets
    /// copy/paste and reorder preserve each chain's collapse state instead of
    /// rebuilding the whole tree and re-expanding everything.
    /// </para>
    /// </summary>
    public static void SyncChainsInto(
        ObservableCollection<TreeNodeVm> roots,
        IList<AnimationChainSave> chains)
    {
        // Step 1: Remove root VMs whose chain is no longer in the list.
        var chainSet = new HashSet<AnimationChainSave>(chains, ReferenceEqualityComparer.Instance);
        for (int i = roots.Count - 1; i >= 0; i--)
            if (roots[i].Data is AnimationChainSave c && !chainSet.Contains(c))
                // The thumbnail bitmap is owned by ThumbnailService's cache, not the node —
                // drop the node without disposing it (a later cache hit may return it again).
                roots.RemoveAt(i);

        // Build a lookup of surviving VMs keyed by chain reference.
        var existingByChain = new Dictionary<AnimationChainSave, TreeNodeVm>(
            ReferenceEqualityComparer.Instance);
        foreach (var root in roots)
            if (root.Data is AnimationChainSave c)
                existingByChain[c] = root;

        // Step 2: Append new VMs for chains not yet represented (step 3 reorders them).
        foreach (var chain in chains)
            if (!existingByChain.ContainsKey(chain))
                roots.Add(BuildChainNode(chain));

        // Step 3: Reorder to match the target chain order, then resync each retained
        //         node's Header/Meta and diff-update its frame children.
        for (int i = 0; i < chains.Count; i++)
        {
            var target = chains[i];
            if (!ReferenceEquals(roots[i].Data, target))
            {
                for (int j = i + 1; j < roots.Count; j++)
                    if (ReferenceEquals(roots[j].Data, target))
                    { roots.Move(j, i); break; }
            }
            roots[i].Header = target.Name;
            roots[i].Meta   = $"{target.Frames.Count} fr";
            SyncFramesInto(roots[i], target.Frames);
        }
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
            case AARectSave rect:
            {
                var parentFrame = FindParentFrameFor(rect, acls);
                if (parentFrame is null) return true; // stale — shape not reachable from live project
                if (selectedState.SelectedFrame != parentFrame)
                    selectedState.SelectedFrame = parentFrame; // stops playback; clears previous shape
                if (selectedState.SelectedRectangle != rect)
                    selectedState.SelectedRectangle = rect;
                return true;
            }
            case CircleSave circle:
            {
                var parentFrame = FindParentFrameFor(circle, acls);
                if (parentFrame is null) return true; // stale — shape not reachable from live project
                if (selectedState.SelectedFrame != parentFrame)
                    selectedState.SelectedFrame = parentFrame; // stops playback; clears previous shape
                if (selectedState.SelectedCircle != circle)
                    selectedState.SelectedCircle = circle;
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Searches the full animation chain list for the <see cref="AnimationFrameSave"/>
    /// that owns <paramref name="shape"/> via its <c>ShapesSave</c>.
    /// Returns <c>null</c> when the shape cannot be found (e.g. stale node after a test reset).
    /// </summary>
    private static AnimationFrameSave? FindParentFrameFor(object shape, AnimationChainListSave? acls)
    {
        if (acls is null) return null;
        foreach (var chain in acls.AnimationChains)
            foreach (var frame in chain.Frames)
                if (frame.ShapesSave is { } scs && scs.Shapes.Contains(shape))
                    return frame;
        return null;
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

    /// <summary>
    /// Expands every ancestor of the node holding <paramref name="target"/> so its row is visible,
    /// without changing the target's own expand state. Needed because a frame can now be selected
    /// without its chain being expanded first (e.g. scrubbing the timeline selects a frame while
    /// the chain node is collapsed). Returns <c>true</c> if the target was found.
    /// </summary>
    public static bool ExpandAncestorsOf(IEnumerable<TreeNodeVm> roots, object target)
    {
        foreach (var node in roots)
        {
            if (ReferenceEquals(node.Data, target)) return true;
            if (ExpandAncestorsOf(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }
}
