using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Sequences;

/// <summary>The message shown when a <c>.seqj</c> was written by a newer version of the format.</summary>
public static class NewerVersionNotice
{
    /// <summary>The message for <paramref name="save"/> opened from <paramref name="fileName"/>, or null when the file is not from a newer version.</summary>
    public static string? MessageFor(string fileName, SequenceListSave save) =>
        save.IsFromNewerVersion
            ? $"{fileName} was written by a newer version of the editor (format version {save.Version}; this editor understands up to {SequenceListSave.CurrentVersion}). " +
              "It opened as far as possible; fields this editor does not know are kept when you save."
            : null;
}
