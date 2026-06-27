using System;
using System.Collections.Generic;

namespace AnimationEditor.App;

/// <summary>
/// Delegates for every actionable menu item in the app, populated by <see cref="MainWindow"/>
/// and consumed by the macOS <c>NativeMenu</c> registration in <see cref="App"/>.
/// </summary>
internal sealed record NativeMenuActions(
    Action New,
    Action Load,
    Func<IReadOnlyList<(string Header, Action Execute)>> RecentFiles,
    Action Save,
    Action SaveAs,
    Action Undo,
    Action Redo,
    Action Copy,
    Action Paste,
    Action Duplicate,
    Action ReloadFromDisk,
    Action ToggleHotReload,
    Action ResizeTexture,
    Action ShowHistory,
    Action About);
