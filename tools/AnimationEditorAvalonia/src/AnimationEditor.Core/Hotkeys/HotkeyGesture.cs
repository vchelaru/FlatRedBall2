namespace AnimationEditor.Core.Hotkeys;

/// <summary>
/// One key + modifier combination that can trigger a hotkey. A modifier absent from both
/// <see cref="Required"/> and <see cref="Forbidden"/> is ignored, so e.g. a gesture requiring
/// only <see cref="HotkeyModifiers.Command"/> still matches with Shift also held — mirroring the
/// original hand-written if/else chain this was extracted from (Ctrl+Shift+C still copies).
/// </summary>
public readonly record struct HotkeyGesture(
    string KeyName,
    HotkeyModifiers Required = HotkeyModifiers.None,
    HotkeyModifiers Forbidden = HotkeyModifiers.None)
{
    /// <param name="keyName">The pressed key's name (e.g. an Avalonia <c>Key</c>'s <c>ToString()</c>).</param>
    /// <param name="pressed">The modifiers held down when <paramref name="keyName"/> was pressed.</param>
    public bool Matches(string keyName, HotkeyModifiers pressed) =>
        keyName == KeyName
        && (pressed & Required) == Required
        && (pressed & Forbidden) == HotkeyModifiers.None;
}
