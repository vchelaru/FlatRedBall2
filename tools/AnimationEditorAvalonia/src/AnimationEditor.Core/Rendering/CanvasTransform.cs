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
    /// Computes <c>(panX, panY, zoom)</c> to frame a texture-space rectangle
    /// (<paramref name="rectL"/>..<paramref name="rectR"/>, <paramref name="rectT"/>..<paramref name="rectB"/>)
    /// centered in a <paramref name="viewW"/>×<paramref name="viewH"/> viewport. The rect is fitted to
    /// <paramref name="fitFraction"/> of the viewport (e.g. 0.7 leaves context around it); zoom is
    /// clamped to [<see cref="MinZoom"/>, <paramref name="maxZoom"/>], so a 1-pixel region snaps to
    /// <paramref name="maxZoom"/> and centers rather than magnifying absurdly. Pan uses the top-left
    /// convention (<c>screenX = panX + textureX × zoom</c>); the caller should clamp it to the valid band.
    /// </summary>
    public static (float PanX, float PanY, float Zoom) FitRect(
        float rectL, float rectT, float rectR, float rectB,
        float viewW, float viewH,
        float fitFraction, float maxZoom)
    {
        float rectW = Math.Max(1e-3f, rectR - rectL);
        float rectH = Math.Max(1e-3f, rectB - rectT);

        float zoom = Math.Clamp(
            Math.Min(viewW / rectW, viewH / rectH) * fitFraction,
            MinZoom, maxZoom);

        float centerX = (rectL + rectR) / 2f;
        float centerY = (rectT + rectB) / 2f;
        return (viewW / 2f - centerX * zoom, viewH / 2f - centerY * zoom, zoom);
    }

    /// <summary>
    /// True when a texture-space rectangle of <paramref name="rectW"/>×<paramref name="rectH"/> pixels,
    /// drawn at <paramref name="zoom"/>, is too large to fit inside a
    /// <paramref name="viewW"/>×<paramref name="viewH"/> viewport — so at least one edge (and its
    /// resize handle) is necessarily off-screen no matter how the camera is panned. Gates the #616
    /// auto-fit of a freshly added or retextured frame: a frame that already fits is left untouched.
    /// </summary>
    public static bool RectExceedsViewport(
        float rectW, float rectH, float zoom, float viewW, float viewH)
        => rectW * zoom > viewW || rectH * zoom > viewH;

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

    /// <summary>
    /// Device-pixel offset added after rounding in <see cref="SnapPanForPointSampling"/>. Any
    /// power-of-two fraction that is an odd multiple of 1/16 keeps texel boundaries at least 1/16
    /// of a pixel from a pixel centre for the common zooms (multiples of 1/8 device pixel), and is
    /// far below anything visible.
    /// </summary>
    public const float PointSamplingBias = 1f / 16f;

    /// <summary>
    /// Snaps pan so point-sampling never lands a texel boundary exactly on a device pixel centre --
    /// only relevant at <paramref name="zoom"/> &gt;= 1, where <c>TextureViewport</c> switches to
    /// nearest-neighbor sampling for pixel-art fidelity. Below that, bilinear sampling blends
    /// continuously across any offset, so the pan is returned unchanged.
    /// <para>
    /// Rounds to a whole <i>device</i> pixel (<paramref name="renderScaling"/> converts logical to
    /// device units), then adds <see cref="PointSamplingBias"/>. Rounding alone is not enough: at a
    /// fractional zoom such as 1.5 every other texel boundary lands on a pixel centre even with an
    /// integer pan, and at 150% display scaling an integer logical pan is a half device pixel.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A boundary exactly on a pixel centre is a genuine nearest-neighbor tie, and GPU/driver
    /// tie-breaking isn't stable when the geometry changes. Issue #1140 follow-up: fit-to-view landed
    /// on <c>panY = -3446.5</c> at zoom 4 and the image shimmered with an unchanged camera. Fractional
    /// zoom (1.5) reproduced it again as sizzling while resizing a panel, because the first fix only
    /// handled integer zoom. Uses <see cref="MathF.Round(float)"/> (ties-to-even) rather than
    /// floor/ceiling so fits aren't biased in one direction.
    /// </remarks>
    public static (float PanX, float PanY) SnapPanForPointSampling(
        float panX, float panY, float zoom, float renderScaling)
    {
        if (zoom < 1f) return (panX, panY);
        return (Snap(panX, renderScaling), Snap(panY, renderScaling));

        static float Snap(float pan, float scale) => (MathF.Round(pan * scale) + PointSamplingBias) / scale;
    }

    /// <summary>
    /// Snaps the centre of a centred, point-sampled footprint (the AnimationEditor preview's sprite)
    /// so the footprint's top-left lands on a whole device pixel plus <see cref="PointSamplingBias"/>.
    /// Below <paramref name="zoom"/> 1 the sampling is bilinear and the centre is returned unchanged.
    /// <para>
    /// Resizing a panel by 1 px slides a centred sprite by 0.5 px. Drawn as-is, every texel is
    /// resampled at a half-pixel offset on each step and the sprite sizzles. Snapping makes it move
    /// in whole pixels instead: shift, hold, shift, hold. Rounds half up (not ties-to-even) so that
    /// alternation stays even rather than pairing up.
    /// </para>
    /// </summary>
    public static (float Cx, float Cy) SnapCenterForPointSampling(
        float cx, float cy, float footprintW, float footprintH, float zoom, float renderScaling)
    {
        if (zoom < 1f) return (cx, cy);
        return (SnapEdge(cx, footprintW, renderScaling), SnapEdge(cy, footprintH, renderScaling));

        static float SnapEdge(float centre, float extent, float scale)
        {
            float edge = centre - extent / 2f;
            return (MathF.Floor(edge * scale + 0.5f) + PointSamplingBias) / scale + extent / 2f;
        }
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
