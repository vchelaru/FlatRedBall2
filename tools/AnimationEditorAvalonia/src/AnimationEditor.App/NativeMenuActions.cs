using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace AnimationEditor.App;

/// <summary>
/// Delegates for every actionable menu item in the app, populated by <see cref="MainWindow"/>
/// and consumed by the macOS <c>NativeMenu</c> registration in <see cref="App"/>.
/// </summary>
internal sealed record NativeMenuActions(
    Action New,
    Action Load,
    Func<IReadOnlyList<(string Header, Action Execute)>> RecentFiles,
    Action OpenProjectFolder,
    Action CloseProjectFolder,
    Func<bool> HasProjectFolder,
    Action Save,
    Action SaveAs,
    Action Undo,
    Action Redo,
    Action Copy,
    Action Cut,
    Action Paste,
    Action Duplicate,
    Action ReloadFromDisk,
    Action ToggleHotReload,
    Action ResizeTexture,
    Action NavBack,
    Action NavForward,
    Func<bool> CanGoBack,
    Func<bool> CanGoForward,
    Action<Action> SubscribeNavigationChanged,
    KeyGesture NavBackGesture,
    KeyGesture NavForwardGesture,
    Action ShowHistory,
    Action ViewLog,
    Action About);
