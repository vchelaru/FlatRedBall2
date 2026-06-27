using AnimationEditor.App.Controls;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.ObjectModel;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Transport button + click-drag timeline scrubbing (#432). The scrub itself maps an x-position
/// to a frame via <c>TimelineScrubMapper</c> (unit-tested separately); these verify the
/// MainWindow/PreviewControl wiring: pausing, frame selection, and the no-snap playhead offset.
/// </summary>
public class TimelinePlaybackControlsTests
{
    private static AnimationChainSave MakeThreeFrameChain()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        return chain;
    }

    private static (MainWindow window, PreviewControl preview, ObservableCollection<TimelineFrameVm> items)
        ShowWindowWithChain(TestServices ctx, AnimationChainSave chain)
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs(); // let OnOpened settle — it resets the document to an empty one

        // Assign AFTER OnOpened so it isn't replaced; SelectedFrame's FindChainForFrame needs the
        // chain present in the project for the parent-chain lookup to succeed.
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.SelectedState.SelectedChain = chain;
        Dispatcher.UIThread.RunJobs();

        var timeline = window.FindControl<ItemsControl>("TimelineStrip")
            ?? throw new InvalidOperationException("TimelineStrip not found");
        var preview = window.FindControl<PreviewControl>("PreviewCtrl")
            ?? throw new InvalidOperationException("PreviewCtrl not found");
        var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
        return (window, preview, items);
    }

    [AvaloniaFact]
    public void ScrubToFrame_MidFrame_PausesSelectsFrameAndKeepsSubFrameOffset()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeThreeFrameChain();
        var (window, preview, items) = ShowWindowWithChain(ctx, chain);

        try
        {
            // Scrub to the middle of frame 1 (fraction 0.5).
            preview.ScrubToFrame(1, 0.5);
            Dispatcher.UIThread.RunJobs();

            Assert.False(preview.IsPlaying);                          // scrubbing pauses
            Assert.Same(chain.Frames[1], ctx.SelectedState.SelectedFrame); // and selects that frame
            Assert.True(items[1].IsCurrent);
            // The playhead must NOT snap to the cell's left edge — it stays at the clicked position.
            Assert.True(items[1].ScrubberOffset > 0);
            Assert.True(items[1].ScrubberOffset < items[1].Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingChain_AutoPlays_EvenWhenPreviouslyPaused()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeThreeFrameChain();
        var (window, preview, _) = ShowWindowWithChain(ctx, chain);

        try
        {
            ctx.SelectedState.SelectedFrame = chain.Frames[1]; // pauses on a frame
            Dispatcher.UIThread.RunJobs();
            Assert.False(preview.IsPlaying);

            ctx.SelectedState.SelectedChain = chain; // re-selecting the whole animation auto-plays
            Dispatcher.UIThread.RunJobs();

            Assert.True(preview.IsPlaying);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingFrame_PausesAtFrameStart()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeThreeFrameChain();
        var (window, preview, _) = ShowWindowWithChain(ctx, chain);

        try
        {
            ctx.SelectedState.SelectedFrame = chain.Frames[2];
            Dispatcher.UIThread.RunJobs();

            Assert.False(preview.IsPlaying);
            Assert.Equal(2, preview.Playback.CurrentFrameIndex);
            Assert.Equal(0.0, preview.Playback.FrameElapsed, precision: 6); // start of the frame
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TogglePlayPause_WhilePausedOnScrubbedFrame_ResumesFromPlayhead()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeThreeFrameChain();
        var (window, preview, _) = ShowWindowWithChain(ctx, chain);

        try
        {
            preview.PauseAutoPlayback();          // stop the auto timer for determinism
            preview.ScrubToFrame(1, 0.5);         // paused, mid frame 1
            Dispatcher.UIThread.RunJobs();

            preview.TogglePlayPause();            // resume
            Dispatcher.UIThread.RunJobs();

            Assert.True(preview.IsPlaying);
            Assert.Null(ctx.SelectedState.SelectedFrame);          // un-pinned
            Assert.Equal(1, preview.Playback.CurrentFrameIndex);   // resumed from playhead, not reset to 0
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TogglePlayPause_WhilePlaying_PausesAndPinsCurrentFrame()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeThreeFrameChain();
        var (window, preview, _) = ShowWindowWithChain(ctx, chain);

        try
        {
            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();
            preview.Playback.Advance(0.15);       // into frame 1
            Assert.Equal(1, preview.Playback.CurrentFrameIndex);

            preview.TogglePlayPause();            // pause
            Dispatcher.UIThread.RunJobs();

            Assert.False(preview.IsPlaying);
            Assert.Same(chain.Frames[1], ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }
}
