namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure classification of the <c>tiled-achj-import</c> extension's install state from raw
/// file-presence/content facts. Keeps the decision testable without touching disk -- mirrors
/// <see cref="AchxFileAssociationEvaluator"/>'s split between fact-gathering and classification.
/// </summary>
public static class TiledExtensionStatusEvaluator
{
    public static TiledExtensionInstallStatus Classify(bool filesPresent, bool contentMatches)
    {
        if (!filesPresent)
            return TiledExtensionInstallStatus.NotInstalled;

        return contentMatches ? TiledExtensionInstallStatus.UpToDate : TiledExtensionInstallStatus.Outdated;
    }
}
