namespace AnimationEditor.Core.Hotkeys;

/// <summary>What a <see cref="HotkeyDefinition"/> or menu item acts on, which decides the tab kinds it works in.</summary>
public enum HotkeyScope
{
    /// <summary>
    /// Acts on the open animation document (its selection, undo history or file). Fires only while a
    /// tab that edits a document is active, so it never reaches a document hidden behind another tab kind.
    /// </summary>
    Document,

    /// <summary>Acts on the editor itself (open, new, diagnostics, zoom of the focused panel). Fires in every tab kind.</summary>
    App,
}
