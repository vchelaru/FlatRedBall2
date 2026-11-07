using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The clipboard envelopes copied timeline chains and tracks travel in.</summary>
public class TimelineClipboardTests
{
    private static SequenceChainReferenceSave Chain(string source, string name, double start, float loops = 1) =>
        new() { Source = source, Chain = name, Start = start, Loops = loops };

    private static CopiedChain Copied(SequenceChainReferenceSave chain, string track = "Body", int lane = 0) =>
        new(chain, new SequenceTrackSave { Name = track, X = 3, Y = -2, Z = 4, FlipHorizontal = true, Visible = false }, lane);

    private static SequenceTrackSave Track(string name, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Name = name, X = 3, Y = -2, Z = 4, FlipHorizontal = true, Visible = false };
        track.UnknownProperties["tag"] = "boss";
        track.Chains.AddRange(chains);
        return track;
    }

    [Fact]
    public void WriteChains_ProducesAMarkedVersionedEnvelope_WithAbsoluteSources_AndStartsRelativeToTheEarliest()
    {
        var chains = new[] { Chain("hero.achx", "Walk", 2.5, 3), Chain("../shared/fx.achj", "Boom", 1.5) };

        var json = JsonNode.Parse(TimelineClipboard.WriteChains(chains.Select(c => Copied(c)).ToList(), @"C:\game\anims"))!.AsObject();

        json["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-chains");
        json["version"]!.GetValue<int>().ShouldBe(1);
        json["track"]!.GetValue<string>().ShouldBe("Body");
        json["sources"]!.AsArray().Select(node => node!.GetValue<string>())
            .ShouldBe(new[] { "C:/game/anims/hero.achx", "C:/game/shared/fx.achj" });
        var items = json["chains"]!.AsArray();
        items[0]!["source"]!.GetValue<string>().ShouldBe("C:/game/anims/hero.achx");
        items[0]!["start"]!.GetValue<double>().ShouldBe(1.0);
        items[0]!["loops"]!.GetValue<float>().ShouldBe(3f);
        items[1]!["start"]!.GetValue<double>().ShouldBe(0.0);
        chains[0].Start.ShouldBe(2.5, "the copied chains are not changed");
        chains[0].Source.ShouldBe("hero.achx");
    }

    [Fact]
    public void TryRead_OfCopiedChains_GivesThemBackWithTheirUnknownFields_InTheOrderCopied()
    {
        var first = Chain("hero.achx", "Walk", 4);
        first.UnknownProperties["tint"] = new JsonObject { ["r"] = 1 };
        var text = TimelineClipboard.WriteChains(new[] { Copied(first), Copied(Chain("hero.achx", "Run", 5)) }, @"C:\game");

        TimelineClipboard.TryRead(text, out var content, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        content!.Kind.ShouldBe(TimelineClipboardKind.Chains);
        content.TrackName.ShouldBe("Body");
        content.Sources.ShouldBe(new[] { "C:/game/hero.achx" });
        content.Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 0.0), ("Run", 1.0) });
        content.Chains[0].UnknownProperties["tint"]!["r"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void WriteChains_AndWriteTracks_AreDifferentKinds()
    {
        var chains = TimelineClipboard.WriteChains(new[] { Copied(Chain("a.achx", "Walk", 0)) }, "C:/x");
        var tracks = TimelineClipboard.WriteTracks(new[] { Track("Body") }, "C:/x");

        JsonNode.Parse(tracks)!["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-tracks");
        TimelineClipboard.TryRead(tracks, out var read, out _).ShouldBeTrue();
        read!.Kind.ShouldBe(TimelineClipboardKind.Tracks);
        TimelineClipboard.TryRead(chains, out read, out _).ShouldBeTrue();
        read!.Kind.ShouldBe(TimelineClipboardKind.Chains);
    }

    [Fact]
    public void TryRead_OfCopiedTracks_KeepsEveryFieldTheirChainsAndOffsets()
    {
        var tracks = new[]
        {
            Track("Body", Chain("hero.achx", "Walk", 0.5, 2), Chain("hero.achx", "Run", 1.5)),
            Track("Head", Chain("fx.achx", "Boom", 0)),
        };

        TimelineClipboard.TryRead(TimelineClipboard.WriteTracks(tracks, @"C:\game"), out var content, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        content!.Sources.ShouldBe(new[] { "C:/game/hero.achx", "C:/game/fx.achx" });
        var body = content.Tracks[0];
        (body.Name, body.X, body.Y, body.Z, body.FlipHorizontal, body.Visible).ShouldBe(("Body", 3f, -2f, 4, true, false));
        body.UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        body.Chains.Select(c => (c.Source, c.Chain, c.Start, c.Loops)).ShouldBe(new[]
        {
            ("C:/game/hero.achx", "Walk", 0.5, 2f), ("C:/game/hero.achx", "Run", 1.5, 1f),
        });
        content.Tracks[1].Name.ShouldBe("Head");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"kind\": \"something-else\" }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1, \"sequence\": { \"name\": \"A\" } }")]
    public void TryRead_OfTextThatIsNotAChainsOrTracksEnvelope_IsIgnoredWithNoError(string? text)
    {
        TimelineClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1 }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-chains\", \"chains\": [] }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1, \"chains\": [ { \"chain\": 7 } ] }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-tracks\", \"version\": 1, \"tracks\": 5 }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-tracks\", \"version\": 1, \"tracks\": [ { \"name\": 3 } ] }")]
    public void TryRead_OfABrokenEnvelope_ReportsAnError(string text)
    {
        TimelineClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SequenceClipboardTryRead_IgnoresAChainsEnvelope_SoTheListNeverPastesOne()
    {
        var text = TimelineClipboard.WriteChains(new[] { Copied(Chain("a.achx", "Walk", 0)) }, "C:/x");

        SequenceClipboard.TryRead(text, out _, out var error).ShouldBeFalse();

        error.ShouldBeNull();
    }

    [Fact]
    public void WriteChains_CarriesEachChainsTrackAndLane_WithTheSettingsAMissingTrackIsMadeWith()
    {
        var items = new[]
        {
            Copied(Chain("a.achx", "Walk", 1), "Ship", lane: 0),
            Copied(Chain("a.achx", "Glow", 1.5), "Booster", lane: 2),
            Copied(Chain("a.achx", "Run", 2), "Ship", lane: 0),
        };

        var json = JsonNode.Parse(TimelineClipboard.WriteChains(items, "C:/x"))!.AsObject();

        var tracks = json["sourceTracks"]!.AsArray();
        tracks.Select(t => (t!["name"]!.GetValue<string>(), t["lane"]!.GetValue<int>())).ShouldBe(new[] { ("Ship", 0), ("Booster", 2) });
        var ship = tracks[0]!;
        (ship["x"]!.GetValue<float>(), ship["y"]!.GetValue<float>(), ship["z"]!.GetValue<int>()).ShouldBe((3f, -2f, 4));
        ship["flipHorizontal"]!.GetValue<bool>().ShouldBeTrue();
        ship["visible"]!.GetValue<bool>().ShouldBeFalse();
        json["chainTracks"]!.AsArray().Select(n => n!.GetValue<int>()).ShouldBe(new[] { 0, 1, 0 });
        json["track"]!.GetValue<string>().ShouldBe("Ship");
    }

    [Fact]
    public void TryRead_OfCopiedChains_GivesTheSourceTracksInLaneOrder_AndWhichOneEachChainIsOn()
    {
        var items = new[]
        {
            Copied(Chain("a.achx", "Walk", 1), "Ship", lane: 0),
            Copied(Chain("a.achx", "Glow", 1.5), "Booster", lane: 2),
        };

        TimelineClipboard.TryRead(TimelineClipboard.WriteChains(items, "C:/x"), out var content, out _).ShouldBeTrue();

        content!.SourceTracks.Select(t => (t.Name, t.Lane, t.X, t.Z, t.FlipHorizontal, t.Visible))
            .ShouldBe(new[] { ("Ship", 0, 3f, 4, true, false), ("Booster", 2, 3f, 4, true, false) });
        content.ChainTracks.ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void TryRead_OfAnEnvelopeWithoutTrackInfo_StillReads_WithNoSourceTracks()
    {
        const string legacy = "{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1, \"track\": \"Body\", \"chains\": [ { \"source\": \"C:/a.achx\", \"chain\": \"Walk\", \"start\": 0 } ] }";

        TimelineClipboard.TryRead(legacy, out var content, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        content!.TrackName.ShouldBe("Body");
        content.SourceTracks.ShouldBeEmpty();
        content.ChainTracks.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("\"sourceTracks\": [ { \"name\": \"A\", \"lane\": 0 } ], \"chainTracks\": [ 0, 0 ]")]
    [InlineData("\"sourceTracks\": [ { \"name\": \"A\", \"lane\": 0 } ], \"chainTracks\": [ 3 ]")]
    [InlineData("\"sourceTracks\": [ { \"name\": \"A\", \"lane\": \"x\" } ], \"chainTracks\": [ 0 ]")]
    [InlineData("\"sourceTracks\": 5, \"chainTracks\": [ 0 ]")]
    public void TryRead_OfBrokenTrackInfo_ReportsAnError(string trackInfo)
    {
        string text = "{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1, " + trackInfo
            + ", \"chains\": [ { \"source\": \"C:/a.achx\", \"chain\": \"Walk\", \"start\": 0 } ] }";

        TimelineClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }
}
