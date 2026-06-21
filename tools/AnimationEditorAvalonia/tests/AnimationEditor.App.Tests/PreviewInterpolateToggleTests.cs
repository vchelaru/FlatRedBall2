using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for the preview-only "Interpolate" toggle (<see cref="PreviewControl.InterpolateOffsets"/>),
/// which eases the sprite between frame offsets during playback. It is transient: selecting a
/// different chain clears it.
/// </summary>
public class PreviewInterpolateToggleTests
{
    [AvaloniaFact]
    public void InterpolateOffsets_ResetsToFalse_WhenSelectionChanges()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();

        var chain = new AnimationChainSave { Name = "A" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        ctx.SelectedState.SelectedChain = chain;
        Dispatcher.UIThread.RunJobs();

        ctrl.InterpolateOffsets = true;

        // Selecting a different chain must clear the transient interpolate toggle.
        var other = new AnimationChainSave { Name = "B" };
        other.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        ctx.SelectedState.SelectedChain = other;
        Dispatcher.UIThread.RunJobs();

        Assert.False(ctrl.InterpolateOffsets);
    }
}
