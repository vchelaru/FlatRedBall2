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

    // ── Wireframe camera (top-left pan convention) ─────────────────────────────

    /// <summary>
    /// Clamps a wireframe pan so the texture's far edge can reach the viewport centre but no
    /// further — the texture is never scrolled fully out of view, yet any texture point can
    /// still be brought to the centre for editing. The wireframe's pan is the screen position
    /// of texture pixel (0,0): <c>screenX = panX + textureX × zoom</c>, so the per-axis band is
    /// <c>[viewport/2 − bitmap × zoom, viewport/2]</c>.
    /// <para>
    /// The clamp is a pure function of <c>(pan, zoom, viewport, bitmap)</c> — it never depends
    /// on a layout-resolved <c>ScrollViewer.Extent</c>. That is what makes a symmetric zoom
    /// in/out sequence an exact round-trip and the reachable bounds at a given zoom identical
    /// regardless of zoom direction (#422). Internally this maps the top-left pan to the
    /// centre-relative convention <see cref="ClampPan"/> uses (origin = texture top-left,
    /// on-screen extent <c>[0, bitmap × zoom]</c>, padding <c>−viewport/2</c>) and back.
    /// </para>
    /// </summary>
    public static (float PanX, float PanY) ClampWireframePan(
        float panX, float panY,
        float viewW, float viewH,
        float bitmapW, float bitmapH, float zoom)
    {
        // padding = −viewport/2 pulls the band in so the content's far edge stops at the centre.
        // Clamp each axis with its own padding (ClampPan takes a single padding for both axes).
        var (minX, maxX) = PanRange(viewW, 0f, bitmapW * zoom, -viewW / 2f);
        var (minY, maxY) = PanRange(viewH, 0f, bitmapH * zoom, -viewH / 2f);
        return (Math.Clamp(panX - viewW / 2f, minX, maxX) + viewW / 2f,
                Math.Clamp(panY - viewH / 2f, minY, maxY) + viewH / 2f);
    }

    /// <summary>
    /// Zooms the wireframe camera toward a viewport-space pivot and clamps the result, as a
    /// single pure step shared by the control and its invariant tests. Composes
    /// <see cref="ZoomToward"/> (pivot preserved) with <see cref="ClampWireframePan"/>. See
    /// <see cref="ClampWireframePan"/> for the round-trip / direction-independence guarantee.
    /// </summary>
    public static (float PanX, float PanY, float Zoom) ZoomWireframe(
        float pivotX, float pivotY, float factor,
        float panX, float panY, float zoom,
        float viewW, float viewH,
        float bitmapW, float bitmapH)
    {
        var (npx, npy, nz) = ZoomToward(pivotX, pivotY, factor, panX, panY, zoom);
        var (cpx, cpy) = ClampWireframePan(npx, npy, viewW, viewH, bitmapW, bitmapH, nz);
        return (cpx, cpy, nz);
    }

    // ── Drag auto-pan (#540) ──────────────────────────────────────────────────

    /// <summary>
    /// Camera pan velocity (screen pixels/second) to auto-scroll a viewport while a drag holds
    /// the pointer near or past its edge — the "drag past the edge and it scrolls" behaviour of
    /// most canvas editors. Zero inside the viewport minus <paramref name="marginPx"/>; ramps
    /// linearly from zero (at the margin boundary) to <paramref name="maxSpeed"/> (at the
    /// viewport edge itself), then holds at <paramref name="maxSpeed"/> for a pointer captured
    /// past the edge (a capturing drag can report coordinates outside the control's own bounds).
    /// <para>
    /// The sign follows the wireframe's pan convention (<c>screenX = panX + textureX × zoom</c>):
    /// overflow on the min side (left/top) returns a positive velocity — panX/panY must increase
    /// to reveal more content in that direction — and overflow on the max side (right/bottom)
    /// returns negative.
    /// </para>
    /// </summary>
    public static (float VelX, float VelY) AutoPanVelocity(
        float pointerX, float pointerY,
        float viewW, float viewH,
        float marginPx, float maxSpeed)
        => (AutoPanAxisVelocity(pointerX, viewW, marginPx, maxSpeed),
            AutoPanAxisVelocity(pointerY, viewH, marginPx, maxSpeed));

    private static float AutoPanAxisVelocity(float pointer, float viewExtent, float marginPx, float maxSpeed)
    {
        if (marginPx <= 0f) return 0f;

        if (pointer < marginPx)
            return Math.Clamp((marginPx - pointer) / marginPx, 0f, 1f) * maxSpeed;

        float farBoundary = viewExtent - marginPx;
        if (pointer > farBoundary)
            return -Math.Clamp((pointer - farBoundary) / marginPx, 0f, 1f) * maxSpeed;

        return 0f;
    }
}
