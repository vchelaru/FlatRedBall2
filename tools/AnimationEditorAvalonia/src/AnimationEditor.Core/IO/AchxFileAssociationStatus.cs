namespace AnimationEditor.Core.IO;

/// <summary>
/// How the current process relates to the operating-system default handler for
/// <c>.achx</c> files. Used by the startup banner and file-association settings UI.
/// </summary>
public enum AchxFileAssociationStatus
{
    /// <summary>File association is not implemented on this platform.</summary>
    NotSupported,

    /// <summary>Another app (or no handler) is registered for <c>.achx</c>.</summary>
    NotAssociated,

    /// <summary>Our ProgId is registered and its open command targets this build.</summary>
    AssociatedWithThisBuild,

    /// <summary>Our ProgId is registered but points at a missing or different exe path.</summary>
    Stale,
}
