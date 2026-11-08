using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Playback speed, fit-to-sequence zoom and the remembered preview view of a <c>.seqj</c> tab.</summary>
public class SequenceViewParityTests
{
    private static double Length(string name) => 2;

    private static bool Loop(string name) => false;

    [Fact]
    public void Speed_ScalesEverySelectedClock_ByTheSameFactor()
    {
        var playback = new SequencePlayback { Speed = 2 };
        playback.Select(new[] { "A", "B" }, Length);
        playback.Play(Length);

        playback.AdvanceTick(0.1, Length, Loop);

        playback.TimeOf("A").ShouldBe(0.2, 1e-9);
        playback.TimeOf("B").ShouldBe(0.2, 1e-9);
    }

    [Fact]
    public void Speed_DefaultsToRuntimeSpeed_AndStallsAreStillCappedBeforeItScales()
    {
        var playback = new SequencePlayback();
        playback.Select(new[] { "A" }, Length);
        playback.Play(Length);
        playback.AdvanceTick(0.1, Length, Loop);
        playback.TimeOf("A").ShouldBe(0.1, 1e-9);

        // A stalled frame (10 s) advances by the 0.25 s cap, then scales: 0.5 s at 2x, not 20 s.
        playback.Speed = 2;
        playback.AdvanceTick(10, Length, Loop);
        playback.TimeOf("A").ShouldBe(0.6, 1e-9);
    }

    [Fact]
    public void Speed_IsHeldInTheRangeTheAchxFieldAllows()
    {
        var playback = new SequencePlayback { Speed = 500 };
        playback.Speed.ShouldBe(10);
        playback.Speed = 0;
        playback.Speed.ShouldBe(0.1, 1e-9);
    }

    [Fact]
    public void PixelsPerSecondToFit_PutsTheWholeSequenceInTheViewBesideTheHeadersAndEndMargin()
    {
        double view = TimelineViewport.HeaderWidth + TimelineScrollBars.EndMarginPixels + 600;
        // 600 px for 3 s, after the header column and the end margin the scrollbar reserves.
        TimelineViewport.PixelsPerSecondToFit(3, view).ShouldBe(200, 1e-9);
    }

    [Fact]
    public void PixelsPerSecondToFit_IsHeldWithinTheZoomLimits_AndAnEmptySequenceKeepsTheDefault()
    {
        TimelineViewport.PixelsPerSecondToFit(0.001, 2000).ShouldBe(TimelineViewport.MaxPixelsPerSecond);
        TimelineViewport.PixelsPerSecondToFit(10_000, 800).ShouldBe(TimelineViewport.MinPixelsPerSecond);
        TimelineViewport.PixelsPerSecondToFit(0, 800).ShouldBe(TimelineViewport.DefaultPixelsPerSecond);
        TimelineViewport.PixelsPerSecondToFit(3, 50).ShouldBe(TimelineViewport.MinPixelsPerSecond);
    }

    [Fact]
    public void PreviewView_IsRemembered_PerFile_AndTheDefaultViewClearsTheEntry()
    {
        var store = new Dictionary<string, SequencePreviewView>();

        SequencePreviewViewMemory.Remember(store, "a.seqj", 250, 12.5f, -4).ShouldBeTrue();
        SequencePreviewViewMemory.Remember(store, "a.seqj", 250, 12.5f, -4).ShouldBeFalse();
        var view = SequencePreviewViewMemory.ViewToRestore(store, "a.seqj");
        (view.ZoomPercent, view.PanX, view.PanY).ShouldBe((250, 12.5f, -4f));
        SequencePreviewViewMemory.ViewToRestore(store, "b.seqj").ZoomPercent.ShouldBe(100);

        SequencePreviewViewMemory.Remember(store, "a.seqj", 100, 0, 0).ShouldBeTrue();
        store.ShouldBeEmpty();
    }

    [Fact]
    public void PreviewView_ABadStoredValue_IsHeldWithinTheLimits_NeverNaN()
    {
        var store = new Dictionary<string, SequencePreviewView>
        {
            ["a.seqj"] = new() { ZoomPercent = 999_999, PanX = float.NaN, PanY = float.PositiveInfinity },
            ["b.seqj"] = new() { ZoomPercent = -5 },
        };

        var a = SequencePreviewViewMemory.ViewToRestore(store, "a.seqj");
        a.ZoomPercent.ShouldBe(3200);
        a.PanX.ShouldBe(0);
        a.PanY.ShouldBe(0);
        SequencePreviewViewMemory.ViewToRestore(store, "b.seqj").ZoomPercent.ShouldBe(5);
    }
}
