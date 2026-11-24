using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Reads a .seqj as stored on disk, for the read-only commands. Unlike the editor it does not renumber duplicate names.</summary>
public static class SequenceFileReader
{
    /// <summary>The parsed file, or null with the reason in <see cref="Error"/>.</summary>
    public sealed record ReadResult(SequenceListSave? Document, string? Error);

    public static ReadResult Read(string path)
    {
        var name = Path.GetFileName(path);
        if (!IsSequenceFile(path))
            return new(null, $"Only .seqj files are supported here: {path}");
        if (!File.Exists(path))
            return new(null, $"File not found: {path}");
        try
        {
            return new(SequenceListSave.FromFile(path), null);
        }
        catch (Exception ex)
        {
            return new(null, $"Could not read {name}: {ex.Message}");
        }
    }

    /// <summary>True for a path with the sequence file extension.</summary>
    public static bool IsSequenceFile(string path) => path.EndsWith(".seqj", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The source animation files of a sequence file, read from disk once each (stored form; only chain names and frame
/// lengths are needed). A source that is missing or unreadable is null.
/// </summary>
internal sealed class SequenceSources(SequenceListSave save, string sequencePath)
{
    private readonly string _folder = Path.GetDirectoryName(Path.GetFullPath(sequencePath)) ?? "";
    private readonly Dictionary<string, AnimationChainListSave?> _loaded = new(StringComparer.OrdinalIgnoreCase);

    public bool IsListed(string source) => save.Sources.Any(s => string.Equals(s, source, StringComparison.OrdinalIgnoreCase));

    public string FullPath(string source) => Path.Combine(_folder, source);

    public bool Exists(string source) => File.Exists(FullPath(source));

    public AnimationChainListSave? Chains(string source)
    {
        if (!_loaded.TryGetValue(source, out var list))
            _loaded[source] = list = AnimationFileReader.Read(FullPath(source)).Document;
        return list;
    }

    public bool HasChain(SequenceChainReferenceSave reference) =>
        Chains(reference.Source)?.AnimationChains.Any(c => c.Name == reference.Chain) == true;

    /// <summary>Seconds of one playback cycle of the block's chain; 0 when its source or chain is missing.</summary>
    public double CycleLength(SequenceChainReferenceSave reference)
    {
        var list = Chains(reference.Source);
        var chain = list?.AnimationChains.FirstOrDefault(c => c.Name == reference.Chain);
        return chain is null ? 0 : ChainClock.TotalLength(chain, FrameLengthUnit.Of(list));
    }
}
