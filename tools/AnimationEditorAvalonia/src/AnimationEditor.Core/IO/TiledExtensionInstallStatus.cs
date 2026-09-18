namespace AnimationEditor.Core.IO;

/// <summary>
/// State of the <c>tiled-achj-import</c> Tiled scripting extension relative to a known (or
/// candidate) Tiled extensions folder. Used by the startup install banner and the
/// "Install Tiled Integration…" menu command (#1128).
/// </summary>
public enum TiledExtensionInstallStatus
{
    /// <summary>No Tiled extensions folder is known -- neither detected on this machine nor
    /// previously chosen by the user.</summary>
    NotDetected,

    /// <summary>A Tiled extensions folder is known, but the extension files are not there.</summary>
    NotInstalled,

    /// <summary>The extension files are present but differ from the version bundled with this
    /// build of the Animation Editor.</summary>
    Outdated,

    /// <summary>The extension files are present and match the version bundled with this build.</summary>
    UpToDate,
}
