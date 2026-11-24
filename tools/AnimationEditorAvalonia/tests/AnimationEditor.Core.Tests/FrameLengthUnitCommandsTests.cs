using AnimationEditor.Core.DragDrop;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #1829: every frame the editor creates is in the file's own unit. A Millisecond file used to get
/// 0.1 under the Millisecond label, which plays for 0.1 ms.
/// </summary>
[Collection("SequentialSingletons")]
public class FrameLengthUnitCommandsTests
{
    private static TestServices Open(TimeMeasurementUnit unit)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.Acls.TimeMeasurementUnit = unit;
        return ctx;
    }

    [Theory]
    [InlineData(TimeMeasurementUnit.Millisecond, 100f)]
    [InlineData(TimeMeasurementUnit.Second, 0.1f)]
    [InlineData(TimeMeasurementUnit.Undefined, 0.1f)]
    public void AddFrame_NewFrameIsATenthOfASecondInTheFilesUnit(TimeMeasurementUnit unit, float expected)
    {
        var ctx = Open(unit);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run");

        ctx.AppCommands.AddFrame(chain);

        Assert.Equal(expected, chain.Frames[0].FrameLength);
    }

    [Fact]
    public void AddFrameFromPixelBounds_MillisecondFile_NewFrameIs100()
    {
        var ctx = Open(TimeMeasurementUnit.Millisecond);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run");

        ctx.AppCommands.AddFrameFromPixelBounds(chain, "a.png", 0, 0, 15, 15);

        Assert.Equal(100f, chain.Frames[0].FrameLength);
    }

    [Fact]
    public void AddMultipleFrames_MillisecondFile_EmptyChainGetsDefault_NonEmptyInheritsLast()
    {
        var ctx = Open(TimeMeasurementUnit.Millisecond);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run");

        ctx.AppCommands.AddMultipleFrames(chain, 2, incrementUV: false);
        Assert.All(chain.Frames, f => Assert.Equal(100f, f.FrameLength));

        chain.Frames[^1].FrameLength = 250f;
        ctx.AppCommands.AddMultipleFrames(chain, 1, incrementUV: false);
        Assert.Equal(250f, chain.Frames[^1].FrameLength);
    }

    [Fact]
    public void BuildBatch_MillisecondUnit_DefaultIs100()
    {
        var result = BatchFrameBuilder.BuildBatch(null, 1, false, FrameLengthUnit.Milliseconds, null);
        Assert.Equal(100f, result.Frames[0].FrameLength);
    }

    [Theory]
    [InlineData(TimeMeasurementUnit.Millisecond)]
    [InlineData(TimeMeasurementUnit.Second)]
    [InlineData(TimeMeasurementUnit.Undefined)]
    public void UnchangedFile_RoundTripsByteForByte_InBothDialects(TimeMeasurementUnit unit)
    {
        var acls = new AnimationChainListSave { TimeMeasurementUnit = unit };
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "a.png",
            FrameLength = unit == TimeMeasurementUnit.Millisecond ? 100f : 0.1f,
        });
        acls.AnimationChains.Add(chain);

        string xml = acls.ToXmlString();
        string json = acls.ToJsonString();
        var xmlAgain = AnimationChainListSave.FromString(xml);
        var jsonAgain = AnimationChainListSave.FromJsonString(json);

        Assert.Equal(unit, xmlAgain.TimeMeasurementUnit);
        Assert.Equal(unit, jsonAgain.TimeMeasurementUnit);
        Assert.Equal(xml, xmlAgain.ToXmlString());
        Assert.Equal(json, jsonAgain.ToJsonString());
    }
}
