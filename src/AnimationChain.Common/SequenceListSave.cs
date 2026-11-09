using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A <c>.seqj</c> file: named sequences, each playing animation chains from one or more <c>.achx</c>/<c>.achj</c>
/// files on several sprites (tracks) at once. JSON only.
/// </summary>
/// <remarks>
/// Parsed and written by hand through <see cref="JsonNode"/>, like <c>.achj</c>, so it stays trim- and AOT-safe.
/// The format never resolves a reference: a chain or source that no longer exists is kept as written, and timing
/// treats it as zero length (see <see cref="SequenceTimeline"/>).
/// <para>
/// The format is shared with other tools, so a load then save must not destroy what another writer put in the file.
/// Properties this code does not know are kept as raw JSON on the object that held them (<see cref="UnknownProperties"/>
/// on the file, <see cref="SequenceSave"/>, <see cref="SequenceTrackSave"/> and <see cref="SequenceChainReferenceSave"/>)
/// and written back unchanged after the known ones, in their original relative order.
/// </para>
/// <para>
/// The top-level <c>"version"</c> is written first. A file without one is version 1. A file with a higher version than
/// <see cref="CurrentVersion"/> still loads, best effort: known fields are read, everything else is kept, and the
/// version is written back as read rather than downgraded. <see cref="IsFromNewerVersion"/> tells the UI to warn.
/// A <c>"version"</c> that is not a whole number of at least 1 is rejected with a <see cref="JsonException"/>.
/// </para>
/// </remarks>
public class SequenceListSave
{
    /// <summary>The format version this code writes for a new file.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The format version of this file: <see cref="CurrentVersion"/> for a new file, 1 for a file with no
    /// <c>"version"</c>, otherwise the number the file carried. Written back as is, so a file from a newer version is
    /// never downgraded.
    /// </summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>True when the file declares a higher <see cref="Version"/> than this code knows, so some of it may be ignored.</summary>
    public bool IsFromNewerVersion => Version > CurrentVersion;

    /// <summary>
    /// Top-level properties this code does not know, kept as raw JSON and written after the known ones. Values are
    /// copied on save, so the stored nodes stay reusable.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();

    /// <summary>
    /// The <c>.achx</c>/<c>.achj</c> files this file's sequences use, relative to the <c>.seqj</c> with <c>/</c>
    /// separators. Shared by every sequence in the file.
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>The sequences in this file, each played by name.</summary>
    public List<SequenceSave> Sequences { get; set; } = new();

    /// <summary>Loads a <c>.seqj</c> from disk.</summary>
    /// <exception cref="JsonException">The file is not valid JSON, or a field has the wrong kind; the message names the property.</exception>
    public static SequenceListSave FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(() => JsonNode.Parse(stream));
    }

    /// <summary>Parses <c>.seqj</c> JSON. A leading byte-order mark is allowed.</summary>
    /// <exception cref="JsonException"><paramref name="json"/> is not valid JSON, or a field has the wrong kind; the message names the property.</exception>
    public static SequenceListSave FromJsonString(string json) =>
        // JsonNode.Parse(string) rejects a BOM, unlike the stream overload.
        Read(() => JsonNode.Parse(json.TrimStart('﻿')));

    /// <summary>Writes this file as indented JSON. Crash-safe: the old file stays intact until the new one is complete.</summary>
    public void Save(string path) => AtomicFile.Write(path, stream =>
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        ToJsonNode().WriteTo(writer);
    });

    /// <summary>The same indented JSON <see cref="Save"/> writes.</summary>
    public string ToJsonString() => ToJsonNode().ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    // A repeated property name surfaces as ArgumentException, while the parsed object is first read rather than at
    // Parse, so this wraps the whole read.
    private static SequenceListSave Read(Func<JsonNode?> parse)
    {
        try
        {
            return FromJsonNode(SeqjObject.Root(parse()));
        }
        catch (ArgumentException ex)
        {
            throw new JsonException($"The file repeats a property name: {ex.Message}", ex);
        }
    }

    // Every JsonArray.Add casts to JsonNode: the generic Add<T> overload is not trim- or AOT-safe.
    private JsonObject ToJsonNode()
    {
        var sources = new JsonArray();
        foreach (var source in Sources)
            sources.Add((JsonNode?)JsonValue.Create(source));

        var sequences = new JsonArray();
        foreach (var sequence in Sequences)
        {
            var tracks = new JsonArray();
            foreach (var track in sequence.Tracks)
                tracks.Add((JsonNode)WriteTrack(track));

            sequences.Add((JsonNode)AppendUnknown(new JsonObject
            {
                ["name"] = sequence.Name,
                ["loop"] = sequence.Loop,
                ["tracks"] = tracks,
            }, sequence.UnknownProperties));
        }

        return AppendUnknown(new JsonObject
        {
            ["version"] = Version,
            ["sources"] = sources,
            ["sequences"] = sequences,
        }, UnknownProperties);
    }

    private static JsonObject WriteTrack(SequenceTrackSave track)
    {
        var chains = new JsonArray();
        foreach (var reference in track.Chains)
        {
            chains.Add((JsonNode)AppendUnknown(new JsonObject
            {
                ["source"] = reference.Source,
                ["chain"] = reference.Chain,
                ["start"] = Clean(reference.Start),
                ["loops"] = Clean(reference.Loops),
            }, reference.UnknownProperties));
        }

        var result = new JsonObject
        {
            ["name"] = track.Name,
        };
        if (!track.Visible) result["visible"] = false;
        result["chains"] = chains;
        return AppendUnknown(result, track.UnknownProperties);
    }

    // 1e-6 s is far below a frame; rounding drops double/float arithmetic noise like 3.8000000000000003.
    private static double Clean(double value) => Math.Round(value, 6);

    // Copies, because a JsonNode has one parent and the stored node must stay usable for the next save. A key the known
    // fields already wrote wins, so a hand-added unknown entry cannot clobber a real value.
    private static JsonObject AppendUnknown(JsonObject target, JsonObject unknown)
    {
        foreach (var (key, value) in unknown)
            if (!target.ContainsKey(key)) target[key] = value?.DeepClone();
        return target;
    }

    // Stores every property the known-field reads do not consume, in file order.
    private static void CaptureUnknown(JsonObject node, JsonObject unknown, params string[] knownKeys)
    {
        foreach (var (key, value) in node)
            if (Array.IndexOf(knownKeys, key) < 0) unknown[key] = value?.DeepClone();
    }

    // Every typed read goes through SeqjObject, which turns a field of the wrong kind into a JsonException naming it.
    private static SequenceListSave FromJsonNode(SeqjObject root)
    {
        var result = new SequenceListSave { Version = root.PositiveInt("version", fallback: 1) };
        CaptureUnknown(root.Node, result.UnknownProperties, "version", "sources", "sequences");
        result.Sources.AddRange(root.Strings("sources"));
        foreach (var sequence in root.Objects("sequences"))
            result.Sequences.Add(ReadSequence(sequence));
        return result;
    }

    private static SequenceSave ReadSequence(SeqjObject node)
    {
        var sequence = new SequenceSave
        {
            Name = node.String("name"),
            Loop = node.Bool("loop", fallback: true),
        };
        CaptureUnknown(node.Node, sequence.UnknownProperties, "name", "loop", "tracks");
        foreach (var track in node.Objects("tracks"))
            sequence.Tracks.Add(ReadTrack(track));
        return sequence;
    }

    private static SequenceTrackSave ReadTrack(SeqjObject node)
    {
        var track = new SequenceTrackSave
        {
            Name = node.String("name"),
            Visible = node.Bool("visible", fallback: true),
        };
        // "x", "y", "z" and "flipHorizontal" are listed so they are dropped unread, not kept as unknown properties: files
        // written before tracks lost their offset, Z and flip still hold them, and saving must not write them back. Sprites
        // sit at their frames' offsets, draw with their frames' flips and stack in track order.
        CaptureUnknown(node.Node, track.UnknownProperties, "name", "x", "y", "z", "flipHorizontal", "visible", "chains");
        foreach (var chain in node.Objects("chains"))
        {
            var reference = new SequenceChainReferenceSave
            {
                Source = chain.String("source"),
                Chain = chain.String("chain"),
                Start = chain.Double("start"),
                Loops = chain.Float("loops", fallback: 1),
            };
            CaptureUnknown(chain.Node, reference.UnknownProperties, "source", "chain", "start", "loops");
            track.Chains.Add(reference);
        }
        return track;
    }
}
