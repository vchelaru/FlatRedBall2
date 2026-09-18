namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure decision logic for whether to surface the "install the Tiled integration" startup
/// banner (#1128). Kept separate from <see cref="ITiledExtensionInstaller"/> so the rule can be
/// unit-tested without touching disk or the UI -- mirrors <see cref="DefaultHandlerPromptDecider"/>.
/// </summary>
public static class TiledExtensionInstallPromptDecider
{
    /// <summary>
    /// Returns <c>true</c> only when a Tiled extensions folder is known (detected or previously
    /// chosen by the user), the extension is missing or outdated there, and the user has not
    /// dismissed the prompt with "Don't ask again".
    /// </summary>
    /// <param name="tiledDetected">Whether a Tiled extensions folder is known.</param>
    /// <param name="status">The extension's install state, as reported against that folder.</param>
    /// <param name="isPromptSuppressed">Whether the user clicked "Don't ask again".</param>
    public static bool ShouldPrompt(bool tiledDetected, TiledExtensionInstallStatus status, bool isPromptSuppressed)
    {
        if (!tiledDetected)
            return false;
        if (isPromptSuppressed)
            return false;

        return status is TiledExtensionInstallStatus.NotInstalled or TiledExtensionInstallStatus.Outdated;
    }
}
