using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Browser;

/// <summary>
/// #535 M3 / #768: loads a project from a set of already-picked <see cref="IEditorFile"/>s (from
/// a folder picker or a multi-file drag-drop) instead of a filesystem path. The browser has no
/// local filesystem, so texture resolution can't rely on "same folder as the .achx" the way
/// desktop's <see cref="AnimationEditor.Core.ProjectManager"/> does -- the caller must supply
/// every file (the .achx and its textures) up front, discovered by a recursive folder walk
/// (<see cref="NativeReadWriteFolder.GetItemsAsync"/>), and each frame's
/// <see cref="AnimationFrameSave.TextureName"/> is matched to one of them via
/// <see cref="RootRelativePath"/> -- not by bare filename, which breaks as soon as a texture
/// lives in a subfolder or two frames in different folders share a leaf name.
/// </summary>
internal static class BrowserProjectLoader
{
    /// <summary>
    /// Finds the one .achx among <paramref name="files"/>, parses it, resolves each referenced
    /// texture's root-relative path via <see cref="RootRelativePath.Combine"/> (relative to the
    /// achx's own folder among <paramref name="files"/>) and seeds it into
    /// <paramref name="thumbnailService"/>, loads the project into <paramref name="projectManager"/>,
    /// and selects the first chain (which auto-plays). Returns <c>null</c> (no-op) if
    /// <paramref name="files"/> contains no .achx; otherwise returns the matched .achx file's own
    /// handle so the caller can remember it as the tab's writable save location (a real
    /// folder-picker or drag-dropped file handle supports <c>OpenWriteAsync</c> directly, letting
    /// a later plain "Save" write back to it without prompting again the way "Save As" always does).
    /// A texture whose resolved path isn't among <paramref name="files"/> (nonexistent, or living
    /// outside the granted folder tree) is silently skipped, same as a missing texture on desktop.
    /// </summary>
    public static async Task<IEditorFile?> TryLoadAsync(
        IReadOnlyList<IEditorFile> files,
        ProjectManager projectManager,
        ThumbnailService thumbnailService,
        ISelectedState selectedState)
    {
        var achxFile = files.FirstOrDefault(
            f => f.Name.EndsWith(".achx", StringComparison.OrdinalIgnoreCase));
        if (achxFile is null) return null;

        string achxText;
        await using (var achxStream = await achxFile.OpenReadAsync())
        using (var reader = new StreamReader(achxStream))
            achxText = await reader.ReadToEndAsync();

        var acls = AnimationChainListSave.FromString(achxText);
        var achxDirectory = RootRelativePath.DirectoryOf(achxFile.Name);

        var pngsByPath = new Dictionary<string, IEditorFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (ReferenceEquals(file, achxFile)) continue;
            if (file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                pngsByPath[file.Name] = file;
        }

        // Pixel-coordinate .achx files need each texture's pixel size to convert to the UV
        // coordinates ProjectManager works with internally (see ProjectManager.ConvertCoordinates).
        // The browser has no filesystem for that conversion to fall back to, so capture the size
        // of every referenced texture we decode here anyway and hand it to LoadAnimationChain below.
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);

        foreach (var textureName in TextureListBuilder.GetAvailableTextures(acls))
        {
            var resolvedPath = RootRelativePath.Combine(achxDirectory, textureName);
            if (resolvedPath is null || !pngsByPath.TryGetValue(resolvedPath, out var file)) continue;

            await using var pngStream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await pngStream.CopyToAsync(buffer);

            var bitmap = SKBitmap.Decode(buffer.ToArray());
            if (bitmap != null)
            {
                // Keyed by the frame's own TextureName (not resolvedPath) -- ThumbnailService.
                // ResolveTexturePath and ProjectManager.ConvertCoordinates both look up a seeded
                // texture by the literal TextureName string, not by where it was found on disk.
                thumbnailService.SeedTexture(textureName, bitmap);
                knownTextureSizes[textureName] = (bitmap.Width, bitmap.Height);
            }
        }

        // achxFile.Name has no real filesystem meaning here -- it's only the logical identity
        // ProjectManager.FileName exposes, and preParsed means it's never read from disk.
        selectedState.Reset();
        projectManager.LoadAnimationChain(new FilePath(achxFile.Name), acls, knownTextureSizes);
        if (acls.AnimationChains.Count > 0)
            selectedState.SelectedChain = acls.AnimationChains[0];

        return achxFile;
    }

    /// <summary>
    /// #763 fallback: loads from a single already-identified .achx file plus targeted
    /// per-texture-name lookups via <paramref name="resolveTextureFile"/>, instead of a
    /// pre-enumerated file list. Used when directory enumeration
    /// (<c>dirHandle.entries()</c>) throws but named lookups (<c>getFileHandle</c>) on the same
    /// handle still work. A texture name <paramref name="resolveTextureFile"/> can't resolve --
    /// e.g. one living outside the granted folder, the separate cross-folder-texture problem --
    /// is silently skipped rather than failing the whole load.
    /// </summary>
    public static async Task<IEditorFile> TryLoadFromNamedAchxAsync(
        IEditorFile achxFile,
        Func<string, Task<IEditorFile?>> resolveTextureFile,
        ProjectManager projectManager,
        ThumbnailService thumbnailService,
        ISelectedState selectedState)
    {
        string achxText;
        await using (var achxStream = await achxFile.OpenReadAsync())
        using (var reader = new StreamReader(achxStream))
            achxText = await reader.ReadToEndAsync();

        var acls = AnimationChainListSave.FromString(achxText);
        var textureNames = TextureListBuilder.GetAvailableTextures(acls);

        var knownTextureSizes = await NamedTextureResolver.ResolveSizesAsync(textureNames, async name =>
        {
            var file = await resolveTextureFile(name);
            if (file is null) return null;

            await using var pngStream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await pngStream.CopyToAsync(buffer);

            var bitmap = SKBitmap.Decode(buffer.ToArray());
            if (bitmap is null) return null;

            thumbnailService.SeedTexture(name, bitmap);
            return (bitmap.Width, bitmap.Height);
        });

        selectedState.Reset();
        projectManager.LoadAnimationChain(new FilePath(achxFile.Name), acls, knownTextureSizes);
        if (acls.AnimationChains.Count > 0)
            selectedState.SelectedChain = acls.AnimationChains[0];

        return achxFile;
    }
}
