using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Default file access for this package. A relative path is title-relative and read through
/// <see cref="TitleContainer"/>, so it works whatever the working directory is (a Finder-launched
/// macOS <c>.app</c> has cwd <c>/</c>). A rooted path goes to the file system, because
/// <see cref="TitleContainer"/> rejects rooted paths and hot-reload watchers report absolute ones.
/// </summary>
internal static class ContentFile
{
    /// <summary>Opens <paramref name="path"/>, or returns <c>null</c> if the file does not exist.</summary>
    internal static Stream? TryOpen(string path) => TryOpen(path, TitleContainer.OpenStream, File.OpenRead);

    internal static Stream? TryOpen(string path, Func<string, Stream> openTitle, Func<string, Stream> openFile)
    {
        try
        {
            return Path.IsPathRooted(path) ? openFile(path) : openTitle(path);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    /// <summary>Opens <paramref name="path"/>, throwing if it does not exist.</summary>
    internal static Stream Open(string path)
        => Path.IsPathRooted(path) ? File.OpenRead(path) : TitleContainer.OpenStream(path);

    /// <summary>
    /// Parses the .achx/.achj at <paramref name="achxPath"/> (dialect by extension) and sets
    /// <see cref="AnimationChainListSave.FileName"/> so file-relative texture paths resolve next to
    /// it. A relative path stays relative: <c>Path.GetFullPath</c> would root it at the working
    /// directory, which is not where content lives and which <see cref="TitleContainer"/> rejects.
    /// </summary>
    internal static AnimationChainListSave ReadSave(string achxPath, Func<string, Stream> streamProvider)
    {
        var save = achxPath.EndsWith(".achj", StringComparison.OrdinalIgnoreCase)
            ? AnimationChainListSave.FromJsonFile(achxPath, streamProvider)
            : AnimationChainListSave.FromFile(achxPath, streamProvider);
        // Rooted paths are normalized so "a/../b" spellings share one texture-cache entry.
        save.FileName = Path.IsPathRooted(achxPath) ? Path.GetFullPath(achxPath) : achxPath;
        return save;
    }
}
