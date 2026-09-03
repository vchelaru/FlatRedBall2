using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using NativeGumBatch = RenderingLibrary.Graphics.GumBatch;
using NativeRenderer = RenderingLibrary.Graphics.Renderer;

namespace FlatRedBall2.UI;

/// <summary>
/// <see cref="IRenderBatch"/> implementation for Gum UI elements. Wraps Gum's own
/// <c>RenderingLibrary.Graphics.GumBatch</c> so that Gum draws can be interleaved with
/// world-space game objects via the Screen's Layer/Z sort.
/// </summary>
public class GumRenderBatch : IRenderBatch
{
    /// <summary>Singleton for normal per-camera HUD — zoom-coupled. Every <see cref="GumRenderable"/> on a non-screen-space <see cref="Layer"/> shares this batch.</summary>
    public static readonly GumRenderBatch Instance = new GumRenderBatch(usesCameraZoom: true);

    /// <summary>
    /// Singleton for HUD on a <see cref="Layer"/> with <see cref="Layer.IsScreenSpace"/> — applies the
    /// window-vs-design-resolution scale but ignores <see cref="Camera.Zoom"/> (issue #798).
    /// </summary>
    public static readonly GumRenderBatch ScreenSpaceInstance = new GumRenderBatch(usesCameraZoom: false);

    private readonly bool _usesCameraZoom;
    private NativeGumBatch? _inner;

    // TEMP diagnostic (batching investigation) - remove once done. Per-cycle breakdown: how many
    // Begin/End cycles actually happen per real frame, and what each one's OWN independently
    // measured GraphicsDevice.Metrics.DrawCount delta is, next to Gum's own cumulative
    // RenderStateChangeStatistics.DrawCallCount at that point - to find exactly where a
    // per-frame total diverges from the sum of its own cycles.
    private static long s_metricsBeforeThisCycle;
    private static int s_globalCycleIndex;

    private GumRenderBatch(bool usesCameraZoom)
    {
        _usesCameraZoom = usesCameraZoom;
    }

    /// <summary>
    /// Creates the inner <c>RenderingLibrary.Graphics.GumBatch</c>.
    /// Must be called after the engine's <c>GumService</c> has been initialized.
    /// Called automatically by <see cref="FlatRedBallService.Initialize(Microsoft.Xna.Framework.Game, EngineInitSettings)"/>.
    /// </summary>
    internal void Initialize()
    {
        _inner = new NativeGumBatch();

        // Static, process-wide — set once. Pairs with GumBatchDrawMode.Deferred in Begin() below:
        // Deferred accumulates this cycle's Draw() calls and, at End, Z-sorts them and runs them
        // through whichever SiblingOrdering is active before submitting once. Without swapping in
        // the grouped orderer, that submit still walks in Z order (same as HierarchicalOrderer) and
        // gets none of the same-texture batching Deferred mode exists to enable.
        NativeRenderer.SiblingOrdering = RenderingLibrary.Graphics.BatchKeyGroupedOrderer.Instance;
    }

    /// <inheritdoc/>
    public bool FlipsY => false; // Gum renders in screen space; no Y-flip transform applied

    /// <summary>
    /// The Gum render-zoom this instance drives from <paramref name="camera"/>: <see cref="Camera.PixelsPerUnit"/>
    /// (window scale × <see cref="Camera.Zoom"/>) for <see cref="Instance"/>, or window scale alone
    /// (<c>Camera.Zoom</c> excluded) for <see cref="ScreenSpaceInstance"/>. Extracted from <see cref="Begin"/>
    /// so it's testable without a <c>GraphicsDevice</c>-backed <c>SystemManagers.Default</c>.
    /// </summary>
    internal float ResolveZoom(Camera camera) =>
        _usesCameraZoom ? camera.PixelsPerUnit : camera.Viewport.Height / (float)camera.OrthogonalHeight;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
    {
        // We drive Gum rendering and Gum hit-testing from a single source — Renderer.Camera —
        // which Gum's GetZoomAndMatrix bakes into basicEffect.View, and which
        // Cursor.XRespectingGumZoomAndBounds / Camera.ScreenToWorld read directly when converting
        // window pixels into canvas units (ScreenToWorld is what Gum's own cursor hit-testing
        // actually uses for FRB2-hosted UI). Pass null to GumBatch.Begin so we don't double-apply
        // the scale on top of what we just set.
        SyncCursorCamera(RenderingLibrary.SystemManagers.Default.Renderer.Camera, camera);

        // TEMP diagnostic - remove once done.
        s_metricsBeforeThisCycle = spriteBatch.GraphicsDevice.Metrics.DrawCount;

        // Deferred: Draw(element) calls in this cycle accumulate instead of submitting one at a
        // time, and get Z-sorted + run through SiblingOrdering (set in Initialize above) as one
        // batch at End(). Gum resets its own RenderStateChangeStatistics/LastFrameDrawStates once
        // per host frame internally (Renderer.TryResetPerformanceStatsForHostFrame, keyed off
        // GumService.Update's Activity tick) — FRB2 must NOT reset them here itself, since this
        // Begin runs multiple times per host frame (once per camera, plus the overlay pass), and
        // resetting on each cycle would wipe out the earlier cycles' counts instead of letting
        // them accumulate across the frame.
        _inner!.Begin(null, mode: NativeRenderer.GumBatchDrawMode.Deferred);
    }

    /// <summary>
    /// Syncs <paramref name="gumCamera"/> (Gum's own <c>RenderingLibrary.Camera</c>, a single object
    /// shared by every <see cref="Begin"/> call in a frame) from this frame's FlatRedBall
    /// <paramref name="camera"/>. Gum's cursor hit-testing reads <c>Zoom</c> and
    /// <c>ClientWidth</c>/<c>ClientHeight</c>/<c>ClientLeft</c>/<c>ClientTop</c> off this object, so
    /// all four must track the real viewport — not just <c>Zoom</c> — or hit-testing drifts from the
    /// rendered position whenever window size != design resolution (issue #824).
    /// </summary>
    internal void SyncCursorCamera(RenderingLibrary.Camera gumCamera, Camera camera)
    {
        gumCamera.Zoom = ResolveZoom(camera);
        gumCamera.ClientWidth = camera.Viewport.Width;
        gumCamera.ClientHeight = camera.Viewport.Height;
        gumCamera.ClientLeft = camera.Viewport.X;
        gumCamera.ClientTop = camera.Viewport.Y;
    }

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch)
    {
        _inner!.End();

        // TEMP diagnostic (batching investigation) - remove once done. Per-cycle log, appended
        // (not overwritten) so every cycle across every frame in this run is visible: this
        // cycle's own independently-measured Metrics delta next to Gum's cumulative
        // RenderStateChangeStatistics.DrawCallCount at that point.
        // Capped at 200 cycles (~a couple seconds at 2 cycles/frame/60fps) so the file can't grow
        // unbounded if RenderDiagnostics is left on - this only needs a few frames' worth.
        if (FlatRedBallService.Default.RenderDiagnostics.IsEnabled && s_globalCycleIndex < 200)
        {
            try
            {
                s_globalCycleIndex++;
                long metricsAfterThisCycle = spriteBatch.GraphicsDevice.Metrics.DrawCount;
                long thisCycleDelta = metricsAfterThisCycle - s_metricsBeforeThisCycle;
                int gumCumulativeDrawCallCount = NativeRenderer.Self.RenderStateChangeStatistics.DrawCallCount;

                string cycleLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gum-cycle-debug.log");
                string line =
                    $"Cycle #{s_globalCycleIndex}: thisCycleMetricsDelta={thisCycleDelta}, " +
                    $"GumCumulativeDrawCallCount={gumCumulativeDrawCallCount}, " +
                    $"rawMetricsDrawCountNow={metricsAfterThisCycle}" + System.Environment.NewLine;
                System.IO.File.AppendAllText(cycleLogPath, line);
            }
            catch { }
        }

        // TEMPORARY diagnostic (Deferred-mode batching investigation) - remove once done.
        // Gated on the existing RenderDiagnostics.IsEnabled switch. Gum's own
        // GetDrawStateSummary()/LastFrameDrawStates are public API - no Gum changes needed - and
        // by the time _inner.End() above returns they already reflect every Begin/Draw/End cycle
        // in this host frame so far (Gum resets them once per host frame, not per cycle), so this
        // overwrites the same file every End() call and the last call of the frame leaves the
        // complete frame's data in place.
        if (FlatRedBallService.Default.RenderDiagnostics.IsEnabled)
        {
            try
            {
                // Gum resets MergeBlockedByOverlapCount/NoCandidateInWindowBreakCount/the break
                // groups once per host frame now (same cadence as DrawCallCount/LastFrameDrawStates),
                // so by the time the last End() of the frame runs, these are already a true frame
                // total - no need to append across cycles or guess which cycle was the dominant one.
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gum-draw-state-debug.log");

                var renderer = RenderingLibrary.SystemManagers.Default.Renderer;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"DrawCallCount this frame: {renderer.RenderStateChangeStatistics.DrawCallCount}");
                if (NativeRenderer.SiblingOrdering is RenderingLibrary.Graphics.BatchKeyGroupedOrderer orderer)
                {
                    var byType = orderer.GetBreakGroupsByType();
                    var byIdentity = orderer.GetBreakGroups();
                    sb.AppendLine($"Batch breaks this frame ({byType.Count} distinct type pairs):");
                    foreach (var typeGroup in byType)
                    {
                        sb.AppendLine(typeGroup.ToString());
                        foreach (var identityGroup in byIdentity)
                        {
                            if (identityGroup.FromRenderableType != typeGroup.FromRenderableType ||
                                identityGroup.ToRenderableType != typeGroup.ToRenderableType)
                            {
                                continue;
                            }
                            string from = DescribeSortKey(identityGroup.FromSortKey);
                            string to = DescribeSortKey(identityGroup.ToSortKey);
                            sb.AppendLine($"    {from} -> {to} ({identityGroup.Count})");
                        }
                    }
                }
                System.IO.File.WriteAllText(logPath, sb.ToString());
            }
            catch { }
        }
    }

    // TEMPORARY diagnostic (Deferred-mode batching investigation) - remove once done.
    // Gum core's BatchSortKey is a raw object (backend-agnostic); on MonoGame it's typically the
    // Texture2D a Sprite/NineSlice/Text draws from, so this is where the FRB2-side type knowledge
    // (Texture2D.Name, set by the content pipeline to the full asset path) turns that into
    // something readable.
    // Not private: Solitaire.Common's on-screen perf overlay (GameScreen.UpdatePerfOverlay) also
    // needs to describe BatchSortKey values.
    public static string DescribeSortKey(object? sortKey) => sortKey switch
    {
        null => "-",
        Texture2D texture when !string.IsNullOrEmpty(texture.Name) => System.IO.Path.GetFileName(texture.Name),
        Texture2D => "unnamed texture",
        _ => sortKey.ToString() ?? "unknown",
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Reads <c>Renderer.Self.RenderStateChangeStatistics.DrawCallCount</c> — Gum's own
    /// backend-neutral GPU draw-call count, covering both its <c>SpriteBatch</c> and
    /// Apos.Shapes work, reset once per host frame (see <see cref="Begin"/>) and accumulated
    /// across every camera/overlay cycle in that frame.
    /// </remarks>
    public int InternalDrawCallCount => NativeRenderer.Self.RenderStateChangeStatistics.DrawCallCount;

    /// <summary>Draws a Gum element within an active Begin/End block.</summary>
    internal void DrawElement(GraphicalUiElement element)
    {
        _inner!.Draw(element);
    }
}
