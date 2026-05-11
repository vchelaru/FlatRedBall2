using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using System.IO;
using System;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Visual regression tests for animation frames that have <see cref="AnimationFrameSave.FrameLength"/> = 0.
///
/// The <see cref="PlaybackController"/> handles zero-duration frames by substituting a 0.1 s
/// fallback so that playback never hangs or crashes. These tests exercise the end-to-end
/// path through <see cref="PreviewControl"/> to confirm that:
///
///   1. A single zero-duration frame still renders its texture (not just blank background).
///   2. A multi-frame chain with all zero-duration frames advances frames correctly.
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
    /// The PlaybackController fallback means this frame is treated as lasting 0.1 s.
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
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
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
    /// A two-frame chain where both frames have FrameLength=0. The PlaybackController
    /// applies a 0.1 s fallback per frame, giving a total cycle of 0.2 s.
    /// Advancing 0.15 s should move to frame 1, and RenderToBitmap should show
    /// frame 1's texture (green), not frame 0's (red).
    /// </summary>
    [AvaloniaFact]
    public void Preview_ZeroDurationMultiFrameChain_Advance_MovesToNextFrame()
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
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "green.png", FrameLength = 0f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
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
            ctrl.Playback.SetChain(chain);
            ctrl.Playback.Play();               // re-enable after PauseAutoPlayback()
            ctrl.Playback.Advance(0.15);        // 0.15 > 0.1 fallback → frame 1

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);

            var px = bm.GetPixel(42, 42);
            // LimeGreen = (50, 205, 50). Check green dominates and blue channel is also low.
            Assert.True(px.Green > 150 && px.Blue < 100,
                $"After advancing past frame 0's fallback duration, centre should be green; R={px.Red} G={px.Green} B={px.Blue}");
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
    /// The fallback handling must keep the loop well-behaved: no exception,
    /// no infinite loop, and the frame index always stays within valid bounds.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ZeroDurationChain_ManyAdvances_DoesNotCrash()
    {
        var ctx = ResetSingletons();
        var chain = new AnimationChainSave { Name = "ZeroMany" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f, ShapeCollectionSave = new ShapeCollectionSave() });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f, ShapeCollectionSave = new ShapeCollectionSave() });

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctrl.Playback.SetChain(chain);
        ctrl.Playback.Play();

        // Simulate 100 16 ms timer ticks (≈ 1.6 s total)
        for (int i = 0; i < 100; i++)
            ctrl.Playback.Advance(0.016);

        // No assertion needed beyond "no exception"; index must still be in range
        Assert.InRange(ctrl.Playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
    }

    /// <summary>
    /// A mix: frame 0 has a normal duration (0.1 s) and frame 1 has FrameLength=0
    /// (which falls back to 0.1 s). Advancing just past frame 1's window should
    /// loop back to frame 0 and render frame 0's texture (red), not frame 1's.
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
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "blue.png", FrameLength = 0f,   // zero-duration frame 1
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
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
            ctrl.Playback.SetChain(chain);
            ctrl.Playback.Play();

            // Total cycle = 0.1 + 0.1(fallback) = 0.2 s
            // Advance 0.22 s → wraps to animTime = 0.02 → frame 0 again
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
