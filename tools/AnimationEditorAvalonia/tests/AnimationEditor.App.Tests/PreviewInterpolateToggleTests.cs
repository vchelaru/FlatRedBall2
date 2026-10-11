using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for the preview-only "Interpolate" toggle (<see cref="PreviewControl.InterpolateOffsets"/>),
/// which eases the sprite between frame offsets during playback. It is remembered per chain for the
/// session, in memory only.
/// </summary>
public class PreviewInterpolateToggleTests
{
    private static AnimationChainSave MakeChain(string name)
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        return chain;
    }

    [AvaloniaFact]
    public void InterpolateOffsets_IsOff_ForChainNeverToggled()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctx.SelectedState.SelectedChain = MakeChain("A");
        Dispatcher.UIThread.RunJobs();
        ctrl.SetInterpolateOffsets(true);

        ctx.SelectedState.SelectedChain = MakeChain("B");
        Dispatcher.UIThread.RunJobs();

        Assert.False(ctrl.InterpolateOffsets);
    }

    [AvaloniaFact]
    public void InterpolateOffsets_IsRestored_WhenReturningToChain()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();
        ctrl.SetInterpolateOffsets(true);

        ctx.SelectedState.SelectedChain = MakeChain("B");
        Dispatcher.UIThread.RunJobs();
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();

        Assert.True(ctrl.InterpolateOffsets);
    }

    [AvaloniaFact]
    public void InterpolateOffsets_StaysOn_WhenSelectingFrameInSameChain()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(a);
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();
        ctrl.SetInterpolateOffsets(true);

        // Selecting a frame of the same chain (e.g. to delete it) must not clear the toggle.
        ctx.SelectedState.SelectedFrame = a.Frames[1];
        Dispatcher.UIThread.RunJobs();
        Assert.Same(a, ctx.SelectedState.SelectedChain);

        Assert.True(ctrl.InterpolateOffsets);
    }

    [AvaloniaFact]
    public void ScrubToFrame_Interpolating_ShowsEasedOffset()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        a.Frames[1].RelativeX = 10f;
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(a);
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();
        ctrl.SetInterpolateOffsets(true);

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5f, ctrl.GetFrameOffsetForTest().X, 0.01f);
    }

    [AvaloniaFact]
    public void ScrubToFrame_NotInterpolating_ShowsFrameOffset()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        a.Frames[1].RelativeX = 10f;
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(a);
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0f, ctrl.GetFrameOffsetForTest().X, 0.01f);
    }

    [AvaloniaFact]
    public void InterpolateOffsets_GroupWithMixedSettings_IsNull()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        var b = MakeChain("B");
        ctx.SelectedState.SelectedChain = a;
        Dispatcher.UIThread.RunJobs();
        ctrl.SetInterpolateOffsets(true);

        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        Assert.Null(ctrl.InterpolateOffsets);
    }

    [AvaloniaFact]
    public void SetInterpolateOffsets_GroupSelected_AppliesToEveryChain()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var a = MakeChain("A");
        var b = MakeChain("B");
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        ctrl.SetInterpolateOffsets(true);

        Assert.True(ctrl.InterpolateOffsets);
        ctx.SelectedState.SelectedChain = b;
        Dispatcher.UIThread.RunJobs();
        Assert.True(ctrl.InterpolateOffsets);
    }
}
