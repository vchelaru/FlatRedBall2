using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
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
        ctrl.InterpolateOffsets = true;

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
        ctrl.InterpolateOffsets = true;

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
        ctrl.InterpolateOffsets = true;

        // Selecting a frame of the same chain (e.g. to delete it) must not clear the toggle.
        ctx.SelectedState.SelectedFrame = a.Frames[1];
        Dispatcher.UIThread.RunJobs();
        Assert.Same(a, ctx.SelectedState.SelectedChain);

        Assert.True(ctrl.InterpolateOffsets);
    }
}
