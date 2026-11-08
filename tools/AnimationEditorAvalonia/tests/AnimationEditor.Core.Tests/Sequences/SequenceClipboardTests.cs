using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The clipboard envelope a copied sequence travels in.</summary>
public class SequenceClipboardTests
{
    private static SequenceSave SequenceUsing(params string[] sources)
    {
        var track = new SequenceTrackSave { Name = "Body", X = 3, Y = -2, Z = 1, FlipHorizontal = true };
        double start = 0;
        foreach (string source in sources)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = "Walk", Start = start += 0.5, Loops = 2 });
        var sequence = new SequenceSave { Name = "Idle", Loop = true };
        sequence.Tracks.Add(track);
        return sequence;
    }

    [Fact]
    public void Write_ProducesAMarkedVersionedEnvelope_WithAbsoluteSourcePaths()
    {
        var sequence = SequenceUsing("hero.achx", "../shared/fx.achj", "hero.achx");

        var json = JsonNode.Parse(SequenceClipboard.Write(sequence, @"C:\game\anims"))!.AsObject();

        json["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-sequence");
        json["version"]!.GetValue<int>().ShouldBe(1);
        json["sources"]!.AsArray().Select(node => node!.GetValue<string>())
            .ShouldBe(new[] { "C:/game/anims/hero.achx", "C:/game/shared/fx.achj" });
        var references = json["sequence"]!["tracks"]![0]!["chains"]!.AsArray();
        references[1]!["source"]!.GetValue<string>().ShouldBe("C:/game/shared/fx.achj");
    }

    [Fact]
    public void Write_DoesNotChangeTheSequenceItWasGiven()
    {
        var sequence = SequenceUsing("hero.achx");

        SequenceClipboard.Write(sequence, @"C:\game\anims");

        sequence.Tracks[0].Chains[0].Source.ShouldBe("hero.achx");
    }

    [Fact]
    public void TryRead_OfWhatWriteMade_GivesTheSequenceBack()
    {
        var sequence = SequenceUsing("hero.achx");
        sequence.Tracks[0].Visible = false;
        sequence.Tracks[0].UnknownProperties["tag"] = "boss";
        sequence.UnknownProperties["note"] = "keep me";

        SequenceClipboard.TryRead(SequenceClipboard.Write(sequence, @"C:\game\anims"), out var content, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        content!.Sources.ShouldBe(new[] { "C:/game/anims/hero.achx" });
        var read = content.Sequences.Single();
        read.Name.ShouldBe("Idle");
        read.Loop.ShouldBeTrue();
        read.UnknownProperties["note"]!.GetValue<string>().ShouldBe("keep me");
        var track = read.Tracks.Single();
        (track.Name, track.X, track.Y, track.Z, track.FlipHorizontal).ShouldBe(("Body", 3f, -2f, 1, true));
        track.Visible.ShouldBeFalse();
        track.UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        var chain = track.Chains.Single();
        (chain.Source, chain.Chain, chain.Start, chain.Loops).ShouldBe(("C:/game/anims/hero.achx", "Walk", 0.5, 2f));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"version\": 1, \"sources\": [], \"sequences\": [] }")]
    [InlineData("{ \"kind\": \"something-else\", \"sequence\": {} }")]
    [InlineData("{ \"clipboard\": \"chains\" }")]
    public void TryRead_OfTextThatIsNotAnEnvelope_IsIgnoredWithNoError(string? text)
    {
        SequenceClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1 }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1, \"sequence\": 5 }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": \"one\", \"sequence\": { \"name\": \"A\" } }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1, \"sequence\": { \"name\": 7 } }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1, \"sources\": [1], \"sequence\": { \"name\": \"A\" } }")]
    public void TryRead_OfABrokenEnvelope_ReportsAnError(string text)
    {
        SequenceClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Write_OfSeveralSequences_ProducesAVersion2EnvelopeWithASequencesArray_AndOneSharedSourceList()
    {
        var first = SequenceUsing("hero.achx");
        var second = SequenceUsing("hero.achx", "../shared/fx.achj");
        second.Name = "Run";

        var json = JsonNode.Parse(SequenceClipboard.Write(new[] { first, second }, @"C:\game\anims"))!.AsObject();

        json["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-sequence");
        json["version"]!.GetValue<int>().ShouldBe(2);
        json.ContainsKey("sequence").ShouldBeFalse();
        json["sources"]!.AsArray().Select(node => node!.GetValue<string>())
            .ShouldBe(new[] { "C:/game/anims/hero.achx", "C:/game/shared/fx.achj" });
        var sequences = json["sequences"]!.AsArray();
        sequences.Select(node => node!["name"]!.GetValue<string>()).ShouldBe(new[] { "Idle", "Run" });
        sequences[1]!["tracks"]![0]!["chains"]![1]!["source"]!.GetValue<string>().ShouldBe("C:/game/shared/fx.achj");
        first.Tracks[0].Chains[0].Source.ShouldBe("hero.achx");
    }

    [Fact]
    public void Write_OfASingleSequenceInAList_IsExactlyTheSingleSequenceEnvelope()
    {
        var sequence = SequenceUsing("hero.achx");

        SequenceClipboard.Write(new[] { sequence }, @"C:\game\anims").ShouldBe(SequenceClipboard.Write(sequence, @"C:\game\anims"));
    }

    [Fact]
    public void TryRead_OfSeveralSequences_GivesThemAllBackInOrder_WithEverythingTheyHold()
    {
        var first = SequenceUsing("hero.achx");
        first.UnknownProperties["note"] = "keep me";
        var second = SequenceUsing("../shared/fx.achj");
        second.Name = "Run";
        second.Loop = false;

        SequenceClipboard.TryRead(SequenceClipboard.Write(new[] { first, second }, @"C:\game\anims"), out var content, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        content!.Sources.ShouldBe(new[] { "C:/game/anims/hero.achx", "C:/game/shared/fx.achj" });
        content.Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Run" });
        content.Sequences[0].UnknownProperties["note"]!.GetValue<string>().ShouldBe("keep me");
        content.Sequences[1].Loop.ShouldBeFalse();
        content.Sequences[1].Tracks.Single().Chains.Single().Source.ShouldBe("C:/game/shared/fx.achj");
    }

    [Theory]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 2, \"sequences\": [] }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 2, \"sequences\": 5 }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 2, \"sequences\": [ 5 ] }")]
    [InlineData("{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 2, \"sequences\": [ { \"name\": 7 } ] }")]
    public void TryRead_OfABrokenSequencesArray_ReportsAnError(string text)
    {
        SequenceClipboard.TryRead(text, out var content, out var error).ShouldBeFalse();

        content.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TimelineClipboard_IgnoresAMultiSequenceEnvelope()
    {
        string several = SequenceClipboard.Write(new[] { SequenceUsing("hero.achx"), SequenceUsing("hero.achx") }, @"C:\game\anims");

        TimelineClipboard.TryRead(several, out _, out var error).ShouldBeFalse();

        error.ShouldBeNull();
    }

    [Fact]
    public void Clone_KeepsEveryFieldIncludingUnknownOnes_AndSharesNothing()
    {
        var original = SequenceUsing("hero.achx");
        original.UnknownProperties["note"] = "keep";
        original.Tracks[0].Visible = false;
        original.Tracks[0].UnknownProperties["tag"] = "boss";
        original.Tracks[0].Chains[0].UnknownProperties["tint"] = new JsonObject { ["r"] = 1 };

        var clone = SequenceClipboard.Clone(original);
        clone.Tracks[0].Chains[0].Start = 99;
        clone.Tracks[0].Visible = true;
        clone.Tracks[0].UnknownProperties["tag"] = "other";

        original.Tracks[0].Chains[0].Start.ShouldBe(0.5);
        original.Tracks[0].Visible.ShouldBeFalse();
        original.Tracks[0].UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        clone.UnknownProperties["note"]!.GetValue<string>().ShouldBe("keep");
        clone.Tracks[0].Chains[0].UnknownProperties["tint"]!["r"]!.GetValue<int>().ShouldBe(1);
        (clone.Name, clone.Loop, clone.Tracks[0].X, clone.Tracks[0].FlipHorizontal).ShouldBe(("Idle", true, 3f, true));
    }
}
