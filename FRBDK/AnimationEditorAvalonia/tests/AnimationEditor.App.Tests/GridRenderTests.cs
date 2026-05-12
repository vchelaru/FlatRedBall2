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
    /// A frame whose pixel bounds are not aligned to the grid should have its
    /// displayed bounds (and UV coordinates) snapped to the nearest grid line
    /// when grid mode is active.
    /// </summary>
    [AvaloniaFact]
    public void GridEnabled_NonAlignedFrameBounds_SnapsToNearestGridLine()
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
            // Bounds in pixels: left=13, top=23, right=47, bottom=58 — none on the 10px grid.
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
        ctrl.InitializeServices(ctx.SelectedState, ctx.AppState, ctx.AppCommands, ctx.ApplicationEvents, ctx.ProjectManager, ctx.UndoManager);
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        try
        {
            ctrl.SetGrid(true, 10);

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);
            var bounds = rects[0].Bounds;

            // Each edge should be on a 10-pixel boundary.
            Assert.Equal(0, (int)bounds.Left   % 10);
            Assert.Equal(0, (int)bounds.Top    % 10);
            Assert.Equal(0, (int)bounds.Right  % 10);
            Assert.Equal(0, (int)bounds.Bottom % 10);

            // UV data on the frame itself must also be snapped.
            Assert.Equal(0, (int)MathF.Round(frame.LeftCoordinate   * 100) % 10);
            Assert.Equal(0, (int)MathF.Round(frame.TopCoordinate    * 100) % 10);
            Assert.Equal(0, (int)MathF.Round(frame.RightCoordinate  * 100) % 10);
            Assert.Equal(0, (int)MathF.Round(frame.BottomCoordinate * 100) % 10);
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
    /// Toggling grid off must leave the displayed bounds unchanged (the UV data
    /// was already snapped when grid was on, and grid-off does not re-snap).
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
}
