using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AnimationEditor.Core.Sequences;

/// <summary>What a valid sequence envelope on the clipboard holds.</summary>
/// <param name="Sequence">The copied sequence. Each chain reference's source is an absolute path with <c>/</c> separators.</param>
/// <param name="Sources">The distinct absolute source paths the sequence uses, in first-use order.</param>
public sealed record SequenceClipboardContent(SequenceSave Sequence, IReadOnlyList<string> Sources);

/// <summary>What <see cref="SequenceDocument.PasteSequence"/> did: the pasted sequence's name, and a message for each source refused.</summary>
public sealed record SequencePasteResult(string Name, IReadOnlyList<string> Errors);

/// <summary>
/// The clipboard text for a copied sequence: a JSON envelope with a marker and version around the sequence in the
/// <c>.seqj</c> format's own shape, with its sources as absolute paths so it can be pasted into another <c>.seqj</c>.
/// </summary>
/// <remarks>
/// <code>{ "kind": "frb2-animation-editor-sequence", "version": 1, "sources": ["C:/a/hero.achx"], "sequence": { ... } }</code>
/// Inside <c>sequence</c> every chain reference's <c>source</c> is the same absolute path, so the paste needs nothing
/// from the file it was copied from. <c>sources</c> lists them once each for anyone reading the text; the reader works
/// from the references.
/// </remarks>
public static class SequenceClipboard
{
    public const string Kind = "frb2-animation-editor-sequence";
    public const int CurrentVersion = 1;

    /// <summary>A deep copy of <paramref name="sequence"/>, made through the format's own reader and writer so unknown properties survive.</summary>
    public static SequenceSave Clone(SequenceSave sequence) => FromWrapper(ToWrapper(sequence));

    /// <summary>
    /// The envelope for <paramref name="sequence"/>, whose relative sources are resolved against
    /// <paramref name="sourceFolder"/> (the folder of the <c>.seqj</c> that holds it).
    /// </summary>
    public static string Write(SequenceSave sequence, string sourceFolder)
    {
        var copy = Clone(sequence);
        var sources = new List<string>();
        foreach (var reference in copy.Tracks.SelectMany(track => track.Chains))
        {
            if (reference.Source.Length == 0) continue;
            reference.Source = AbsolutePath(sourceFolder, reference.Source);
            if (!sources.Contains(reference.Source, System.StringComparer.OrdinalIgnoreCase)) sources.Add(reference.Source);
        }

        var sourceArray = new JsonArray();
        foreach (string source in sources) sourceArray.Add((JsonNode?)JsonValue.Create(source));
        var envelope = new JsonObject
        {
            ["kind"] = Kind,
            ["version"] = CurrentVersion,
            ["sources"] = sourceArray,
            ["sequence"] = JsonNode.Parse(ToWrapper(copy))!["sequences"]![0]!.DeepClone(),
        };
        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads clipboard text. False with a null <paramref name="error"/> when the text is not a sequence envelope at all
    /// (ordinary text, other JSON), so the caller does nothing; false with a message when it carries the marker but is
    /// broken.
    /// </summary>
    public static bool TryRead(string? text, out SequenceClipboardContent? content, out string? error)
    {
        content = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{')) return false;

        JsonObject envelope;
        try
        {
            if (JsonNode.Parse(text) is not JsonObject parsed) return false;
            envelope = parsed;
        }
        catch (System.Exception ex) when (ex is JsonException or System.ArgumentException)
        {
            return false;
        }
        if (!envelope.TryGetPropertyValue("kind", out var kind) || kind is not JsonValue kindValue
            || !kindValue.TryGetValue<string>(out var kindText) || kindText != Kind)
            return false;

        if (!envelope.TryGetPropertyValue("version", out var version) || version is not JsonValue versionValue
            || !versionValue.TryGetValue<int>(out int versionNumber) || versionNumber < 1)
            return Fail("it has no valid version.", out error);
        if (envelope.TryGetPropertyValue("sources", out var sources)
            && (sources is not JsonArray list || list.Any(source => source is not JsonValue value || !value.TryGetValue<string>(out _))))
            return Fail("its sources are not a list of paths.", out error);
        if (!envelope.TryGetPropertyValue("sequence", out var sequenceNode) || sequenceNode is not JsonObject)
            return Fail("it has no sequence.", out error);

        SequenceSave sequence;
        try
        {
            sequence = FromWrapper(new JsonObject
            {
                ["version"] = 1,
                ["sources"] = new JsonArray(),
                ["sequences"] = new JsonArray((JsonNode)sequenceNode.DeepClone()),
            }.ToJsonString());
        }
        catch (JsonException ex)
        {
            return Fail(ex.Message, out error);
        }

        var used = new List<string>();
        foreach (var reference in sequence.Tracks.SelectMany(track => track.Chains))
            if (reference.Source.Length > 0 && !used.Contains(reference.Source, System.StringComparer.OrdinalIgnoreCase))
                used.Add(reference.Source);
        content = new SequenceClipboardContent(sequence, used);
        return true;
    }

    /// <summary>
    /// <paramref name="source"/> as an absolute path with <c>/</c> separators: itself when already absolute, else resolved
    /// against <paramref name="folder"/>.
    /// </summary>
    internal static string AbsolutePath(string folder, string source) =>
        new FilePath(IsAbsolute(source) ? source : System.IO.Path.Combine(folder, source)).FullPath;

    private static bool IsAbsolute(string path) =>
        path.StartsWith('/') || path.StartsWith(@"\\")
        || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\');

    private static bool Fail(string reason, out string error)
    {
        error = $"Can't paste: the copied sequence is damaged, {reason}";
        return false;
    }

    private static string ToWrapper(SequenceSave sequence)
    {
        var wrapper = new SequenceListSave();
        wrapper.Sequences.Add(sequence);
        return wrapper.ToJsonString();
    }

    private static SequenceSave FromWrapper(string json) => SequenceListSave.FromJsonString(json).Sequences.Single();
}
