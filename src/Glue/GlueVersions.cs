namespace FlatRedBall2.Glue;

/// <summary>
/// Glue's project schema version, mirrored for diagnostics only.
/// </summary>
/// <remarks>
/// The reader implements exactly one schema — the current one — and never branches on version.
/// These constants exist so a project saved by an older Glue can be reported, not handled
/// differently. Versions upstream move often, so treat a mismatch as information, never as a
/// failure.
/// </remarks>
public static class GlueVersions
{
    /// <summary>
    /// The newest schema version known to this build, mirroring FRB1's
    /// <c>GlueProjectSave.LatestVersion</c>. Refreshing it is optional: nothing behaves differently.
    /// </summary>
    public const int Latest = 68;

    /// <summary>
    /// The last version that changed what Glue writes to disk (<c>CaseSensitiveLoading</c>). A
    /// project at or above this reads identically to a current one for everything the loader
    /// touches; below it, shapes on disk genuinely differ.
    /// </summary>
    public const int LastFileShapeChange = 55;
}
