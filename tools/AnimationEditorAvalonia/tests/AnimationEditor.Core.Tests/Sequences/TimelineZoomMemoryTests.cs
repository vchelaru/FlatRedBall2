using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineZoomMemoryTests
{
    [Fact]
    public void RememberedPercent_IsRestored()
    {
        var store = new Dictionary<string, int>();

        TimelineZoomMemory.Remember(store, "c:/boss.seqj", 200).ShouldBeTrue();

        store["c:/boss.seqj"].ShouldBe(200);
        TimelineZoomMemory.PercentToRestore(store, "c:/boss.seqj").ShouldBe(200);
    }

    [Fact]
    public void ALivePercentWithFloatNoise_IsStoredAsTheWholePercentTheFieldShows()
    {
        var store = new Dictionary<string, int>();

        TimelineZoomMemory.Remember(store, "f", 149.99999);

        store["f"].ShouldBe(150);
    }

    [Fact]
    public void NothingRemembered_RestoresTo100()
    {
        TimelineZoomMemory.PercentToRestore(new Dictionary<string, int>(), "f").ShouldBe(100);
    }

    [Fact]
    public void RememberingTheSamePercentAgain_ChangesNothing()
    {
        var store = new Dictionary<string, int>();
        TimelineZoomMemory.Remember(store, "f", 300);

        TimelineZoomMemory.Remember(store, "f", 300).ShouldBeFalse();
        TimelineZoomMemory.Remember(store, "f", 300.2).ShouldBeFalse();
    }

    [Fact]
    public void ZoomBackTo100_DropsTheEntry()
    {
        var store = new Dictionary<string, int> { ["f"] = 200 };

        TimelineZoomMemory.Remember(store, "f", 100).ShouldBeTrue();

        store.ShouldBeEmpty();
        TimelineZoomMemory.Remember(store, "f", 100).ShouldBeFalse();
    }

    [Fact]
    public void EachFileKeepsItsOwnZoom()
    {
        var store = new Dictionary<string, int>();
        TimelineZoomMemory.Remember(store, "a", 50);
        TimelineZoomMemory.Remember(store, "b", 400);

        TimelineZoomMemory.PercentToRestore(store, "a").ShouldBe(50);
        TimelineZoomMemory.PercentToRestore(store, "b").ShouldBe(400);
        TimelineZoomMemory.PercentToRestore(store, "c").ShouldBe(100);
    }

    [Fact]
    public void AStoredPercentPastTheTimelineLimits_IsClampedOnRestore()
    {
        var store = new Dictionary<string, int> { ["big"] = 99999, ["small"] = 1, ["zero"] = 0, ["negative"] = -40 };

        TimelineZoomMemory.PercentToRestore(store, "big").ShouldBe(TimelineZoomMemory.MaxPercent);
        TimelineZoomMemory.PercentToRestore(store, "small").ShouldBe(TimelineZoomMemory.MinPercent);
        TimelineZoomMemory.PercentToRestore(store, "zero").ShouldBe(TimelineZoomMemory.MinPercent);
        TimelineZoomMemory.PercentToRestore(store, "negative").ShouldBe(TimelineZoomMemory.MinPercent);
    }

    [Fact]
    public void TheLimitsAreTheTimelinesOwn_RoundedToTheWholePercentTheFieldShows()
    {
        // 10 px/s and 2000 px/s over the 120 px/s default.
        TimelineZoomMemory.MinPercent.ShouldBe(8);
        TimelineZoomMemory.MaxPercent.ShouldBe(1667);
    }

    [Fact]
    public void AZoomAtTheLimit_IsStoredAsTheLimitsPercent_AndRestoresToTheSameValue()
    {
        var store = new Dictionary<string, int>();
        TimelineZoomMemory.Remember(store, "f", TimelineViewport.MinZoom * 100.0);

        store["f"].ShouldBe(TimelineZoomMemory.MinPercent);
        TimelineZoomMemory.Remember(store, "f", TimelineViewport.MinZoom * 100.0).ShouldBeFalse();
    }

    [Fact]
    public void RememberingAnOutOfRangePercent_StoresTheClampedValue()
    {
        var store = new Dictionary<string, int>();

        TimelineZoomMemory.Remember(store, "f", 50000);

        store["f"].ShouldBe(TimelineZoomMemory.MaxPercent);
    }
}
