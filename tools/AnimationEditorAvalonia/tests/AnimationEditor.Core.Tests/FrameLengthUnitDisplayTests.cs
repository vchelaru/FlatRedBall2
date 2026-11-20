using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>#1829 slice 2: everything the editor shows or edits about a frame length uses the file's unit.</summary>
[Collection("SequentialSingletons")]
public class FrameLengthUnitDisplayTests
{
    private static AnimationChainSave Chain(params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = "Run" };
        foreach (var length in lengths)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = length });
        return chain;
    }

    [Fact]
    public void Unit_Limits_AreTheSameDurationsInEitherUnit()
    {
        Assert.Equal("seconds", FrameLengthUnit.Seconds.Name);
        Assert.Equal("milliseconds", FrameLengthUnit.Milliseconds.Name);
        Assert.Equal(1m, FrameLengthUnit.Milliseconds.Minimum);
        Assert.Equal(60000m, FrameLengthUnit.Milliseconds.Maximum);
        Assert.Equal(50m, FrameLengthUnit.Milliseconds.Step);
        Assert.Equal(0.05m, FrameLengthUnit.Seconds.Step);
        Assert.Equal("0.1s", FrameLengthUnit.Seconds.FormatCompact(0.1m));
        Assert.Equal("100ms", FrameLengthUnit.Milliseconds.FormatCompact(100m));
    }

    [Theory]
    [InlineData(TimeMeasurementUnit.Millisecond, 100f, "100ms")]
    [InlineData(TimeMeasurementUnit.Second, 0.1f, "0.10s")]
    [InlineData(TimeMeasurementUnit.Undefined, 0.1f, "0.10s")]
    public void FrameNode_MetaShowsTheLengthInTheFilesUnit(TimeMeasurementUnit unit, float length, string expected)
    {
        var node = TreeBuilder.BuildFrameNode(Chain(length).Frames[0], new FrameLengthUnit(unit));
        Assert.Equal(expected, node.Meta);
    }

    [Fact]
    public void ChainNode_MetaShowsTheTotalInTheFilesUnit()
    {
        var chain = Chain(100f, 100f);
        var ms = TreeBuilder.BuildChainNode(chain, FrameLengthUnit.Milliseconds);
        Assert.Equal("2 fr · 200ms", ms.Meta);
        Assert.Equal("100ms", ms.Children[0].Meta);

        var seconds = TreeBuilder.BuildChainNode(Chain(0.1f, 0.1f), FrameLengthUnit.Seconds);
        Assert.Equal("2 fr · 0.20s", seconds.Meta);
    }

    [Fact]
    public void SyncFramesInto_RefreshesFrameMetaInTheFilesUnit()
    {
        var chain = Chain(100f, 250f);
        var node = TreeBuilder.BuildChainNode(chain, FrameLengthUnit.Milliseconds);
        chain.Frames[0].FrameLength = 300f;

        TreeBuilder.SyncFramesInto(node, chain.Frames, FrameLengthUnit.Milliseconds);

        Assert.Equal("300ms", node.Children[0].Meta);
        Assert.Equal("250ms", node.Children[1].Meta);
    }

    [Fact]
    public void Timeline_MillisecondChain_HasTheSameCellWidthsAsTheEquivalentSecondsChain()
    {
        var ms = TimelineBuilder.BuildFrameItems(Chain(100f, 250f), FrameLengthUnit.Milliseconds);
        var seconds = TimelineBuilder.BuildFrameItems(Chain(0.1f, 0.25f), FrameLengthUnit.Seconds);

        Assert.Equal(seconds.Count, ms.Count);
        for (int i = 0; i < ms.Count; i++)
            Assert.Equal(seconds[i].Width, ms[i].Width, precision: 3);
        Assert.Equal(
            TimelineBuilder.ComputeEffectivePixelsPerSecond(Chain(0.02f), FrameLengthUnit.Seconds),
            TimelineBuilder.ComputeEffectivePixelsPerSecond(Chain(20f), FrameLengthUnit.Milliseconds), precision: 3);
        Assert.Equal(
            TimelineBuilder.ComputeSharedEffectivePixelsPerSecond(new[] { Chain(0.02f) }, FrameLengthUnit.Seconds),
            TimelineBuilder.ComputeSharedEffectivePixelsPerSecond(new[] { Chain(20f) }, FrameLengthUnit.Milliseconds), precision: 3);
    }

    [Fact]
    public void Timeline_TotalIsShownInTheFilesUnit()
    {
        Assert.Equal("350ms", TimelineBuilder.FormatTotal(Chain(100f, 250f), FrameLengthUnit.Milliseconds));
        Assert.Equal("0.35s", TimelineBuilder.FormatTotal(Chain(0.1f, 0.25f), FrameLengthUnit.Seconds));

        var acls = new AnimationChainListSave { TimeMeasurementUnit = TimeMeasurementUnit.Millisecond };
        acls.AnimationChains.Add(Chain(100f));
        acls.AnimationChains.Add(Chain(250f));
        Assert.Equal("350ms", TimelineBuilder.FormatTotal(acls));
    }

    [Fact]
    public void SetFrameLength_UndoLabelUsesTheFilesUnit()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.Acls.TimeMeasurementUnit = TimeMeasurementUnit.Millisecond;
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);

        ctx.AppCommands.SetFrameLength(chain.Frames, 250m);

        Assert.Equal(250f, chain.Frames[0].FrameLength);
        Assert.Equal("Set Length: 250ms", ctx.UndoManager.UndoHistory.Last().Description);
    }
}
