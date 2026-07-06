namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// The kind of content a <see cref="TabEntry"/> holds, which selects the view shown in the
    /// editor's main pane. PNG tabs are read-only image previews and bypass the animation-editing
    /// machinery (model load, undo stack, companion files) entirely.
    /// </summary>
    public enum TabKind
    {
        /// <summary>An <c>.achx</c> animation-chain file opened in the full editor.</summary>
        Achx,

        /// <summary>A <c>.png</c> image opened as a plain preview.</summary>
        Png,
    }
}
