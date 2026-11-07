namespace AnimationEditor.Core.Hotkeys;

/// <summary>What a <see cref="HotkeyDefinition"/> or menu item acts on, which decides the tab kinds it works in.</summary>
public enum HotkeyScope
{
    /// <summary>
    /// Acts on the open animation chain document's content (chains, frames, shapes, textures, the wireframe and
    /// preview). Fires only while an <c>.achx</c> tab is active, so it never reaches that document while it is
    /// hidden behind another tab kind.
    /// </summary>
    Achx,

    /// <summary>
    /// Acts on whichever document the active tab edits: undo, redo, save, the History panel, and Delete, which each
    /// tab kind routes to its own selection. Fires in every tab kind that has a document.
    /// </summary>
    Document,

    /// <summary>Acts on the editor itself (open, new, diagnostics, zoom of the focused panel). Fires in every tab kind.</summary>
    App,
}
