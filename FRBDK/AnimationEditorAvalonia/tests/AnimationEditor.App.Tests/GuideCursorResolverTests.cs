using AnimationEditor.App.Controls;
using Avalonia.Input;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Pure unit tests for <see cref="GuideCursorResolver"/>.
/// No Avalonia headless infrastructure needed — the resolver contains no UI state.
///
/// Camera math (viewWidth=64, viewHeight=64, pan=0, zoom=1):
///   cx = (64−20)/2 + 20 = 42   (screen X of world origin)
///   cy = (64−20)/2 + 20 = 42   (screen Y of world origin)
///   hitPx = 4
/// </summary>
public class GuideCursorResolverTests
{
    private const float V = 64f;  // view size used across tests

    // ── Positive hit cases ────────────────────────────────────────────────────

    [Fact]
    public void HoverAtHGuide_ReturnsNorthSouth()
    {
        // guide at worldY=0 → screen Y = cy + 0*1 = 42
        var result = GuideCursorResolver.CursorTypeAt(
            32f, 42f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Equal(StandardCursorType.SizeNorthSouth, result);
    }

    [Fact]
    public void HoverAtVGuide_ReturnsWestEast()
    {
        // guide at worldX=0 → screen X = cx + 0*1 = 42
        var result = GuideCursorResolver.CursorTypeAt(
            42f, 32f,
            hGuides: [], vGuides: [0f],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Equal(StandardCursorType.SizeWestEast, result);
    }

    // ── Near-miss / miss cases ────────────────────────────────────────────────

    [Fact]
    public void HoverFarFromHGuide_ReturnsNull()
    {
        // guide at screen Y=42; hover at Y=50 → |50-42|=8 > hitPx=4
        var result = GuideCursorResolver.CursorTypeAt(
            32f, 50f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }

    [Fact]
    public void HoverFarFromVGuide_ReturnsNull()
    {
        // guide at screen X=42; hover at X=50 → |50-42|=8 > hitPx=4
        var result = GuideCursorResolver.CursorTypeAt(
            50f, 32f,
            hGuides: [], vGuides: [0f],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }

    [Fact]
    public void NoGuides_ReturnsNull()
    {
        var result = GuideCursorResolver.CursorTypeAt(
            42f, 42f,
            hGuides: [], vGuides: [],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }

    // ── Ruler-area guard ──────────────────────────────────────────────────────

    [Fact]
    public void HoverInLeftRuler_ReturnsNull()
    {
        // px < RulerSize(20) — clicking there creates a new guide, not dragging
        var result = GuideCursorResolver.CursorTypeAt(
            10f, 42f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }

    [Fact]
    public void HoverInTopRuler_ReturnsNull()
    {
        // py < RulerSize(20)
        var result = GuideCursorResolver.CursorTypeAt(
            42f, 10f,
            hGuides: [], vGuides: [0f],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }

    // ── Pan and zoom affect guide screen position ─────────────────────────────

    [Fact]
    public void PanX_ShiftsVGuideScreenPosition()
    {
        // panX=10 → cx = 42+10 = 52; guide at worldX=0 → screen X=52
        var hitResult = GuideCursorResolver.CursorTypeAt(
            52f, 32f,
            hGuides: [], vGuides: [0f],
            panX: 10f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        var oldResult = GuideCursorResolver.CursorTypeAt(
            42f, 32f,   // old position without pan
            hGuides: [], vGuides: [0f],
            panX: 10f, panY: 0f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Equal(StandardCursorType.SizeWestEast, hitResult);
        Assert.Null(oldResult);
    }

    [Fact]
    public void PanY_ShiftsHGuideScreenPosition()
    {
        // panY=10 → cy = 42+10 = 52; guide at worldY=0 → screen Y=52
        var hitResult = GuideCursorResolver.CursorTypeAt(
            32f, 52f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 10f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        var oldResult = GuideCursorResolver.CursorTypeAt(
            32f, 42f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 10f, zoom: 1f,
            viewWidth: V, viewHeight: V);

        Assert.Equal(StandardCursorType.SizeNorthSouth, hitResult);
        Assert.Null(oldResult);
    }

    [Fact]
    public void Zoom_ScalesGuideOffset()
    {
        // zoom=2, guide at worldY=5 → screen Y = cy + 5*2 = 42+10 = 52
        var hitResult = GuideCursorResolver.CursorTypeAt(
            32f, 52f,
            hGuides: [5f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 2f,
            viewWidth: V, viewHeight: V);

        Assert.Equal(StandardCursorType.SizeNorthSouth, hitResult);
    }

    // ── Defensive guards ──────────────────────────────────────────────────────

    [Fact]
    public void ZeroSizedView_ReturnsNull()
    {
        var result = GuideCursorResolver.CursorTypeAt(
            32f, 32f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 1f,
            viewWidth: 0f, viewHeight: 0f);

        Assert.Null(result);
    }

    [Fact]
    public void ZeroZoom_ReturnsNull()
    {
        var result = GuideCursorResolver.CursorTypeAt(
            32f, 32f,
            hGuides: [0f], vGuides: [],
            panX: 0f, panY: 0f, zoom: 0f,
            viewWidth: V, viewHeight: V);

        Assert.Null(result);
    }
}
