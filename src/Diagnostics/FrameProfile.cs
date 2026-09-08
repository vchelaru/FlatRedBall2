namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Per-frame timing and GPU-metrics breakdown captured by the engine and exposed as
/// <see cref="FlatRedBallService.LastFrame"/>. Timing fields are wall-clock milliseconds; the
/// <c>*Count</c> fields are raw GPU counters from <c>GraphicsDevice.Metrics</c>. All values are
/// for the most recently completed frame — a snapshot, not a running average — caller smooths
/// if desired.
/// </summary>
/// <remarks>
/// Phase fields are end-to-end wall-clock for that pass: <see cref="PhysicsMs"/> covers entity
/// and camera physics integration; <see cref="CollisionMs"/> covers every registered
/// <see cref="Collision.CollisionRelationship{A,B}"/>'s <c>RunCollisions</c>;
/// <see cref="ActivityMs"/> covers entity <see cref="Entity.CustomActivity"/> calls plus the
/// screen's own <see cref="Screen.CustomActivity"/>. <see cref="UpdateTotalMs"/> and
/// <see cref="DrawTotalMs"/> are wall-clock for their full passes (so they exceed the sum of
/// inner phases by a small amount — input, audio, and other minor work). <see cref="FrameTotalMs"/>
/// is end-to-end Update + Draw.
/// </remarks>
public struct FrameProfile
{
    /// <summary>Entity and camera physics integration (Position += Velocity * dt + ...).</summary>
    public double PhysicsMs;

    /// <summary>Per-frame factory partition sort (sweep-and-prune precondition).</summary>
    public double PartitionSortMs;

    /// <summary>Lazy-spawn tilemap activation tick.</summary>
    public double LazySpawnMs;

    /// <summary>All <see cref="Collision.CollisionRelationship{A,B}"/> RunCollisions calls.</summary>
    public double CollisionMs;

    /// <summary>Entity <see cref="Entity.CustomActivity"/> + screen <see cref="Screen.CustomActivity"/>.</summary>
    public double ActivityMs;

    /// <summary>Entity-owned and screen-owned tween advancement.</summary>
    public double TweenMs;

    /// <summary>Per-frame content-watcher tick (file mtime polling for hot reload).</summary>
    public double ContentWatcherMs;

    /// <summary>Keyboard / mouse / gamepad / cursor state read.</summary>
    public double InputMs;

    /// <summary>Audio mixer / fade tick.</summary>
    public double AudioMs;

    /// <summary>Gum service tree walk (UI input + animation).</summary>
    public double GumUpdateMs;

    /// <summary>The full Draw pass, including Gum overlay rendering.</summary>
    public double RenderMs;

    /// <summary>Wall-clock for the engine's full <see cref="FlatRedBallService.Update"/> call.</summary>
    public double UpdateTotalMs;

    /// <summary>Wall-clock for the engine's full <see cref="FlatRedBallService.Draw"/> call.</summary>
    public double DrawTotalMs;

    /// <summary>End-to-end wall-clock for the most recent Update + Draw pair.</summary>
    public double FrameTotalMs;

    /// <summary>
    /// GPU draw calls issued this frame (<c>GraphicsDevice.Metrics.DrawCount</c>), captured just
    /// before <c>Present</c>. Each render-state change forces a batch flush, so a spike here with
    /// no change in on-screen content usually means state thrashing, not more content.
    /// </summary>
    public long DrawCallCount;

    /// <summary>
    /// Sprites/quads submitted this frame (<c>GraphicsMetrics.SpriteCount</c>). Contrast with
    /// <see cref="DrawCallCount"/> to gauge batching efficiency — a low ratio (few sprites per
    /// draw call) means batches are being flushed early, e.g. by texture or effect switches.
    /// </summary>
    public long SpriteCount;

    /// <summary>Triangles rendered this frame (<c>GraphicsMetrics.PrimitiveCount</c>).</summary>
    public long PrimitiveCount;

    /// <summary>
    /// Distinct textures bound this frame (<c>GraphicsMetrics.TextureCount</c>). Texture-atlas
    /// fragmentation — e.g. many small per-glyph font textures instead of one shared atlas —
    /// inflates this and drives up <see cref="DrawCallCount"/> via texture-swap flushes.
    /// </summary>
    public long TextureCount;
}
