namespace AnimationEditor.Core.Models
{
    /// <summary>Result of calling <see cref="TabManager.OpenOrFocus"/>.</summary>
    public enum TabOpenResult
    {
        /// <summary>A new tab was created and activated.</summary>
        Opened,
        /// <summary>The path was already open; its existing tab was activated instead.</summary>
        Focused,
    }
}
