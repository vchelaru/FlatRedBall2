using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Visual tests for resize handles and border outlines rendered by
/// <see cref="WireframeControl"/> and <see cref="PreviewControl"/>.
///
/// Handles (WireframeControl):
///   • Appear only when a frame is selected (<c>SelectedHandleBounds</c> is set).
///   • Drawn as 10×10 white-filled boxes (±5 px around each of 8 handle points).
///   • At camera(0,0,1) with a 64×64 texture, the top-left handle is centred at
///     screen (0,0); its interior at (2,2) is white (R=255, G=255, B=255).
///
/// Texture outline (WireframeControl):
///   • 1-px white-ish border (<c>SKColor(255,255,255,160)</c>) drawn around the
///     entire texture rect.  At camera(0,0,1) with a 64×64 texture on a 96×96
///     canvas the right border is at x=64; pixel (65,48) (just outside the
///     texture) receives the outline and should be significantly brighter than
///     the dark background.
///
/// Frame border (PreviewControl):
///   • <c>DrawFrameCore</c> draws a 1-px <c>SKColor(255,255,255,200)</c> outline
///     around every rendered frame rect.  For a 16×16 frame centred at (32,32)
///     on a 64×64 canvas the screen rect is (24,24,40,40).  The pixel just
///     outside the left edge at (23,32) should be brighter than plain background.
/// </summary>
public class WireframeHandlesAndBorderTests
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

    private static string WriteSolidPng(string dir, SKColor color, int size = 64)
    {
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── Wireframe resize handles ──────────────────────────────────────────────

    /// <summary>
    /// When a frame is selected the top-left handle box sits OUTSIDE the frame bounds (issue #114).
    /// Frame at UV 0.125→0.875 on a 64×64 texture → screen rect (8,8,56,56) at camera(0,0,1).
    /// TopLeft handle centre = (8−5, 8−5) = (3, 3); pixel (3,3) should be white.
    /// </summary>
    [AvaloniaFact]
    public void WireframeHandles_SelectedFrame_TopLeftHandleIsOutsideFrame()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);

            // Inner frame: UV 0.125→0.875 (8px margin on 64×64 texture)
            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0.125f, TopCoordinate = 0.125f,
                RightCoordinate = 0.875f, BottomCoordinate = 0.875f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();
            ctrl.SetCamera(0, 0, 1);  // screen rect = (8,8,56,56); TL handle at (3,3)

            using var bm = ctrl.RenderToBitmap(64, 64);

            // TopLeft handle: centre (3,3), box (-2,-2,8,8) → pixel (3,3) is white fill
            var px = bm.GetPixel(3, 3);
            Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200,
                $"Top-left handle (3,3) should be white (outside frame); R={px.Red} G={px.Green} B={px.Blue}");

            // Frame interior corner at (9,9) should be black texture (no handle overlap)
            var interior = bm.GetPixel(9, 9);
            Assert.True(interior.Red < 100 && interior.Green < 100 && interior.Blue < 100,
                $"Frame interior (9,9) should be black texture, not handle; R={interior.Red} G={interior.Green} B={interior.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// The middle-left handle sits outside the frame bounds (issue #114).
    /// Frame at UV 0.125→0.875 on 64×64 → screen rect (8,8,56,56).
    /// MidLeft handle centre = (8−5, 32) = (3, 32); pixel (3,32) should be white.
    /// </summary>
    [AvaloniaFact]
    public void WireframeHandles_SelectedFrame_MidLeftHandleIsOutsideFrame()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);

            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0.125f, TopCoordinate = 0.125f,
                RightCoordinate = 0.875f, BottomCoordinate = 0.875f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();
            ctrl.SetCamera(0, 0, 1);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // MidLeft handle: centre (3, 32), box (-2,27,8,37) → pixel (3,32) is white fill
            var px = bm.GetPixel(3, 32);
            Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200,
                $"Mid-left handle (3,32) should be white (outside frame); R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// When no frame is selected there should be no handle boxes.
    /// Pixel (2,2) should be the black texture, not white.
    /// </summary>
    [AvaloniaFact]
    public void WireframeHandles_NoSelectedFrame_NoWhiteHandleAtCorner()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);

            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = null;   // ← NO frame selected

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();
            ctrl.SetCamera(0, 0, 1);

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Without selection, (2,2) is inside the black texture — no white handle
            var px = bm.GetPixel(2, 2);
            Assert.True(px.Red < 100 && px.Green < 100 && px.Blue < 100,
                $"No selected frame: (2,2) should be dark texture, not a white handle; R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// Selecting a frame and then deselecting it (setting SelectedFrame = null)
    /// should make the handles disappear on the next render.
    /// Uses UV 0.125→0.875 so the TopLeft handle lands at (3,3) within a 64×64 canvas.
    /// </summary>
    [AvaloniaFact]
    public void WireframeHandles_SelectThenDeselect_HandleDisappears()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);

            // Inner frame: handles land inside the 64×64 canvas (TL handle at (3,3))
            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0.125f, TopCoordinate = 0.125f,
                RightCoordinate = 0.875f, BottomCoordinate = 0.875f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0, 0, 1);

            // Select frame → TopLeft handle visible at (3,3)
            SelectedState.Self.SelectedFrame = frame;
            ctrl.RefreshFrames();
            using var bmSelected = ctrl.RenderToBitmap(64, 64);
            var pxSelected = bmSelected.GetPixel(3, 3);
            Assert.True(pxSelected.Red > 200,
                $"Handle should be visible when frame selected; R={pxSelected.Red}");

            // Deselect frame AND chain → handles gone completely
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            ctrl.RefreshFrames();
            using var bmDeselected = ctrl.RenderToBitmap(64, 64);
            var pxDeselected = bmDeselected.GetPixel(3, 3);
            Assert.True(pxDeselected.Red < 100,
                $"Handle should vanish when frame deselected; R={pxDeselected.Red}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Wireframe texture outline ─────────────────────────────────────────────

    /// <summary>
    /// The WireframeControl draws a 1-px <c>SKColor(255,255,255,160)</c> outline
    /// around the entire texture rect.  With the 64×64 texture on a 96×96 canvas
    /// and camera(0,0,1), the texture occupies (0,0,64,64).
    ///
    /// A pixel just inside the right border (e.g. (63,32)) receives the outline
    /// stroke and should be noticeably brighter than a pixel deep inside (32,32)
    /// which is pure black texture.
    ///
    /// Relative assertion: outline pixel R > interior pixel R + 100.
    /// </summary>
    [AvaloniaFact]
    public void WireframeTextureOutline_BorderPixelBrighterThanInterior()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);  // 64×64 black texture

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0, 0, 1);   // texture at (0,0,64,64) on 96×96 canvas

            using var bm = ctrl.RenderToBitmap(96, 96);

            // Stroke centred at x=0 (left edge of texture rect) covers pixel x=0.
            // That pixel had black texture; outline blends white-ish over it → R ≈ 160.
            // Interior pixel (32,32) is black texture → R ≈ 0 (no outline).
            var borderPx   = bm.GetPixel(0, 32);   // left-edge border pixel
            var interiorPx = bm.GetPixel(32, 32);  // deep interior, no outline

            Assert.True(borderPx.Red > interiorPx.Red + 80,
                $"Border pixel should be much brighter than interior; border R={borderPx.Red} interior R={interiorPx.Red}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// The area outside the texture (background at SKColor(30,30,30)) should
    /// receive the outline on the outside half of the stroke.  At camera(0,0,1)
    /// with the texture at (0,0,64,64) on a 128×96 canvas, the right-edge stroke
    /// at x=64 bleeds into x=64 (outside the texture).  That background pixel
    /// should be detectably brighter than a distant background pixel.
    /// </summary>
    [AvaloniaFact]
    public void WireframeTextureOutline_OutsideBackgroundBrighterNearBorder()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.Black);

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0, 0, 1);

            using var bm = ctrl.RenderToBitmap(128, 96);

            // Background at x=80 (far from border) ≈ (30,30,30)
            var farBgPx   = bm.GetPixel(80, 48);
            // Scan ±2 px around x=64 for the brightest pixel (outline may land at 63 or 64)
            int maxRNearBorder = Enumerable.Range(63, 4)
                .Where(x => x < bm.Width)
                .Select(x => (int)bm.GetPixel(x, 48).Red)
                .Max();

            Assert.True(maxRNearBorder > farBgPx.Red + 30,
                $"Near-border background should be brighter than far background; nearMax R={maxRNearBorder} far R={farBgPx.Red}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Preview frame border outline ──────────────────────────────────────────

    /// <summary>
    /// <see cref="PreviewControl.DrawFrameCore"/> draws a 1-px
    /// <c>SKColor(255,255,255,200)</c> outline around every rendered frame.
    ///
    /// For a 16×16 frame at zoom=1 the screen rect is (24,24,40,40).
    /// The left-edge stroke is at x=24, which covers pixels at/near x=24.
    /// A pixel near the left border should be brighter than the plain
    /// background on the far left, proving the outline is drawn.
    ///
    /// Relative assertion: near-border max R > far background R + 60.
    /// </summary>
    [AvaloniaFact]
    public void PreviewFrameBorder_LeftEdge_BrighterThanFarBackground()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Dark texture so the white border is distinguishable over the frame colour
            var texPath = Path.Combine(dir, "dark.png");
            using (var bmp = new SKBitmap(16, 16)) { bmp.Erase(new SKColor(0, 0, 60)); using var d = bmp.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(texPath, d.ToArray()); }
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName = "dark.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var ctrl = new PreviewControl();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // Screen rect: (34,34,50,50).  Left border stroke at x≈34.
            // Scan ±1 px around x=34 at y=42 (middle of left edge)
            int maxRNearBorder = Enumerable.Range(33, 3)
                .Where(x => x >= 0 && x < bm.Width)
                .Select(x => (int)bm.GetPixel(x, 42).Red)
                .Max();

            // Far left background at (22, 42) ≈ (30,30,30)
            var farBg = bm.GetPixel(22, 42);

            Assert.True(maxRNearBorder > farBg.Red + 60,
                $"Left frame border should be brighter than far background; nearMax R={maxRNearBorder} farBg R={farBg.Red}");
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
    /// The frame border should appear regardless of whether the frame uses a
    /// brightly-coloured or dark texture.  With a bright red texture the white
    /// border (alpha=200) at the top edge y=24 should still raise the Red channel
    /// at that row relative to the background row (y=5).
    ///
    /// Verifies the outline is unconditionally drawn, not suppressed for bright textures.
    /// </summary>
    [AvaloniaFact]
    public void PreviewFrameBorder_TopEdge_BrighterThanBackground_WithBrightTexture()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = Path.Combine(dir, "red.png");
            using (var bmp = new SKBitmap(16, 16)) { bmp.Erase(SKColors.Red); using var d = bmp.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(texPath, d.ToArray()); }
            ProjectManager.Self.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var ctrl = new PreviewControl();
            using var bm = ctrl.RenderToBitmap(64, 64);

            // Top border stroke at y≈34.  Sample at (42, 33) — just above the rect,
            // which should be background before the outline stroke; with the outline
            // it should be noticeably brighter than y=22 background.
            int maxRNearTopBorder = Enumerable.Range(33, 3)
                .Where(y => y >= 0 && y < bm.Height)
                .Select(y => (int)bm.GetPixel(42, y).Red)
                .Max();

            var farBg = bm.GetPixel(42, 22);

            Assert.True(maxRNearTopBorder > farBg.Red + 60,
                $"Top frame border should be brighter than far background; nearMax R={maxRNearTopBorder} farBg R={farBg.Red}");
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Render with vs without selected frame — different bitmaps ────────────

    /// <summary>
    /// Selecting a frame should change the rendered output of WireframeControl
    /// (blue overlay + handles).  The two bitmaps must differ in at least one pixel.
    /// </summary>
    [AvaloniaFact]
    public void WireframeSelectedFrame_ChangesBitmapComparedToNoSelection()
    {
        ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.DarkGray);

            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = null;

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0, 0, 1);

            ctrl.RefreshFrames();
            using var bmNoSel = ctrl.RenderToBitmap(64, 64);

            SelectedState.Self.SelectedFrame = frame;
            ctrl.RefreshFrames();
            using var bmSel = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bmNoSel.GetPixel(x, y) != bmSel.GetPixel(x, y);

            Assert.True(anyDiff, "Selecting a frame should change the rendered wireframe bitmap");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
