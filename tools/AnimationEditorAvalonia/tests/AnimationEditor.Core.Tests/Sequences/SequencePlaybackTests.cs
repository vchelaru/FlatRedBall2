using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Several selected sequences each run on their own clock and loop by their own flag. There is no shared loop length
/// and no realigning: this is the same as multi-selecting chains in an <c>.achx</c> tab.
/// </summary>
public class SequencePlaybackTests
{
    // Short is 1 s and loops; Long is 3 s and does not.
    private static readonly Dictionary<string, double> Lengths = new() { ["Short"] = 1, ["Long"] = 3 };
    private static readonly Dictionary<string, bool> Loops = new() { ["Short"] = true, ["Long"] = false };

    private static double Length(string name) => Lengths[name];

    private static bool Loop(string name) => Loops[name];

    private static SequencePlayback Playing(params string[] names)
    {
        var playback = new SequencePlayback();
        playback.Select(names, Length);
        playback.Play(Length);
        return playback;
    }

    [Fact]
    public void EachSequence_LoopsOnItsOwnLength_WithoutWaitingForTheLongest()
    {
        var playback = Playing("Short", "Long");

        for (int i = 0; i < 5; i++) playback.AdvanceTick(0.25, Length, Loop);

        // 1.25 s: Short has wrapped to 0.25; Long has not reached its end.
        playback.TimeOf("Short").ShouldBe(0.25, 1e-9);
        playback.TimeOf("Long").ShouldBe(1.25, 1e-9);
    }

    [Fact]
    public void ANonLoopingSequence_StopsAtItsEnd_WhileALoopingOneKeepsPlaying()
    {
        var playback = Playing("Short", "Long");

        for (int i = 0; i < 14; i++) playback.AdvanceTick(0.25, Length, Loop);

        // 3.5 s: Long ended at 3 and holds; Short is still looping.
        playback.TimeOf("Long").ShouldBe(3, 1e-9);
        playback.TimeOf("Short").ShouldBe(0.5, 1e-9);
        playback.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void IsPlaying_IsFalseOnceEverySequenceHasEnded()
    {
        var playback = Playing("Long");

        for (int i = 0; i < 14; i++) playback.AdvanceTick(0.25, Length, Loop);

        playback.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Pause_FreezesEverySequence_AndPlayResumesFromWhereEachWas()
    {
        var playback = Playing("Short", "Long");
        playback.AdvanceTick(0.25, Length, Loop);
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Pause();
        playback.AdvanceTick(0.25, Length, Loop);
        playback.TimeOf("Short").ShouldBe(0.5, 1e-9);
        playback.TimeOf("Long").ShouldBe(0.5, 1e-9);

        playback.Play(Length);
        playback.AdvanceTick(0.25, Length, Loop);
        playback.TimeOf("Long").ShouldBe(0.75, 1e-9);
    }

    [Fact]
    public void Stop_RewindsEverySequenceToZero()
    {
        var playback = Playing("Short", "Long");
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Stop();

        playback.IsPlaying.ShouldBeFalse();
        playback.TimeOf("Short").ShouldBe(0);
        playback.TimeOf("Long").ShouldBe(0);
    }

    [Fact]
    public void Seek_MovesOnlyThatSequence()
    {
        var playback = Playing("Short", "Long");
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Seek("Long", 2, Length("Long"));

        playback.TimeOf("Long").ShouldBe(2);
        playback.TimeOf("Short").ShouldBe(0.25, 1e-9);
    }

    [Fact]
    public void SelectingAnotherSequence_StartsItFromTheBeginning_Stopped()
    {
        var playback = Playing("Short");
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Select(new[] { "Long" }, Length);

        playback.IsPlaying.ShouldBeFalse();
        playback.TimeOf("Long").ShouldBe(0);
    }

    [Fact]
    public void AddingASequenceToThePlayingSelection_StartsItAtZero_PlayingAlongsideTheOthers()
    {
        var playback = Playing("Short");
        playback.AdvanceTick(0.25, Length, Loop);
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Select(new[] { "Short", "Long" }, Length);
        playback.AdvanceTick(0.25, Length, Loop);

        playback.TimeOf("Short").ShouldBe(0.75, 1e-9);
        playback.TimeOf("Long").ShouldBe(0.25, 1e-9);
    }

    [Fact]
    public void AddingASequenceToAPausedSelection_LeavesItPausedAtZero()
    {
        var playback = Playing("Short");
        playback.AdvanceTick(0.25, Length, Loop);
        playback.Pause();

        playback.Select(new[] { "Short", "Long" }, Length);
        playback.AdvanceTick(0.25, Length, Loop);

        playback.TimeOf("Long").ShouldBe(0);
        playback.TimeOf("Short").ShouldBe(0.25, 1e-9);
    }

    [Fact]
    public void RemovingASequence_LeavesTheOthersPlayingWhereTheyWere()
    {
        var playback = Playing("Short", "Long");
        playback.AdvanceTick(0.25, Length, Loop);
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Select(new[] { "Long" }, Length);

        playback.IsPlaying.ShouldBeTrue();
        playback.TimeOf("Long").ShouldBe(0.5, 1e-9);
        playback.TimeOf("Short").ShouldBe(0);
    }

    [Fact]
    public void Rename_KeepsTheClock()
    {
        var playback = Playing("Short");
        playback.AdvanceTick(0.25, Length, Loop);

        playback.Rename("Short", "Quick");
        playback.Select(new[] { "Quick" }, name => 1);

        playback.TimeOf("Quick").ShouldBe(0.25, 1e-9);
        playback.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void Clamp_PullsAPlayheadBackIntoAShortenedSequence()
    {
        var playback = Playing("Long");
        playback.Seek("Long", 2.5, 3);

        playback.Clamp(name => 1);

        playback.TimeOf("Long").ShouldBe(1);
    }

    [Fact]
    public void Play_AtTheEnd_StartsAgainFromZero()
    {
        var playback = Playing("Long");
        playback.Seek("Long", 3, 3);
        playback.Pause();

        playback.Play(Length);

        playback.TimeOf("Long").ShouldBe(0);
        playback.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void AHugeTick_AdvancesAtMostTheCap()
    {
        var playback = Playing("Long");

        playback.AdvanceTick(10, Length, Loop);

        playback.TimeOf("Long").ShouldBe(SequenceClock.MaxTickSeconds, 1e-9);
    }
}
