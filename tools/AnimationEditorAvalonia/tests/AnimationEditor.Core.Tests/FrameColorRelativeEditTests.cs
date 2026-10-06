using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>#1330: a relative edit (+ 10) on a color or alpha channel applies to each frame's own value.</summary>
[Collection("SequentialSingletons")]
public class FrameColorRelativeEditTests
{
    private static NumericEdit Relative(string text)
    {
        NumericEdit.TryParse(text, out NumericEdit edit).ShouldBeTrue();
        return edit;
    }

    [Fact]
    public void SetFrameColor_RelativeEdit_AppliesToEachFrameClampedAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash", frameCount: 2);
        chain.Frames[0].Red = 100;
        chain.Frames[1].Red = 200;
        chain.Frames[0].Green = 10;
        chain.Frames[1].Green = 20;

        ctx.AppCommands.SetFrameColor(chain.Frames.ToList(), ChannelEdit.Edit(Relative("+ 100")), ChannelEdit.Keep, ChannelEdit.Keep);

        chain.Frames[0].Red.ShouldBe(200);
        chain.Frames[1].Red.ShouldBe(255);
        chain.Frames[0].Green.ShouldBe(10);
        chain.Frames[1].Green.ShouldBe(20);
        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
    }

    [Fact]
    public void SetFrameColor_RelativeEditOnUnsetChannel_StartsFromInheritedValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash", frameCount: 2);
        chain.Frames[0].Red = 100;
        // Frame 1's red is unset, so it inherits 100 from frame 0.

        ctx.AppCommands.SetFrameColor(new[] { chain.Frames[1] }, ChannelEdit.Edit(Relative("- 30")), ChannelEdit.Keep, ChannelEdit.Keep);

        chain.Frames[1].Red.ShouldBe(70);
    }

    [Fact]
    public void SetFrameAlpha_RelativeEdit_ClampsToZero()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Fade", frameCount: 2);
        chain.Frames[0].Alpha = 30;
        chain.Frames[1].Alpha = 100;

        ctx.AppCommands.SetFrameAlpha(chain.Frames.ToList(), ChannelEdit.Edit(Relative("- 50")));

        chain.Frames[0].Alpha.ShouldBe(0);
        chain.Frames[1].Alpha.ShouldBe(50);
    }

    [Fact]
    public void SetFrameAlpha_Keep_LeavesEachFrameAlone()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Fade", frameCount: 2);
        chain.Frames[0].Alpha = 30;
        chain.Frames[1].Alpha = 100;

        ctx.AppCommands.SetFrameAlpha(chain.Frames.ToList(), ChannelEdit.Keep);

        chain.Frames[0].Alpha.ShouldBe(30);
        chain.Frames[1].Alpha.ShouldBe(100);
        ctx.UndoManager.CanUndo.ShouldBeFalse();
    }
}
