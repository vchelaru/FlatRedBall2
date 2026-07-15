using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless visual-render tests for <see cref="WireframeControl"/> and
/// <see cref="PreviewControl"/>.
///
/// These tests exercise the agent-visible rendering APIs that let AI agents
/// "see" what's on screen without a live UI:
///   • <c>RenderToBitmap</c>   – off-screen pixel-level capture
///   • <c>GetFrameRects</c>    – texture-space frame bounding boxes
///   • <c>PauseAutoPlayback</c> / <c>Playback.Advance</c> – deterministic frame stepping
///
/// All tests run on the headless Avalonia UI thread via [AvaloniaFact].
/// </summary>
public class VisualRenderTests
{
    // ── Test helpers ──────────────────────────────────────────────────────────

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

    /// <summary>
    /// Writes a solid-color PNG of the requested size to <paramref name="path"/>.
    /// Uses SkiaSharp directly to avoid any dependency on the controls under test.
    /// </summary>
    private static void WriteColorPng(string path, SKColor color, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    /// <summary>
    /// Writes a PNG split vertically down the middle: left half is
    /// <paramref name="leftColor"/>, right half is <paramref name="rightColor"/>.
    /// Used by flip-horizontal visual tests.
    /// </summary>
    private static void WriteHSplitPng(string path,
        SKColor leftColor, SKColor rightColor, int width = 16, int height = 16)
    {
        using var bm  = new SKBitmap(width, height);
        int mid = width / 2;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bm.SetPixel(x, y, x < mid ? leftColor : rightColor);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    /// <summary>
    /// Writes a PNG split horizontally across the middle: top half is
    /// <paramref name="topColor"/>, bottom half is <paramref name="bottomColor"/>.
    /// Used by flip-vertical visual tests.
    /// </summary>
    private static void WriteVSplitPng(string path,
        SKColor topColor, SKColor bottomColor, int width = 16, int height = 16)
    {
        using var bm  = new SKBitmap(width, height);
        int mid = height / 2;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bm.SetPixel(x, y, y < mid ? topColor : bottomColor);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    // ── WireframeControl — dimensions ─────────────────────────────────────────

    [AvaloniaFact]
    public void WireframeRenderToBitmap_ReturnsRequestedDimensions()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();

        using var bm = ctrl.RenderToBitmap(320, 240);

        Assert.Equal(320, bm.Width);
        Assert.Equal(240, bm.Height);
    }

    // ── WireframeControl — background color ───────────────────────────────────

    [AvaloniaFact]
    public void WireframeRenderToBitmap_NoTexture_FillsWithBackgroundColor()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();

        using var bm = ctrl.RenderToBitmap(64, 64);
        var center = bm.GetPixel(32, 32);

        // Background matches BgCanvas design token (#0e0f12)
        Assert.Equal(0x0e, (int)center.Red);
        Assert.Equal(0x0f, (int)center.Green);
        Assert.Equal(0x12, (int)center.Blue);
    }

    // ── WireframeControl — texture rendering ──────────────────────────────────

    [AvaloniaFact]
    public void WireframeRenderToBitmap_WithRedTexture_DrawsTexturePixelAtCenter()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "red.png");
        try
        {
            WriteColorPng(png, SKColors.Red, size: 16);

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.CenterFitForSize(64, 64);

            using var bm = ctrl.RenderToBitmap(64, 64);
            var center = bm.GetPixel(32, 32);

            // The red texture is fitted to the center of the 64×64 bitmap.
            // With CenterFit(16, 16, 64, 64): zoom=3.4, panX=panY≈4.8
            // so the texture covers roughly x=[5,59], y=[5,59] → (32,32) is inside.
            Assert.True(center.Red   > 200, $"Expected red channel >200, got {center.Red}");
            Assert.True(center.Green <  50, $"Expected green <50, got {center.Green}");
            Assert.True(center.Blue  <  50, $"Expected blue <50, got {center.Blue}");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── WireframeControl — frame rects ────────────────────────────────────────

    [AvaloniaFact]
    public void WireframeGetFrameRects_CountMatchesChainFrameCount()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "sheet.png");
        try
        {
            WriteColorPng(png, SKColors.Blue, size: 64);

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 0.5f,
                ShapesSave = new ShapesSave()
            });
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1f,   BottomCoordinate = 0.5f,
                ShapesSave = new ShapesSave()
            });

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();

            // ProjectManager.FileName is null → texture filter is skipped →
            // both frames (with non-empty TextureName) should appear.
            Assert.Equal(2, ctrl.GetFrameRects().Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public void WireframeGetFrameRects_BoundsMatchUvCoordinates()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "sheet.png");
        try
        {
            WriteColorPng(png, SKColors.Green, size: 64);  // 64×64 atlas

            var frame = new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 0.5f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Run" };
            chain.Frames.Add(frame);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);

            // UV (0, 0, 0.5, 0.5) on a 64×64 bitmap → pixel rect (0, 0, 32, 32)
            Assert.Equal(0f,  rects[0].Bounds.Left,   precision: 1);
            Assert.Equal(0f,  rects[0].Bounds.Top,    precision: 1);
            Assert.Equal(32f, rects[0].Bounds.Right,  precision: 1);
            Assert.Equal(32f, rects[0].Bounds.Bottom, precision: 1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── PreviewControl — dimensions ───────────────────────────────────────────

    [AvaloniaFact]
    public void PreviewRenderToBitmap_ReturnsRequestedDimensions()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        using var bm = ctrl.RenderToBitmap(160, 120);

        Assert.Equal(160, bm.Width);
        Assert.Equal(120, bm.Height);
    }

    // ── PreviewControl — playback control ─────────────────────────────────────

    [AvaloniaFact]
    public void Preview_PauseAutoPlayback_FreezesFrameIndex()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();  // stop timer + pause state machine

        var chain = new AnimationChainSave { Name = "Idle" };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave
            {
                FrameLength         = 0.1f,
                ShapesSave = new ShapesSave()
            });
        ctrl.Playback.SetChain(chain);  // resets to frame 0, keeps IsPlaying=false

        int before = ctrl.Playback.CurrentFrameIndex;
        ctrl.Playback.Advance(1.0);     // no-op because IsPlaying=false
        int after  = ctrl.Playback.CurrentFrameIndex;

        Assert.Equal(0,      before);
        Assert.Equal(before, after);
    }

    [AvaloniaFact]
    public void Preview_ManualAdvance_UpdatesFrameIndex()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        var chain = new AnimationChainSave { Name = "Run" };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave
            {
                FrameLength         = 0.1f,  // each frame = 100 ms
                ShapesSave = new ShapesSave()
            });
        ctrl.Playback.SetChain(chain);
        ctrl.Playback.Play();   // re-enable playback with timer still stopped

        // 0.15 s > 0.1 s (frame 0 length) → should have advanced to frame 1
        ctrl.Playback.Advance(0.15);

        Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);
    }

    [AvaloniaFact]
    public void Preview_ResumeAfterPause_RestoresIsPlaying()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();

        ctrl.PauseAutoPlayback();
        Assert.False(ctrl.Playback.IsPlaying);

        ctrl.ResumeAutoPlayback();
        Assert.True(ctrl.Playback.IsPlaying);

        // Clean up: stop the timer again so it doesn't fire after the test.
        ctrl.PauseAutoPlayback();
    }

    // ── "Prove I can see" — pixel-level visual assertions ────────────────────
    //
    // The tests below prove the agent can observe rendered output at a pixel
    // level, not just check structural metadata.  Each one targets a specific
    // visual feature and asserts on the resulting SKBitmap pixels.

    /// <summary>
    /// A selected frame draws a semi-transparent blue fill over the texture.
    /// Sampling a pixel well inside the selected region should reveal a measurable
    /// blue tint — proving the agent can detect the selection highlight colour.
    ///
    /// Math (selected fill = SKColor(80,160,255,45) over white 255,255,255):
    ///   R ≈ 224, G ≈ 238, B = 255  →  Blue > Red by ~31 counts.
    /// </summary>
    [AvaloniaFact]
    public void WireframeSelectedFrame_PixelInsideOverlay_HasBlueTint()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "white.png");
        try
        {
            WriteColorPng(png, SKColors.White, size: 64);

            var frame = new AnimationFrameSave
            {
                TextureName      = "white.png",
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;   // IsSelected=true → fill alpha=45

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.CenterFitForSize(128, 128);
            ctrl.RefreshFrames();

            using var bm = ctrl.RenderToBitmap(128, 128);

            // CenterFit(64,64,128,128): zoom=1.7, pan≈(9.6,9.6).
            // Frame covers screen (9.6,9.6)→(118.4,118.4).  Pixel (30,30) is well inside
            // the frame while being away from the origin crosshair (centred at screen ~(64,64)).
            var px = bm.GetPixel(30, 30);

            // Blue fill on white makes B stay at 255 while R drops to ~224.
            Assert.True(px.Blue > px.Red,
                $"Expected blue > red inside selected-frame overlay; got R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// GetFrameRects reports IsSelected=true only for the frame that matches
    /// SelectedState.SelectedFrame — proving the agent can read selection state
    /// from the control's metadata API.
    ///
    /// Behavioural note: when a single frame is selected the control shows only
    /// that frame (so the agent sees exactly 1 rect with IsSelected=true).
    /// When no frame is selected but a whole chain is (SelectedFrame is null,
    /// SelectedChain is set), every one of the chain's frames is highlighted —
    /// selecting a chain "plays" the same shrink-to-rest reveal a single frame
    /// selection gets (#716). This two-phase assertion verifies both sides of
    /// the selection contract.
    /// </summary>
    [AvaloniaFact]
    public void WireframeGetFrameRects_IsSelectedMatchesSelectedFrameState()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "sheet.png");
        try
        {
            WriteColorPng(png, SKColors.White, size: 64);

            var frame0 = new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1f,   BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);

            // Phase 1: no selected frame, whole chain selected → the control shows every
            // chain frame, all of them highlighted (#716).
#pragma warning disable CS8625
            ctx.SelectedState.SelectedFrame = null;
#pragma warning restore CS8625
            ctrl.RefreshFrames();
            var allRects = ctrl.GetFrameRects();
            Assert.Equal(2, allRects.Count);
            Assert.All(allRects, r => Assert.True(r.IsSelected, "a whole-chain selection highlights every frame"));

            // Phase 2: select frame0 → the control shows only that frame and
            // reports IsSelected=true on it.
            ctx.SelectedState.SelectedFrame = frame0;
            ctrl.RefreshFrames();
            var selectedRects = ctrl.GetFrameRects();
            Assert.Single(selectedRects);
            Assert.True(selectedRects[0].IsSelected, "the selected frame should report IsSelected=true");
            // Bounds should match frame0's UV rect on the 64×64 texture (left half).
            Assert.Equal(0f,  selectedRects[0].Bounds.Left);
            Assert.Equal(32f, selectedRects[0].Bounds.Right, precision: 0);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// Samples a pixel inside the frame overlay and one outside it on the same
    /// white texture row — proving the agent can spatially locate where a frame
    /// region starts and stops by comparing pixel channels.
    ///
    /// Inside (left half):  blue fill lowers R to ~224.
    /// Outside (right half): pure white — R stays at 255.
    /// → insidePixel.Red &lt; outsidePixel.Red.
    /// </summary>
    [AvaloniaFact]
    public void WireframeFrameOverlay_InsidePixelRedIsLowerThanOutside()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "white.png");
        try
        {
            WriteColorPng(png, SKColors.White, size: 64);

            var frame = new AnimationFrameSave
            {
                TextureName      = "white.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 1f,   // left half only
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Half" };
            chain.Frames.Add(frame);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;  // selected → fill alpha=45

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.CenterFitForSize(128, 128);
            ctrl.RefreshFrames();

            using var bm = ctrl.RenderToBitmap(128, 128);

            // CenterFit(64,64,128,128): zoom=1.7, pan=(9.6,9.6).
            // Frame screen rect: left=9.6, right=64.0, top=9.6, bottom=118.4.
            // Pixel (30,64): texture x=(30-9.6)/1.7≈12  → inside  the frame (x<32).
            // Pixel (90,64): texture x=(90-9.6)/1.7≈47  → outside the frame (x>32) but on texture.
            var inside  = bm.GetPixel(30, 64);
            var outside = bm.GetPixel(90, 64);

            Assert.True(inside.Red < outside.Red,
                $"Blue fill should reduce red inside the frame; inside.R={inside.Red} outside.R={outside.Red}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// ShowOrigin draws a 1 px green cross-hair through the canvas centre.
    /// Sampling the intersection pixel proves the agent can detect the origin
    /// crosshair rendered over a plain background.
    ///
    /// Math (crosshair = SKColor(100,200,100,160) over background SKColor(30,30,30)):
    ///   R≈74, G≈137, B≈74  →  green channel is clearly dominant.
    /// </summary>
    [AvaloniaFact]
    public void ShowOrigin_DrawGreenCrossHairAtCenter()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctrl.ShowOrigin = true;   // no chain; crosshair is drawn over dark background

        using var bm = ctrl.RenderToBitmap(64, 64);

        // With pan=(0,0): cx=42, cy=42.  Both guide lines pass through (42,42).
        var center = bm.GetPixel(42, 42);

        Assert.True(center.Green > center.Red,
            $"Guide cross-hair should be green-dominant; G={center.Green} R={center.Red}");
        Assert.True(center.Green > center.Blue,
            $"Guide cross-hair should be green-dominant; G={center.Green} B={center.Blue}");
    }

    /// <summary>
    /// Full end-to-end render of a real texture via PreviewControl.
    /// Creates a 16×16 red PNG on disk, sets up ProjectManager so
    /// ResolveTexturePath can locate it, then verifies the preview renders
    /// the red pixel at the canvas centre — proving the agent can see the
    /// animated frame content.
    ///
    /// Math (default zoom=1, pan=(0,0)): 16×16 frame centred at (42,42)
    ///   → occupies screen (34,34)→(50,50).  Pixel (42,42) is well inside → red.
    /// </summary>
    [AvaloniaFact]
    public void PreviewWithTexture_DrawsRedFrameAtCanvasCenter()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "red.png");
        try
        {
            WriteColorPng(png, SKColors.Red, size: 16);

            // ResolveTexturePath requires FileName to be set so it can resolve
            // the texture path relative to the .achx directory.
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName      = "red.png",
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                FrameLength      = 0.1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;   // pin to this frame

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();

            using var bm = ctrl.RenderToBitmap(64, 64);
            var center = bm.GetPixel(42, 42);

            Assert.True(center.Red   > 200, $"Expected red >200 at center; got R={center.Red}");
            Assert.True(center.Green <  50, $"Expected green <50 at center; got G={center.Green}");
            Assert.True(center.Blue  <  50, $"Expected blue <50 at center; got B={center.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── FlipHorizontal rendering ──────────────────────────────────────────────

    /// <summary>
    /// FlipHorizontal = true mirrors the frame left-to-right around the canvas
    /// centre (x=42 on a 64-wide canvas).  A texture that is red on the left
    /// and green on the right should appear green on the left and red on the
    /// right after the flip.
    ///
    /// Math: canvas.Scale(-1, 1, 42, 42) → screen_x = 84 – canvas_x.
    ///   screen (38,42) → canvas_x=46 → tex_x=22 → right half → GREEN.
    ///   screen (46,42) → canvas_x=38 → tex_x=14 → left  half → RED.
    /// </summary>
    [AvaloniaFact]
    public void PreviewFlipHorizontal_LeftAndRightPixelsAreSwapped()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteHSplitPng(Path.Combine(dir, "hsplit.png"), SKColors.Red, SKColors.Green);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName      = "hsplit.png",
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                FrameLength      = 0.1f,
                FlipHorizontal   = true,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // Frame screen rect: (24,24)→(40,40).
            // After H-flip: left-screen pixel shows the RIGHT texture colour (green),
            // and right-screen pixel shows the LEFT texture colour (red).
            var pxLeft  = bm.GetPixel(38, 42);
            var pxRight = bm.GetPixel(46, 42);

            Assert.True(pxLeft.Green > pxLeft.Red,
                $"After H-flip: left screen px should be green; R={pxLeft.Red} G={pxLeft.Green}");
            Assert.True(pxRight.Red > pxRight.Green,
                $"After H-flip: right screen px should be red; R={pxRight.Red} G={pxRight.Green}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── FlipVertical rendering ────────────────────────────────────────────────

    /// <summary>
    /// FlipVertical = true mirrors the frame top-to-bottom around the canvas
    /// centre (y=42 on a 64-tall canvas).
    ///
    /// Math: canvas.Scale(1, -1, 42, 42) → screen_y = 84 – canvas_y.
    ///   screen (42,38) → canvas_y=46 → tex_y=22 → bottom half → BLUE.
    ///   screen (42,46) → canvas_y=38 → tex_y=14 → top    half → RED.
    /// </summary>
    [AvaloniaFact]
    public void PreviewFlipVertical_TopAndBottomPixelsAreSwapped()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteVSplitPng(Path.Combine(dir, "vsplit.png"), SKColors.Red, SKColors.Blue);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName      = "vsplit.png",
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                FrameLength      = 0.1f,
                FlipVertical     = true,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // Frame screen rect: (34,34)→(50,50).
            // After V-flip: top screen pixel shows the BOTTOM texture colour (blue),
            // and bottom screen pixel shows the TOP texture colour (red).
            var pxTop    = bm.GetPixel(42, 38);
            var pxBottom = bm.GetPixel(42, 46);

            Assert.True(pxTop.Blue > pxTop.Red,
                $"After V-flip: top screen px should be blue; R={pxTop.Red} B={pxTop.Blue}");
            Assert.True(pxBottom.Red > pxBottom.Blue,
                $"After V-flip: bottom screen px should be red; R={pxBottom.Red} B={pxBottom.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Onion skin rendering ──────────────────────────────────────────────────

    /// <summary>
    /// ShowOnionSkin draws the previous frame at alpha=0.4 under the current
    /// frame at alpha=1.0.  When the ghost (frame0, 16×16) is larger than the
    /// current frame (frame1, 8×8), the ghost bleeds into pixels that are NOT
    /// covered by the current frame — and those pixels should show a red tint.
    /// Pixels inside BOTH rects should show the current frame colour (green)
    /// at full strength, proving that the current frame renders on top.
    ///
    /// Ghost-only pixel (36,42):
    ///   Onion red at alpha=0.4 over bg (30,30,30):
    ///   R = 255×0.4 + 30×0.6 = 120.  Assert Red > 100.
    ///
    /// Overlap pixel (42,42):
    ///   Current frame (green, alpha=1.0) draws over onion → G=255, R≈0.
    ///   Assert Green > 200, Red &lt; 50.
    /// </summary>
    [AvaloniaFact]
    public void PreviewOnionSkin_GhostVisible_AndCurrentFrameCoversGhost_InOverlapRegion()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // frame0 (ghost/onion): 16×16 red — rendered at screen rect (34,34,50,50)
            // frame1 (current):      8×8 lime — rendered at screen rect (38,38,46,46)
            WriteColorPng(Path.Combine(dir, "red.png"),  SKColors.Red,  size: 16);
            WriteColorPng(Path.Combine(dir, "lime.png"), SKColors.Lime, size:  8);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame0 = new AnimationFrameSave
            {
                TextureName = "red.png",   FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName = "lime.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame1;  // frame0 becomes the onion

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.ShowOnionSkin = true;

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Pixel (36,42): inside ghost rect (34,34,50,50), outside current rect (38,38,46,46) → ghost only
            var ghostPx = bm.GetPixel(36, 42);
            Assert.True(ghostPx.Red > 100,
                $"Ghost frame should be visible outside current frame region; R={ghostPx.Red} at (36,42)");

            // Pixel (42,42): inside both rects → current frame (green) must cover the ghost
            var overlapPx = bm.GetPixel(42, 42);
            Assert.True(overlapPx.Green > 200,
                $"Current frame (green) should dominate in the overlap region; G={overlapPx.Green} at (42,42)");
            Assert.True(overlapPx.Red < 50,
                $"Ghost red should be hidden under current frame in overlap; R={overlapPx.Red} at (42,42)");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Multi-frame playback rendering ────────────────────────────────────────

    /// <summary>
    /// When no specific frame is pinned (SelectedFrame == null) the preview
    /// renders whichever frame the PlaybackController is pointing at.
    /// Advancing past the first frame's duration should switch the visible
    /// texture from frame 0 (red) to frame 1 (green).
    /// </summary>
    [AvaloniaFact]
    public void Preview_MultiFrameChain_Playback_RendersCurrentFrameTexture()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"),  SKColors.Red,  size: 16);
            WriteColorPng(Path.Combine(dir, "lime.png"), SKColors.Lime, size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame0 = new AnimationFrameSave
            {
                TextureName = "red.png",  FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var frame1 = new AnimationFrameSave
            {
                TextureName = "lime.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            // SelectedFrame stays null → playback controller drives frame selection

            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();
            ctrl.Playback.SetChain(chain);  // reset to frame 0, IsPlaying=false
            ctrl.Playback.Play();           // re-enable; timer is still stopped
            ctrl.Playback.Advance(0.15);    // 0.15 s > 0.1 s → advances to frame 1

            Assert.Equal(1, ctrl.Playback.CurrentFrameIndex);  // sanity check

            using var bm = ctrl.RenderToBitmap(64, 64);
            // Frame 1 (lime.png, 16×16) centred at (42,42) → screen rect (34,34,50,50).
            var center = bm.GetPixel(42, 42);
            Assert.True(center.Green > 200,
                $"Frame 1 (lime.png) should render at canvas centre; G={center.Green}");
            Assert.True(center.Red < 50,
                $"Frame 0 (red.png) should no longer be visible; R={center.Red}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── PreviewControl — UV subregion rendering ───────────────────────────────

    /// <summary>
    /// A frame whose UV covers only the left half of a texture should render the
    /// left-half pixels, not the right half.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_UvSubregion_LeftHalf_RendersLeftTexturePixels()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 32×32 H-split: left half=Red, right half=Blue
            WriteHSplitPng(Path.Combine(dir, "hsplit.png"), SKColors.Red, SKColors.Blue, 32, 32);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            // UV selects only the left half (columns 0–15)
            var frame = new AnimationFrameSave
            {
                TextureName = "hsplit.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.5f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // sw=16, sh=32, dw=16, dh=32, dx=34, dy=26 → screen rect (34,26,50,58)
            // Pixel (42,42): src (8,16) in texture → x=8 < 16 → left half → Red
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Red > 150 && px.Blue < 50,
                $"Left-UV frame should show red at centre; R={px.Red} G={px.Green} B={px.Blue}");
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
    /// A frame whose UV covers only the right half should render the right-half pixels.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_UvSubregion_RightHalf_RendersRightTexturePixels()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteHSplitPng(Path.Combine(dir, "hsplit.png"), SKColors.Red, SKColors.Blue, 32, 32);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            // UV selects only the right half (columns 16–31)
            var frame = new AnimationFrameSave
            {
                TextureName = "hsplit.png", FrameLength = 0.1f,
                LeftCoordinate = 0.5f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // Same screen rect; src starts at x=16, so pixel (42,42) maps to texture (24,16) → right → Blue
            var px = bm.GetPixel(42, 42);
            Assert.True(px.Blue > 150 && px.Red < 50,
                $"Right-UV frame should show blue at centre; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── PreviewControl — zoom rendering ──────────────────────────────────────

    /// <summary>
    /// At zoom=200% a 16×16 frame expands to a 32×32 screen rect (26,26,58,58).
    /// Pixels that would be background at zoom=100% are now inside the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_Zoom200_FrameOccupiesLargerScreenArea()
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
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetZoomPercent(200);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // zoom=2: dw=32, dh=32, dx=42-16=26, dy=26 → screen rect (26,26,58,58)
            // Pixel (28,42): inside at zoom=2, outside zoom=1 rect (34,34,50,50) → red
            var insidePx = bm.GetPixel(28, 42);
            Assert.True(insidePx.Red > 150,
                $"Pixel (28,42) should be inside frame at zoom=200%; R={insidePx.Red}");

            // At zoom=1 the same pixel would be outside (rect is 34,34,50,50); verify background
            ctrl.SetZoomPercent(100);
            using var bmZoom1 = ctrl.RenderToBitmap(64, 64);
            var outsidePx = bmZoom1.GetPixel(28, 42);
            Assert.True(outsidePx.Red < 50,
                $"Pixel (28,42) should be outside frame at zoom=100%; R={outsidePx.Red}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── PreviewControl — pan rendering ────────────────────────────────────────

    /// <summary>
    /// With panX=16 the frame centre shifts 16px right.  A 16×16 frame that
    /// was centred at (42,42) is now centred at (58,42), rect=(50,34,66,50).
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_PanRight_FrameShiftsRight()
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
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetPan(16f, 0f);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // cx=42+16=58; dw=16, dx=58-8=50 → screen rect (50,34,66,50)
            var newCentre = bm.GetPixel(54, 42);
            Assert.True(newCentre.Red > 150,
                $"Frame centre should be at (54,42) after pan right; R={newCentre.Red}");

            var oldCentre = bm.GetPixel(42, 42);
            Assert.True(oldCentre.Red < 50,
                $"Old centre (42,42) should now be background after pan; R={oldCentre.Red}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Loads a single frame into a fresh chain and sets it as selected.</summary>
    private static void SetupSingleFrame(TestServices ctx, string dir, AnimationFrameSave frame)
    {
        var chain = new AnimationChainSave { Name = "Test" };
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
    }

    // ── WireframeControl — edge cases ─────────────────────────────────────────

    /// <summary>
    /// GetFrameRects with a texture loaded but no chain selected returns an
    /// empty list — the control has nothing to measure against.
    /// </summary>
    [AvaloniaFact]
    public void WireframeGetFrameRects_TextureLoadedButNoChainSelected_ReturnsEmpty()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "tex.png");
        try
        {
            WriteColorPng(png, SKColors.Blue, size: 16);

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            // SelectedChain remains null from ResetSingletons
            ctrl.RefreshFrames();

            Assert.Empty(ctrl.GetFrameRects());
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── WireframeControl — 8-frame spritesheet (tutorial step 5) ─────────────

    /// <summary>
    /// Tutorial docs show an 8-frame "Idle" animation on a 128×128 spritesheet
    /// with 4 columns and 2 rows. GetFrameRects must return exactly 8 rects.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_8FrameSpriteSheet_GetFrameRects_Returns8()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "Idle.png");
        try
        {
            // 128×128 solid-colour sheet — pixel values don't matter for UV math
            WriteColorPng(png, SKColors.Green, size: 128);

            var chain = new AnimationChainSave { Name = "Idle" };
            int cellW = 32, cellH = 64, texW = 128, texH = 128;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                    chain.Frames.Add(new AnimationFrameSave
                    {
                        TextureName      = "Idle.png",
                        LeftCoordinate   = (float)(col * cellW) / texW,
                        RightCoordinate  = (float)(col * cellW + cellW) / texW,
                        TopCoordinate    = (float)(row * cellH) / texH,
                        BottomCoordinate = (float)(row * cellH + cellH) / texH,
                        FrameLength      = 0.1f,
                        ShapesSave = new ShapesSave()
                    });

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();

            Assert.Equal(8, ctrl.GetFrameRects().Count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// The first frame's GetFrameRects Bounds should correspond to the top-left
    /// cell: left=0, top=0, right=32, bottom=64 (texture-pixel coordinates).
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_8FrameSpriteSheet_FirstFrame_RectMatchesTopLeftCell()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, "Idle.png");
        try
        {
            WriteColorPng(png, SKColors.Green, size: 128);

            var chain = new AnimationChainSave { Name = "Idle" };
            int cellW = 32, cellH = 64, texW = 128, texH = 128;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                    chain.Frames.Add(new AnimationFrameSave
                    {
                        TextureName      = "Idle.png",
                        LeftCoordinate   = (float)(col * cellW) / texW,
                        RightCoordinate  = (float)(col * cellW + cellW) / texW,
                        TopCoordinate    = (float)(row * cellH) / texH,
                        BottomCoordinate = (float)(row * cellH + cellH) / texH,
                        FrameLength      = 0.1f,
                        ShapesSave = new ShapesSave()
                    });

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();

            var rects  = ctrl.GetFrameRects();
            var first  = rects[0].Bounds;

            Assert.Equal(0f,   first.Left,   precision: 3);
            Assert.Equal(0f,   first.Top,    precision: 3);
            Assert.Equal(32f,  first.Right,  precision: 3);
            Assert.Equal(64f,  first.Bottom, precision: 3);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── PreviewControl — RelativeX / RelativeY rendering ─────────────────────
    //
    // Tutorial docs: "Change the RelativeY value so that the character is
    // positioned properly … the preview window will update immediately."
    //
    // Sign convention (FRB +Y = up, screen +Y = down):
    //   fcx = cx + RelativeX * OffsetMultiplier * zoom
    //   fcy = cy - RelativeY * OffsetMultiplier * zoom

    /// <summary>
    /// RelativeY=+8 with default OffsetMultiplier=1 at zoom=1 shifts the frame
    /// 8 px upward on screen (fcy = 34 on a 64-px-tall canvas).
    /// Pixel (42,30) should be inside the shifted frame (red), while the
    /// old centre (42,46) should now be background.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_RelativeY_Positive_ShiftsFrameHigherOnScreen()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"), SKColors.Red, size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            // 16×16 frame covering the full texture, RelativeY = +8 (up in game space)
            var frame = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                RelativeY = 8f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetZoomPercent(100);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // fcx=42, fcy=42-8=34  →  screen rect (34,26,50,42)
            var insidePx = bm.GetPixel(42, 30);
            Assert.True(insidePx.Red > 150,
                $"Pixel (42,30) should be inside shifted frame (RelativeY=+8); R={insidePx.Red}");

            // (42,46) is inside the original unshifted rect but below the shifted rect
            var oldCentrePx = bm.GetPixel(42, 46);
            Assert.True(oldCentrePx.Red < 50,
                $"Pixel (42,46) should be background — below shifted frame; R={oldCentrePx.Red}");
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
    /// RelativeX=+8 with default OffsetMultiplier=1 at zoom=1 shifts the frame
    /// 8 px to the right on screen (fcx = 50 on a 64-px-wide canvas).
    /// Pixel (50,42) should be inside the shifted frame (red), while the
    /// old centre (38,42) should now be background.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_RelativeX_Positive_ShiftsFrameRightOfCenter()
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
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                RelativeX = 8f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetZoomPercent(100);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // fcx=42+8=50, fcy=42  →  screen rect (42,34,58,50)
            var insidePx = bm.GetPixel(50, 42);
            Assert.True(insidePx.Red > 150,
                $"Pixel (50,42) should be inside shifted frame (RelativeX=+8); R={insidePx.Red}");

            // (38,42) is inside the original unshifted rect but left of the shifted rect
            var oldCentrePx = bm.GetPixel(38, 42);
            Assert.True(oldCentrePx.Red < 50,
                $"Pixel (38,42) should be background — left of shifted frame; R={oldCentrePx.Red}");
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
    /// With OffsetMultiplier=2 and RelativeY=+8 the screen offset is doubled:
    /// fcy = 42 - 8*2*1 = 26.  The frame rect becomes (34,18,50,34).
    /// Pixel (42,26) should be inside the further-shifted frame, while (42,38)
    /// (where zoom=1 OffsetMultiplier=1 would place the top edge) is outside.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_OffsetMultiplier2_RelativeY_DoublesScreenShift()
    {
        var ctx = ResetSingletons();
        ctx.AppState.OffsetMultiplier = 2f;
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteColorPng(Path.Combine(dir, "red.png"), SKColors.Red, size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                RelativeY = 8f,
                ShapesSave = new ShapesSave()
            };
            SetupSingleFrame(ctx, dir, frame);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetZoomPercent(100);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // fcy = 42 - 8*2*1 = 26  →  screen rect (34,18,50,34)
            var insidePx = bm.GetPixel(42, 26);
            Assert.True(insidePx.Red > 150,
                $"Pixel (42,26) should be inside doubly-shifted frame (mult=2, RelativeY=8); R={insidePx.Red}");

            // Pixel (42,38) is where mult=1 would put the frame centre — must be background now
            var outsidePx = bm.GetPixel(42, 38);
            Assert.True(outsidePx.Red < 50,
                $"Pixel (42,38) should be background when OffsetMultiplier=2; R={outsidePx.Red}");
        }
        finally
        {
            ctx.AppState.OffsetMultiplier = 1f;
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Onion-skin frame with a different RelativeY from the main frame must be
    /// drawn at its own shifted position, not at the main frame's position.
    /// The previous frame in the chain is the onion skin when ShowOnionSkin=true.
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_OnionSkin_DifferentRelativeY_RendersAtOwnPosition()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Main frame (index 1): green, no offset   → centred at (42,42)
            // Onion frame (index 0, prev): red, RelativeY=+8 → centred at (42,34)
            WriteColorPng(Path.Combine(dir, "green.png"), SKColors.Lime, size: 16);
            WriteColorPng(Path.Combine(dir, "red.png"),   SKColors.Red,  size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var onionFrame = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                RelativeY = 8f,
                ShapesSave = new ShapesSave()
            };
            var mainFrame = new AnimationFrameSave
            {
                TextureName = "green.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                RelativeY = 0f,
                ShapesSave = new ShapesSave()
            };

            // index 0 = onion (previous), index 1 = current (main)
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(onionFrame);
            chain.Frames.Add(mainFrame);

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = mainFrame;  // current = index 1

            var ctrl = ctx.CreatePreviewControl();
            ctrl.SetZoomPercent(100);
            ctrl.ShowOnionSkin = true;

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Main (green) centre (42,42) should be green-ish
            var mainCentre = bm.GetPixel(42, 42);
            Assert.True(mainCentre.Green > 100,
                $"Main frame centre (42,42) should be greenish; G={mainCentre.Green}");

            // Onion skin (red) shifted to fcy=34, rect=(34,26,50,42).
            // Pixel (42,30) is inside the onion rect but outside the main rect → red contribution.
            var onionArea = bm.GetPixel(42, 30);
            Assert.True(onionArea.Red > 50,
                $"Onion skin area (42,30) should carry red contribution (RelativeY=+8); R={onionArea.Red}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Collision shape rendering ─────────────────────────────────────────────

    /// <summary>
    /// A pinned frame containing an AARectSave should render a green
    /// rectangle outline in the preview. At default zoom=1 and OffsetMultiplier=1,
    /// a rect at world (0,0) with ScaleX=ScaleY=15 on a 100×100 canvas (RulerSize=20)
    /// gives screen centre (60,60) and top edge at y=45. Check that pixel (60,45)
    /// is greenish (the shape outline colour) and the canvas interior is dark background.
    /// </summary>
    [AvaloniaFact]
    public void Preview_RectShape_RendersGreenOutlineAtExpectedPosition()
    {
        var ctx = ResetSingletons();
        var frame = new AnimationFrameSave
        {
            FrameLength = 0.1f,
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave.Shapes.Add(
            new AARectSave { Name = "Box", X = 0, Y = 0, ScaleX = 15, ScaleY = 15 });

        ctx.SelectedState.SelectedFrame = frame;
        ctx.AppState.OffsetMultiplier = 1f;

        try
        {
            var ctrl = ctx.CreatePreviewControl();
            // 100×100: RulerSize=20, cx=cy=60, top-edge of rect → y = 60 - 15 = 45
            using var bm = ctrl.RenderToBitmap(100, 100);

            var topEdge = bm.GetPixel(60, 45);
            Assert.True(topEdge.Green > 100,
                $"Top edge of rect outline at (60,45) should be greenish; G={topEdge.Green} R={topEdge.Red} B={topEdge.Blue}");

            // Interior of rect should be background (dark)
            var interior = bm.GetPixel(60, 50);
            Assert.True(interior.Green < 50,
                $"Interior of rect (60,50) should be dark background; G={interior.Green}");
        }
        finally
        {
            ctx.SelectedState.SelectedFrame = null;
        }
    }

    /// <summary>
    /// A pinned frame with a CircleSave should render a green circle outline in
    /// the preview. Circle at world (0,0) with radius=15 on 100×100 (RulerSize=20)
    /// gives screen centre (60,60), top of circle at y≈45.
    /// </summary>
    [AvaloniaFact]
    public void Preview_CircleShape_RendersGreenOutlineAtTopOfCircle()
    {
        var ctx = ResetSingletons();
        var frame = new AnimationFrameSave
        {
            FrameLength = 0.1f,
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave.Shapes.Add(
            new CircleSave { Name = "Ring", X = 0, Y = 0, Radius = 15 });

        ctx.SelectedState.SelectedFrame = frame;
        ctx.AppState.OffsetMultiplier = 1f;

        try
        {
            var ctrl = ctx.CreatePreviewControl();
            using var bm = ctrl.RenderToBitmap(100, 100);

            // Top of circle is at (60, 60-15) = (60, 45)
            var topEdge = bm.GetPixel(60, 45);
            Assert.True(topEdge.Green > 100,
                $"Top of circle outline at (60,45) should be greenish; G={topEdge.Green} R={topEdge.Red} B={topEdge.Blue}");
        }
        finally
        {
            ctx.SelectedState.SelectedFrame = null;
        }
    }

    /// <summary>
    /// When the rectangle on a pinned frame is also the selected shape,
    /// BuildShapeInfos marks it IsSelected=true, and the outline should be gold
    /// (R≈255, G≈220, B≈0) rather than green.
    /// </summary>
    [AvaloniaFact]
    public void Preview_SelectedRect_RendersGoldOutline()
    {
        var ctx = ResetSingletons();
        var rect = new AARectSave { Name = "Box", X = 0, Y = 0, ScaleX = 15, ScaleY = 15 };
        var frame = new AnimationFrameSave
        {
            FrameLength = 0.1f,
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave.Shapes.Add(rect);

        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedRectangle = rect; // mark as selected
        ctx.AppState.OffsetMultiplier = 1f;

        try
        {
            var ctrl = ctx.CreatePreviewControl();
            using var bm = ctrl.RenderToBitmap(100, 100);

            // Top edge of rect at (60, 45): selected shape uses gold color (R>200, G>150, B<50)
            var topEdge = bm.GetPixel(60, 45);
            Assert.True(topEdge.Red > 200,
                $"Selected rect top edge at (60,45) should have high Red (gold); R={topEdge.Red}");
            Assert.True(topEdge.Blue < 80,
                $"Selected rect top edge should be gold, not blue; B={topEdge.Blue}");
        }
        finally
        {
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedRectangle = null;
        }
    }

    /// <summary>
    /// When SelectedFrame is null (free playback, no pinned frame), BuildShapeInfos
    /// returns an empty array and no shape outlines are drawn. The entire canvas
    /// inside the content area should be background-coloured (no stray green pixels).
    /// </summary>
    [AvaloniaFact]
    public void Preview_NoPinnedFrame_NoShapesRendered()
    {
        var ctx = ResetSingletons();
        // SelectedFrame stays null — shapes should NOT appear
        ctx.SelectedState.SelectedFrame = null;
        ctx.AppState.OffsetMultiplier = 1f;

        var ctrl = ctx.CreatePreviewControl();
        using var bm = ctrl.RenderToBitmap(100, 100);

        // Sample several points inside the content area where a shape outline
        // at origin (0,0) ScaleX=15 would land: (60,45), (60,75), (45,60), (75,60)
        foreach (var (x, y) in new[] { (60, 45), (60, 75), (45, 60), (75, 60) })
        {
            var px = bm.GetPixel(x, y);
            Assert.True(px.Green < 50,
                $"No shape should be drawn at ({x},{y}) when no frame is pinned; G={px.Green}");
        }
    }
}
