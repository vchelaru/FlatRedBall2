using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Reads an .achx or .achj as stored on disk (no coordinate conversion), for the read-only commands.</summary>
public static class AnimationFileReader
{
    /// <summary>The parsed file, or null with the reason in <see cref="Error"/>.</summary>
    public sealed record ReadResult(AnimationChainListSave? Document, string? Error);

    public static ReadResult Read(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".achx" && ext != ".achj")
            return new(null, $"Only .achx and .achj files are supported: {path}");
        if (!File.Exists(path))
            return new(null, $"File not found: {path}");
        try
        {
            var doc = ext == ".achj" ? AnimationChainListSave.FromJsonFile(path) : AnimationChainListSave.FromFile(path);
            return new(doc, null);
        }
        catch (Exception ex)
        {
            return new(null, $"Could not read {name}: {ex.Message}");
        }
    }
}
