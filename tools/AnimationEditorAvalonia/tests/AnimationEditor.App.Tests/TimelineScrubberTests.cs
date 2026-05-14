using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

public class TimelineScrubberTests
{
    [AvaloniaFact]
    public void TimelineScrubber_FollowsPlaybackFrame_WhenChainSelected()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;

        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timeline = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var preview = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")
                ?? throw new InvalidOperationException("PreviewCtrl not found");

            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
            Assert.Equal(2, items.Count);

            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();
            Dispatcher.UIThread.RunJobs();

            Assert.True(items[0].IsCurrent);
            Assert.False(items[1].IsCurrent);

            preview.Playback.Advance(0.15);
            Dispatcher.UIThread.RunJobs();

            Assert.False(items[0].IsCurrent);
            Assert.True(items[1].IsCurrent);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrubberOffset_AdvancesMidFrame_UpdatesToProportionalPosition()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;

        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timeline = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var preview = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")
                ?? throw new InvalidOperationException("PreviewCtrl not found");

            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
            Assert.Equal(2, items.Count);

            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();
            Dispatcher.UIThread.RunJobs();

            // Derive the effective PPS from the cell width (cell = frameDuration * effectivePps).
            double effectivePps = items[0].Width / 0.1;

            // Advance halfway through frame 0.
            const double elapsed = 0.05;
            preview.Playback.Advance(elapsed);

            // ScrubberOffset is updated synchronously from PlaybackTicked — no RunJobs needed
            double expectedOffset = elapsed * effectivePps;
            Assert.Equal(expectedOffset, items[0].ScrubberOffset, precision: 6);
            Assert.True(items[0].ScrubberOffset > 0);
            Assert.True(items[0].ScrubberOffset < items[0].Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrubberOffset_IsZero_AfterReset()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;

        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timeline = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var preview = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")
                ?? throw new InvalidOperationException("PreviewCtrl not found");

            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);

            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();
            Dispatcher.UIThread.RunJobs();

            preview.Playback.Advance(0.05); // advance mid-frame
            Assert.True(items[0].ScrubberOffset > 0);

            preview.Playback.Reset(); // reset should bring offset back to 0

            Assert.Equal(0.0, items[0].ScrubberOffset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrubberOffset_ClampedToTravelWidth_AtEndOfFrame()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;

        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timeline = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var preview = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")
                ?? throw new InvalidOperationException("PreviewCtrl not found");

            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);

            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();
            Dispatcher.UIThread.RunJobs();

            // Advance just before the frame boundary — elapsed * PixelsPerSecond would exceed travelWidth
            preview.Playback.Advance(0.0999);

            double travelWidth = items[0].Width - TimelineFrameVm.PlayheadWidth;
            // Offset must not exceed travel width (right edge of playhead == right edge of cell)
            Assert.True(items[0].ScrubberOffset <= travelWidth + 1e-9);
            Assert.True(items[0].ScrubberOffset >= travelWidth * 0.9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbnail_IsAvaloniaBitmap_WhenFrameHasValidTexture()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var pngPath = Path.Combine(dir, "sprite.png");
        using (var bm = new SKBitmap(64, 64))
        {
            bm.Erase(SKColors.Red);
            using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(pngPath, data.ToArray());
        }

        try
        {
            var ctx = TestHelpers.BuildServices();
            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName         = pngPath,
                FrameLength         = 0.1f,
                LeftCoordinate      = 0f,
                TopCoordinate       = 0f,
                RightCoordinate     = 1f,
                BottomCoordinate    = 1f,
                ShapesSave          = new ShapesSave()
            });
            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;

            var window = ctx.CreateMainWindow();
            window.Show();

            try
            {
                ctx.SelectedState.SelectedChain = chain;
                Dispatcher.UIThread.RunJobs();

                var timeline = window.FindControl<ItemsControl>("TimelineStrip")
                    ?? throw new InvalidOperationException("TimelineStrip not found");
                var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
                Assert.Single(items);
                Assert.IsType<Bitmap>(items[0].Thumbnail);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
