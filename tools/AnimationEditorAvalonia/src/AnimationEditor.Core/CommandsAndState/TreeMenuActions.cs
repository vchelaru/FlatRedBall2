using System;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Host-supplied callbacks for tree context-menu actions <see cref="TreeMenuPlanBuilder"/> can't
/// express purely through <see cref="IAppCommands"/>/<see cref="ISelectedState"/>/
/// <see cref="IObjectFinder"/> — either because they touch UI-framework state (inline tree
/// rename needs the tree's visual nodes) or because the existing host implementation is already
/// working, framework-free logic the host owns (copy/cut/paste/duplicate/delete).
/// </summary>
/// <param name="Rename">Only invoked for rectangle/circle/chain nodes.</param>
/// <param name="AddAnimation">Only invoked for the chain and empty-selection nodes.</param>
/// <param name="DuplicateChainFlip">Only invoked from the chain node's Duplicate submenu (flipH, flipV).</param>
public sealed record TreeMenuActions(
    Action Copy,
    Action Cut,
    Action Paste,
    Action Duplicate,
    Action Delete,
    Action? Rename = null,
    Action? AddAnimation = null,
    Action<bool, bool>? DuplicateChainFlip = null);
