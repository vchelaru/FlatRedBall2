using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Hotkeys;

/// <summary>
/// A single hotkey: one or more equivalent <see cref="Gestures"/> (e.g. Redo accepts both
/// Ctrl+Y and Ctrl+Shift+Z), the action it runs, and the metadata a menu or shortcuts panel
/// needs to display it. <see cref="ShouldSkip"/> lets a hotkey opt out at dispatch time — e.g.
/// Copy/Paste/Delete must not fire while a TextBox has focus — without a separate guard table.
/// </summary>
public sealed class HotkeyDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyList<HotkeyGesture> Gestures { get; init; }
    public required Action Action { get; init; }
    public Func<bool>? ShouldSkip { get; init; }

    /// <summary>Defaults to <see cref="HotkeyScope.Achx"/>, the narrowest scope, so a new hotkey cannot reach a hidden document by accident.</summary>
    public HotkeyScope Scope { get; init; } = HotkeyScope.Achx;

    public bool Matches(string keyName, HotkeyModifiers pressed) =>
        Gestures.Any(g => g.Matches(keyName, pressed));

    /// <summary>
    /// Display text for the primary (first) gesture, e.g. <c>"Ctrl+Z"</c>. Used to drive
    /// <c>MenuItem.InputGesture</c> so the menu can never show a shortcut this hotkey doesn't
    /// actually respond to.
    /// </summary>
    public string DisplayText => Format(Gestures[0]);

    /// <summary>
    /// The gesture macOS shows and prefers when it differs from the first one (Redo is Cmd+Shift+Z there, Ctrl+Y
    /// elsewhere). Must be one of <see cref="Gestures"/> so the menu never shows a shortcut that does nothing.
    /// </summary>
    public HotkeyGesture? MacPreferredGesture { get; init; }

    /// <summary>
    /// <see cref="DisplayText"/> for the host platform: the <see cref="MacPreferredGesture"/> on macOS, else the first gesture.
    /// </summary>
    public string DisplayTextFor(bool isMacOS) =>
        Format(isMacOS && MacPreferredGesture is { } mac ? mac : Gestures[0]);

    private static string Format(HotkeyGesture gesture)
    {
        var parts = new List<string>(4);
        if (gesture.Required.HasFlag(HotkeyModifiers.Command)) parts.Add("Ctrl");
        if (gesture.Required.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.Required.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        parts.Add(gesture.KeyName);
        return string.Join("+", parts);
    }
}
