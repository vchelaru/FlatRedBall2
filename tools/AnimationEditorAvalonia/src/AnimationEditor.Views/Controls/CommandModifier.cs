using Avalonia.Input;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The platform's command modifier for mouse gestures: ⌘ (Meta) on macOS, Ctrl elsewhere.
/// On macOS Control+click is a right-click, so a Ctrl-modified click gesture must check this
/// instead of <see cref="KeyModifiers.Control"/>. The host picks the instance once
/// (<see cref="ForHost"/>) and passes it down, so tests can exercise either platform on any OS.
/// </summary>
public sealed class CommandModifier
{
    public static CommandModifier Control { get; } = new(KeyModifiers.Control, "Ctrl", Key.LeftCtrl, Key.RightCtrl);

    /// <summary>macOS ⌘. Avalonia reports the Command keys as <see cref="Key.LWin"/>/<see cref="Key.RWin"/>.</summary>
    public static CommandModifier Meta { get; } = new(KeyModifiers.Meta, "⌘", Key.LWin, Key.RWin);

    public static CommandModifier ForHost(bool isMacOS) => isMacOS ? Meta : Control;

    private readonly Key _leftKey;
    private readonly Key _rightKey;

    private CommandModifier(KeyModifiers modifier, string displayName, Key leftKey, Key rightKey)
    {
        Modifier = modifier;
        DisplayName = displayName;
        _leftKey = leftKey;
        _rightKey = rightKey;
    }

    public KeyModifiers Modifier { get; }

    /// <summary>Label for user-facing text, e.g. "Ctrl" or "⌘".</summary>
    public string DisplayName { get; }

    public bool IsHeld(KeyModifiers modifiers) => (modifiers & Modifier) != 0;

    /// <summary>True when <paramref name="key"/> is this modifier's own key, pressed or released.</summary>
    public bool IsModifierKey(Key key) => key == _leftKey || key == _rightKey;
}
