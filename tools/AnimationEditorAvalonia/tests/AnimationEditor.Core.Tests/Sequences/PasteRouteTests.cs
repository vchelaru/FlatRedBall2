using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Where a paste goes is decided by what the clipboard holds, never by which pane had the focus.</summary>
public class PasteRouteTests
{
    private static SequenceTrackSave Track(string name)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" });
        return track;
    }

    [Fact]
    public void CopiedSequences_GoToTheSequenceList()
    {
        var text = SequenceClipboard.Write(new SequenceSave { Name = "Idle" }, @"C:\game");

        var route = PasteRoute.For(text);

        route.Kind.ShouldBe(PasteRouteKind.Sequences);
        route.Sequences!.Sequences.Count.ShouldBe(1);
    }

    [Fact]
    public void CopiedTracks_GoToTheTimeline()
    {
        var text = TimelineClipboard.WriteTracks(new[] { Track("Body"), Track("Head") }, @"C:\game");

        var route = PasteRoute.For(text);

        route.Kind.ShouldBe(PasteRouteKind.Timeline);
        route.Timeline!.Kind.ShouldBe(TimelineClipboardKind.Tracks);
        route.Timeline.Tracks.Count.ShouldBe(2);
    }

    [Fact]
    public void CopiedChains_GoToTheTimeline()
    {
        var track = Track("Body");
        var text = TimelineClipboard.WriteChains(new[] { new CopiedChain(track.Chains[0], track, 0) }, @"C:\game");

        var route = PasteRoute.For(text);

        route.Kind.ShouldBe(PasteRouteKind.Timeline);
        route.Timeline!.Kind.ShouldBe(TimelineClipboardKind.Chains);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyClipboard_IsRefusedWithAReason(string? text)
    {
        var route = PasteRoute.For(text);

        route.Kind.ShouldBe(PasteRouteKind.Refused);
        route.Message.ShouldBe(PasteRoute.EmptyMessage);
        route.IsDamaged.ShouldBeFalse();
    }

    [Theory]
    [InlineData("just some words")]
    [InlineData("{ \"kind\": \"something-else\" }")]
    [InlineData("[1, 2]")]
    public void OrdinaryText_IsRefusedWithAReason(string text)
    {
        var route = PasteRoute.For(text);

        route.Kind.ShouldBe(PasteRouteKind.Refused);
        route.Message.ShouldBe(PasteRoute.NotACopyMessage);
        route.IsDamaged.ShouldBeFalse();
    }

    [Fact]
    public void ADamagedCopy_IsRefusedAndFlaggedDamaged()
    {
        var route = PasteRoute.For("{ \"kind\": \"frb2-animation-editor-tracks\", \"version\": 1 }");

        route.Kind.ShouldBe(PasteRouteKind.Refused);
        route.IsDamaged.ShouldBeTrue();
        route.Message.ShouldNotBeNullOrEmpty();
    }
}
