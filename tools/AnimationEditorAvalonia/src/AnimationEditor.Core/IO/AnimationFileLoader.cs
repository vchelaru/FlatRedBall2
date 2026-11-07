using FlatRedBall2.AnimationEditorCommon;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>An <c>.achx</c>/<c>.achj</c> in the editor's in-memory form.</summary>
/// <param name="List">The file's chains, with frame coordinates in UV (0-1) whatever the file stores.</param>
/// <param name="OnDiskCoordinateType">The coordinate format the file was written in, so a save can write it back the same way.</param>
public sealed record EditorAnimationFile(AnimationChainListSave List, TextureCoordinateType OnDiskCoordinateType);

/// <summary>
/// Reads an <c>.achx</c>/<c>.achj</c> into the form the editor draws and edits: UV frame coordinates, converted from
/// pixels using each texture's PNG header. Use it for any file the editor reads besides the open document, such as a
/// sequence's sources; <see cref="ProjectManager.LoadAnimationChain"/> goes through the same steps.
/// </summary>
public static class AnimationFileLoader
{
    /// <summary>Reads <paramref name="path"/> and converts it to UV.</summary>
    /// <remarks>
    /// A frame whose texture is missing or not a readable PNG keeps its stored coordinates, since a texture that has not
    /// been built yet is normal while editing.
    /// </remarks>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file contains merge conflict markers.</exception>
    public static EditorAnimationFile Load(string path)
    {
        var filePath = new FilePath(path);
        var list = Read(filePath);
        var onDisk = ConvertToEditorCoordinates(list, filePath);
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
    /// Converts <paramref name="list"/>, read from <paramref name="path"/>, to UV in place and returns the coordinate
    /// format it was stored in.
    /// </summary>
    internal static TextureCoordinateType ConvertToEditorCoordinates(AnimationChainListSave list, FilePath path)
    {
        var onDisk = list.CoordinateType;
        TextureCoordinateConverter.ConvertCoordinates(list, path.GetDirectoryContainingThis().FullPath, TextureCoordinateType.UV);
        return onDisk;
    }
}
