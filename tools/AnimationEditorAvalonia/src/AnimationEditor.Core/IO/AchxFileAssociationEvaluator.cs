namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure classification of whether the editor is the live default handler for
/// <c>.achx</c> files. Keeps registry/UI wiring testable without touching the registry.
/// </summary>
public static class AchxFileAssociationEvaluator
{
    /// <summary>
    /// Returns <c>true</c> only when our ProgId is registered and its open command targets
    /// <paramref name="currentExePath"/> and that registered exe still exists.
    /// </summary>
    public static bool IsDefaultForCurrentBuild(
        bool isOurProgId,
        string? registeredExePath,
        string? currentExePath,
        bool registeredExeExists)
    {
        return Classify(isOurProgId, registeredExePath, currentExePath, registeredExeExists)
            == AchxFileAssociationStatus.AssociatedWithThisBuild;
    }

    /// <summary>Classifies association state from registry facts and the running process path.</summary>
    public static AchxFileAssociationStatus Classify(
        bool isOurProgId,
        string? registeredExePath,
        string? currentExePath,
        bool registeredExeExists)
    {
        if (!isOurProgId)
            return AchxFileAssociationStatus.NotAssociated;

        if (string.IsNullOrEmpty(registeredExePath) || string.IsNullOrEmpty(currentExePath))
            return AchxFileAssociationStatus.Stale;

        if (!registeredExeExists)
            return AchxFileAssociationStatus.Stale;

        if (!ExePathsMatch(registeredExePath, currentExePath))
            return AchxFileAssociationStatus.Stale;

        return AchxFileAssociationStatus.AssociatedWithThisBuild;
    }

    internal static bool ExePathsMatch(string registeredExePath, string currentExePath) =>
        string.Equals(
            Path.GetFullPath(registeredExePath),
            Path.GetFullPath(currentExePath),
            StringComparison.OrdinalIgnoreCase);
}
