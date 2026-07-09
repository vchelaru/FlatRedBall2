using System.Collections.Generic;

namespace AnimationEditor.App.Models;

/// <summary>View model for a single row in the Keyboard Shortcuts panel.</summary>
/// <param name="Description">Human-readable description of the action.</param>
/// <param name="Gesture">Display text for the gesture, e.g. <c>"Ctrl+Z"</c>.</param>
internal sealed record HotkeyEntryVm(string Description, string Gesture);

/// <summary>A category header plus its rows, e.g. "Edit" grouping Copy/Cut/Paste/Undo/Redo.</summary>
internal sealed record HotkeyCategoryVm(string Category, IReadOnlyList<HotkeyEntryVm> Hotkeys);
