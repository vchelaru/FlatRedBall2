using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// One entry in a tree context-menu plan built by <see cref="TreeMenuPlanBuilder"/>. Hosts
/// (desktop Avalonia, browser) walk the ordered list and materialize their own menu-item type
/// from each entry — this type carries no UI-framework dependency.
/// </summary>
public sealed class TreeMenuItem
{
    public string? Header { get; }
    public Action? OnClick { get; }
    public IReadOnlyList<TreeMenuItem>? Children { get; }
    public TreeMenuHostSlot? HostSlot { get; }
    public bool IsSeparator { get; }

    private TreeMenuItem(string? header, Action? onClick, IReadOnlyList<TreeMenuItem>? children,
        TreeMenuHostSlot? hostSlot, bool isSeparator)
    {
        Header = header;
        OnClick = onClick;
        Children = children;
        HostSlot = hostSlot;
        IsSeparator = isSeparator;
    }

    public static TreeMenuItem Item(string header, Action onClick) => new(header, onClick, null, null, false);

    public static TreeMenuItem Separator() => new(null, null, null, null, true);

    public static TreeMenuItem SubMenu(string header, params TreeMenuItem[] children) =>
        new(header, null, children, null, false);

    /// <summary>
    /// A placeholder for a menu item the host builds itself — a dialog or filesystem action
    /// that needs UI-framework types <see cref="TreeMenuPlanBuilder"/> can't depend on (see
    /// <see cref="TreeMenuHostSlot"/>). The host substitutes its own menu item at this position
    /// when walking the plan.
    /// </summary>
    public static TreeMenuItem HostSlotItem(TreeMenuHostSlot slot) => new(null, null, null, slot, false);
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
