using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Sequences;

/// <summary>The message shown when a <c>.seqj</c> was written by a newer version of the format.</summary>
public static class NewerVersionNotice
{
    /// <summary>The message for <paramref name="save"/>, or null when the file is not from a newer version.</summary>
    public static string? MessageFor(SequenceListSave save) =>
        save.IsFromNewerVersion ? $"Newer file format (v{save.Version}). Unknown data is kept when you save." : null;
}
