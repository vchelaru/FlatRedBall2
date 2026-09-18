namespace AnimationEditor.Core.IO;

/// <summary>
/// Detects Tiled's extensions folder and installs/updates the <c>tiled-achj-import</c> scripting
/// extension there (#1128). Unlike <see cref="IFileAssociationService"/>, this is implemented
/// identically on every OS -- installation is a plain file copy, only the well-known candidate
/// path differs by platform.
/// </summary>
public interface ITiledExtensionInstaller
{
    /// <summary>
    /// Attempts to locate Tiled's extensions folder using well-known per-OS paths. Returns
    /// <c>null</c> when Tiled's own config folder isn't present -- callers should not prompt to
    /// install in that case, only offer the manual "choose a folder" path.
    /// </summary>
    string? DetectExtensionsFolder();

    /// <summary>Install state of the extension against <paramref name="extensionsFolder"/>.</summary>
    TiledExtensionInstallStatus GetStatus(string extensionsFolder);

    /// <summary>
    /// Installs (or overwrites) the extension's two files into a <c>tiled-achj-import</c>
    /// subfolder of <paramref name="extensionsFolder"/>, creating folders as needed. Returns
    /// <c>null</c> on success, or a user-facing error message if the write failed (e.g. the
    /// folder isn't writable).
    /// </summary>
    string? Install(string extensionsFolder);

    /// <summary>
    /// Validates that <paramref name="folderPath"/> (typically hand-picked by the user when
    /// Tiled wasn't auto-detected) can be created and written to. Returns <c>null</c> when it's
    /// usable, or a user-facing error message otherwise.
    /// </summary>
    string? ValidateFolder(string folderPath);
}
