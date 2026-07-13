using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for the grid overlay in <see cref="WireframeControl"/>.
///
/// Covers:
///   • Visual rendering – whether grid lines appear at the expected pixel columns
///   • Cell-size changes – simulating the NumericUpDown "+" / "−" spinners
///   • State – GridState property reflects SetGrid calls
///   • Snap-click – SimulateGridSnapClick fires FrameCreatedFromRegion with snapped bounds
///   • Hover preview – GetPreviewStateForScreenPoint returns snapped rect when grid is on
///
/// All tests load a 64 × 64 black PNG and fix the camera at pan=(0,0) zoom=1 so that
/// texture pixels map 1-to-1 with screen pixels.  Grid lines therefore appear at exact
/// integer column/row multiples of the cell size.
/// </summary>
public class GridRenderTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static string WriteSolidPng(string dir, SKColor color, int w = 64, int h = 64)
    {
        var path = System.IO.Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(w, h);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Loads a black texture and sets the camera to pan=(0,0) zoom=1 so that
    /// texture coordinates equal screen coordinates in <see cref="WireframeControl.RenderToBitmap"/>.
    /// </summary>
    private static (WireframeControl ctrl, string dir) BuildCtrl(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black);
        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);   // pan=(0,0), zoom=1 → screen ≡ texture coordinates
        return (ctrl, dir);
    }

    /// <summary>
    /// Returns the maximum Red channel value within a ±2-pixel window around
    /// <paramref name="centerX"/> at the given <paramref name="y"/>.
    /// Scanning a window makes tests robust against sub-pixel rasterisation.
    /// </summary>
    private static int ScanMaxRed(SKBitmap bm, int centerX, int y)
        => Enumerable.Range(centerX - 2, 5)
                     .Where(x => x >= 0 && x < bm.Width)
                     .Select(x => (int)bm.GetPixel(x, y).Red)
                     .Max();

    // ── Visual: grid on / off ─────────────────────────────────────────────────

    /// <summary>
    /// Enabling the grid should produce a different bitmap than disabling it.
    /// This verifies that the grid rendering code path actually executes and
    /// modifies at least one pixel in the off-screen canvas.
    /// </summary>
    [AvaloniaFact]
    public void Grid_Enabled_ProducesDifferentBitmapThanDisabled()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);
            using var bmOn = ctrl.RenderToBitmap(64, 64);

            ctrl.SetGrid(false, 16);
            using var bmOff = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bmOn.GetPixel(x, y) != bmOff.GetPixel(x, y);

            Assert.True(anyDiff, "Grid-on and grid-off renders should differ — grid lines should modify at least one pixel.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Visual: cell-size changes ──────────────────────────────────────────────

    /// <summary>
    /// Changing the cell size should shift where grid lines land and therefore
    /// produce a different bitmap.  This covers the NumericUpDown "+" / "−" effect:
    /// cellSize=32 has lines at different positions than cellSize=16.
    /// </summary>
    [AvaloniaFact]
    public void Grid_DifferentCellSizes_ProduceDifferentBitmaps()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);
            using var bm16 = ctrl.RenderToBitmap(64, 64);

            ctrl.SetGrid(true, 32);
            using var bm32 = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bm16.GetPixel(x, y) != bm32.GetPixel(x, y);

            Assert.True(anyDiff, "Cell-size 16 and 32 should produce different renders (lines at different positions).");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Incrementing the cell size by 1 (simulating one "+" press: 16 → 17) should
    /// produce a different bitmap because the grid line positions shift by 1 pixel.
    /// </summary>
    [AvaloniaFact]
    public void Grid_IncrementCellSizeByOne_ProducesDifferentBitmap()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);
            using var bm16 = ctrl.RenderToBitmap(64, 64);

            ctrl.SetGrid(true, 17);
            using var bm17 = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bm16.GetPixel(x, y) != bm17.GetPixel(x, y);

            Assert.True(anyDiff, "A +1 cell-size change (16→17) should shift grid line positions and produce a different render.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Decrementing the cell size by 1 (simulating one "−" press: 16 → 15) should
    /// produce a different bitmap because the grid line positions shift by 1 pixel.
    /// </summary>
    [AvaloniaFact]
    public void Grid_DecrementCellSizeByOne_ProducesDifferentBitmap()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);
            using var bm16 = ctrl.RenderToBitmap(64, 64);

            ctrl.SetGrid(true, 15);
            using var bm15 = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bm16.GetPixel(x, y) != bm15.GetPixel(x, y);

            Assert.True(anyDiff, "A -1 cell-size change (16→15) should shift grid line positions and produce a different render.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Visual: major vs. minor lines ──────────────────────────────────────────

    /// <summary>
    /// Issue #539: every 4th grid line ("major") must render more prominently than
    /// the lines in between ("minor") so users can eyeball distances at a glance.
    /// With cellSize=8 on a 64px texture, x=32 is the 4th line (major) and x=8 is
    /// the 1st (minor). Sampling at y=4 avoids the horizontal lines (first at y=8).
    /// </summary>
    [AvaloniaFact]
    public void Grid_EveryFourthLine_IsBrighterThanMinorLines()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 8);
            using var bm = ctrl.RenderToBitmap(64, 64);

            int minorBrightness = ScanMaxRed(bm, centerX: 8, y: 4);
            int majorBrightness = ScanMaxRed(bm, centerX: 32, y: 4);

            Assert.True(majorBrightness > minorBrightness,
                $"4th grid line (x=32) should render brighter than the 1st (x=8): major={majorBrightness}, minor={minorBrightness}");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Issue #701: the major-line pattern (every 4th line brighter) must stay anchored to the
    /// texture origin (PanX/PanY), not to whichever line happens to be first visible. With
    /// gridSize=8, majors sit at texture-space multiples of 32. Panning the camera by -16 (a
    /// half-major, non-multiple-of-32 offset) moves the texture origin off past the left edge:
    /// screen x=0 now maps to texture x=16 (minor) and screen x=16 maps to texture x=32 (major).
    /// Before the fix, the emphasis index reset to 0 at the first visible line (screen x=0),
    /// so x=0 was wrongly marked major and x=16 wrongly marked minor.
    /// </summary>
    [AvaloniaFact]
    public void Grid_PannedCamera_MajorLinePatternStaysAnchoredToTextureOrigin()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 8);
            ctrl.SetCamera(-16, -16, 1);   // shift origin by half a major interval
            using var bm = ctrl.RenderToBitmap(64, 64);

            int atZero = ScanMaxRed(bm, centerX: 0, y: 4);
            int atSixteen = ScanMaxRed(bm, centerX: 16, y: 4);

            Assert.True(atSixteen > atZero,
                $"Major line should land at screen x=16 (texture x=32, a multiple of the major interval), " +
                $"not x=0 (texture x=16): atSixteen={atSixteen}, atZero={atZero}");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Visual: guard cases ───────────────────────────────────────────────────

    /// <summary>
    /// SetGrid(true, 0) must not crash and must produce the SAME bitmap as
    /// SetGrid(false, 16) because the guard <c>GridSize &gt; 0</c> prevents
    /// DrawGrid from being called — avoiding an infinite loop with step=0.
    /// </summary>
    [AvaloniaFact]
    public void Grid_CellSizeZero_RendersIdenticallyToGridOff()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 0);
            using var bmZero = ctrl.RenderToBitmap(64, 64);

            ctrl.SetGrid(false, 16);
            using var bmOff = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = bmZero.GetPixel(x, y) != bmOff.GetPixel(x, y);

            Assert.False(anyDiff, "SetGrid(true, 0) should render identically to grid-off — no lines drawn, no infinite loop.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// SetGrid(true, 1) draws a line every pixel; the render should complete
    /// without crashing and the canvas should be noticeably brighter than
    /// with no grid (many overlapping semi-transparent lines).
    /// </summary>
    [AvaloniaFact]
    public void Grid_CellSizeOne_DoesNotCrash()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 1);
            // The render must complete (no infinite loop, no crash).
            using var bm = ctrl.RenderToBitmap(64, 64);
            Assert.Equal(64, bm.Width);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <see cref="WireframeControl.GridState"/> returns the exact values passed
    /// to <see cref="WireframeControl.SetGrid"/>.
    /// </summary>
    [AvaloniaFact]
    public void SetGrid_GridState_ReflectsParameters()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();

        ctrl.SetGrid(true, 32);
        Assert.Equal((true, 32), ctrl.GridState);

        ctrl.SetGrid(false, 8);
        Assert.Equal((false, 8), ctrl.GridState);
    }

    // ── Snap-click: FrameCreatedFromRegion ────────────────────────────────────

    /// <summary>
    /// Clicking at screen (20, 20) with cellSize=16 and pan=(0,0) zoom=1 should
    /// snap to the grid cell that starts at texture (16, 16) and extends to (32, 32).
    /// </summary>
    [AvaloniaFact]
    public void SnapClick_At20_20_FiresEventWithBounds16_16_32_32()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);

            (int x0, int y0, int x1, int y1)? received = null;
            ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

            ctrl.SimulateGridSnapClick(20f, 20f);

            Assert.NotNull(received);
            Assert.Equal((16, 16, 32, 32), received!.Value);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Clicking at screen (40, 40) with cellSize=16 snaps to the cell at (32, 32)→(48, 48).
    /// </summary>
    [AvaloniaFact]
    public void SnapClick_At40_40_FiresEventWithBounds32_32_48_48()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);

            (int x0, int y0, int x1, int y1)? received = null;
            ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

            ctrl.SimulateGridSnapClick(40f, 40f);

            Assert.NotNull(received);
            Assert.Equal((32, 32, 48, 48), received!.Value);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// The frame created by a snap-click is always exactly one cell wide and tall.
    /// </summary>
    [AvaloniaFact]
    public void SnapClick_FrameSizeAlwaysEqualsCellSize()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            const int cellSize = 24;
            ctrl.SetGrid(true, cellSize);

            (int x0, int y0, int x1, int y1)? received = null;
            ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

            ctrl.SimulateGridSnapClick(30f, 30f);

            Assert.NotNull(received);
            var (x0, y0, x1, y1) = received!.Value;
            Assert.Equal(cellSize, x1 - x0);
            Assert.Equal(cellSize, y1 - y0);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When the grid is disabled, SimulateGridSnapClick must not fire FrameCreatedFromRegion.
    /// </summary>
    [AvaloniaFact]
    public void SnapClick_GridDisabled_NoEventFired()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(false, 16);

            bool fired = false;
            ctrl.FrameCreatedFromRegion += (_, _, _, _) => fired = true;

            ctrl.SimulateGridSnapClick(20f, 20f);

            Assert.False(fired, "FrameCreatedFromRegion should not fire when grid is disabled.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Hover preview ─────────────────────────────────────────────────────────

    /// <summary>
    /// Grid hover preview (yellow dashed cell highlight) has been removed.
    /// Hovering over the wireframe with grid enabled must not produce a ShowPreview=true result.
    /// </summary>
    [AvaloniaFact]
    public void HoverPreview_GridEnabled_NeverShowsPreview()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 16);

            var (show, _) = ctrl.GetPreviewStateForScreenPoint(20f, 20f);

            Assert.False(show, "Grid hover preview was removed; ShowPreview must be false even when grid is on.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When the grid is disabled the hover preview must not show.
    /// </summary>
    [AvaloniaFact]
    public void HoverPreview_GridDisabled_ShowPreviewIsFalse()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(false, 16);

            var (show, _) = ctrl.GetPreviewStateForScreenPoint(20f, 20f);

            Assert.False(show, "ShowPreview should be false when grid is disabled.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Grid-snap: boundary box alignment ─────────────────────────────────────

    /// <summary>
    /// Enabling the grid must NOT modify existing frame UV coordinates.
    /// Grid is a future-edit setting: it snaps new drags and new frame creation
    /// but must never rewrite the positions of frames that already exist.
    /// </summary>
    [AvaloniaFact]
    public void SetGrid_Enable_DoesNotModifyExistingFrameUvCoordinates()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // Deliberately off-grid (not divisible by 10): 13, 23, 47, 58 pixels.
            LeftCoordinate   = 13f / 100f,
            TopCoordinate    = 23f / 100f,
            RightCoordinate  = 47f / 100f,
            BottomCoordinate = 58f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        float origLeft   = frame.LeftCoordinate;
        float origTop    = frame.TopCoordinate;
        float origRight  = frame.RightCoordinate;
        float origBottom = frame.BottomCoordinate;

        try
        {
            ctrl.SetGrid(true, 10);

            Assert.Equal(origLeft,   frame.LeftCoordinate);
            Assert.Equal(origTop,    frame.TopCoordinate);
            Assert.Equal(origRight,  frame.RightCoordinate);
            Assert.Equal(origBottom, frame.BottomCoordinate);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Issue #538: turning the grid on must never alter how an existing frame is
    /// displayed. Grid only affects active drags in the editor — it must not snap
    /// or resize a frame's displayed bounds just because it is on. A small,
    /// off-grid frame must render at its true UV pixel bounds, not ballooned to a
    /// grid cell.
    /// </summary>
    [AvaloniaFact]
    public void GridEnabled_SmallOffGridFrame_DisplayBoundsMatchRawUVAndPreserveSize()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        // 100×100 texture so pixel coords are easy to reason about.
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // A tiny 4×4 frame at (13,23) — the issue #538 repro shape. None of the
            // edges land on the 10px grid, so a display-snap would balloon it to a cell.
            LeftCoordinate   = 13f / 100f,
            TopCoordinate    = 23f / 100f,
            RightCoordinate  = 17f / 100f,
            BottomCoordinate = 27f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 10);

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);
            var bounds = rects[0].Bounds;

            // Display bounds equal raw UV pixels — no snapping, no resizing.
            Assert.Equal(13f, bounds.Left,   precision: 2);
            Assert.Equal(23f, bounds.Top,    precision: 2);
            Assert.Equal(17f, bounds.Right,  precision: 2);
            Assert.Equal(27f, bounds.Bottom, precision: 2);
            // Size stays 4×4 — grid never resizes an existing frame.
            Assert.Equal(4f, bounds.Right  - bounds.Left, precision: 2);
            Assert.Equal(4f, bounds.Bottom - bounds.Top,  precision: 2);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Bug from issue #538 discussion: changing a frame's coordinates through a
    /// means other than a visual drag (e.g. typing a new Y in the property panel,
    /// which calls <see cref="WireframeControl.RefreshFrames"/>) must not snap the
    /// displayed bounds to the grid. The wireframe must reflect the exact value the
    /// user typed while grid is on.
    /// </summary>
    [AvaloniaFact]
    public void RefreshFrames_GridEnabled_CoordinateChange_DisplayBoundsMatchRawUV()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            LeftCoordinate   = 10f / 100f,
            TopCoordinate    = 20f / 100f,
            RightCoordinate  = 30f / 100f,
            BottomCoordinate = 40f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 10);

            // Simulate the property-panel edit: user types Y = 23 (off-grid).
            frame.TopCoordinate    = 23f / 100f;
            frame.BottomCoordinate = 43f / 100f;
            ctrl.RefreshFrames();

            var bounds = ctrl.GetFrameRects()[0].Bounds;

            // Display must show the typed value, not a grid-snapped one.
            Assert.Equal(23f, bounds.Top,    precision: 2);
            Assert.Equal(43f, bounds.Bottom, precision: 2);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// A frame that is already aligned to the grid must be unchanged after
    /// <see cref="WireframeControl.SetGrid"/> is called.
    /// </summary>
    [AvaloniaFact]
    public void GridEnabled_AlreadyAlignedBounds_NoChange()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // Already on 10-pixel grid: left=10, top=20, right=50, bottom=60.
            LeftCoordinate   = 10f / 100f,
            TopCoordinate    = 20f / 100f,
            RightCoordinate  = 50f / 100f,
            BottomCoordinate = 60f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 10);

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);
            var bounds = rects[0].Bounds;

            Assert.Equal(10f, bounds.Left,   precision: 3);
            Assert.Equal(20f, bounds.Top,    precision: 3);
            Assert.Equal(50f, bounds.Right,  precision: 3);
            Assert.Equal(60f, bounds.Bottom, precision: 3);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When the grid is disabled, displayed bounds must equal the raw UV pixel
    /// coordinates — no snapping is applied in either direction.
    /// </summary>
    [AvaloniaFact]
    public void GridDisabled_BoundsMatchRawUV()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // Deliberately off-grid.
            LeftCoordinate   = 13f / 100f,
            TopCoordinate    = 23f / 100f,
            RightCoordinate  = 47f / 100f,
            BottomCoordinate = 58f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(false, 10);   // grid OFF — no snapping

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);
            var bounds = rects[0].Bounds;

            // Bounds should equal the raw UV pixels (no snapping applied).
            Assert.Equal(13f, bounds.Left,   precision: 2);
            Assert.Equal(23f, bounds.Top,    precision: 2);
            Assert.Equal(47f, bounds.Right,  precision: 2);
            Assert.Equal(58f, bounds.Bottom, precision: 2);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Double-click: ApplyRegionToSelectedFrame ──────────────────────────────

    /// <summary>
    /// Helper: loads a 64×64 texture, creates a single frame of the given pixel size
    /// at pixel origin (frameX, frameY), selects it, and returns the control and frame.
    /// </summary>
    private static (WireframeControl ctrl, AnimationFrameSave frame, string dir)
        BuildCtrlWithFrame(TestServices ctx, int frameX, int frameY, int frameW, int frameH)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black);

        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            LeftCoordinate   = frameX / 64f,
            TopCoordinate    = frameY / 64f,
            RightCoordinate  = (frameX + frameW) / 64f,
            BottomCoordinate = (frameY + frameH) / 64f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);   // pan=(0,0), zoom=1 → screen ≡ texture coordinates
        return (ctrl, frame, dir);
    }

    /// <summary>
    /// Issue #538: grid double-click relocates the frame's origin to the clicked
    /// grid cell but must preserve its existing size. A 4×4 frame double-clicked at
    /// screen (20,20) with cellSize=16 snaps its origin to (16,16) and stays 4×4 →
    /// bounds (16,16,20,20), never (16,16,32,32).
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_SmallFrame_SnapsOriginAndPreservesSize()
    {
        var ctx = ResetSingletons();
        // 4×4 frame at pixel (5,5).
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 5, frameY: 5, frameW: 4, frameH: 4);
        try
        {
            ctrl.SetGrid(true, 16);

            ctrl.SimulateGridSnapDoubleClick(20f, 20f);

            // Origin snaps to (16,16); size stays 4×4 → (16,16,20,20) on 64×64.
            Assert.Equal(16f / 64f, frame.LeftCoordinate,   precision: 5);
            Assert.Equal(16f / 64f, frame.TopCoordinate,    precision: 5);
            Assert.Equal(20f / 64f, frame.RightCoordinate,  precision: 5);
            Assert.Equal(20f / 64f, frame.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Verifies the grid-snap math preserves size at the top-left cell: a 4×4 frame
    /// double-clicked at (5,5) snaps its origin to (0,0) and stays 4×4 → (0,0,4,4).
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_SnapsOriginToGridCell_PreservesSize()
    {
        var ctx = ResetSingletons();
        // 4×4 frame at pixel (13,23) — deliberately off-grid.
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 13, frameY: 23, frameW: 4, frameH: 4);
        try
        {
            ctrl.SetGrid(true, 16);

            ctrl.SimulateGridSnapDoubleClick(5f, 5f);

            // (5,5) snaps origin to (0,0); size stays 4×4 → (0,0,4,4) on 64×64.
            Assert.Equal(0f,       frame.LeftCoordinate,   precision: 5);
            Assert.Equal(0f,       frame.TopCoordinate,    precision: 5);
            Assert.Equal(4f / 64f, frame.RightCoordinate,  precision: 5);
            Assert.Equal(4f / 64f, frame.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When the grid is disabled, SimulateGridSnapDoubleClick must not modify the selected frame.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_GridDisabled_IsNoOp()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 0, frameY: 0, frameW: 64, frameH: 64);
        try
        {
            ctrl.SetGrid(false, 16);

            ctrl.SimulateGridSnapDoubleClick(20f, 20f);

            // UV should remain at full-sheet values
            Assert.Equal(0f, frame.LeftCoordinate,   precision: 5);
            Assert.Equal(0f, frame.TopCoordinate,    precision: 5);
            Assert.Equal(1f, frame.RightCoordinate,  precision: 5);
            Assert.Equal(1f, frame.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When no frame is selected, SimulateGridSnapDoubleClick must not throw and must be a no-op.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_NoSelectedFrame_IsNoOp()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, dir) = BuildCtrlWithFrame(ctx, frameX: 0, frameY: 0, frameW: 64, frameH: 64);
        try
        {
            ctx.SelectedState.SelectedFrame = null;
            ctrl.SetGrid(true, 16);

            // Must not throw
            var ex = Record.Exception(() => ctrl.SimulateGridSnapDoubleClick(20f, 20f));
            Assert.Null(ex);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Grid click-to-place (single click or double-click, both routed through
    /// <c>SnapSelectedFrameToGridCell</c> / <c>ApplyRegionToSelectedFrame</c>) must
    /// push an undo entry when it actually repositions the selected frame — just
    /// like a handle drag does via <c>FrameRegionChangedCommand</c>. Silently
    /// repositioning a frame with no way to undo it is the reported bug.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_RepositionsFrame_RecordsUndoEntry()
    {
        var ctx = ResetSingletons();
        // 4×4 frame at pixel (5,5); snap-click at (20,20) with cellSize=16 moves it to (16,16).
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 5, frameY: 5, frameW: 4, frameH: 4);
        try
        {
            ctrl.SetGrid(true, 16);

            ctrl.SimulateGridSnapDoubleClick(20f, 20f);

            Assert.True(ctx.UndoManager.CanUndo,
                "Repositioning the selected frame via grid click-to-place must record an undo entry.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Undoing after a grid click-to-place reposition must restore the frame's
    /// exact pre-click UV coordinates.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_RepositionsFrame_UndoRestoresOriginalPosition()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 5, frameY: 5, frameW: 4, frameH: 4);
        try
        {
            ctrl.SetGrid(true, 16);

            float origLeft = frame.LeftCoordinate, origTop = frame.TopCoordinate;
            float origRight = frame.RightCoordinate, origBottom = frame.BottomCoordinate;

            ctrl.SimulateGridSnapDoubleClick(20f, 20f);
            Assert.NotEqual(origLeft, frame.LeftCoordinate);   // sanity: it did move

            ctx.UndoManager.Undo();

            Assert.Equal(origLeft,   frame.LeftCoordinate,   precision: 5);
            Assert.Equal(origTop,    frame.TopCoordinate,    precision: 5);
            Assert.Equal(origRight,  frame.RightCoordinate,  precision: 5);
            Assert.Equal(origBottom, frame.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Grid click-to-place at a position that does not actually move the frame
    /// (already aligned) must not record a no-op undo entry.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapDoubleClick_NoActualMovement_DoesNotRecordUndo()
    {
        var ctx = ResetSingletons();
        // Frame already at the grid-aligned origin (0,0) with cellSize=16.
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 0, frameY: 0, frameW: 4, frameH: 4);
        try
        {
            ctrl.SetGrid(true, 16);

            ctrl.SimulateGridSnapDoubleClick(5f, 5f);   // snaps to (0,0) — same as current origin

            Assert.False(ctx.UndoManager.CanUndo,
                "Clicking a cell that doesn't change the frame's position must not record undo.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Plain single click in Grid mode: select, never silently reposition ────

    /// <summary>
    /// Was never requested by any issue (traced through #538 and #363, both of
    /// which govern the explicit double-click placement gesture only) — a plain
    /// single click in Grid mode must behave like plain mode: select the frame
    /// under the cursor. It must NOT move the currently-selected frame.
    ///
    /// Selecting a whole chain (no single frame) is the reachable case where more
    /// than one frame renders at once — <c>RefreshFramesInternal</c> only shows the
    /// individually-selected frame's box when one frame is selected, so this test
    /// starts from chain-selection to make frame B actually hit-testable.
    /// </summary>
    [AvaloniaFact]
    public void GridPlainClick_OnAnotherFrame_SelectsThatFrame_DoesNotMoveSelectedFrame()
    {
        var ctx = ResetSingletons();
        // Frame A: 4x4 at (5,5). Frame B: 4x4 at (40,40). Whole chain selected (no single frame).
        var (ctrl, frameA, dir) = BuildCtrlWithFrame(ctx, frameX: 5, frameY: 5, frameW: 4, frameH: 4);
        try
        {
            var chain = ctx.SelectedState.SelectedChain!;
            var frameB = new AnimationFrameSave
            {
                TextureName      = frameA.TextureName,
                LeftCoordinate   = 40f / 64f, TopCoordinate    = 40f / 64f,
                RightCoordinate  = 44f / 64f, BottomCoordinate = 44f / 64f,
            };
            chain.Frames.Add(frameB);
            ctx.SelectedState.SelectedFrame = null;   // show the whole chain, not just one frame
            ctrl.RefreshFrames();

            float aL = frameA.LeftCoordinate, aT = frameA.TopCoordinate;
            float aR = frameA.RightCoordinate, aB = frameA.BottomCoordinate;

            ctrl.SetGrid(true, 16);
            ctrl.SimulateGridPlainClick(42f, 42f);   // lands inside frame B

            Assert.Same(frameB, ctx.SelectedState.SelectedFrame);
            Assert.Equal(aL, frameA.LeftCoordinate,   precision: 5);
            Assert.Equal(aT, frameA.TopCoordinate,    precision: 5);
            Assert.Equal(aR, frameA.RightCoordinate,  precision: 5);
            Assert.Equal(aB, frameA.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// A plain click on empty canvas (no frame under the cursor) in Grid mode
    /// must be a no-op — it must not reposition the currently-selected frame,
    /// and must not record an undo entry.
    /// </summary>
    [AvaloniaFact]
    public void GridPlainClick_OnEmptySpace_DoesNotMoveSelectedFrame_NoUndo()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithFrame(ctx, frameX: 5, frameY: 5, frameW: 4, frameH: 4);
        try
        {
            float origLeft = frame.LeftCoordinate, origTop = frame.TopCoordinate;
            float origRight = frame.RightCoordinate, origBottom = frame.BottomCoordinate;

            ctrl.SetGrid(true, 16);
            ctrl.SimulateGridPlainClick(45f, 45f);   // empty space, far from frame

            Assert.Equal(origLeft,   frame.LeftCoordinate,   precision: 5);
            Assert.Equal(origTop,    frame.TopCoordinate,    precision: 5);
            Assert.Equal(origRight,  frame.RightCoordinate,  precision: 5);
            Assert.Equal(origBottom, frame.BottomCoordinate, precision: 5);
            Assert.False(ctx.UndoManager.CanUndo,
                "A plain click on empty space in Grid mode must not record an undo entry.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Resize: dragging a size handle must not snap position ──────────────────

    /// <summary>
    /// Resizing a frame with the grid on must snap only the dragged edges — the
    /// opposite (position) edges must stay exactly where they were, even if they
    /// are off-grid. Regression guard: before the display-snap removal the drag
    /// started from the grid-snapped display rect, so resizing also yanked the
    /// frame's position to the grid.
    /// </summary>
    [AvaloniaFact]
    public void SimulateHandleDrag_ResizeWithGridOn_PreservesOffGridPositionEdges()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // Off-grid origin at (13,23); 4×4 → bottom-right at (17,27).
            LeftCoordinate   = 13f / 100f,
            TopCoordinate    = 23f / 100f,
            RightCoordinate  = 17f / 100f,
            BottomCoordinate = 27f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 16);

            // Drag the bottom-right handle from (17,27) to (44,44): right/bottom snap
            // to 48 (nearest 16); left/top must remain the off-grid 13/23.
            ctrl.SimulateHandleDrag(HandleKind.BotRight, 17f, 27f, 44f, 44f);

            Assert.Equal(13f / 100f, frame.LeftCoordinate,   precision: 5);
            Assert.Equal(23f / 100f, frame.TopCoordinate,    precision: 5);
            Assert.Equal(48f / 100f, frame.RightCoordinate,  precision: 5);
            Assert.Equal(48f / 100f, frame.BottomCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Dragging the right handle left past an off-grid left edge with grid on must
    /// not invert the frame (Right &lt; Left, handles rendering on the inside). The
    /// right edge stays clamped to the left edge; the frame never flips.
    /// </summary>
    [AvaloniaFact]
    public void SimulateHandleDrag_ResizeRightEdgePastOffGridLeftWithGridOn_DoesNotInvert()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black, 100, 100);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = System.IO.Path.GetFileName(png),
            // Off-grid X and width: left=21, right=45 (width 24).
            LeftCoordinate   = 21f / 100f,
            TopCoordinate    = 21f / 100f,
            RightCoordinate  = 45f / 100f,
            BottomCoordinate = 45f / 100f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 16);

            // Grab the right edge at x=45 and drag far left to x=5 (past left=21).
            ctrl.SimulateHandleDrag(HandleKind.MidRight, 45f, 33f, 5f, 33f);

            // Frame must stay non-inverted; left edge stays put at 21.
            Assert.True(frame.RightCoordinate > frame.LeftCoordinate,
                $"Frame inverted: Left={frame.LeftCoordinate}, Right={frame.RightCoordinate}");
            Assert.Equal(21f / 100f, frame.LeftCoordinate, precision: 5);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
