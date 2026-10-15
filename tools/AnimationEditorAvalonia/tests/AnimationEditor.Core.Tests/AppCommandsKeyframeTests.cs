using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsKeyframeTests
{
    [Fact]
    public void SetFrameKeyframe_Off_WritesTheSkipFlag_AndUndoRestoresIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 3);

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Offset, false);

        Assert.True(chain.Frames[1].SkipOffsetKeyframe);
        Assert.False(chain.Frames[1].SkipColorKeyframe);
        Assert.Equal("Disable Offset Keyframe", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.False(chain.Frames[1].SkipOffsetKeyframe);
    }

    [Fact]
    public void SetFrameKeyframe_On_ClearsTheFlag_WithAGroupedDescription()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.Frames[1].SkipColorKeyframe = true;

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Color, true);

        Assert.False(chain.Frames[1].SkipColorKeyframe);
        Assert.Equal("Enable Color Keyframe", ctx.UndoManager.UndoHistory.Single().Description);
    }

    [Fact]
    public void SetFrameKeyframe_FirstFrame_IsNeverWritten()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[0] }, KeyframeGroup.Offset, false);

        Assert.False(chain.Frames[0].SkipOffsetKeyframe);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameKeyframe_MultiSelect_IsOneUndoStep_AndLeavesTheFirstFrameAlone()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 3);

        ctx.AppCommands.SetFrameKeyframe(chain.Frames.ToList(), KeyframeGroup.Offset, false);

        Assert.False(chain.Frames[0].SkipOffsetKeyframe);
        Assert.True(chain.Frames[1].SkipOffsetKeyframe);
        Assert.True(chain.Frames[2].SkipOffsetKeyframe);
        Assert.Single(ctx.UndoManager.UndoHistory);

        ctx.UndoManager.Undo();
        Assert.False(chain.Frames[1].SkipOffsetKeyframe);
        Assert.False(chain.Frames[2].SkipOffsetKeyframe);
    }

    [Fact]
    public void SetFrameKeyframe_SameState_RecordsNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Offset, true);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameKeyframe_KeepsTheStoredCurveWhileOff()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.Frames[1].OffsetInterpolation = "Cubic";
        chain.Frames[1].OffsetEasing = "In";

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Offset, false);
        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Offset, true);

        Assert.Equal("Cubic", chain.Frames[1].OffsetInterpolation);
        Assert.Equal("In", chain.Frames[1].OffsetEasing);
    }

    [Fact]
    public void SetFrameKeyframeCurve_WritesTheFixedPair_WithADescription()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);

        ctx.AppCommands.SetFrameKeyframeCurve(new[] { chain.Frames[1] }, KeyframeGroup.Color, KeyframeCurveChoice.Anticipate);

        Assert.Equal("Back", chain.Frames[1].ColorInterpolation);
        Assert.Equal("In", chain.Frames[1].ColorEasing);
        Assert.Null(chain.Frames[1].OffsetInterpolation);
        Assert.Equal("Set Color Curve: Anticipate", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.Null(chain.Frames[1].ColorInterpolation);
        Assert.Null(chain.Frames[1].ColorEasing);
    }

    [Fact]
    public void SetFrameKeyframeCurve_Snap_WritesNullForBoth()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.Frames[1].OffsetInterpolation = "Quadratic";
        chain.Frames[1].OffsetEasing = "InOut";

        ctx.AppCommands.SetFrameKeyframeCurve(new[] { chain.Frames[1] }, KeyframeGroup.Offset, KeyframeCurveChoice.Snap);

        Assert.Null(chain.Frames[1].OffsetInterpolation);
        Assert.Null(chain.Frames[1].OffsetEasing);
    }

    [Fact]
    public void SetFrameKeyframeCurve_ReplacesAnOutOfSetCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.Frames[1].OffsetInterpolation = "Cubic";
        chain.Frames[1].OffsetEasing = "In";

        ctx.AppCommands.SetFrameKeyframeCurve(new[] { chain.Frames[1] }, KeyframeGroup.Offset, KeyframeCurveChoice.Linear);

        Assert.Equal("Linear", chain.Frames[1].OffsetInterpolation);
        Assert.Equal("Out", chain.Frames[1].OffsetEasing);
    }

    [Fact]
    public void SetFrameKeyframeCurve_MultiSelect_OneUndoStep_SkipsFramesThatAreNotKeyframes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 3);
        chain.Frames[2].SkipOffsetKeyframe = true;
        chain.Frames[2].OffsetInterpolation = "Cubic";

        ctx.AppCommands.SetFrameKeyframeCurve(chain.Frames.ToList(), KeyframeGroup.Offset, KeyframeCurveChoice.Bounce);

        Assert.Equal("Bounce", chain.Frames[0].OffsetInterpolation);
        Assert.Equal("Bounce", chain.Frames[1].OffsetInterpolation);
        Assert.Equal("Cubic", chain.Frames[2].OffsetInterpolation);
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void EditingOtherFields_NeverNormalizesAStoredCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.Frames[1].OffsetInterpolation = "Wobble";
        chain.Frames[1].OffsetEasing = "Sideways";
        chain.Frames[1].ColorInterpolation = "Cubic";

        ctx.AppCommands.SetFrameRelative(new[] { chain.Frames[1] }, 5m, null);
        ctx.AppCommands.SetFrameAlpha(new[] { chain.Frames[1] }, 10);
        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Color, false);
        ctx.UndoManager.Undo();

        Assert.Equal("Wobble", chain.Frames[1].OffsetInterpolation);
        Assert.Equal("Sideways", chain.Frames[1].OffsetEasing);
        Assert.Equal("Cubic", chain.Frames[1].ColorInterpolation);
    }

    [Fact]
    public void LockedFrames_AreSkipped()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        chain.IsLocked = true;

        ctx.AppCommands.SetFrameKeyframe(new[] { chain.Frames[1] }, KeyframeGroup.Offset, false);
        ctx.AppCommands.SetFrameKeyframeCurve(new[] { chain.Frames[1] }, KeyframeGroup.Offset, KeyframeCurveChoice.Linear);

        Assert.False(chain.Frames[1].SkipOffsetKeyframe);
        Assert.Null(chain.Frames[1].OffsetInterpolation);
    }
}
