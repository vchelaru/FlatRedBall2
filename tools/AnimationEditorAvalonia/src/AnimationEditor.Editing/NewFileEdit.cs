using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Creates new empty animation and sequence files (#1811). The format follows the path's
/// extension (<c>.achx</c> XML or <c>.achj</c> JSON for animation files). Never opens the file or
/// touches selection; <see cref="EditResult.ChangedItems"/> holds the created path. IO failures
/// come back as errors, never exceptions.
/// </summary>
public static class NewFileEdit
{
    /// <summary>
    /// Writes an empty chain list at <paramref name="path"/>. Fails with
    /// <see cref="EditErrorCode.AlreadyExists"/> rather than overwrite: the write uses
    /// <see cref="FileMode.CreateNew"/>, so a file that appeared after the caller last looked is safe.
    /// </summary>
    public static EditResult CreateAnimationFile(string path)
    {
        var name = new FilePath(path).NoPath;
        if (new FilePath(path).Extension == "tsx")
            return Failed(name, "aecli does not create Tiled tilesets; author the .tsx in Tiled, then describe and edit it here.", EditErrorCode.UnsupportedFile);
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            NewAnimationFileWriter.WriteEmpty(stream, name);
            return Created(path);
        }
        catch (IOException) when (File.Exists(path))
        {
            return AlreadyExists(name);
        }
        catch (Exception ex)
        {
            return Failed(name, ex.Message, EditErrorCode.IoFailed);
        }
    }

    /// <summary>
    /// Writes a sequence file holding one empty sequence at <paramref name="path"/>. An existing file
    /// fails with <see cref="EditErrorCode.AlreadyExists"/> unless <paramref name="overwrite"/> is
    /// set, for a caller that already confirmed the overwrite with the user.
    /// </summary>
    public static EditResult CreateSequenceFile(string path, bool overwrite = false)
    {
        var name = new FilePath(path).NoPath;
        if (!AchxFolderScanner.IsSequencePath(path))
            return Failed(name, "it is not a .seqj file. Give it a .seqj extension.", EditErrorCode.UnsupportedFile);
        if (!overwrite && File.Exists(path)) return AlreadyExists(name);

        var save = new SequenceListSave();
        var sequence = new SequenceSave { Name = "Sequence" };
        save.Sequences.Add(sequence);
        try
        {
            save.Save(path);
            return Created(path) with { Sequence = sequence.Name };
        }
        catch (Exception ex)
        {
            return Failed(name, ex.Message, EditErrorCode.IoFailed);
        }
    }

    private static EditResult Created(string path) =>
        new(new[] { path }, Array.Empty<string>(), Array.Empty<string>());

    private static EditResult AlreadyExists(string name) => Failed(name, "it already exists.", EditErrorCode.AlreadyExists);

    private static EditResult Failed(string name, string reason, EditErrorCode code = EditErrorCode.Unspecified) =>
        new(Array.Empty<string>(), new[] { $"Could not create {name}: {reason}" }, Array.Empty<string>(), new[] { code });
}
