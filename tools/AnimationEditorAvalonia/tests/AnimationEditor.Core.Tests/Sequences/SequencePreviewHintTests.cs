using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The line under an empty sequence preview, so a chain that starts after the playhead does not look broken.</summary>
public class SequencePreviewHintTests
{
    private static SequenceSave SequenceWithChainsAt(params double[] starts)
    {
        var track = new SequenceTrackSave { Name = "Body" };
        foreach (double start in starts)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = start });
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(track);
        return sequence;
    }

    [Fact]
    public void ASpriteShows_NoHint() =>
        SequencePreviewHint.For(SequenceWithChainsAt(0), spritesShown: 1, time: 0).ShouldBeNull();

    [Fact]
    public void NoChainsAtAll_NoHint() =>
        SequencePreviewHint.For(new SequenceSave { Name = "Empty" }, spritesShown: 0, time: 0).ShouldBeNull();

    [Fact]
    public void NoSequence_NoHint() =>
        SequencePreviewHint.For(null, spritesShown: 0, time: 0).ShouldBeNull();

    [Fact]
    public void FirstChainStartsLater_NamesTheTimeItStarts() =>
        SequencePreviewHint.For(SequenceWithChainsAt(1.5, 0.4), spritesShown: 0, time: 0)
            .ShouldBe("Nothing plays at the playhead. The next chain starts at 0.40 s.");

    [Fact]
    public void PastEveryChainStart_JustSaysNothingPlays() =>
        SequencePreviewHint.For(SequenceWithChainsAt(0.4), spritesShown: 0, time: 2)
            .ShouldBe("Nothing plays at the playhead.");
}
