using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Builds the ordered tree right-click menu plan shared by every editor host, branching on the
/// selected tree node's data type (rectangle, circle, polygon, frame, chain, or nothing selected).
/// </summary>
public static class TreeMenuPlanBuilder
{
    public static IReadOnlyList<TreeMenuItem> Build(
        object? nodeData,
        IAppCommands appCommands,
        ISelectedState selectedState,
        IObjectFinder objectFinder,
        IProjectManager projectManager,
        TreeMenuActions actions)
    {
        var items = new List<TreeMenuItem>();
        // A native tsx project can't store shapes, flips, or sprite offsets (see
        // Tiled.TsxLossyDataCheck), so the items that would create them aren't offered.
        var isNativeTsx = projectManager.IsNativeTsxProject;
        // Non-null when the tree holds a multi-selection: Delete/Copy/Cut/Duplicate act on all of
        // it, so their labels carry the count; items that touch only the right-clicked node are
        // hidden because they'd silently ignore the rest.
        var selection = Selection.Describe(selectedState.SelectedNodes);
        string DeleteLabel(string singular) => selection is null ? $"Delete {singular}" : selection.Label("Delete");

        switch (nodeData)
        {
            case AARectSave rect:
                AddShapeReorderItems(items, rect, objectFinder.GetAnimationFrameContaining(rect), appCommands);
                items.Add(TreeMenuItem.Item("Match Frame Size", () => MatchFrameSize(rect, appCommands, selectedState), TreeMenuIcon.MatchSize));
                items.Add(TreeMenuItem.Separator());
                AddCopyCutPasteDuplicate(items, actions, selection);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(DeleteLabel("Rectangle"), actions.Delete, TreeMenuIcon.Delete));
                break;

            case CircleSave circle:
                AddShapeReorderItems(items, circle, objectFinder.GetAnimationFrameContaining(circle), appCommands);
                AddCopyCutPasteDuplicate(items, actions, selection);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(DeleteLabel("Circle"), actions.Delete, TreeMenuIcon.Delete));
                break;

            case PolygonSave polygon:
                AddShapeReorderItems(items, polygon, objectFinder.GetAnimationFrameContaining(polygon), appCommands);
                if (selection is null)
                {
                    items.Add(TreeMenuItem.Item("Flip Horizontal", () => appCommands.FlipPolygonHorizontally(polygon), TreeMenuIcon.FlipHorizontal));
                    items.Add(TreeMenuItem.Item("Flip Vertical", () => appCommands.FlipPolygonVertically(polygon), TreeMenuIcon.FlipVertical));
                    items.Add(TreeMenuItem.Separator());
                }
                AddCopyCutPasteDuplicate(items, actions, selection);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(DeleteLabel("Polygon"), actions.Delete, TreeMenuIcon.Delete));
                break;

            case AnimationFrameSave frame:
            {
                var chain = objectFinder.GetAnimationChainContaining(frame);
                if (chain is not null && chain.Frames.Count > 1)
                {
                    AddFrameReorderItems(items, frame, chain, appCommands);
                    items.Add(TreeMenuItem.Separator());
                }
                if (!isNativeTsx)
                {
                    items.Add(TreeMenuItem.Item("Add AxisAlignedRectangle", () => appCommands.AddAxisAlignedRectangle(frame), TreeMenuIcon.Rectangle));
                    items.Add(TreeMenuItem.Item("Add Circle", () => appCommands.AddCircle(frame), TreeMenuIcon.Circle));
                    items.Add(TreeMenuItem.Item("Add Polygon", () => appCommands.AddPolygon(frame), TreeMenuIcon.Polygon));
                    items.Add(TreeMenuItem.Separator());
                }
                items.Add(TreeMenuItem.Item(CountedVerb("Copy", selection), actions.Copy, TreeMenuIcon.Copy));
                items.Add(TreeMenuItem.Item(CountedVerb("Cut", selection), actions.Cut, TreeMenuIcon.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
                if (chain is not null)
                    items.Add(TreeMenuItem.Item(CountedVerb("Duplicate", selection), actions.Duplicate, TreeMenuIcon.Duplicate));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.ViewTextureInExplorer));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(DeleteLabel("Frame"), actions.Delete, TreeMenuIcon.Delete));
                break;
            }

            case AnimationChainSave chain2:
            {
                var chains = projectManager.AnimationChainListSave?.AnimationChains;
                if (chains is not null && chains.Count > 1)
                {
                    AddChainReorderItems(items, chain2, chains, appCommands);
                    items.Add(TreeMenuItem.Separator());
                }
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AdjustFrameTime));
                if (!isNativeTsx && selection is null)
                {
                    items.Add(TreeMenuItem.Item("Flip Horizontally", () => appCommands.FlipChainHorizontally(chain2), TreeMenuIcon.FlipHorizontal));
                    items.Add(TreeMenuItem.Item("Flip Vertically", () => appCommands.FlipChainVertically(chain2), TreeMenuIcon.FlipVertical));
                }
                if (selection is null)
                    items.Add(TreeMenuItem.Item("Invert Frame Order", () => appCommands.InvertFrameOrder(chain2), TreeMenuIcon.Reverse));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Add Animation", actions.AddAnimation!, TreeMenuIcon.Animation));
                // A locked chain refuses new frames (AddFrame and AddMultipleFrames are no-ops on
                // it) and the row hides its + button; the menu must not offer inert items either.
                if (!chain2.IsLocked)
                {
                    items.Add(TreeMenuItem.Item("Add Frame", () => appCommands.AddFrame(chain2), TreeMenuIcon.Frame));
                    items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AddMultipleFrames));
                }
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(CountedVerb("Copy", selection), actions.Copy, TreeMenuIcon.Copy));
                items.Add(TreeMenuItem.Item(CountedVerb("Cut", selection), actions.Cut, TreeMenuIcon.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
                if (isNativeTsx)
                    items.Add(TreeMenuItem.Item(CountedVerb("Duplicate", selection), actions.Duplicate, TreeMenuIcon.Duplicate));
                else
                    items.Add(TreeMenuItem.SubMenu(CountedVerb("Duplicate", selection), TreeMenuIcon.Duplicate,
                        TreeMenuItem.Item("Original", actions.Duplicate, TreeMenuIcon.Duplicate),
                        TreeMenuItem.Item("Flip Horizontal", () => actions.DuplicateChainFlip!(true, false), TreeMenuIcon.FlipHorizontal),
                        TreeMenuItem.Item("Flip Vertical", () => actions.DuplicateChainFlip!(false, true), TreeMenuIcon.FlipVertical)));
                items.Add(TreeMenuItem.Separator());
                if (!isNativeTsx)
                    items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AdjustOffsets));
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                var fileName = projectManager.FileName;
                if (selection is null && actions.CopyText is not null && !string.IsNullOrEmpty(fileName))
                    items.Add(TreeMenuItem.Item("Copy Qualified Name", () => actions.CopyText(QualifiedName.Format(fileName, chain2.Name)), TreeMenuIcon.CopyName));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item(DeleteLabel("Animation"), actions.Delete, TreeMenuIcon.Delete));
                break;
            }

            default:
                items.Add(TreeMenuItem.Item("Add Animation", actions.AddAnimation!, TreeMenuIcon.Animation));
                break;
        }

        items.Add(TreeMenuItem.Separator());
        items.Add(TreeMenuItem.Item("Sort Animations Alphabetically", appCommands.SortAnimationsAlphabetically, TreeMenuIcon.Sort));

        return items;
    }

    // Matches the whole multi-selection, not just the right-clicked rectangle (issue #567) —
    // mirrors HandleDelete's fallback-to-single-item pattern. Each rectangle is matched to its
    // own owning frame (see AppCommands.MatchRectanglesToFrames), so a selection spanning
    // multiple frames still resizes correctly.
    private static void MatchFrameSize(AARectSave rect, IAppCommands appCommands, ISelectedState selectedState)
    {
        var rects = selectedState.SelectedRectangles;
        appCommands.MatchRectanglesToFrames(rects.Count > 0 ? rects : new List<AARectSave> { rect });
        appCommands.RefreshAnimationFrameDisplay();
        appCommands.SaveCurrentAnimationChainList();
    }

    private static void AddCopyCutPasteDuplicate(List<TreeMenuItem> items, TreeMenuActions actions, Selection? selection)
    {
        items.Add(TreeMenuItem.Item(CountedVerb("Copy", selection), actions.Copy, TreeMenuIcon.Copy));
        items.Add(TreeMenuItem.Item(CountedVerb("Cut", selection), actions.Cut, TreeMenuIcon.Cut));
        items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
        items.Add(TreeMenuItem.Item(CountedVerb("Duplicate", selection), actions.Duplicate, TreeMenuIcon.Duplicate));
    }

    // Copy/Cut/Duplicate refuse a mixed selection (SelectionCopyContext.MixedSelectionMessage), so
    // only a uniform one gets a count; a mixed one keeps the bare verb rather than promise "5 Items".
    private static string CountedVerb(string verb, Selection? selection) =>
        selection is { IsMixed: false } ? selection.Label(verb) : verb;

    /// <summary>
    /// The multi-selection a tree menu acts on, as the count and noun a label needs. Counts the
    /// same chain/frame/shape nodes <c>HandleDelete</c> deletes.
    /// </summary>
    private sealed class Selection
    {
        private readonly int _count;
        private readonly string _singular;
        private readonly string _plural;

        /// <summary>True when the selection spans animations, frames and shapes.</summary>
        public bool IsMixed { get; }

        private Selection(int count, string singular, string plural, bool isMixed)
        {
            _count = count;
            _singular = singular;
            _plural = plural;
            IsMixed = isMixed;
        }

        public string Label(string verb) => $"{verb} {Plural.Format(_count, _singular, _plural)}";

        /// <returns>Null for zero or one selected item, which keeps the single-node labels.</returns>
        public static Selection? Describe(IReadOnlyList<object> nodes)
        {
            var items = nodes.Where(n => n is AnimationChainSave or AnimationFrameSave or ShapeSave).ToList();
            if (items.Count < 2) return null;

            int categories = items
                .Select(n => n switch { AnimationChainSave => 0, AnimationFrameSave => 1, _ => 2 })
                .Distinct().Count();
            if (categories > 1) return new Selection(items.Count, "Item", "Items", isMixed: true);

            return items[0] switch
            {
                AnimationChainSave => new Selection(items.Count, "Animation", "Animations", false),
                AnimationFrameSave => new Selection(items.Count, "Frame", "Frames", false),
                _ when items.All(n => n is AARectSave) => new Selection(items.Count, "Rectangle", "Rectangles", false),
                _ when items.All(n => n is CircleSave) => new Selection(items.Count, "Circle", "Circles", false),
                _ when items.All(n => n is PolygonSave) => new Selection(items.Count, "Polygon", "Polygons", false),
                _ => new Selection(items.Count, "Shape", "Shapes", false),
            };
        }
    }

    // Mirrors the shape/frame/chain reorder convention: four items (Move to Top/Up/Down/Bottom)
    // guarded by the node's position within its containing list. No-op (and no separator) when
    // that list has one entry or less.
    private static void AddShapeReorderItems(
        List<TreeMenuItem> items, object shape, AnimationFrameSave? frame, IAppCommands appCommands)
    {
        // Shapes reorder only among shapes of the same type (the file groups them by type).
        var siblings = frame?.ShapesSave?.Shapes.Where(s => s.GetType() == shape.GetType()).ToList();
        if (siblings is null || siblings.Count <= 1) return;
        int index = siblings.IndexOf(shape);
        bool isFirst = index == 0;
        bool isLast = index == siblings.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("Move To Top", () => appCommands.MoveShapeToTop(shape, frame!), TreeMenuIcon.MoveToTop));
        if (!isFirst) items.Add(TreeMenuItem.Item("Move Up", () => appCommands.MoveShape(shape, frame!, -1), TreeMenuIcon.MoveUp));
        if (!isLast) items.Add(TreeMenuItem.Item("Move Down", () => appCommands.MoveShape(shape, frame!, +1), TreeMenuIcon.MoveDown));
        if (!isLast) items.Add(TreeMenuItem.Item("Move To Bottom", () => appCommands.MoveShapeToBottom(shape, frame!), TreeMenuIcon.MoveToBottom));
        items.Add(TreeMenuItem.Separator());
    }

    private static void AddFrameReorderItems(
        List<TreeMenuItem> items, AnimationFrameSave frame, AnimationChainSave chain, IAppCommands appCommands)
    {
        int index = chain.Frames.IndexOf(frame);
        bool isFirst = index == 0;
        bool isLast = index == chain.Frames.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("Move To Top", () => appCommands.MoveFrameToTop(frame, chain), TreeMenuIcon.MoveToTop));
        if (!isFirst) items.Add(TreeMenuItem.Item("Move Up", () => appCommands.MoveFrame(frame, chain, -1), TreeMenuIcon.MoveUp));
        if (!isLast) items.Add(TreeMenuItem.Item("Move Down", () => appCommands.MoveFrame(frame, chain, +1), TreeMenuIcon.MoveDown));
        if (!isLast) items.Add(TreeMenuItem.Item("Move To Bottom", () => appCommands.MoveFrameToBottom(frame, chain), TreeMenuIcon.MoveToBottom));
    }

    private static void AddChainReorderItems(
        List<TreeMenuItem> items, AnimationChainSave chain, List<AnimationChainSave> chains, IAppCommands appCommands)
    {
        int index = chains.IndexOf(chain);
        bool isFirst = index == 0;
        bool isLast = index == chains.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("Move To Top", () => appCommands.MoveChainToTop(chain), TreeMenuIcon.MoveToTop));
        if (!isFirst) items.Add(TreeMenuItem.Item("Move Up", () => appCommands.MoveChain(chain, -1), TreeMenuIcon.MoveUp));
        if (!isLast) items.Add(TreeMenuItem.Item("Move Down", () => appCommands.MoveChain(chain, +1), TreeMenuIcon.MoveDown));
        if (!isLast) items.Add(TreeMenuItem.Item("Move To Bottom", () => appCommands.MoveChainToBottom(chain), TreeMenuIcon.MoveToBottom));
    }
}
