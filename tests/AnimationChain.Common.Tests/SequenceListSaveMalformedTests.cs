using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// A <c>.seqj</c> is untrusted input: a field of the wrong JSON kind must surface as a <see cref="JsonException"/> that
/// names the property, never as the <see cref="InvalidOperationException"/> a raw <c>GetValue</c> throws.
/// </summary>
public class SequenceListSaveMalformedTests
{
    private enum Kind { String, Number, Bool, Array }

    // Every typed field at every level: where it lives, its name, the path the message must carry, and what it holds.
    private static readonly (string Level, string Property, string Path, Kind Kind)[] Fields =
    {
        ("root", "sources", "sources", Kind.Array),
        ("root", "sequences", "sequences", Kind.Array),
        ("sequence", "name", "sequences[0].name", Kind.String),
        ("sequence", "loop", "sequences[0].loop", Kind.Bool),
        ("sequence", "tracks", "sequences[0].tracks", Kind.Array),
        ("track", "name", "sequences[0].tracks[0].name", Kind.String),
        ("track", "flipHorizontal", "sequences[0].tracks[0].flipHorizontal", Kind.Bool),
        ("track", "chains", "sequences[0].tracks[0].chains", Kind.Array),
        ("chain", "source", "sequences[0].tracks[0].chains[0].source", Kind.String),
        ("chain", "chain", "sequences[0].tracks[0].chains[0].chain", Kind.String),
        ("chain", "start", "sequences[0].tracks[0].chains[0].start", Kind.Number),
        ("chain", "loops", "sequences[0].tracks[0].chains[0].loops", Kind.Number),
    };

    // One value of each JSON kind; the ones a field of that kind accepts are left out for it.
    private static readonly Dictionary<string, string> RawValues = new()
    {
        ["string"] = "\"text\"", ["number"] = "5", ["fraction"] = "1.5", ["bool"] = "true", ["array"] = "[]",
        ["object"] = "{}", ["null"] = "null", ["numberText"] = "\"5\"",
    };

    private static readonly Dictionary<Kind, string[]> Accepts = new()
    {
        [Kind.String] = new[] { "string", "numberText" },
        [Kind.Number] = new[] { "number", "fraction" },
        [Kind.Bool] = new[] { "bool" },
        [Kind.Array] = new[] { "array" },
    };

    public static IEnumerable<object[]> WrongKindCases()
    {
        foreach (var (level, property, path, kind) in Fields)
        foreach (var (name, raw) in RawValues)
        {
            if (Accepts[kind].Contains(name)) continue;
            yield return new object[] { level, property, raw, path };
        }
    }

    [Theory]
    [MemberData(nameof(WrongKindCases))]
    public void FromJsonString_FieldOfTheWrongKind_ThrowsJsonExceptionNamingThePath(string level, string property, string raw, string path)
    {
        var json = With(level, property, raw);

        var error = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(json));

        error.Message.ShouldStartWith(path + " must be");
    }

    [Theory]
    [InlineData("1e50")]
    [InlineData("-1e50")]
    public void FromJsonString_NumberTooBigForAFloat_ThrowsInsteadOfLoadingInfinity(string raw)
    {
        var error = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(With("chain", "loops", raw)));

        error.Message.ShouldStartWith("sequences[0].tracks[0].chains[0].loops must be");
    }

    [Theory]
    [InlineData("sequences", "5", "sequences[1]")]
    [InlineData("sequences", "\"text\"", "sequences[1]")]
    [InlineData("sequences", "null", "sequences[1]")]
    [InlineData("sequences", "[]", "sequences[1]")]
    [InlineData("tracks", "5", "sequences[1].tracks[1]")]
    [InlineData("tracks", "null", "sequences[1].tracks[1]")]
    [InlineData("chains", "\"text\"", "sequences[1].tracks[1].chains[1]")]
    [InlineData("chains", "null", "sequences[1].tracks[1].chains[1]")]
    public void FromJsonString_ArrayElementThatIsNotAnObject_ThrowsNamingTheElementIndex(string array, string badElement, string path)
    {
        // Index 1 in every array, so the path proves the index is carried down, not just that a throw happened.
        var json = WithSecondElementReplaced(array, badElement);

        var error = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(json));

        error.Message.ShouldStartWith(path + " must be an object");
    }

    [Theory]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    public void FromJsonString_SourcesEntryThatIsNotAString_ThrowsNamingTheElementIndex(string entry)
    {
        var error = Should.Throw<JsonException>(() =>
            SequenceListSave.FromJsonString($$"""{ "sources": [ "a.achx", {{entry}} ] }"""));

        error.Message.ShouldStartWith("sources[1] must be a string");
    }

    [Fact]
    public void FromJsonString_MalformedFieldInALaterSequence_NamesThatSequence()
    {
        const string json = """
            { "sequences": [ { "name": "A" }, { "name": "B", "tracks": [ { "name": 5 } ] } ] }
            """;

        var error = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(json));

        error.Message.ShouldStartWith("sequences[1].tracks[0].name must be a string");
    }

    [Fact]
    public void FromJsonString_DuplicateProperty_ThrowsJsonException()
    {
        Should.Throw<JsonException>(() => SequenceListSave.FromJsonString("""{ "version": 1, "version": 2 }"""));
    }

    [Fact]
    public void FromFile_WrongKindField_ThrowsJsonException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".seqj");
        File.WriteAllText(path, """{ "sequences": [ { "name": 5 } ] }""");
        try
        {
            Should.Throw<JsonException>(() => SequenceListSave.FromFile(path))
                .Message.ShouldStartWith("sequences[0].name must be a string");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromJsonString_FieldsOfTheRightKind_StillLoad()
    {
        var loaded = SequenceListSave.FromJsonString(With("track", "flipHorizontal", "true"));

        loaded.Sequences[0].Tracks[0].FlipHorizontal.ShouldBeTrue();
    }

    // A complete, valid document with one property of one object replaced by raw JSON.
    private static string With(string level, string property, string raw)
    {
        var chain = Parse("""{ "source": "a.achx", "chain": "Walk", "start": 0, "loops": 1 }""");
        var track = Parse("""{ "name": "T", "flipHorizontal": false }""");
        track["chains"] = new JsonArray((JsonNode)chain);
        var sequence = Parse("""{ "name": "S", "loop": false }""");
        sequence["tracks"] = new JsonArray((JsonNode)track);
        var root = Parse("""{ "version": 1, "sources": [ "a.achx" ] }""");
        root["sequences"] = new JsonArray((JsonNode)sequence);

        var target = level switch
        {
            "root" => root,
            "sequence" => sequence,
            "track" => track,
            "chain" => chain,
            _ => throw new ArgumentException(level),
        };
        target[property] = JsonNode.Parse(raw);
        return root.ToJsonString();
    }

    // Two of everything, so the second element of the named array (and the parents above it) is index 1.
    private static string WithSecondElementReplaced(string array, string badElement)
    {
        string Object(string inner) => "{" + inner + "}";
        string chains = array == "chains"
            ? $$"""[ {{Object("")}}, {{badElement}} ]"""
            : $$"""[ {{Object("")}}, {{Object("")}} ]""";
        string tracks = array == "tracks"
            ? $$"""[ {{Object("")}}, {{badElement}} ]"""
            : $$"""[ {{Object("")}}, {{Object($"\"chains\": {chains}")}} ]""";
        string sequences = array == "sequences"
            ? $$"""[ {{Object("")}}, {{badElement}} ]"""
            : $$"""[ {{Object("")}}, {{Object($"\"tracks\": {tracks}")}} ]""";
        return $$"""{ "sequences": {{sequences}} }""";
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
