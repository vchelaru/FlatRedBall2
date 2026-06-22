namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure decision logic for whether to surface the "make me the default .achx handler"
/// prompt on startup. Kept separate from the platform-specific
/// <see cref="IFileAssociationService"/> so the three-state rule can be unit-tested
/// without touching the registry or the UI.
/// </summary>
public static class DefaultHandlerPromptDecider
{
    /// <summary>
    /// Returns <c>true</c> only when the platform supports file association, the editor is
    /// not already the default handler, and the user has not previously dismissed the prompt.
    /// </summary>
    /// <param name="isSupported">Whether file association is implemented on the current platform.</param>
    /// <param name="isDefault">Whether the editor is already the registered default for <c>.achx</c>.</param>
    /// <param name="isPromptSuppressed">Whether the user clicked "Don't show again".</param>
    public static bool ShouldPrompt(bool isSupported, bool isDefault, bool isPromptSuppressed)
    {
        if (!isSupported)
            return false;
        if (isDefault)
            return false;
        if (isPromptSuppressed)
            return false;

        return true;
    }
}
