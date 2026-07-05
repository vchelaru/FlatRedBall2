using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using Avalonia.Platform.Storage;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Browser;

/// <summary>
/// #535 M3: loads a project from a set of already-picked <see cref="IStorageFile"/>s (from a
/// folder picker or a multi-file drag-drop) instead of a filesystem path. The browser has no
/// local filesystem, so texture resolution can't rely on "same folder as the .achx" the way
/// desktop's <see cref="AnimationEditor.Core.ProjectManager"/> does -- the caller must supply
/// every file (the .achx and its textures) up front, and this matches them by filename.
/// </summary>
internal static class BrowserProjectLoader
{
    /// <summary>
    /// Finds the one .achx among <paramref name="files"/>, parses it, seeds every other file
    /// whose name matches a frame's <c>TextureName</c> into <paramref name="thumbnailService"/>,
    /// loads it into <paramref name="projectManager"/>, and selects the first chain (which
    /// auto-plays). Returns <c>false</c> (no-op) if <paramref name="files"/> contains no .achx.
    /// </summary>
    public static async Task<bool> TryLoadAsync(
        IReadOnlyList<IStorageFile> files,
        ProjectManager projectManager,
        ThumbnailService thumbnailService,
        ISelectedState selectedState)
    {
        var achxFile = files.FirstOrDefault(
            f => f.Name.EndsWith(".achx", StringComparison.OrdinalIgnoreCase));
        if (achxFile is null) return false;

        string achxText;
        await using (var achxStream = await achxFile.OpenReadAsync())
        using (var reader = new StreamReader(achxStream))
            achxText = await reader.ReadToEndAsync();

        var acls = AnimationChainListSave.FromString(achxText);

        // Pixel-coordinate .achx files need each texture's pixel size to convert to the UV
        // coordinates ProjectManager works with internally (see ProjectManager.ConvertCoordinates).
        // The browser has no filesystem for that conversion to fall back to, so capture the size
        // of every texture we decode here anyway and hand it to LoadAnimationChain below.
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (ReferenceEquals(file, achxFile)) continue;
            if (!file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

            await using var pngStream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await pngStream.CopyToAsync(buffer);

            var bitmap = SKBitmap.Decode(buffer.ToArray());
            if (bitmap != null)
            {
                thumbnailService.SeedTexture(file.Name, bitmap);
                knownTextureSizes[file.Name] = (bitmap.Width, bitmap.Height);
            }
        }

        // achxFile.Name has no real filesystem meaning here -- it's only the logical identity
        // ProjectManager.FileName exposes, and preParsed means it's never read from disk.
        selectedState.Reset();
        projectManager.LoadAnimationChain(new FilePath(achxFile.Name), acls, knownTextureSizes);
        if (acls.AnimationChains.Count > 0)
            selectedState.SelectedChain = acls.AnimationChains[0];

        return true;
    }
}
