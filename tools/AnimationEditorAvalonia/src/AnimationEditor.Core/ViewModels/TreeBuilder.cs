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
        RestripeRoots(result);
        return result;
    }

    // ── Zebra striping ────────────────────────────────────────────────────────

    /// <summary>
    /// Reassigns <see cref="TreeNodeVm.IsOddStripe"/> across all root chain nodes by their
    /// current position — even index → <c>false</c>, odd index → <c>true</c> — and cascades
    /// each root's parity to every frame/shape descendant so an animation and all its children
    /// share one zebra band. Purely positional, so it must be re-run after any add, remove, or
    /// reorder; <see cref="BuildTree"/> and <see cref="SyncChainsInto"/> call it automatically,
    /// and partial-refresh paths that mutate the tree directly should call it too.
    /// </summary>
    public static void RestripeRoots(IReadOnlyList<TreeNodeVm> roots)
    {
        for (int i = 0; i < roots.Count; i++)
            SetStripeRecursive(roots[i], (i & 1) == 1);
    }

    private static void SetStripeRecursive(TreeNodeVm node, bool isOdd)
    {
        node.IsOddStripe = isOdd;
        foreach (var child in node.Children)
            SetStripeRecursive(child, isOdd);
    }

    /// <summary>
    /// The chain node's <see cref="TreeNodeVm.Meta"/> text: frame count plus total play time,
    /// e.g. <c>"12 fr · 1.50s"</c> (#623). Single source of truth so every chain-meta refresh
    /// path stays in sync.
    /// </summary>
    public static string BuildChainMeta(AnimationChainSave chain) =>
        $"{chain.Frames.Count} fr · {TimelineBuilder.FormatSeconds(TimelineBuilder.TotalSeconds(chain))}";

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
            Meta = BuildChainMeta(chain),
        };
        for (int i = 0; i < chain.Frames.Count; i++)
            node.Children.Add(BuildFrameNode(chain.Frames[i], i));
        return node;
    }

    /// <summary>
    /// Builds a single frame node with any shape children from
    /// <see cref="AnimationFrameSave.ShapesSave"/>. Frames always display a dynamic
    /// position-based label ("Frame N") computed at display time — see
    /// <see cref="BuildFrameHeader"/>.
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
    /// Returns the display label for a frame node: the dynamic position-based label
    /// "Frame N" based on <paramref name="index"/>, so labels update automatically on
    /// reorder. Frame names are not user-overridable — a frame's identity is its index.
    /// </summary>
    public static string BuildFrameHeader(AnimationFrameSave frame, int index = 0) =>
        $"Frame {index + 1}";

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
    /// Frames display "Frame N" based on their current position — labels update
    /// automatically on reorder.
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
            roots[i].Meta   = BuildChainMeta(target);
            SyncFramesInto(roots[i], target.Frames);
        }

        // Striping is positional and cascades to descendants; recompute after the diff so
        // reorder/add/delete land correct parity (and newly-synced frames/shapes inherit it).
        RestripeRoots(roots);
    }

    // ── Expand-state capture/restore across a full rebuild (#687) ────────────

    /// <summary>
    /// Walks <paramref name="roots"/> and every descendant, recording each node's
    /// <see cref="TreeNodeVm.IsExpanded"/> keyed by <see cref="TreeNodeVm.Data"/> (by reference).
    /// Pair with <see cref="ApplyExpandState"/> to survive a full tree rebuild (e.g. a tab
    /// switch) that discards the old <see cref="TreeNodeVm"/> instances but keeps the same
    /// underlying data objects — this covers frame nodes with shape children, which have no
    /// other expand-state persistence (only chain-level expand state is saved to the companion
    /// file). Nodes with a <c>null</c> Data are skipped since they can't be matched back up.
    /// </summary>
    public static Dictionary<object, bool> CaptureExpandState(IEnumerable<TreeNodeVm> roots)
    {
        var state = new Dictionary<object, bool>(ReferenceEqualityComparer.Instance);
        CaptureExpandStateRecursive(roots, state);
        return state;
    }

    private static void CaptureExpandStateRecursive(IEnumerable<TreeNodeVm> nodes, Dictionary<object, bool> state)
    {
        foreach (var node in nodes)
        {
            if (node.Data is not null)
                state[node.Data] = node.IsExpanded;
            CaptureExpandStateRecursive(node.Children, state);
        }
    }

    /// <summary>
    /// Applies previously-<see cref="CaptureExpandState"/>d expand flags onto <paramref name="roots"/>
    /// by matching each node's <see cref="TreeNodeVm.Data"/> reference. Nodes whose data isn't in
    /// <paramref name="state"/> (e.g. a node freshly created since capture) are left at whatever
    /// default <see cref="BuildTree"/> gave them.
    /// </summary>
    public static void ApplyExpandState(IEnumerable<TreeNodeVm> roots, IReadOnlyDictionary<object, bool> state)
    {
        foreach (var node in roots)
        {
            if (node.Data is not null && state.TryGetValue(node.Data, out var expanded))
                node.IsExpanded = expanded;
            ApplyExpandState(node.Children, state);
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
                // Always re-assign, even when this chain is already the selection: the setter
                // clears SelectedFrame and fires SelectionChanged unconditionally, and clicking
                // an already-selected chain must still replay the wireframe's reveal (#716),
                // which is driven entirely off SelectionChanged.
                selectedState.SelectedChain = chain;
                return true;
            case AnimationFrameSave frame:
            {
                if (acls is null || !acls.AnimationChains.Any(c => c.Frames.Contains(frame)))
                    return true; // stale — recognised type, but don't corrupt state
                // Always re-assign, even when this frame is already the selection — same reason
                // as the chain case above: re-clicking an already-selected frame must replay the
                // reveal (#716), not just clear a stale shape selection.
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

    // ── Search / filter ───────────────────────────────────────────────────────

    /// <summary>
    /// Case-insensitive substring match of a single chain name against a search
    /// <paramref name="query"/>. A <c>null</c>, empty, or whitespace-only query
    /// matches every name (no filter active). Leading/trailing whitespace in the
    /// query is ignored.
    /// </summary>
    public static bool MatchesFilter(string chainName, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return chainName.Contains(query.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Query-change path: sets each chain (root) node's
    /// <see cref="TreeNodeVm.PinnedVisible"/> to whether its header matches
    /// <paramref name="query"/> under <see cref="MatchesFilter"/>, or one of its frames holds
    /// a shape (<see cref="AARectSave"/>/<see cref="CircleSave"/>) whose name matches (#726).
    /// This is the <b>only</b> filter path allowed to hide a row — typing/refining the query
    /// shrinks the visible set; an empty/whitespace query shows every chain. Non-chain nodes
    /// (frames, shapes) are otherwise left untouched: they are hidden only when their parent
    /// chain is.
    /// <para>
    /// A shape-level match also expands its owning chain and frame via
    /// <see cref="ExpandAncestorsOf"/> so the matched row is actually visible, not just
    /// present-but-collapsed.
    /// </para>
    /// <para>
    /// Contrast with <see cref="ComputeVisibleAfterModelChange"/>, the grow-only path used
    /// when the model mutates but the query is unchanged.
    /// </para>
    /// </summary>
    public static void ApplyQueryFilter(IEnumerable<TreeNodeVm> roots, string? query)
    {
        foreach (var node in roots)
        {
            if (node.Data is AnimationChainSave)
            {
                var matchingShapes = FindMatchingShapeNodes(node, query);
                node.PinnedVisible = MatchesFilter(node.Header, query) || matchingShapes.Count > 0;

                foreach (var shapeNode in matchingShapes)
                    ExpandAncestorsOf(roots, shapeNode.Data!);
            }
        }
    }

    /// <summary>
    /// Collects every shape child (<see cref="NodeKind.RectShape"/>/<see cref="NodeKind.CircleShape"/>)
    /// across all of <paramref name="chainNode"/>'s frames whose name matches <paramref name="query"/>
    /// under <see cref="MatchesFilter"/>. Returns empty for a blank query — a blank query means
    /// "no filter", not "match every shape".
    /// </summary>
    private static List<TreeNodeVm> FindMatchingShapeNodes(TreeNodeVm chainNode, string? query)
    {
        var matches = new List<TreeNodeVm>();
        if (string.IsNullOrWhiteSpace(query)) return matches;

        foreach (var frameNode in chainNode.Children)
            foreach (var shapeNode in frameNode.Children)
                if ((shapeNode.Kind == NodeKind.RectShape || shapeNode.Kind == NodeKind.CircleShape)
                    && MatchesFilter(shapeNode.Header, query))
                    matches.Add(shapeNode);
        return matches;
    }

    /// <summary>
    /// Computes which chains should be visible after a <b>model mutation</b> while the
    /// search <paramref name="query"/> is unchanged (rename, add, delete, undo/redo,
    /// hot-reload). This is <b>grow-only</b>: it never hides a chain that was already
    /// visible, so the chain the user is editing can't vanish out from under them.
    /// A chain is visible when it was <paramref name="previouslyVisible"/>, is
    /// <paramref name="brandNew"/> to the tree (just created or undo-restored), or its
    /// current name matches <paramref name="query"/> (newly relevant, or no filter).
    /// <para>
    /// Identity is by reference — a renamed chain keeps the same object, so it stays
    /// visible via <paramref name="previouslyVisible"/> even though its name changed.
    /// The <b>selected</b> chain is intentionally not handled here (folding it in would
    /// permanently pin every chain the user ever clicked); selection is kept visible
    /// reactively at the view layer via a <c>PinnedVisible || IsSelected</c> binding.
    /// </para>
    /// </summary>
    public static HashSet<AnimationChainSave> ComputeVisibleAfterModelChange(
        IEnumerable<AnimationChainSave> previouslyVisible,
        IReadOnlyList<AnimationChainSave> currentChains,
        string? query,
        IEnumerable<AnimationChainSave> brandNew)
    {
        var prev = new HashSet<AnimationChainSave>(previouslyVisible, ReferenceEqualityComparer.Instance);
        var brand = new HashSet<AnimationChainSave>(brandNew, ReferenceEqualityComparer.Instance);
        var result = new HashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);
        foreach (var chain in currentChains)
            if (prev.Contains(chain) || brand.Contains(chain) || MatchesFilter(chain.Name, query))
                result.Add(chain);
        return result;
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
    /// Reads each source chain's current <see cref="TreeNodeVm.IsExpanded"/> from the
    /// tree, matched by name. Used when pasting/duplicating so copies inherit collapse state.
    /// </summary>
    public static IReadOnlyList<bool> ExpandStatesForChainNames(
        IEnumerable<TreeNodeVm> roots,
        IReadOnlyList<string> sourceNames) =>
        sourceNames
            .Select(name => roots.FirstOrDefault(n =>
                n.Data is AnimationChainSave c && c.Name == name)?.IsExpanded ?? false)
            .ToList();

    /// <summary>
    /// Applies per-chain expand flags to the VMs for <paramref name="chains"/>.
    /// </summary>
    public static void ApplyExpandStates(
        IEnumerable<TreeNodeVm> roots,
        IReadOnlyList<AnimationChainSave> chains,
        IReadOnlyList<bool> expanded)
    {
        for (int i = 0; i < chains.Count && i < expanded.Count; i++)
        {
            var node = FindNodeForData(roots, chains[i]);
            if (node is not null)
                node.IsExpanded = expanded[i];
        }
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
