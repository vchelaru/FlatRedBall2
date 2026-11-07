using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class SequenceClockTests
{
    [Fact]
    public void Advance_PastTheEndWithoutLoop_StopsAtTheEnd()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);

        clock.Advance(1.5, length: 2, loop: false);
        clock.Advance(1.0, length: 2, loop: false);

        clock.Time.ShouldBe(2);
        clock.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Advance_PastTheEndWithLoop_WrapsAndKeepsPlaying()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);

        clock.Advance(2.5, length: 2, loop: true);

        clock.Time.ShouldBe(0.5, 1e-9);
        clock.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void Play_AtTheEnd_StartsAgainFromZero()
    {
        var clock = new SequenceClock();
        clock.Seek(2, length: 2);

        clock.Play(length: 2);

        clock.Time.ShouldBe(0);
        clock.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void Play_AnEmptySequence_DoesNotStart()
    {
        var clock = new SequenceClock();

        clock.Play(length: 0);

        clock.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Stop_PausesAndRewindsToZero()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);
        clock.Advance(1, length: 2, loop: false);

        clock.Stop();

        clock.Time.ShouldBe(0);
        clock.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Seek_ClampsToTheSequence()
    {
        var clock = new SequenceClock();

        clock.Seek(5, length: 2);
        clock.Time.ShouldBe(2);
        clock.Seek(-1, length: 2);
        clock.Time.ShouldBe(0);
    }

    [Fact]
    public void AdvanceTick_AfterALongStall_MovesNoFurtherThanTheCap()
    {
        var clock = new SequenceClock();
        clock.Play(length: 10);

        clock.AdvanceTick(5, length: 10, loop: false);

        clock.Time.ShouldBe(0.25, 1e-9);
        clock.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void AdvanceTick_ANormalFrame_PassesThroughUnchanged()
    {
        var clock = new SequenceClock();
        clock.Play(length: 10);

        clock.AdvanceTick(0.016, length: 10, loop: false);
        clock.AdvanceTick(0.016, length: 10, loop: false);

        clock.Time.ShouldBe(0.032, 1e-9);
    }

    [Fact]
    public void AdvanceTick_ExactlyTheCap_IsNotReduced()
    {
        var clock = new SequenceClock();
        clock.Play(length: 10);

        clock.AdvanceTick(0.25, length: 10, loop: false);

        clock.Time.ShouldBe(0.25, 1e-9);
    }

    [Fact]
    public void AdvanceTick_StallNearTheEnd_StillStopsThereWithoutLoop()
    {
        var clock = new SequenceClock();
        clock.Play(length: 1);
        clock.Seek(0.9, length: 1);

        clock.AdvanceTick(5, length: 1, loop: false);

        clock.Time.ShouldBe(1, 1e-9);
        clock.IsPlaying.ShouldBeFalse();
    }
}
