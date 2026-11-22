using FlatRedBall2.Animation;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineBuilderTests
{
    [Fact]
    public void BuildFrameItems_NullChain_ReturnsEmptyList()
    {
        var result = TimelineBuilder.BuildFrameItems(null, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildFrameItems_UsesFrameLengthForRelativeWidths()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.35f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(2, result.Count);
        Assert.True(result[1].Width > result[0].Width);
        Assert.Equal("1", result[0].IndexLabel);
        Assert.Equal("2", result[1].IndexLabel);
    }

    [Fact]
    public void BuildFrameItems_WidthsAreProportionalToDuration()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.2f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        // A 0.2s frame must be exactly twice as wide as a 0.1s frame regardless of scaling.
        Assert.Equal(result[0].Width * 2.0, result[1].Width, precision: 6);
    }

    [Fact]
    public void BuildFrameItems_ClampsNegativeFrameLengthToMinCellWidth()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = -1f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Single(result);
        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width);
    }

    [Fact]
    public void BuildFrameItems_ZeroLengthFrame_GetsMinCellWidth()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Single(result);
        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width);
    }

    [Fact]
    public void BuildFrameItems_ShortFrame_ScaledToMinCellWidth()
    {
        // 0.1s is short enough that baseline 120px/s would give 12px < MinCellWidth.
        // EffectivePps must be raised so the shortest frame is exactly MinCellWidth wide.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width, precision: 6);
    }

    [Fact]
    public void BuildFrameItems_MixedFrames_ShortestIsMinCellWidth_OthersAreProportional()
    {
        // Shortest frame (0.05s) should be MinCellWidth; others proportional to it.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.05f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.10f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.50f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width, precision: 3);
        Assert.Equal(result[0].Width * 2.0, result[1].Width, precision: 3);  // 0.10 = 2× 0.05
        Assert.Equal(result[0].Width * 10.0, result[2].Width, precision: 3); // 0.50 = 10× 0.05
    }

    [Fact]
    public void BuildFrameItems_LongFrames_UseBaselinePixelsPerSecond()
    {
        // All frames long enough that baseline PPS applies — no scaling needed.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.5f });

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(0.5 * TimelineBuilder.PixelsPerSecond, result[0].Width, precision: 6);
    }

    // ── ComputeEffectivePixelsPerSecond ──────────────────────────────────────

    [Fact]
    public void ComputeEffectivePps_NullChain_ReturnsBaseline()
    {
        Assert.Equal(TimelineBuilder.PixelsPerSecond, TimelineBuilder.ComputeEffectivePixelsPerSecond(null, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds));
    }

    [Fact]
    public void ComputeEffectivePps_AllZeroFrames_ReturnsBaseline()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f });

        Assert.Equal(TimelineBuilder.PixelsPerSecond, TimelineBuilder.ComputeEffectivePixelsPerSecond(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds));
    }

    [Fact]
    public void ComputeEffectivePps_LongFrames_ReturnsBaseline()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 1.0f });

        Assert.Equal(TimelineBuilder.PixelsPerSecond, TimelineBuilder.ComputeEffectivePixelsPerSecond(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds));
    }

    [Fact]
    public void ComputeEffectivePps_ShortFrame_ScaledSoMinDurationGivesMinCellWidth()
    {
        // minDuration = 0.05s → effectivePps = MinCellWidth / 0.05 = 480
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.05f });

        double pps = TimelineBuilder.ComputeEffectivePixelsPerSecond(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TimelineBuilder.MinCellWidth / 0.05, pps, precision: 3);
        Assert.True(pps > TimelineBuilder.PixelsPerSecond);
    }

    [Fact]
    public void ComputeEffectivePps_MultipleChains_UsesGlobalShortestFrame()
    {
        // #1056: two chains shown together in the multi-track timeline must share one pps,
        // derived from the shortest frame across ALL of them, not each chain's own shortest.
        var chainA = new AnimationChainSave { Name = "A" };
        chainA.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        var chainB = new AnimationChainSave { Name = "B" };
        chainB.Frames.Add(new AnimationFrameSave { FrameLength = 0.01f });

        double sharedPps = TimelineBuilder.ComputeSharedEffectivePixelsPerSecond(new[] { chainA, chainB }, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TimelineBuilder.MinCellWidth / 0.01, sharedPps, precision: 3);
    }

    [Fact]
    public void BuildFrameItems_WithExplicitPps_SameDurationGivesSameWidthAcrossChains()
    {
        // A 1-second frame in two different chains must render at the same width when both
        // are built against a shared pps — this is what makes the multi-track timeline comparable.
        var chainA = new AnimationChainSave { Name = "A" };
        chainA.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chainA.Frames.Add(new AnimationFrameSave { FrameLength = 1.0f });
        var chainB = new AnimationChainSave { Name = "B" };
        chainB.Frames.Add(new AnimationFrameSave { FrameLength = 0.01f });
        chainB.Frames.Add(new AnimationFrameSave { FrameLength = 1.0f });

        double sharedPps = TimelineBuilder.ComputeSharedEffectivePixelsPerSecond(new[] { chainA, chainB }, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
        var itemsA = TimelineBuilder.BuildFrameItems(chainA, sharedPps, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
        var itemsB = TimelineBuilder.BuildFrameItems(chainB, sharedPps, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(itemsA[1].Width, itemsB[1].Width, precision: 6);
    }

    // ── TotalSeconds & FormatSeconds ─────────────────────────────────────────

    [Fact]
    public void TotalSeconds_MultipleFrames_ReturnsSumOfFrameLengths()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.2f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.2f });

        Assert.Equal(0.5, TimelineBuilder.TotalSeconds(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds), precision: 4);
    }

    [Fact]
    public void TotalSeconds_NullChain_ReturnsZero()
    {
        Assert.Equal(0f, TimelineBuilder.TotalSeconds((AnimationChainSave?)null, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds));
    }

    [Fact]
    public void TotalSeconds_ChainList_SumsEveryChainsDuration()
    {
        var acls = new AnimationChainListSave();
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave { FrameLength = 0.25f });
        walk.Frames.Add(new AnimationFrameSave { FrameLength = 0.25f });
        var run = new AnimationChainSave { Name = "Run" };
        run.Frames.Add(new AnimationFrameSave { FrameLength = 0.5f });
        acls.AnimationChains.Add(walk);
        acls.AnimationChains.Add(run);

        Assert.Equal(1.0, TimelineBuilder.TotalSeconds(acls), precision: 4);
    }

    [Fact]
    public void FormatSeconds_Value_FormatsWithTwoDecimalsAndSuffix()
    {
        Assert.Equal("1.50s", TimelineBuilder.FormatSeconds(1.5f));
    }

    [Fact]
    public void BuildFrameItems_FramesWithEvents_ExposeEventNames()
    {
        var chain = new AnimationChainSave { Name = "Hit" };
        var first = new AnimationFrameSave { FrameLength = 0.1f };
        first.Events.Add(new AnimationFrameEvent { Name = "Start" });
        chain.Frames.Add(first);
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        var last = new AnimationFrameSave { FrameLength = 0.1f };
        last.Events.Add(new AnimationFrameEvent { Name = "Spark" });
        last.Events.Add(new AnimationFrameEvent { Name = "Boom" });
        chain.Frames.Add(last);

        var result = TimelineBuilder.BuildFrameItems(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.True(result[0].HasEvents);
        Assert.Equal("Start", result[0].EventTooltip);
        Assert.False(result[1].HasEvents);
        Assert.True(result[2].HasEvents);
        Assert.Equal("Spark\nBoom", result[2].EventTooltip);
    }
}
