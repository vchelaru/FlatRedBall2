using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.IO;
using System;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Visual regression tests for animation frames that have <see cref="AnimationFrameSave.FrameLength"/> = 0.
///
/// The <see cref="PlaybackController"/> skips zero-duration frames, as the game does, and never hangs
/// or crashes. These tests exercise the end-to-end
/// path through <see cref="PreviewControl"/> to confirm that:
///
///   1. A single zero-duration frame still renders its texture (not just blank background).
///   2. A multi-frame chain with all zero-duration frames shows its last frame, as the game does.
///   3. Repeated advances (simulating timer ticks) never crash, even with all-zero durations.
///
/// Important pattern: after calling <c>ctrl.PauseAutoPlayback()</c>, the playback state is
/// paused, so <c>ctrl.Playback.Play()</c> must be called before any <c>Advance()</c> call.
/// </summary>
public class ZeroDurationFrameVisualTests
{
    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = null;
        ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier = 1f;
        return ctx;
    }

    private static void WriteColorPng(string path, SKColor color, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    /// <summary>
    /// A single frame with FrameLength=0 must still render the texture.
    /// Since there is only one frame, playback stays on frame 0 and the texture
    /// should be visible at the canvas centre.
    /// </summary>
    [AvaloniaFact]
    public void Preview_SingleZeroDurationFrame_RendersTexture()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"), SKColors.Red, size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName      = "red.png",
                FrameLength      = 0f,          // zero duration — the critical case
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 16f, BottomCoordinate = 16f,
                ShapesSave = new ShapesSave()
            };

            var chain = new AnimationChainSave { Name = "ZeroTest" };
            chain.Frames.Add(frame);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreatePreviewControl();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // At zoom=1 a 16×16 texture sits in screen rect (34,34,50,50).
            // Center (42,42) must be red, not the dark background.
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Red > 150,
                $"Zero-duration frame should still render red texture at centre; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A two-frame chain where both frames have FrameLength=0 has no length. Playback shows
    /// its last frame, as the game does: frame 1's texture (green), not frame 0's (red).
    /// </summary>
    [AvaloniaFact]
    public void Preview_ZeroDurationMultiFrameChain_ShowsTheLastFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),   SKColors.Red,       size: 16);
            WriteColorPng(Path.Combine(dir, "green.png"), SKColors.LimeGreen, size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame0 = new AnimationFrameSave
            {
                TextureName      = "red.png",  FrameLength = 0f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 16f, BottomCoordinate = 16f,
                ShapesSave = new ShapesSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "green.png", FrameLength = 0f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 16f, BottomCoordinate = 16f,
                ShapesSave = new ShapesSave()
            };

            var chain = new AnimationChainSave { Name = "ZeroMulti" };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;
            ctx.SelectedState.SelectedChain = chain;
            // No SelectedFrame — lets playback drive the frame index

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
            ctrl.Playback.Play();               // re-enable after PauseAutoPlayback()
            ctrl.Playback.Advance(0.15);

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);

            var px = bm.GetPixel(42, 42);
            // LimeGreen = (50, 205, 50). Check green dominates and blue channel is also low.
            Assert.True(px.Green > 150 && px.Blue < 100,
                $"The last frame of a chain of no length: centre should be green; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Simulates 100 timer ticks on a chain where both frames have FrameLength=0.
    /// The loop must be well-behaved: no exception,
    /// no infinite loop, and the frame index always stays within valid bounds.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ZeroDurationChain_ManyAdvances_DoesNotCrash()
    {
        var ctx = ResetSingletons();
        var chain = new AnimationChainSave { Name = "ZeroMany" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f, ShapesSave = new ShapesSave() });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f, ShapesSave = new ShapesSave() });

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctrl.Playback.SetChain(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
        ctrl.Playback.Play();

        // Simulate 100 16 ms timer ticks (≈ 1.6 s total)
        for (int i = 0; i < 100; i++)
            ctrl.Playback.Advance(0.016);

        // No assertion needed beyond "no exception"; index must still be in range
        Assert.InRange(ctrl.Playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
    }

    /// <summary>
    /// A mix: frame 0 has a normal duration (0.1 s) and frame 1 has FrameLength=0
    /// (which is skipped). Playback should always render frame 0's texture (red), not frame 1's.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ZeroDurationSecondFrame_LoopBackToFirstFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),   SKColors.Red,       size: 16);
            WriteColorPng(Path.Combine(dir, "blue.png"),  SKColors.Blue,      size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame0 = new AnimationFrameSave
            {
                TextureName      = "red.png",  FrameLength = 0.1f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 16f, BottomCoordinate = 16f,
                ShapesSave = new ShapesSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "blue.png", FrameLength = 0f,   // zero-duration frame 1
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 16f, BottomCoordinate = 16f,
                ShapesSave = new ShapesSave()
            };

            var chain = new AnimationChainSave { Name = "MixDuration" };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);
            ctrl.Playback.Play();

            // The cycle is frame 0's 0.1 s, so 0.22 s is 0.02 s into it.
            ctrl.Playback.Advance(0.22);

            Assert.Equal(0, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Red > 150 && px.Blue < 50,
                $"After looping past zero-duration frame 1, should render frame 0 (red); R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }
}
