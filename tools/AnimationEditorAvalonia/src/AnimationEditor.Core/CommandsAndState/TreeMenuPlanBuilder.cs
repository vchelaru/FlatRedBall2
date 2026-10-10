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

        switch (nodeData)
        {
            case AARectSave rect:
                AddShapeReorderItems(items, rect, objectFinder.GetAnimationFrameContaining(rect), appCommands);
                items.Add(TreeMenuItem.Item("Match Frame Size", () => MatchFrameSize(rect, appCommands, selectedState), TreeMenuIcon.MatchSize));
                items.Add(TreeMenuItem.Separator());
                AddCopyCutPasteDuplicate(items, actions);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Rectangle", actions.Delete, TreeMenuIcon.Delete));
                break;

            case CircleSave circle:
                AddShapeReorderItems(items, circle, objectFinder.GetAnimationFrameContaining(circle), appCommands);
                AddCopyCutPasteDuplicate(items, actions);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Circle", actions.Delete, TreeMenuIcon.Delete));
                break;

            case PolygonSave polygon:
                AddShapeReorderItems(items, polygon, objectFinder.GetAnimationFrameContaining(polygon), appCommands);
                items.Add(TreeMenuItem.Item("Flip Horizontal", () => appCommands.FlipPolygonHorizontally(polygon), TreeMenuIcon.FlipHorizontal));
                items.Add(TreeMenuItem.Item("Flip Vertical", () => appCommands.FlipPolygonVertically(polygon), TreeMenuIcon.FlipVertical));
                items.Add(TreeMenuItem.Separator());
                AddCopyCutPasteDuplicate(items, actions);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Polygon", actions.Delete, TreeMenuIcon.Delete));
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
                items.Add(TreeMenuItem.Item("Copy", actions.Copy, TreeMenuIcon.Copy));
                items.Add(TreeMenuItem.Item("Cut", actions.Cut, TreeMenuIcon.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
                if (chain is not null)
                    items.Add(TreeMenuItem.Item("Duplicate", actions.Duplicate, TreeMenuIcon.Duplicate));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.ViewTextureInExplorer));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Frame", actions.Delete, TreeMenuIcon.Delete));
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
                if (!isNativeTsx)
                {
                    items.Add(TreeMenuItem.Item("Flip Horizontally", () => appCommands.FlipChainHorizontally(chain2), TreeMenuIcon.FlipHorizontal));
                    items.Add(TreeMenuItem.Item("Flip Vertically", () => appCommands.FlipChainVertically(chain2), TreeMenuIcon.FlipVertical));
                }
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
                items.Add(TreeMenuItem.Item("Copy", actions.Copy, TreeMenuIcon.Copy));
                items.Add(TreeMenuItem.Item("Cut", actions.Cut, TreeMenuIcon.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
                if (isNativeTsx)
                    items.Add(TreeMenuItem.Item("Duplicate", actions.Duplicate, TreeMenuIcon.Duplicate));
                else
                    items.Add(TreeMenuItem.SubMenu("Duplicate", TreeMenuIcon.Duplicate,
                        TreeMenuItem.Item("Original", actions.Duplicate, TreeMenuIcon.Duplicate),
                        TreeMenuItem.Item("Flip Horizontal", () => actions.DuplicateChainFlip!(true, false), TreeMenuIcon.FlipHorizontal),
                        TreeMenuItem.Item("Flip Vertical", () => actions.DuplicateChainFlip!(false, true), TreeMenuIcon.FlipVertical)));
                items.Add(TreeMenuItem.Separator());
                if (!isNativeTsx)
                    items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AdjustOffsets));
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!, TreeMenuIcon.Rename));
                var fileName = projectManager.FileName;
                if (actions.CopyText is not null && !string.IsNullOrEmpty(fileName))
                    items.Add(TreeMenuItem.Item("Copy Qualified Name", () => actions.CopyText(QualifiedName.Format(fileName, chain2.Name)), TreeMenuIcon.CopyName));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Animation", actions.Delete, TreeMenuIcon.Delete));
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

    private static void AddCopyCutPasteDuplicate(List<TreeMenuItem> items, TreeMenuActions actions)
    {
        items.Add(TreeMenuItem.Item("Copy", actions.Copy, TreeMenuIcon.Copy));
        items.Add(TreeMenuItem.Item("Cut", actions.Cut, TreeMenuIcon.Cut));
        items.Add(TreeMenuItem.Item("Paste", actions.Paste, TreeMenuIcon.Paste));
        items.Add(TreeMenuItem.Item("Duplicate", actions.Duplicate, TreeMenuIcon.Duplicate));
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
