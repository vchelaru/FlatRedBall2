using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Multi-select preview compositing (#576): 2+ selected AnimationChains each get their own
/// independent PlaybackController, drawn back-to-front in selection (click) order.
/// </summary>
public class GroupPreviewPlaybackTests
{
    private static AnimationChainSave MakeChain(string name, int frameCount, float frameLength = 0.1f)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"{name}_{i}.png", FrameLength = frameLength, ShapesSave = new ShapesSave() });
        return chain;
    }

    [AvaloniaFact]
    public void IsGroupPreviewActive_TwoChainsSelected_IsTrue()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 2);

        var ctrl = ctx.CreatePreviewControl();
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        Assert.True(ctrl.IsGroupPreviewActive);
        Assert.Equal(2, ctrl.GroupTracks.Count);
    }

    [AvaloniaFact]
    public void GroupTracks_ReturnsChainsInSelectionClickOrder()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 2);

        var ctrl = ctx.CreatePreviewControl();
        // Selected B first, then A — click order is [B, A], the reverse of declaration order.
        ctx.SelectedState.SelectedNodes = new List<object> { b, a };
        Dispatcher.UIThread.RunJobs();

        var tracks = ctrl.GroupTracks;
        Assert.Same(b, tracks[0].Chain);
        Assert.Same(a, tracks[1].Chain);
    }

    /// <summary>
    /// Each track's PlaybackController is independent: advancing time moves a 2-frame chain to its
    /// second frame while a 4-frame chain (same per-frame length) is still on its first.
    /// </summary>
    [AvaloniaFact]
    public void GroupTracks_DifferentFrameLengths_AdvanceIndependently()
    {
        var ctx = TestHelpers.BuildServices();
        var shortChain = MakeChain("Short", 2, frameLength: 0.1f);
        var longChain = MakeChain("Long", 4, frameLength: 0.1f);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctx.SelectedState.SelectedNodes = new List<object> { shortChain, longChain };
        Dispatcher.UIThread.RunJobs();

        foreach (var (_, playback) in ctrl.GroupTracks)
        {
            playback.Play();
            playback.Advance(0.15); // past frame 0 (0.1s) for both chains
        }

        var shortTrack = ctrl.GroupTracks.First(t => t.Chain == shortChain);
        var longTrack = ctrl.GroupTracks.First(t => t.Chain == longChain);
        Assert.Equal(1, shortTrack.Playback.CurrentFrameIndex);
        Assert.Equal(1, longTrack.Playback.CurrentFrameIndex);

        // Advance further: the 2-frame chain wraps back to 0, the 4-frame chain keeps progressing.
        foreach (var (_, playback) in ctrl.GroupTracks)
            playback.Advance(0.1);

        Assert.Equal(0, shortTrack.Playback.CurrentFrameIndex); // wrapped
        Assert.Equal(2, longTrack.Playback.CurrentFrameIndex);  // still progressing
    }

    /// <summary>
    /// Scrubbing one track seeks only that track's controller but pauses every group track in
    /// place (#576 scope item 6), and leaves the singular SelectedChain/SelectedFrame untouched.
    /// </summary>
    [AvaloniaFact]
    public void ScrubGroupTrack_SeeksOnlyThatChain_ButPausesAllTracks()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 3);
        var b = MakeChain("B", 3);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        foreach (var (_, playback) in ctrl.GroupTracks) playback.Play();

        ctrl.ScrubGroupTrack(b, frameIndex: 2, fraction: 0.5);

        var trackA = ctrl.GroupTracks.First(t => t.Chain == a);
        var trackB = ctrl.GroupTracks.First(t => t.Chain == b);
        Assert.Equal(0, trackA.Playback.CurrentFrameIndex); // untouched, just paused
        Assert.False(trackA.Playback.IsPlaying);
        Assert.Equal(2, trackB.Playback.CurrentFrameIndex); // seeked
        Assert.False(trackB.Playback.IsPlaying);

        Assert.Null(ctx.SelectedState.SelectedFrame); // singular selection left untouched
    }

    /// <summary>
    /// The transport Play/Pause resumes every group track together, overriding an
    /// individually-scrubbed pause (#576 scope item 7).
    /// </summary>
    [AvaloniaFact]
    public void TogglePlayPause_ResumesEveryGroupTrackTogether()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 3);
        var b = MakeChain("B", 3);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        foreach (var (_, playback) in ctrl.GroupTracks) playback.Play();
        ctrl.ScrubGroupTrack(a, frameIndex: 1, fraction: 0); // pauses both tracks

        ctrl.TogglePlayPause(); // global resume

        Assert.True(ctrl.GroupTracks.All(t => t.Playback.IsPlaying));
    }
}
