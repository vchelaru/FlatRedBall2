namespace AnimationEditor.Core.IO;

/// <summary>User-visible labels for <see cref="AchxFileAssociationStatus"/> in settings UI.</summary>
public static class AchxFileAssociationStatusFormatter
{
    public static string Describe(AchxFileAssociationStatus status) => status switch
    {
        AchxFileAssociationStatus.NotSupported => "File association is not available on this platform.",
        AchxFileAssociationStatus.NotAssociated =>
            "Animation Editor is not the default app for .achx files.",
        AchxFileAssociationStatus.AssociatedWithThisBuild =>
            "Animation Editor is the default app for .achx files (this build).",
        AchxFileAssociationStatus.Stale =>
            "A previous Animation Editor install is registered, but its executable is missing or different from this build.",
        _ => string.Empty,
    };
}
