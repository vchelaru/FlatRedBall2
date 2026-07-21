using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Builds the ordered tree right-click menu plan shared by every editor host, branching on the
/// selected tree node's data type (rectangle, circle, frame, chain, or nothing selected).
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

        switch (nodeData)
        {
            case AARectSave rect:
                AddShapeReorderItems(items, rect, objectFinder.GetAnimationFrameContaining(rect), appCommands);
                items.Add(TreeMenuItem.Item("Match Frame Size", () => MatchFrameSize(rect, appCommands, selectedState)));
                items.Add(TreeMenuItem.Separator());
                AddCopyCutPasteDuplicate(items, actions);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Rectangle", actions.Delete));
                break;

            case CircleSave circle:
                AddShapeReorderItems(items, circle, objectFinder.GetAnimationFrameContaining(circle), appCommands);
                AddCopyCutPasteDuplicate(items, actions);
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Circle", actions.Delete));
                break;

            case AnimationFrameSave frame:
            {
                var chain = objectFinder.GetAnimationChainContaining(frame);
                if (chain is not null && chain.Frames.Count > 1)
                {
                    AddFrameReorderItems(items, frame, chain, appCommands);
                    items.Add(TreeMenuItem.Separator());
                }
                items.Add(TreeMenuItem.Item("Add AxisAlignedRectangle", () => appCommands.AddAxisAlignedRectangle(frame)));
                items.Add(TreeMenuItem.Item("Add Circle", () => appCommands.AddCircle(frame)));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Copy", actions.Copy));
                items.Add(TreeMenuItem.Item("Cut", actions.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste));
                if (chain is not null)
                    items.Add(TreeMenuItem.Item("Duplicate", actions.Duplicate));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.ViewTextureInExplorer));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Frame", actions.Delete));
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
                items.Add(TreeMenuItem.Item("Flip Horizontally", () => appCommands.FlipChainHorizontally(chain2)));
                items.Add(TreeMenuItem.Item("Flip Vertically", () => appCommands.FlipChainVertically(chain2)));
                items.Add(TreeMenuItem.Item("Invert Frame Order", () => appCommands.InvertFrameOrder(chain2)));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Add Animation", actions.AddAnimation!));
                items.Add(TreeMenuItem.Item("Add Frame", () => appCommands.AddFrame(chain2)));
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AddMultipleFrames));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Copy", actions.Copy));
                items.Add(TreeMenuItem.Item("Cut", actions.Cut));
                items.Add(TreeMenuItem.Item("Paste", actions.Paste));
                items.Add(TreeMenuItem.SubMenu("Duplicate",
                    TreeMenuItem.Item("Original", actions.Duplicate),
                    TreeMenuItem.Item("Flip Horizontal", () => actions.DuplicateChainFlip!(true, false)),
                    TreeMenuItem.Item("Flip Vertical", () => actions.DuplicateChainFlip!(false, true))));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.HostSlotItem(TreeMenuHostSlot.AdjustOffsets));
                items.Add(TreeMenuItem.Item("Rename…", actions.Rename!));
                items.Add(TreeMenuItem.Separator());
                items.Add(TreeMenuItem.Item("Delete Animation", actions.Delete));
                break;
            }

            default:
                items.Add(TreeMenuItem.Item("Add Animation", actions.AddAnimation!));
                break;
        }

        items.Add(TreeMenuItem.Separator());
        items.Add(TreeMenuItem.Item("Sort Animations Alphabetically", appCommands.SortAnimationsAlphabetically));

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
        items.Add(TreeMenuItem.Item("Copy", actions.Copy));
        items.Add(TreeMenuItem.Item("Cut", actions.Cut));
        items.Add(TreeMenuItem.Item("Paste", actions.Paste));
        items.Add(TreeMenuItem.Item("Duplicate", actions.Duplicate));
    }

    // Mirrors the shape/frame/chain reorder convention: four items (Move to Top/Up/Down/Bottom)
    // guarded by the node's position within its containing list. No-op (and no separator) when
    // that list has one entry or less.
    private static void AddShapeReorderItems(
        List<TreeMenuItem> items, object shape, AnimationFrameSave? frame, IAppCommands appCommands)
    {
        var shapes = frame?.ShapesSave?.Shapes;
        if (shapes is null || shapes.Count <= 1) return;
        int index = shapes.IndexOf(shape);
        bool isFirst = index == 0;
        bool isLast = index == shapes.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("^^ Move To Top", () => appCommands.MoveShapeToTop(shape, frame!)));
        if (!isFirst) items.Add(TreeMenuItem.Item("^  Move Up", () => appCommands.MoveShape(shape, frame!, -1)));
        if (!isLast) items.Add(TreeMenuItem.Item("v  Move Down", () => appCommands.MoveShape(shape, frame!, +1)));
        if (!isLast) items.Add(TreeMenuItem.Item("vv Move To Bottom", () => appCommands.MoveShapeToBottom(shape, frame!)));
        items.Add(TreeMenuItem.Separator());
    }

    private static void AddFrameReorderItems(
        List<TreeMenuItem> items, AnimationFrameSave frame, AnimationChainSave chain, IAppCommands appCommands)
    {
        int index = chain.Frames.IndexOf(frame);
        bool isFirst = index == 0;
        bool isLast = index == chain.Frames.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("^^ Move To Top", () => appCommands.MoveFrameToTop(frame, chain)));
        if (!isFirst) items.Add(TreeMenuItem.Item("^  Move Up", () => appCommands.MoveFrame(frame, chain, -1)));
        if (!isLast) items.Add(TreeMenuItem.Item("v  Move Down", () => appCommands.MoveFrame(frame, chain, +1)));
        if (!isLast) items.Add(TreeMenuItem.Item("vv Move To Bottom", () => appCommands.MoveFrameToBottom(frame, chain)));
    }

    private static void AddChainReorderItems(
        List<TreeMenuItem> items, AnimationChainSave chain, List<AnimationChainSave> chains, IAppCommands appCommands)
    {
        int index = chains.IndexOf(chain);
        bool isFirst = index == 0;
        bool isLast = index == chains.Count - 1;
        if (!isFirst) items.Add(TreeMenuItem.Item("^^ Move To Top", () => appCommands.MoveChainToTop(chain)));
        if (!isFirst) items.Add(TreeMenuItem.Item("^  Move Up", () => appCommands.MoveChain(chain, -1)));
        if (!isLast) items.Add(TreeMenuItem.Item("v  Move Down", () => appCommands.MoveChain(chain, +1)));
        if (!isLast) items.Add(TreeMenuItem.Item("vv Move To Bottom", () => appCommands.MoveChainToBottom(chain)));
    }
}
