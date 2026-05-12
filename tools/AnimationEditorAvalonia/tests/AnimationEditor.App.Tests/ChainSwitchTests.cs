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
/// Tests that <see cref="PreviewControl"/> correctly reacts when the selected animation
/// chain changes after the control has already been constructed and subscribed to events.
///
/// The risk: <see cref="SelectedState.SelectedChain"/> fires <see cref="SelectedState.SelectionChanged"/>
/// synchronously, but <see cref="PreviewControl"/> handles it via
/// <c>Dispatcher.UIThread.InvokeAsync</c>. If the dispatcher is not flushed, the
/// <see cref="PlaybackController"/> retains the old chain and <see cref="PlaybackController.CurrentFrameIndex"/>
/// can point to a frame that doesn't exist in the new chain. Although the preview clamps the index, this
/// means the wrong frame is shown — a silent, hard-to-notice display bug.
///
/// The tests cover:
///   1. Direct API path: <c>ctrl.Playback.SetChain()</c> resets frame index and shows new chain.
///   2. Event path: <c>SelectedState.SelectedChain = newChain</c> → <c>RunJobs()</c> → frame 0 of new chain.
///   3. Switch to null: preview shows dark background.
///   4. SpeedMultiplier property binding: the PreviewControl property delegates to the playback controller.
///
/// Key patterns:
///   • After <c>PauseAutoPlayback()</c>, always call <c>Play()</c> before <c>Advance()</c>.
///   • After setting <c>SelectedState.SelectedChain</c>, call <c>Dispatcher.UIThread.RunJobs()</c>
///     to flush the <c>InvokeAsync</c> callback so the playback controller is updated.
/// </summary>
public class ChainSwitchTests
{
    private static void ResetSingletons()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName = null;
        SelectedState.Self.SelectedChain = null;
        SelectedState.Self.SelectedFrame = null;
        SelectedState.Self.SelectedNodes = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread = a => a();
        AppCommands.Self.FileDialogService = NullFileDialogService.Instance;
        AppState.Self.OffsetMultiplier = 1f;
    }

    private static void WriteColorPng(string path, SKColor color, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static AnimationFrameSave MakeFrame(string textureName, float length = 0.1f) =>
        new AnimationFrameSave
        {
            TextureName      = textureName, FrameLength = length,
            LeftCoordinate   = 0f, TopCoordinate    = 0f,
            RightCoordinate  = 1f, BottomCoordinate = 1f,
            ShapeCollectionSave = new ShapeCollectionSave()
        };

    // ── Direct API path ───────────────────────────────────────────────────────

    /// <summary>
    /// Using the direct <c>ctrl.Playback.SetChain()</c> API: advance chain A to frame 1,
    /// then switch to chain B. After <c>SetChain(chainB)</c> the frame index resets to 0
    /// and <see cref="PreviewControl.RenderToBitmap"/> must show chain B's frame 0 texture.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ChainSwitch_DirectReset_ShowsNewChainFrame0()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),    SKColors.Red,       size: 16);
            WriteColorPng(Path.Combine(dir, "lime.png"),   SKColors.LimeGreen, size: 16);
            WriteColorPng(Path.Combine(dir, "blue.png"),   SKColors.Blue,      size: 16);
            WriteColorPng(Path.Combine(dir, "yellow.png"), SKColors.Yellow,    size: 16);
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var chainA = new AnimationChainSave { Name = "ChainA" };
            chainA.Frames.Add(MakeFrame("red.png"));
            chainA.Frames.Add(MakeFrame("lime.png"));

            var chainB = new AnimationChainSave { Name = "ChainB" };
            chainB.Frames.Add(MakeFrame("blue.png"));
            chainB.Frames.Add(MakeFrame("yellow.png"));

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chainA);
            acls.AnimationChains.Add(chainB);
            ProjectManager.Self.AnimationChainListSave = acls;

            SelectedState.Self.SelectedChain = chainA;

            var ctrl = new PreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chainA);
            ctrl.Playback.Play();
            ctrl.Playback.Advance(0.15);    // now at frame 1 (lime)

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);

            // Switch to chain B via direct API and update SelectedState
            SelectedState.Self.SelectedChain = chainB;
            ctrl.Playback.SetChain(chainB); // direct reset — bypasses dispatcher
            ctrl.Playback.Play();

            Assert.Equal(0, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Frame 0 of chain B is blue
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Blue > 150 && px.Red < 50,
                $"After switching to chain B frame 0 should be blue; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Event path (SelectedState → dispatcher → SetChain) ───────────────────

    /// <summary>
    /// The production code path: the user clicks a different chain in the tree view,
    /// which sets <c>SelectedState.SelectedChain</c>. That fires <c>SelectionChanged</c>
    /// synchronously, which enqueues <c>OnSelectionChanged</c> via <c>InvokeAsync</c>.
    ///
    /// After <c>Dispatcher.UIThread.RunJobs()</c> the playback controller must have been
    /// reset to chain B frame 0, and <c>RenderToBitmap</c> must render chain B frame 0.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ChainSwitch_SelectionEventPath_ResetsPlaybackAndRendersFrame0()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),  SKColors.Red,  size: 16);
            WriteColorPng(Path.Combine(dir, "blue.png"), SKColors.Blue, size: 16);
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var chainA = new AnimationChainSave { Name = "ChainA" };
            chainA.Frames.Add(MakeFrame("red.png"));
            chainA.Frames.Add(MakeFrame("red.png"));  // two identical frames so we can advance

            var chainB = new AnimationChainSave { Name = "ChainB" };
            chainB.Frames.Add(MakeFrame("blue.png")); // frame 0 = blue
            chainB.Frames.Add(MakeFrame("red.png"));  // frame 1 = red (different)

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chainA);
            acls.AnimationChains.Add(chainB);
            ProjectManager.Self.AnimationChainListSave = acls;

            // Create control FIRST so its SelectionChanged subscription is active
            var ctrl = new PreviewControl();
            ctrl.PauseAutoPlayback();

            // Now set chain A — fires SelectionChanged, which the control handles via InvokeAsync
            SelectedState.Self.SelectedChain = chainA;
            Dispatcher.UIThread.RunJobs();  // flush → OnSelectionChanged → _playback.SetChain(chainA)

            ctrl.Playback.Play();
            ctrl.Playback.Advance(0.15);    // advance to frame 1 of chain A

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);

            // Production-style switch: change SelectedState only (no direct Playback call)
            SelectedState.Self.SelectedChain = chainB;
            Dispatcher.UIThread.RunJobs();  // flush OnSelectionChanged → SetChain(chainB)

            // After the dispatcher flush the playback controller must be at frame 0
            Assert.Equal(0, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Chain B frame 0 is blue
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Blue > 150 && px.Red < 50,
                $"After event-driven chain switch, frame 0 of chain B should be blue; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Long chain → short chain stale-index scenario: chain A has 3 frames and playback
    /// is at frame 2. Switching to chain B (2 frames) WITHOUT flushing the dispatcher
    /// leaves <c>_playback.CurrentFrameIndex</c> = 2. The preview must clamp to
    /// <c>chain.Frames.Count - 1 = 1</c> rather than throwing an <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ChainSwitch_StaleIndex_ClampsWithoutException()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),  SKColors.Red,  size: 16);
            WriteColorPng(Path.Combine(dir, "blue.png"), SKColors.Blue, size: 16);
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var chainA = new AnimationChainSave { Name = "Long" };
            chainA.Frames.Add(MakeFrame("red.png"));
            chainA.Frames.Add(MakeFrame("red.png"));
            chainA.Frames.Add(MakeFrame("red.png"));    // 3 frames, indices 0-2

            var chainB = new AnimationChainSave { Name = "Short" };
            chainB.Frames.Add(MakeFrame("blue.png"));
            chainB.Frames.Add(MakeFrame("red.png"));    // 2 frames, indices 0-1

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chainA);
            acls.AnimationChains.Add(chainB);
            ProjectManager.Self.AnimationChainListSave = acls;

            SelectedState.Self.SelectedChain = chainA;

            var ctrl = new PreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chainA);
            ctrl.Playback.Play();
            // Advance to frame 2 (last frame of chain A, index = 2)
            ctrl.Playback.Advance(0.25);

            Assert.Equal(2, ctrl.Playback.CurrentFrameIndex);

            // Switch to chain B through SelectedState but do NOT flush the dispatcher.
            // This simulates the window between the selection change and the async callback.
            SelectedState.Self.SelectedChain = chainB;
            // _playback.CurrentFrameIndex is still 2 (stale), but RenderToBitmap must
            // clamp it to chainB.Frames.Count - 1 = 1 without crashing.
            var ex = Record.Exception(() =>
            {
                using var bm = ctrl.RenderToBitmap(64, 64);
                // Any pixel read is fine; we're just verifying no exception is thrown.
                _ = bm.GetPixel(42, 42);
            });

            Assert.Null(ex);
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Null chain ────────────────────────────────────────────────────────────

    /// <summary>
    /// Switching to a null chain (e.g., user deselects everything in the tree view)
    /// must render only the dark background — no texture, no crash.
    /// </summary>
    [AvaloniaFact]
    public void Preview_ChainSwitch_ToNull_RendersBackground()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"), SKColors.Red, size: 16);
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var chain = new AnimationChainSave { Name = "A" };
            chain.Frames.Add(MakeFrame("red.png"));

            SelectedState.Self.SelectedChain = chain;

            var ctrl = new PreviewControl();
            ctrl.PauseAutoPlayback();

            // Verify red texture is rendered with chain set
            using (var bmWithChain = ctrl.RenderToBitmap(64, 64))
            {
                var px = bmWithChain.GetPixel(42, 42);
                Assert.True(px.Red > 150,
                    $"Sanity: with chain set, centre should be red; R={px.Red}");
            }

            // Now switch to null
            SelectedState.Self.SelectedChain = null;
            Dispatcher.UIThread.RunJobs();

            using var bm = ctrl.RenderToBitmap(64, 64);
            var bgPx = bm.GetPixel(42, 42);

            // Background colour is (30, 30, 30). All channels should be dark.
            Assert.True(bgPx.Red < 50 && bgPx.Green < 50 && bgPx.Blue < 50,
                $"Null chain should render only background; R={bgPx.Red} G={bgPx.Green} B={bgPx.Blue}");
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── SpeedMultiplier property binding ─────────────────────────────────────

    /// <summary>
    /// <see cref="PreviewControl.SpeedMultiplier"/> is a simple pass-through to
    /// <see cref="PlaybackController.SpeedMultiplier"/>. Setting it must:
    ///   1. Be reflected immediately in <c>ctrl.Playback.SpeedMultiplier</c>.
    ///   2. Cause <c>Advance(dt)</c> to consume dt×speed seconds of animation time,
    ///      so at 2× speed, advancing 0.075 s consumes 0.15 s of animation and
    ///      moves past a 0.1 s frame.
    /// </summary>
    [AvaloniaFact]
    public void Preview_SpeedMultiplier_PropertyBinding_DelegatesToPlayback()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),  SKColors.Red,       size: 16);
            WriteColorPng(Path.Combine(dir, "lime.png"), SKColors.LimeGreen, size: 16);
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var chain = new AnimationChainSave { Name = "SpeedTest" };
            chain.Frames.Add(MakeFrame("red.png",  0.1f));
            chain.Frames.Add(MakeFrame("lime.png", 0.1f));

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ProjectManager.Self.AnimationChainListSave = acls;
            SelectedState.Self.SelectedChain = chain;

            var ctrl = new PreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chain);

            // Set 2× speed via the PreviewControl property
            ctrl.SpeedMultiplier = 2.0;

            // Assert delegation is immediate
            Assert.Equal(2.0, ctrl.Playback.SpeedMultiplier);

            ctrl.Playback.Play();
            // 0.075 s real time × 2.0 = 0.15 s animation time > 0.1 s frame 0 duration
            ctrl.Playback.Advance(0.075);

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);

            using var bm = ctrl.RenderToBitmap(64, 64);

            var px = bm.GetPixel(42, 42);
            // LimeGreen = (50, 205, 50). Check green dominates and blue channel is also low.
            Assert.True(px.Green > 150 && px.Blue < 100,
                $"At 2× speed, 0.075 s should advance to frame 1 (lime); R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }
}
