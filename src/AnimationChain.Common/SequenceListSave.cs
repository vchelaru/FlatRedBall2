using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A <c>.seqj</c> file: named sequences that play animation chains from one or more <c>.achx</c>/<c>.achj</c>
/// files on several sprites (tracks) at once. JSON only. Parsed by hand through <see cref="JsonNode"/> so it stays
/// AOT-safe, like <c>.achj</c>.
/// </summary>
public class SequenceListSave
{
    /// <summary>
    /// The <c>.achx</c>/<c>.achj</c> files this file's sequences use, relative to the <c>.seqj</c> with <c>/</c>
    /// separators. A <see cref="SequenceChainReferenceSave.Source"/> names one of these.
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>The sequences in this file, each played by name.</summary>
    public List<SequenceSave> Sequences { get; set; } = new();

    /// <summary>Loads a <c>.seqj</c> from disk.</summary>
    public static SequenceListSave FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromJsonNode(JsonNode.Parse(stream)!.AsObject());
    }

    /// <summary>Parses <c>.seqj</c> JSON from a string.</summary>
    public static SequenceListSave FromJsonString(string json) =>
        FromJsonNode(JsonNode.Parse(json.TrimStart('﻿'))!.AsObject());

    /// <summary>Writes this file atomically as indented JSON.</summary>
    public void Save(string path) => AtomicFile.Write(path, stream =>
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        ToJsonNode().WriteTo(writer);
    });

    /// <summary>The indented JSON <see cref="Save"/> writes.</summary>
    public string ToJsonString() => ToJsonNode().ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private JsonObject ToJsonNode()
    {
        var sources = new JsonArray();
        foreach (var source in Sources) sources.Add((JsonNode?)JsonValue.Create(source));

        var sequences = new JsonArray();
        foreach (var sequence in Sequences)
        {
            var tracks = new JsonArray();
            foreach (var track in sequence.Tracks)
            {
                var chains = new JsonArray();
                foreach (var reference in track.Chains)
                {
                    chains.Add((JsonNode)new JsonObject
                    {
                        ["source"] = reference.Source,
                        ["chain"] = reference.Chain,
                        ["start"] = reference.Start,
                        ["loops"] = reference.Loops,
                    });
                }
                var trackObject = new JsonObject
                {
                    ["name"] = track.Name,
                    ["x"] = track.X,
                    ["y"] = track.Y,
                    ["z"] = track.Z,
                };
                if (track.FlipHorizontal) trackObject["flipHorizontal"] = true;
                trackObject["chains"] = chains;
                tracks.Add((JsonNode)trackObject);
            }
            sequences.Add((JsonNode)new JsonObject
            {
                ["name"] = sequence.Name,
                ["loop"] = sequence.Loop,
                ["tracks"] = tracks,
            });
        }

        return new JsonObject { ["sources"] = sources, ["sequences"] = sequences };
    }

    private static SequenceListSave FromJsonNode(JsonObject root)
    {
        var result = new SequenceListSave();
        if (root["sources"] is JsonArray sources)
            foreach (var source in sources)
                if (source?.GetValue<string>() is { } s) result.Sources.Add(s);

        if (root["sequences"] is not JsonArray sequences) return result;
        foreach (var sequenceNode in sequences.OfType<JsonObject>())
        {
            var sequence = new SequenceSave
            {
                Name = sequenceNode["name"]?.GetValue<string>() ?? "",
                Loop = sequenceNode["loop"]?.GetValue<bool>() ?? false,
            };
            if (sequenceNode["tracks"] is JsonArray tracks)
            {
                foreach (var trackNode in tracks.OfType<JsonObject>())
                {
                    var track = new SequenceTrackSave
                    {
                        Name = trackNode["name"]?.GetValue<string>() ?? "",
                        X = trackNode["x"]?.GetValue<float>() ?? 0,
                        Y = trackNode["y"]?.GetValue<float>() ?? 0,
                        Z = trackNode["z"]?.GetValue<int>() ?? 0,
                        FlipHorizontal = trackNode["flipHorizontal"]?.GetValue<bool>() ?? false,
                    };
                    if (trackNode["chains"] is JsonArray chains)
                    {
                        foreach (var chainNode in chains.OfType<JsonObject>())
                        {
                            track.Chains.Add(new SequenceChainReferenceSave
                            {
                                Source = chainNode["source"]?.GetValue<string>() ?? "",
                                Chain = chainNode["chain"]?.GetValue<string>() ?? "",
                                Start = chainNode["start"]?.GetValue<double>() ?? 0,
                                Loops = chainNode["loops"]?.GetValue<float>() ?? 1,
                            });
                        }
                    }
                    sequence.Tracks.Add(track);
                }
            }
            result.Sequences.Add(sequence);
        }
        return result;
    }
}

/// <summary>A named timeline game code plays by name. It ends at the end of its last chain; there is no stored length.</summary>
public class SequenceSave
{
    /// <summary>The name game code plays the sequence by.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether playback restarts at 0 after the last chain ends.</summary>
    public bool Loop { get; set; }

    /// <summary>The sprites, one per track.</summary>
    public List<SequenceTrackSave> Tracks { get; set; } = new();
}

/// <summary>One sprite's lane. The offset, flip and z-order hold for the whole sequence.</summary>
public class SequenceTrackSave
{
    /// <summary>Display name of the sprite.</summary>
    public string Name { get; set; } = "";

    /// <summary>Offset from the entity origin (0,0), Y+ up like the chains' own offsets.</summary>
    public float X { get; set; }

    /// <summary>See <see cref="X"/>.</summary>
    public float Y { get; set; }

    /// <summary>Mirrors the sprite and its frame offsets across the track's X.</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>Draw order: higher draws on top. Ties draw in track order.</summary>
    public int Z { get; set; }

    /// <summary>Chain references in any order; playback orders them by <see cref="SequenceChainReferenceSave.Start"/>.</summary>
    public List<SequenceChainReferenceSave> Chains { get; set; } = new();
}

/// <summary>One placement of a chain on a track. See <see cref="SequenceTimeline"/> for the cut rule.</summary>
public class SequenceChainReferenceSave
{
    /// <summary>One of <see cref="SequenceListSave.Sources"/>.</summary>
    public string Source { get; set; } = "";

    /// <summary>The chain's name inside <see cref="Source"/>.</summary>
    public string Chain { get; set; } = "";

    /// <summary>Seconds from the sequence start.</summary>
    public double Start { get; set; }

    /// <summary>
    /// How many playback cycles play, overriding the chain's own Loop flag. One cycle is the chain's full length, twice
    /// that for a ping-pong chain. A fraction plays part of the last cycle.
    /// </summary>
    public float Loops { get; set; } = 1;
}
