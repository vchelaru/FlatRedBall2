using System;
using System.IO;

namespace AnimationEditor.Core.IO;

/// <summary>
/// File-IO-backed <see cref="ITiledExtensionInstaller"/>. The two extension files
/// (<c>achj-mapper.mjs</c>/<c>achj-import.mjs</c>) are embedded resources baked in at build time
/// from <c>tools/tiled-achj-import/</c>, so installing never depends on the source repo's layout
/// being present alongside the shipped app.
/// </summary>
public sealed class TiledExtensionInstaller : ITiledExtensionInstaller
{
    private static readonly Lazy<byte[]> MapperBytes = new(() => ReadEmbeddedResource(TiledExtensionPaths.MapperFileName));
    private static readonly Lazy<byte[]> ImportBytes = new(() => ReadEmbeddedResource(TiledExtensionPaths.ImportFileName));

    public string? DetectExtensionsFolder()
    {
        bool isWindows = OperatingSystem.IsWindows();
        string? localAppData = isWindows
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : null;
        string? home = isWindows
            ? null
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string? dataRoot = TiledExtensionPaths.GetTiledDataRoot(isWindows, localAppData, home);
        if (dataRoot is null || !Directory.Exists(dataRoot))
            return null;

        return TiledExtensionPaths.GetExtensionsFolder(isWindows, dataRoot);
    }

    public TiledExtensionInstallStatus GetStatus(string extensionsFolder)
    {
        try
        {
            string targetDir = Path.Combine(extensionsFolder, TiledExtensionPaths.SubfolderName);
            string mapperPath = Path.Combine(targetDir, TiledExtensionPaths.MapperFileName);
            string importPath = Path.Combine(targetDir, TiledExtensionPaths.ImportFileName);

            bool filesPresent = File.Exists(mapperPath) && File.Exists(importPath);
            bool contentMatches = filesPresent
                && File.ReadAllBytes(mapperPath).AsSpan().SequenceEqual(MapperBytes.Value)
                && File.ReadAllBytes(importPath).AsSpan().SequenceEqual(ImportBytes.Value);

            return TiledExtensionStatusEvaluator.Classify(filesPresent, contentMatches);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TiledExtensionInstallStatus.NotInstalled;
        }
    }

    public string? Install(string extensionsFolder)
    {
        try
        {
            string targetDir = Path.Combine(extensionsFolder, TiledExtensionPaths.SubfolderName);
            Directory.CreateDirectory(targetDir);
            File.WriteAllBytes(Path.Combine(targetDir, TiledExtensionPaths.MapperFileName), MapperBytes.Value);
            File.WriteAllBytes(Path.Combine(targetDir, TiledExtensionPaths.ImportFileName), ImportBytes.Value);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not install the Tiled integration to \"{extensionsFolder}\": {ex.Message}";
        }
    }

    public string? ValidateFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return "No folder was selected.";

        try
        {
            Directory.CreateDirectory(folderPath);
            string probePath = Path.Combine(folderPath, $".frb2-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"\"{folderPath}\" isn't writable: {ex.Message}";
        }
    }

    private static byte[] ReadEmbeddedResource(string fileName)
    {
        string logicalName = $"AnimationEditor.Core.TiledExtension.{fileName}";
        using var stream = typeof(TiledExtensionInstaller).Assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' was not found.");
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
