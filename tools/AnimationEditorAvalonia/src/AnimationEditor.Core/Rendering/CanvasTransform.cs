using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure coordinate-transform math for a pan/zoom canvas. Shared by both editor
/// panels (Wireframe and Preview) so zoom-toward-cursor, fit-centering, and
/// pan-clamping behave identically and only have to be fixed once.
/// <para>
/// Screen-space → texture-space:  textureX = (screenX − panX) / zoom<br/>
/// Texture-space → screen-space:  screenX  = panX + textureX × zoom
/// </para>
/// All methods are static and side-effect-free; they can be unit-tested
/// without any UI or rendering infrastructure.
/// </summary>
public static class CanvasTransform
{
    /// <summary>Minimum allowed zoom factor (5 %).</summary>
    public const float MinZoom = 0.05f;

    /// <summary>Maximum allowed zoom factor (3 200 %).</summary>
    public const float MaxZoom = 32f;

    // ── Coordinate conversion ─────────────────────────────────────────────────

    /// <summary>
    /// Convert a screen-space point to texture-space pixel coordinates.
    /// </summary>
    public static (float X, float Y) ScreenToTexture(
        float screenX, float screenY,
        float panX, float panY, float zoom) =>
        ((screenX - panX) / zoom, (screenY - panY) / zoom);

    /// <summary>
    /// Convert a texture-space rectangle (pixel coordinates) to screen-space.
    /// Returns (left, top, right, bottom) in screen coordinates.
    /// </summary>
    public static (float Left, float Top, float Right, float Bottom) TextureRectToScreen(
        float left, float top, float right, float bottom,
        float panX, float panY, float zoom) =>
        (panX + left  * zoom,
         panY + top   * zoom,
         panX + right * zoom,
         panY + bottom * zoom);

    // ── Camera manipulation ───────────────────────────────────────────────────

    /// <summary>
    /// Zoom toward a screen-space pivot point by <paramref name="factor"/>.
    /// Returns updated <c>(panX, panY, zoom)</c>.
    /// The pivot's texture-space position is preserved after the zoom.
    /// <paramref name="zoom"/> is clamped to [<see cref="MinZoom"/>, <see cref="MaxZoom"/>].
    /// </summary>
    public static (float PanX, float PanY, float Zoom) ZoomToward(
        float pivotScreenX, float pivotScreenY,
        float factor,
        float panX, float panY, float zoom)
    {
        // Where the pivot lies in texture space (must stay fixed)
        float wx = (pivotScreenX - panX) / zoom;
        float wy = (pivotScreenY - panY) / zoom;

        float newZoom = Math.Clamp(zoom * factor, MinZoom, MaxZoom);

        // Reposition pan so pivot screen coords map to the same texture coords
        return (pivotScreenX - wx * newZoom,
                pivotScreenY - wy * newZoom,
                newZoom);
    }

    /// <summary>
    /// Compute <c>(panX, panY, zoom)</c> to center a bitmap of
    /// <paramref name="bitmapW"/> × <paramref name="bitmapH"/> pixels inside
    /// a control of <paramref name="ctrlW"/> × <paramref name="ctrlH"/> pixels
    /// at 85 % fit. Zoom is clamped to [<see cref="MinZoom"/>, 4].
    /// </summary>
    public static (float PanX, float PanY, float Zoom) CenterFit(
        float bitmapW, float bitmapH,
        float ctrlW,   float ctrlH)
    {
        float zoom = Math.Clamp(
            (float)Math.Min(ctrlW / bitmapW, ctrlH / bitmapH) * 0.85f,
            MinZoom, 4f);

        float panX = (ctrlW - bitmapW * zoom) / 2f;
        float panY = (ctrlH - bitmapH * zoom) / 2f;
        return (panX, panY, zoom);
    }

    /// <summary>
    /// Returns the new (panX, panY) when the user drags the canvas.
    /// The pan anchor is the screen position captured when the drag started;
    /// the anchor camera position is the pan state at that moment.
    /// </summary>
    public static (float PanX, float PanY) Pan(
        float anchorPanX, float anchorPanY,
        float anchorX,    float anchorY,
        float currentX,   float currentY)
        => (anchorPanX + currentX - anchorX,
            anchorPanY + currentY - anchorY);

    /// <summary>
    /// Clamps a center-relative pan so the displayed content can be scrolled across the
    /// viewport but never fully off it. The allowed band scales with the content's
    /// on-screen extent, so zooming in (which grows the extent) keeps every part of the
    /// content reachable instead of pinning it to one edge.
    /// <para>
    /// Pan is the screen-pixel offset of the content origin from the viewport center
    /// (the Preview panel's convention). The content extent is given in screen pixels
    /// relative to that same origin: a point at origin-offset <c>e</c> renders at
    /// <c>center + pan + e</c>. The band lets the content's far edge reach the opposite
    /// viewport edge plus <paramref name="padding"/>; passing a zero-size extent
    /// (<c>min == max == 0</c>) reproduces the simple "origin stays on screen" clamp.
    /// </para>
    /// </summary>
    public static (float PanX, float PanY) ClampPan(
        float panX, float panY,
        float viewW, float viewH,
        float contentMinX, float contentMaxX,
        float contentMinY, float contentMaxY,
        float padding = 0f)
    {
        var (minPanX, maxPanX) = PanRange(viewW, contentMinX, contentMaxX, padding);
        var (minPanY, maxPanY) = PanRange(viewH, contentMinY, contentMaxY, padding);

        return (Math.Clamp(panX, minPanX, maxPanX),
                Math.Clamp(panY, minPanY, maxPanY));
    }

    /// <summary>
    /// The inclusive <c>[Min, Max]</c> band a single pan axis may take so the content stays
    /// reachable — the per-axis band that <see cref="ClampPan"/> enforces, exposed so the same
    /// limits can drive a scrollbar's value range (#415). <paramref name="viewExtent"/> is the
    /// viewport size on that axis; <paramref name="contentMin"/>/<paramref name="contentMax"/>
    /// are the content's on-screen extent relative to the origin. A zero-size extent
    /// (<c>min == max == 0</c>) yields ±<paramref name="viewExtent"/>/2 — the simple
    /// "origin stays on screen" band.
    /// </summary>
    public static (float Min, float Max) PanRange(
        float viewExtent, float contentMin, float contentMax, float padding = 0f)
        => (-viewExtent / 2f - padding - contentMax,
             viewExtent / 2f + padding - contentMin);
}
