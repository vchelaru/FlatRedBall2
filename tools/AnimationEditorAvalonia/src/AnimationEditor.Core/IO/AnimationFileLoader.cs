using FlatRedBall2.AnimationEditorCommon;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>An <c>.achx</c>/<c>.achj</c> in the editor's in-memory form.</summary>
/// <param name="List">The file's chains, with frame coordinates in pixels whatever the file stores.</param>
/// <param name="OnDiskCoordinateType">The coordinate format the file was written in, so a save can write it back the same way.</param>
public sealed record EditorAnimationFile(AnimationChainListSave List, TextureCoordinateType OnDiskCoordinateType);

/// <summary>
/// Reads an <c>.achx</c>/<c>.achj</c> into the form the editor draws and edits: pixel frame coordinates. A UV file is
/// converted using each texture's PNG header. Use it for any file the editor reads besides the open document, such as a
/// sequence's sources; <see cref="ProjectManager.LoadAnimationChain"/> goes through the same steps.
/// </summary>
public static class AnimationFileLoader
{
    /// <summary>Reads <paramref name="path"/> and converts it to pixels.</summary>
    /// <remarks>A UV frame whose texture size can't be read gets an unset region (<see cref="Utilities.FrameRegion.Unset"/>).</remarks>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file contains merge conflict markers.</exception>
    public static EditorAnimationFile Load(string path)
    {
        var filePath = new FilePath(path);
        var list = Read(filePath);
        var onDisk = list.CoordinateType;
        TextureCoordinateConverter.ToPixels(list, SizesBeside(filePath));
        return new EditorAnimationFile(list, onDisk);
    }

    /// <summary>Parses the file as stored, without converting coordinates.</summary>
    internal static AnimationChainListSave Read(FilePath path)
    {
        if (!path.Exists())
            throw new FileNotFoundException($"Animation chain file not found: {path.FullPath}", path.FullPath);

        var rawContent = File.ReadAllText(path.FullPath);
        if (AchxConflictMarkerDetector.HasConflictMarkers(rawContent))
            throw new InvalidDataException($"{AchxConflictMarkerDetector.ConflictMarkerMessage} ({path.FullPath})");

        return AnimationChainListSave.FromString(rawContent);
    }

    /// <summary>
    /// Converts <paramref name="list"/>, read from <paramref name="path"/> to be edited, to pixels in place and returns the
    /// coordinate format it was stored in.
    /// </summary>
    /// <exception cref="InvalidOperationException">A UV region's texture size can't be read, so saving would lose it.</exception>
    internal static TextureCoordinateType ConvertToEditorCoordinates(AnimationChainListSave list, FilePath path)
    {
        var onDisk = list.CoordinateType;
        if (onDisk == TextureCoordinateType.UV)
        {
            var sizes = SizesBeside(path);
            TextureCoordinateConverter.ThrowIfUnsized(list, sizes, "convert UV coordinates to pixels");
            TextureCoordinateConverter.ToPixels(list, sizes);
        }
        return onDisk;
    }

    private static Func<string, (int Width, int Height)?> SizesBeside(FilePath path) =>
        TextureCoordinateConverter.SizesIn(path.GetDirectoryContainingThis().FullPath);
}
