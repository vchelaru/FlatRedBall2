namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure per-OS path construction for Tiled's extensions folder (#1128). Deliberately builds
/// paths by string interpolation with an explicit separator rather than <see cref="System.IO.Path.Combine"/>,
/// which always uses the *host* OS's separator regardless of the <c>isWindows</c> argument -- that
/// would make the Windows-shaped branch untestable on a non-Windows CI runner. In production
/// <c>isWindows</c> always matches the real current OS, so the chosen separator is always the
/// correct one for the machine actually doing the file IO.
/// </summary>
public static class TiledExtensionPaths
{
    /// <summary>Subfolder under the extensions folder the extension's files are installed into.</summary>
    public const string SubfolderName = "tiled-achj-import";

    public const string MapperFileName = "achj-mapper.mjs";

    public const string ImportFileName = "achj-import.mjs";

    /// <summary>
    /// Tiled's own per-user config root (e.g. <c>%LOCALAPPDATA%\Tiled</c> or
    /// <c>~/.local/share/Tiled</c>) -- created by Tiled itself the first time it runs, regardless
    /// of whether any extension is installed. Its presence is the detection signal for "Tiled
    /// exists on this machine"; the <c>extensions</c> subfolder itself may not exist yet, since
    /// Tiled only creates it lazily. Returns <c>null</c> when the relevant special-folder value
    /// wasn't available.
    /// </summary>
    public static string? GetTiledDataRoot(bool isWindows, string? localAppData, string? homeDirectory)
    {
        if (isWindows)
            return string.IsNullOrEmpty(localAppData) ? null : $@"{localAppData}\Tiled";

        return string.IsNullOrEmpty(homeDirectory) ? null : $"{homeDirectory}/.local/share/Tiled";
    }

    public static string GetExtensionsFolder(bool isWindows, string tiledDataRoot) =>
        isWindows ? $@"{tiledDataRoot}\extensions" : $"{tiledDataRoot}/extensions";
}
