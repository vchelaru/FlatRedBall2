using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// One entry in a tree context-menu plan built by <see cref="TreeMenuPlanBuilder"/>. The UI
/// layer walks the ordered list and materializes a menu item from each entry — this type carries no UI-framework dependency.
/// </summary>
public sealed class TreeMenuItem
{
    public string? Header { get; }
    public Action? OnClick { get; }
    public IReadOnlyList<TreeMenuItem>? Children { get; }
    public TreeMenuHostSlot? HostSlot { get; }
    public bool IsSeparator { get; }
    public TreeMenuIcon? Icon { get; }

    private TreeMenuItem(string? header, Action? onClick, IReadOnlyList<TreeMenuItem>? children,
        TreeMenuHostSlot? hostSlot, bool isSeparator, TreeMenuIcon? icon = null)
    {
        Header = header;
        OnClick = onClick;
        Children = children;
        HostSlot = hostSlot;
        IsSeparator = isSeparator;
        Icon = icon;
    }

    public static TreeMenuItem Item(string header, Action onClick, TreeMenuIcon? icon = null) =>
        new(header, onClick, null, null, false, icon);

    public static TreeMenuItem Separator() => new(null, null, null, null, true);

    public static TreeMenuItem SubMenu(string header, TreeMenuIcon icon, params TreeMenuItem[] children) =>
        new(header, null, children, null, false, icon);

    /// <summary>
    /// A placeholder for a menu item the host builds itself — a dialog or filesystem action
    /// that needs UI-framework types <see cref="TreeMenuPlanBuilder"/> can't depend on (see
    /// <see cref="TreeMenuHostSlot"/>). The host substitutes its own menu item at this position
    /// when walking the plan.
    /// </summary>
    public static TreeMenuItem HostSlotItem(TreeMenuHostSlot slot) => new(null, null, null, slot, false);
}

/// <summary>
/// A menu-item icon, kept framework-free so each host maps it to its own image type.
/// </summary>
public enum TreeMenuIcon
{
    Rectangle,
    Circle,
    Polygon,
    Frame,
    Animation,
    Copy,
    Cut,
    Paste,
    Duplicate,
    Delete,
    Rename,
    MoveToTop,
    MoveUp,
    MoveDown,
    MoveToBottom,
    Sort,
    FlipHorizontal,
    FlipVertical,
    Reverse,
    FrameTime,
    Offsets,
    RevealFile,
    MatchSize,
}

/// <summary>
/// Identifies which host-built item belongs at a <see cref="TreeMenuItem.HostSlot"/> position.
/// These four are excluded from the shared plan (tracked separately, issue #756) because each
/// needs UI-framework or filesystem access <see cref="TreeMenuPlanBuilder"/> can't depend on:
/// a dialog window (<see cref="AdjustFrameTime"/>, <see cref="AddMultipleFrames"/>,
/// <see cref="AdjustOffsets"/>) or shelling out to the OS file explorer
/// (<see cref="ViewTextureInExplorer"/>).
/// </summary>
public enum TreeMenuHostSlot
{
    AdjustFrameTime,
    AddMultipleFrames,
    AdjustOffsets,
    ViewTextureInExplorer,
}
