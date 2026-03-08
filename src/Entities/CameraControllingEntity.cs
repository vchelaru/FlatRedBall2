using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;

// Influenced by https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers

namespace FlatRedBall2.Entities;

public enum TargetApproachStyle
{
    /// <summary>
    /// The camera position locks to the target every frame with no lag.
    /// </summary>
    Immediate,

    /// <summary>
    /// The camera approaches the target at a speed proportional to its distance —
    /// fast when far, slow when close. The result is an exponential ease-in.
    /// Speed = <see cref="CameraControllingEntity.TargetApproachCoefficient"/> × distance.
    /// </summary>
    Smooth,

    /// <summary>
    /// The camera moves toward the target at a constant speed regardless of distance.
    /// Speed = <see cref="CameraControllingEntity.TargetApproachCoefficient"/> world units per second.
    /// Snaps when within one frame of the target to prevent overshoot.
    /// </summary>
    ConstantSpeed
}

/// <summary>
/// An <see cref="Entity"/> that controls a <see cref="Camera"/> by following one or more target
/// entities, with optional map bounds clamping, deadzone, pixel-perfect snapping, and screen shake.
/// </summary>
/// <remarks>
/// Create via <c>Factory&lt;CameraControllingEntity&gt;</c> — Factory calls <see cref="CustomInitialize"/>,
/// which wires up <see cref="Camera"/> automatically. <see cref="Screen.Register"/> does not call
/// <see cref="CustomInitialize"/>, so the camera will not be set if you use <c>new</c> + Register.
/// <para>Minimal setup:</para>
/// <code>
/// var camFactory = new Factory&lt;CameraControllingEntity&gt;(this);
/// var cam = camFactory.Create();
/// cam.Target = player;
/// cam.Map = mapBoundsRect; // optional; null = no clamping
/// </code>
/// </remarks>
public class CameraControllingEntity : Entity
{
    #region Fields / Properties

    /// <summary>
    /// The camera controlled by this instance.
    /// Defaults to <see cref="Screen.Camera"/> on the first <see cref="CustomActivity"/> call.
    /// Assign before the first frame to control a different camera.
    /// </summary>
    public Camera? Camera { get; set; }

    private bool _hasActivityBeenCalled;

    /// <summary>
    /// The entities to follow. In a single-player game assign one entity; in multiplayer
    /// assign all players and the camera will frame their bounding box.
    /// </summary>
    public List<Entity> Targets { get; } = new();

    /// <summary>
    /// Convenience setter for single-target following. Replaces all current <see cref="Targets"/>
    /// with the given entity. Set to <c>null</c> to clear all targets.
    /// </summary>
    public Entity? Target
    {
        set
        {
            Targets.Clear();
            if (value != null) Targets.Add(value);
        }
    }

    /// <summary>
    /// The level bounds. When set, the camera will not reveal world space outside this rectangle.
    /// Assign a non-visible <see cref="AxisAlignedRectangle"/> sized to your level.
    /// When null, the camera moves without bounds.
    /// </summary>
    public AxisAlignedRectangle? Map { get; set; }

    /// <summary>
    /// Padding inset from each edge of <see cref="Map"/>. Positive values shrink the usable camera
    /// area (the camera pulls away from map edges); negative values allow the camera to reveal space
    /// outside the map.
    /// </summary>
    public float ExtraMapPadding { get; set; }

    /// <summary>The movement style used to approach the target position each frame.</summary>
    public TargetApproachStyle TargetApproachStyle { get; set; } = TargetApproachStyle.Smooth;

    /// <summary>
    /// Controls approach speed. Ignored when <see cref="TargetApproachStyle"/> is
    /// <see cref="TargetApproachStyle.Immediate"/>.
    /// <para>
    /// <see cref="TargetApproachStyle.Smooth"/>: velocity-per-world-unit offset
    /// (e.g. 5 → camera velocity = 5× the distance to the target).
    /// </para>
    /// <para>
    /// <see cref="TargetApproachStyle.ConstantSpeed"/>: world units per second.
    /// </para>
    /// </summary>
    public float TargetApproachCoefficient { get; set; } = 5f;

    /// <summary>
    /// Width of the deadzone window in world units. While the target center is inside this
    /// window the camera does not pan horizontally. Set to 0 to disable.
    /// </summary>
    public float ScrollingWindowWidth { get; set; }

    /// <summary>
    /// Height of the deadzone window in world units. While the target center is inside this
    /// window the camera does not pan vertically. Set to 0 to disable.
    /// </summary>
    public float ScrollingWindowHeight { get; set; }

    /// <summary>
    /// When true, snaps the final camera position to the nearest pixel boundary each frame,
    /// eliminating sub-pixel shimmer in pixel-art games.
    /// The snap interval is <c>1 / Camera.PixelsPerUnit</c> world units, which accounts for
    /// both zoom and the actual viewport size.
    /// </summary>
    public bool SnapToPixel { get; set; } = true;

    /// <summary>
    /// Additive world-space offset applied on top of this entity's position when setting
    /// <see cref="Camera.X"/> / <see cref="Camera.Y"/>. Useful for screen shake — randomize
    /// this each frame via <see cref="ShakeScreen"/> and it resets to zero automatically.
    /// </summary>
    public Vector2 CameraOffset;

    /// <summary>When false, <see cref="CustomActivity"/> is a no-op.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When true, draws the deadzone window as a yellow debug overlay each frame.
    /// Useful for diagnosing camera behavior during development.
    /// </summary>
    public bool ShowDebugOverlay { get; set; }

    // ── Auto-zoom ─────────────────────────────────────────────────────────

    private float _defaultZoom = 1f;
    private float _furthestMultiplier;
    private bool _isAutoZoomEnabled;

    /// <summary>
    /// Current zoom-out multiplier applied when auto-zoom is active.
    /// 1 = no zoom out; 2 = twice the default visible area is shown.
    /// </summary>
    public float ViewableAreaMultiplier { get; private set; } = 1f;

    /// <summary>Whether to lerp the zoom smoothly when auto-zoom adjusts it.</summary>
    public bool LerpSmoothZoom { get; set; } = true;

    /// <summary>
    /// The maximum <see cref="ViewableAreaMultiplier"/> allowed before <see cref="Map"/> clamping
    /// is applied. Returns <see cref="float.PositiveInfinity"/> when <see cref="Map"/> is null.
    /// </summary>
    public float MaxViewableAreaMultiplier
    {
        get
        {
            if (Camera == null || Map == null)
                return float.PositiveInfinity;

            float defaultVisibleW = Camera.TargetWidth  / _defaultZoom;
            float defaultVisibleH = Camera.TargetHeight / _defaultZoom;
            float mapW = Map.Width  - 2 * ExtraMapPadding;
            float mapH = Map.Height - 2 * ExtraMapPadding;

            return MathF.Max(1f, MathF.Min(mapW / defaultVisibleW, mapH / defaultVisibleH));
        }
    }

    // ── IsKeepingTargetsInView ────────────────────────────────────────────

    /// <summary>
    /// When true, clamps target entity positions to remain inside the maximum viewable area.
    /// Useful in multiplayer to prevent one player from pulling the camera away from others.
    /// </summary>
    public bool IsKeepingTargetsInView { get; set; }

    #endregion

    #region Initialization

    public override void CustomInitialize()
    {
        Camera ??= Engine.CurrentScreen.Camera;
    }

    #endregion

    #region Activity

    public override void CustomActivity(FrameTime time)
    {
        if (!IsActive || Camera == null) return;

        if (ShowDebugOverlay)
            DrawDeadzoneOverlay();

        if (IsKeepingTargetsInView && _hasActivityBeenCalled)
            KeepTargetsInView();

        if (_isAutoZoomEnabled)
            ApplySeparationForZoom(GetTargetSeparation());

        var target = GetTarget();

        // On the first frame, always snap immediately so the camera doesn't slide in from (0,0).
        var approachX = _hasActivityBeenCalled ? TargetApproachStyle : TargetApproachStyle.Immediate;
        var approachY = _hasActivityBeenCalled ? TargetApproachStyle : TargetApproachStyle.Immediate;

        // When smooth-following and outside map bounds, push this entity back inside rather than
        // disabling smooth-follow (which causes a jarring snap at the boundary).
        if (Map != null && approachX != TargetApproachStyle.Immediate)
        {
            float visibleW = Camera.TargetWidth  / Camera.Zoom;
            float visibleH = Camera.TargetHeight / Camera.Zoom;
            float mapLeft   = Map.AbsoluteX - Map.Width  / 2f + ExtraMapPadding;
            float mapRight  = Map.AbsoluteX + Map.Width  / 2f - ExtraMapPadding;
            float mapBottom = Map.AbsoluteY - Map.Height / 2f + ExtraMapPadding;
            float mapTop    = Map.AbsoluteY + Map.Height / 2f - ExtraMapPadding;

            if (Map.Width - 2 * ExtraMapPadding >= visibleW)
            {
                if (X - visibleW / 2f < mapLeft)  X = mapLeft  + visibleW / 2f;
                if (X + visibleW / 2f > mapRight)  X = mapRight - visibleW / 2f;
            }
            if (Map.Height - 2 * ExtraMapPadding >= visibleH)
            {
                if (Y - visibleH / 2f < mapBottom) Y = mapBottom + visibleH / 2f;
                if (Y + visibleH / 2f > mapTop)    Y = mapTop    - visibleH / 2f;
            }
        }

        ApplyTarget(target, approachX, approachY, time.DeltaSeconds);

        _hasActivityBeenCalled = true;
    }

    #endregion

    #region Auto-zoom

    /// <summary>
    /// Enables auto-zoom to keep all <see cref="Targets"/> visible as they spread apart.
    /// The camera zooms out proportionally to the separation between targets.
    /// </summary>
    /// <param name="defaultZoom">
    /// The <see cref="Camera.Zoom"/> used when targets are close together — typically the
    /// zoom set on <see cref="Screen.Camera"/> when the screen starts.
    /// </param>
    /// <param name="furthestMultiplier">
    /// The maximum number of times the default visible area that may be shown.
    /// 1 prevents any zoom-out; <see cref="float.PositiveInfinity"/> allows unlimited zoom-out
    /// (still clamped by <see cref="MaxViewableAreaMultiplier"/> when <see cref="Map"/> is set).
    /// </param>
    public void EnableAutoZooming(float defaultZoom, float furthestMultiplier = float.PositiveInfinity)
    {
        _defaultZoom = defaultZoom;
        _furthestMultiplier = furthestMultiplier;
        _isAutoZoomEnabled = true;
    }

    private void ApplySeparationForZoom(Vector2 separation)
    {
        float noZoomDistance = MathF.Min(
            Camera!.TargetWidth  / _defaultZoom,
            Camera!.TargetHeight / _defaultZoom) * 0.8f;

        float currentDistance = separation.Length();
        float desired;

        if (currentDistance > noZoomDistance)
        {
            desired = MathF.Min(_furthestMultiplier, currentDistance / noZoomDistance);
            desired = MathF.Min(desired, MaxViewableAreaMultiplier);
            desired = MathF.Max(desired, 1f);
        }
        else
        {
            desired = 1f;
        }

        ViewableAreaMultiplier = LerpSmoothZoom
            ? Lerp(ViewableAreaMultiplier, desired, 0.1f)
            : desired;

        Camera.Zoom = _defaultZoom / ViewableAreaMultiplier;
    }

    #endregion

    #region Targeting

    /// <summary>
    /// Returns the desired camera position given the current targets, deadzone, and map bounds.
    /// Override to implement custom targeting logic.
    /// </summary>
    public Vector2 GetTarget()
    {
        var center = ComputeTargetCenter();

        float halfWindowW = ScrollingWindowWidth  / 2f;
        float halfWindowH = ScrollingWindowHeight / 2f;

        // Compute new target position relative to the deadzone window.
        float targetX, targetY;

        if      (center.X < X - halfWindowW) targetX = center.X + halfWindowW;
        else if (center.X > X + halfWindowW) targetX = center.X - halfWindowW;
        else                                 targetX = X;

        if      (center.Y < Y - halfWindowH) targetY = center.Y + halfWindowH;
        else if (center.Y > Y + halfWindowH) targetY = center.Y - halfWindowH;
        else                                 targetY = Y;

        // Clamp to map bounds.
        if (Camera != null && Map != null)
        {
            float visibleW = Camera.TargetWidth  / Camera.Zoom;
            float visibleH = Camera.TargetHeight / Camera.Zoom;
            float effectiveMapW = Map.Width  - 2 * ExtraMapPadding;
            float effectiveMapH = Map.Height - 2 * ExtraMapPadding;
            float mapLeft   = Map.AbsoluteX - Map.Width  / 2f + ExtraMapPadding;
            float mapRight  = Map.AbsoluteX + Map.Width  / 2f - ExtraMapPadding;
            float mapBottom = Map.AbsoluteY - Map.Height / 2f + ExtraMapPadding;
            float mapTop    = Map.AbsoluteY + Map.Height / 2f - ExtraMapPadding;

            if (visibleW >= effectiveMapW)
                targetX = Map.AbsoluteX;
            else
            {
                targetX = MathF.Max(targetX, mapLeft  + visibleW / 2f);
                targetX = MathF.Min(targetX, mapRight - visibleW / 2f);
            }

            if (visibleH >= effectiveMapH)
                targetY = Map.AbsoluteY;
            else
            {
                targetY = MathF.Max(targetY, mapBottom + visibleH / 2f);
                targetY = MathF.Min(targetY, mapTop    - visibleH / 2f);
            }
        }

        return new Vector2(targetX, targetY);
    }

    /// <summary>
    /// Immediately snaps this entity's position to the current target, bypassing approach smoothing.
    /// Useful for initial placement or teleportation.
    /// </summary>
    public void ForceToTarget()
    {
        var target = GetTarget();
        ApplyTarget(target, TargetApproachStyle.Immediate, TargetApproachStyle.Immediate, 0f);
    }

    private void ApplyTarget(Vector2 target, TargetApproachStyle approachX, TargetApproachStyle approachY, float dt)
    {
        switch (approachX)
        {
            case TargetApproachStyle.Smooth:
                VelocityX = (target.X - X) * TargetApproachCoefficient;
                break;
            case TargetApproachStyle.ConstantSpeed:
                float diffX = target.X - X;
                // Snap if we'd overshoot within this frame to prevent jitter.
                if (MathF.Abs(diffX) <= TargetApproachCoefficient * dt + 0.001f)
                { X = target.X; VelocityX = 0f; }
                else
                    VelocityX = MathF.Sign(diffX) * TargetApproachCoefficient;
                break;
            case TargetApproachStyle.Immediate:
                X = target.X;
                VelocityX = 0f;
                break;
        }

        switch (approachY)
        {
            case TargetApproachStyle.Smooth:
                VelocityY = (target.Y - Y) * TargetApproachCoefficient;
                break;
            case TargetApproachStyle.ConstantSpeed:
                float diffY = target.Y - Y;
                if (MathF.Abs(diffY) <= TargetApproachCoefficient * dt + 0.001f)
                { Y = target.Y; VelocityY = 0f; }
                else
                    VelocityY = MathF.Sign(diffY) * TargetApproachCoefficient;
                break;
            case TargetApproachStyle.Immediate:
                Y = target.Y;
                VelocityY = 0f;
                break;
        }

        if (SnapToPixel)
        {
            float snap = 1f / Camera!.PixelsPerUnit;
            Camera.X = MathF.Round((X + CameraOffset.X) / snap) * snap;
            Camera.Y = MathF.Round((Y + CameraOffset.Y) / snap) * snap;
        }
        else
        {
            Camera!.X = X + CameraOffset.X;
            Camera.Y  = Y + CameraOffset.Y;
        }
    }

    private Vector2 ComputeTargetCenter()
    {
        if (Targets.Count == 0)
            return new Vector2(X, Y);

        float minX = Targets[0].AbsoluteX, maxX = minX;
        float minY = Targets[0].AbsoluteY, maxY = minY;

        for (int i = 1; i < Targets.Count; i++)
        {
            float ax = Targets[i].AbsoluteX, ay = Targets[i].AbsoluteY;
            if (ax < minX) minX = ax; if (ax > maxX) maxX = ax;
            if (ay < minY) minY = ay; if (ay > maxY) maxY = ay;
        }

        return new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
    }

    private Vector2 GetTargetSeparation()
    {
        if (Targets.Count == 0) return Vector2.Zero;

        float minX = Targets[0].AbsoluteX, maxX = minX;
        float minY = Targets[0].AbsoluteY, maxY = minY;

        for (int i = 1; i < Targets.Count; i++)
        {
            float ax = Targets[i].AbsoluteX, ay = Targets[i].AbsoluteY;
            if (ax < minX) minX = ax; if (ax > maxX) maxX = ax;
            if (ay < minY) minY = ay; if (ay > maxY) maxY = ay;
        }

        return new Vector2(maxX - minX, maxY - minY);
    }

    #endregion

    #region Keep Targets In View

    private void KeepTargetsInView()
    {
        float visibleW = Camera!.TargetWidth  / Camera.Zoom * ViewableAreaMultiplier;
        float visibleH = Camera!.TargetHeight / Camera.Zoom * ViewableAreaMultiplier;
        float halfW = visibleW / 2f;
        float halfH = visibleH / 2f;

        foreach (var t in Targets)
        {
            if (t.AbsoluteX > X + halfW) t.X -= t.AbsoluteX - (X + halfW);
            if (t.AbsoluteX < X - halfW) t.X += (X - halfW) - t.AbsoluteX;
            if (t.AbsoluteY > Y + halfH) t.Y -= t.AbsoluteY - (Y + halfH);
            if (t.AbsoluteY < Y - halfH) t.Y += (Y - halfH) - t.AbsoluteY;
        }
    }

    #endregion

    #region Screen Shake

    private const float IndividualShakeDuration = 0.05f;

    /// <summary>
    /// Shakes the camera for the specified duration by randomizing <see cref="CameraOffset"/> at
    /// <c>20 Hz</c>. Resets <see cref="CameraOffset"/> to zero when finished.
    /// </summary>
    /// <param name="shakeRadius">Maximum displacement in world units.</param>
    /// <param name="durationInSeconds">Total shake duration in seconds.</param>
    /// <param name="cancellationToken">
    /// Cancel to stop early. <see cref="CameraOffset"/> is NOT reset on cancel — reset it manually
    /// if needed. Pass <see cref="Screen.Token"/> to auto-cancel on screen transition.
    /// </param>
    public async Task ShakeScreen(float shakeRadius, float durationInSeconds,
        CancellationToken cancellationToken = default)
    {
        var random = Engine.Random;
        for (float elapsed = 0; elapsed < durationInSeconds; elapsed += IndividualShakeDuration)
        {
            if (cancellationToken.IsCancellationRequested) return;
            var point = random.PointInCircle(shakeRadius);
            CameraOffset.X = point.X;
            CameraOffset.Y = point.Y;
            await Engine.TimeManager.DelaySeconds(IndividualShakeDuration, cancellationToken);
        }
        CameraOffset = Vector2.Zero;
    }

    /// <summary>
    /// Shakes the camera continuously until <paramref name="taskToAwait"/> completes.
    /// Resets <see cref="CameraOffset"/> to zero when the task finishes or is cancelled.
    /// </summary>
    public async Task ShakeScreenUntil(float shakeRadius, Task taskToAwait)
    {
        var random = Engine.Random;
        while (!taskToAwait.IsCompleted)
        {
            var point = random.PointInCircle(shakeRadius);
            CameraOffset.X = point.X;
            CameraOffset.Y = point.Y;
            try
            {
                await Engine.TimeManager.DelaySeconds(IndividualShakeDuration);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
        CameraOffset = Vector2.Zero;
    }

    #endregion

    #region Debug Overlay

    private void DrawDeadzoneOverlay()
    {
        Engine.CurrentScreen.Overlay.Rectangle(X, Y, ScrollingWindowWidth, ScrollingWindowHeight,
            XnaColor.Yellow);
    }

    #endregion

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
