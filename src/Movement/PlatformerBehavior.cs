using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;

namespace FlatRedBall2.Movement;

public class PlatformerBehavior
{
    /// <summary>
    /// Movement values used while the entity is on the ground. Defaults to
    /// <see cref="AirMovement"/> if null.
    /// </summary>
    public PlatformerValues? GroundMovement { get; set; }

    public PlatformerValues AirMovement { get; set; } = new();

    // Input — must be set before Update is called
    public IPressableInput? JumpInput { get; set; }
    public I2DInput? MovementInput { get; set; }

    /// <summary>
    /// The entity's collision rectangle, used by ground-snap to determine feet position. Feet Y is
    /// derived as <c>CollisionShape.AbsoluteY - CollisionShape.Height / 2</c> at probe time, so it
    /// tracks the shape automatically regardless of the entity origin. Required when
    /// <see cref="PlatformerValues.SlopeSnapDistance"/> &gt; 0 — if a relationship with
    /// <see cref="SlopeCollisionMode.PlatformerFloor"/> tries to contribute a snap target while
    /// this is null, <see cref="ConsiderSnappingTo"/> throws.
    /// </summary>
    public AxisAlignedRectangle? CollisionShape { get; set; }

    /// <summary>
    /// Optional diagnostic hook invoked once per <see cref="ConsiderSnappingTo"/> call describing
    /// why ground-snap did or did not fire. Useful when snap "doesn't work" to avoid inserting
    /// print statements into engine code.
    /// <para>
    /// All messages are prefixed with <c>[fN]</c> where N is a monotonic frame counter — this lets
    /// you tell same-frame repeats from adjacent-frame messages at a glance (e.g. two <c>snap:</c>
    /// messages with the same <c>[fN]</c> would indicate a guard bug; with different <c>[fN]</c>
    /// they are simply two successful snaps in adjacent frames).
    /// Success message: <c>"[fN] snap: y=... hit=(...) normal=(...)"</c>.
    /// Skip messages contain <c>"skip: "</c> naming the gate that aborted (e.g.
    /// <c>"[fN] skip: raycast missed ..."</c>, <c>"[fN] skip: already snapped this frame"</c>).
    /// </para>
    /// <para>
    /// Example: <c>behavior.OnSnapDiagnostic = msg =&gt; System.Diagnostics.Debug.WriteLine(msg);</c>.
    /// When null, no message is formatted — zero allocation cost.
    /// </para>
    /// </summary>
    public Action<string>? OnSnapDiagnostic { get; set; }

    /// <summary>
    /// When true and <see cref="OnSnapDiagnostic"/> is set, emits a <c>state:</c> line at the top
    /// of every <see cref="Update"/> call with the entity's Y, velocity, ground flags, and
    /// <c>LastReposition.Y</c>. Lets you trace per-frame state even on frames where no snap probe
    /// fires — useful when diagnosing "what does the entity look like each frame" vs
    /// "what does snap do when it fires".
    /// </summary>
    public bool DiagLogPerFrameState { get; set; }

    /// <summary>Reflects the ground state determined during the most recent <see cref="Update"/> call.</summary>
    public bool IsOnGround { get; private set; }

    /// <summary>
    /// Signed slope angle (degrees, -90 to 90) of the surface the entity is currently standing
    /// on. Positive values indicate a surface that rises in the +X direction, negative values
    /// indicate a surface that rises in the -X direction. <c>0</c> when on flat ground or
    /// airborne. Refreshed each frame by a short downward probe from any
    /// <see cref="SlopeCollisionMode.PlatformerFloor"/> relationship; consumed by the uphill/
    /// downhill speed multiplier in <see cref="Update"/>.
    /// </summary>
    public float CurrentSlope { get; internal set; }

    /// <summary>
    /// The horizontal direction the entity is currently facing.
    /// Updated each frame from <see cref="MovementInput"/>: non-zero X input sets the direction;
    /// zero input leaves it unchanged (last direction is remembered).
    /// </summary>
    public HorizontalDirection DirectionFacing { get; private set; } = HorizontalDirection.Right;

    /// <summary>
    /// True while the jump sustain is active — the jump button is held and
    /// <see cref="PlatformerValues.JumpApplyLength"/> has not yet elapsed.
    /// </summary>
    public bool IsApplyingJump { get; private set; }

    /// <summary>
    /// True while this frame's <see cref="Update"/> determined the entity should pass through
    /// one-way (jump-through / cloud) collision relationships — either because a Down+Jump
    /// drop-through was just triggered (suppresses for one frame so the entity clears the
    /// surface) or because the entity is airborne with Down held. After one suppressed frame,
    /// the entity's <c>LastPosition</c> is below the surface, so the one-way gate's positional
    /// check naturally prevents re-landing without further suppression. Consumed by
    /// <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}"/> when its
    /// <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}.OneWayDirection"/> is set.
    /// </summary>
    public bool IsSuppressingOneWayCollision => _suppressOneWay;

    private bool _dropThroughFrame;
    private bool _suppressOneWay;
    // Per-frame additive horizontal velocity contributed by a platform the entity is standing on.
    // Set during collision dispatch (CollisionRelationship.TryTransferPlatformVelocity) and
    // consumed in Update before being reset. Survives from collision time → Update time because
    // collision runs before CustomActivity in the frame loop.
    private float _pendingGroundHorizontalVelocity;

    /// <summary>
    /// The horizontal velocity currently being transferred from a moving platform the entity is
    /// standing on, or 0 when not on a moving platform. Reflects the value applied during the most
    /// recent <see cref="Update"/>. Useful when computing the entity's velocity relative to the
    /// platform — e.g. <c>VelocityX - GroundHorizontalVelocity</c> is zero when the player is idle
    /// on a moving platform, regardless of platform speed.
    /// </summary>
    public float GroundHorizontalVelocity { get; private set; }

    private TimeSpan _jumpStartTime;
    private PlatformerValues? _jumpValues;
    private bool _wasOnGroundLastFrame;
    private bool _snappedThisFrame;
    private bool _slopeSampledThisFrame;
    // Monotonically increments each Update. Prefixed onto every diagnostic message so two
    // messages in the same frame are visually distinguishable from two messages in adjacent
    // frames — otherwise log output leaves ambiguous "was this one frame or two?" questions.
    private int _frameIndex;

    /// <summary>
    /// Applies platformer movement to <paramref name="entity"/> for the current frame.
    /// Must be called AFTER collision resolution — reads <c>entity.LastReposition</c>
    /// to determine whether the entity is on the ground.
    /// </summary>
    public void Update(Entity entity, FrameTime time)
    {
        if (time.DeltaSeconds == 0f) return;

        _frameIndex++;

        if (DiagLogPerFrameState && OnSnapDiagnostic != null)
        {
            OnSnapDiagnostic(
                $"[f{_frameIndex}] state: entityY={entity.Y:F2} vy={entity.VelocityY:F2} " +
                $"isOnGround={IsOnGround} wasOnGround={_wasOnGroundLastFrame} " +
                $"lastRepo.Y={entity.LastReposition.Y:F2}");
        }

        // A. Determine ground state. If a relationship already snapped us onto a surface this
        // frame, IsOnGround was set true at snap time — leave it true even if LastReposition.Y
        // isn't positive (the snap adjusted position directly, not via the collision separator).
        if (!_snappedThisFrame)
            IsOnGround = entity.LastReposition.Y > 0;

        // B. Horizontal input
        float inputX = MovementInput?.X ?? 0f;

        if (inputX > 0f)
            DirectionFacing = HorizontalDirection.Right;
        else if (inputX < 0f)
            DirectionFacing = HorizontalDirection.Left;

        var current = IsOnGround ? (GroundMovement ?? AirMovement) : AirMovement;

        if (!IsOnGround) CurrentSlope = 0f;
        float effectiveMaxSpeedX = ComputeSlopeAdjustedMaxSpeed(current, inputX);

        if (current.AccelerationTimeX == TimeSpan.Zero && current.DecelerationTimeX == TimeSpan.Zero)
        {
            entity.VelocityX = inputX * effectiveMaxSpeedX + _pendingGroundHorizontalVelocity;
        }
        else
        {
            float targetSpeed = inputX * effectiveMaxSpeedX + _pendingGroundHorizontalVelocity;
            float velocityX = entity.VelocityX;
            float diff = targetSpeed - velocityX;

            // Use AccelerationTimeX when actively speeding up in the target's direction.
            // Use DecelerationTimeX for: releasing input (target=0), reversing direction
            // (velocity sign != target sign), and braking from over-max (|velocity| > |target|).
            // Without the magnitude guard, a reversal (e.g. running right, pressing left) would
            // use AccelerationTimeX and feel instant on slippery/ice configs — FRB1 matches.
            bool speedingUp = targetSpeed != 0f
                && MathF.Sign(velocityX) == MathF.Sign(targetSpeed)
                && MathF.Abs(velocityX) < MathF.Abs(targetSpeed);

            float accelMagnitude = speedingUp
                ? (current.AccelerationTimeX > TimeSpan.Zero ? effectiveMaxSpeedX / (float)current.AccelerationTimeX.TotalSeconds : float.MaxValue)
                : (current.DecelerationTimeX > TimeSpan.Zero ? current.MaxSpeedX / (float)current.DecelerationTimeX.TotalSeconds : float.MaxValue);

            float maxDeltaV = accelMagnitude * time.DeltaSeconds;
            float clampedDiff = MathF.Abs(diff) <= maxDeltaV ? diff : maxDeltaV * MathF.Sign(diff);
            entity.AccelerationX = clampedDiff / time.DeltaSeconds;
        }

        // C. Apply gravity
        entity.AccelerationY = -current.Gravity;

        // E. Handle jump (before fall-speed clamp so jump velocity is applied first).
        // Down+Jump while grounded triggers drop-through instead of a regular jump, so the
        // entity falls through any jump-through platform it is standing on. Suppression lasts
        // one frame — after that, LastPosition is below the surface and the one-way gate's
        // positional check naturally prevents re-landing.
        float inputY = MovementInput?.Y ?? 0f;
        bool dropThroughTriggered =
            IsOnGround
            && JumpInput?.WasJustPressed == true
            && inputY < -0.5f
            && current.CanFallThroughOneWayCollision;

        if (dropThroughTriggered)
        {
            _dropThroughFrame = true;
        }
        else if (JumpInput?.WasJustPressed == true && IsOnGround)
        {
            entity.VelocityY = current.JumpVelocity;
            _jumpStartTime = time.SinceGameStart;
            _jumpValues = current;
            IsApplyingJump = true;
        }

        if (IsApplyingJump && _jumpValues != null)
        {
            if (entity.LastReposition.Y < 0)
            {
                // Ceiling hit — cancel sustain and kill upward velocity so the entity drops immediately
                IsApplyingJump = false;
                entity.VelocityY = 0f;
            }
            else if (_jumpValues.JumpApplyByButtonHold && JumpInput?.IsDown == false)
            {
                IsApplyingJump = false;
            }
            else if (time.SinceGameStart - _jumpStartTime >= _jumpValues.JumpApplyLength)
            {
                IsApplyingJump = false;
            }
            else
            {
                entity.AccelerationY = 0f;
                entity.VelocityY = _jumpValues.JumpVelocity;
            }
        }

        // D. Clamp fall speed (after jump sustain)
        entity.VelocityY = MathF.Max(-current.MaxFallSpeed, entity.VelocityY);

        // F. Drop-through state: the flag lasts exactly one frame (set above, consumed here).
        _suppressOneWay =
            _dropThroughFrame
            || (!IsOnGround && inputY < -0.5f && current.CanFallThroughOneWayCollision);
        _dropThroughFrame = false;

        // G. Record ground state for next frame's snap gate, and reset per-frame flags.
        _wasOnGroundLastFrame = IsOnGround;
        _snappedThisFrame = false;
        _slopeSampledThisFrame = false;
        GroundHorizontalVelocity = _pendingGroundHorizontalVelocity;
        _pendingGroundHorizontalVelocity = 0f;
    }

    /// <summary>
    /// Contributes the horizontal velocity of a platform the entity is standing on this frame.
    /// Folded into the horizontal target inside the next <see cref="Update"/> call so the entity
    /// rides the platform without input, and carries the platform's momentum into a jump.
    /// Resets to zero at the end of <see cref="Update"/> — the collision pass must call this
    /// every frame the entity is in contact with the platform.
    /// </summary>
    /// <remarks>
    /// Called automatically by <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}"/>
    /// after a successful response when the platformer side was pushed upward (landed on top of
    /// the other side, which must be an <see cref="Entity"/>). Last writer wins within a frame —
    /// if two platforms somehow contribute, only the latter is used.
    /// </remarks>
    internal void ContributeGroundVelocity(float vx) => _pendingGroundHorizontalVelocity = vx;

    private float ComputeSlopeAdjustedMaxSpeed(PlatformerValues values, float inputX)
    {
        float maxSpeed = values.MaxSpeedX;
        if (!IsOnGround || inputX == 0f || CurrentSlope == 0f) return maxSpeed;

        float absSlope = MathF.Abs(CurrentSlope);
        bool walkingUphill = MathF.Sign(inputX) == MathF.Sign(CurrentSlope);

        if (walkingUphill)
        {
            if (values.UphillStopSpeedSlope > values.UphillFullSpeedSlope &&
                absSlope >= values.UphillFullSpeedSlope)
            {
                if (absSlope >= values.UphillStopSpeedSlope) return 0f;
                float t = 1f - (absSlope - values.UphillFullSpeedSlope) /
                    (values.UphillStopSpeedSlope - values.UphillFullSpeedSlope);
                return maxSpeed * t;
            }
        }
        else
        {
            if (values.DownhillMaxSpeedMultiplier != 1f &&
                values.DownhillMaxSpeedSlope > values.DownhillFullSpeedSlope &&
                absSlope >= values.DownhillFullSpeedSlope)
            {
                float t = MathF.Min(1f, (absSlope - values.DownhillFullSpeedSlope) /
                    (values.DownhillMaxSpeedSlope - values.DownhillFullSpeedSlope));
                return maxSpeed * (1f + (values.DownhillMaxSpeedMultiplier - 1f) * t);
            }
        }
        return maxSpeed;
    }

    /// <summary>
    /// Called by <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}"/> with
    /// <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/> to offer a candidate
    /// tile collection as a ground-snap probe target for this frame. No-ops after the first
    /// successful snap within a frame so multiple relationships don't double-snap. Gates on
    /// "was grounded last frame, not grounded this frame, not rising, not jumping" ensure snap
    /// only fires on the "just ran off a surface" transition.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/> if <see cref="CollisionShape"/> is null
    /// while the active values' <see cref="PlatformerValues.SlopeSnapDistance"/> is &gt; 0 — a
    /// wiring error we surface loudly rather than silently skip.
    /// </remarks>
    internal void ConsiderSnappingTo(Entity entity, TileShapeCollection target)
    {
        if (_snappedThisFrame) { Diag("skip: already snapped this frame"); return; }

        var values = IsOnGroundForSnap(entity) ? (GroundMovement ?? AirMovement) : AirMovement;

        // Gates: snapping is specifically for the "just ran off a surface" transition.
        if (!_wasOnGroundLastFrame) { Diag("skip: not grounded last frame"); return; }
        if (entity.LastReposition.Y > 0f) { Diag("skip: already grounded"); return; }
        if (entity.VelocityY > 0f) { DiagRising(entity.VelocityY); return; }
        if (values.SlopeSnapDistance <= 0f) { Diag("skip: SlopeSnapDistance <= 0"); return; }
        // Slope gate: snap is for hugging downslopes across tile seams, not for teleporting off
        // flat ledges. If the prior-frame surface was flat, treat the edge as a cliff drop and
        // fall ballistically. CurrentSlope was set by ContributeSlopeProbe last frame and survives
        // into this frame's collision pass (CurrentSlope is only zeroed in Update when airborne,
        // and the transition Update has not yet run).
        if (CurrentSlope == 0f) { Diag("skip: prior surface flat"); return; }

        if (CollisionShape == null)
        {
            throw new InvalidOperationException(
                "PlatformerBehavior.CollisionShape is null but SlopeSnapDistance > 0 on the active PlatformerValues — " +
                "set CollisionShape to the entity's collision rect, or set SlopeSnapDistance = 0 to disable ground-snap.");
        }

        if (IsApplyingJump) { Diag("skip: jump sustain active"); return; }

        float feetY = CollisionShape.AbsoluteY - CollisionShape.Height / 2f;
        float feetOffset = feetY - entity.Y;
        // Start slightly above feet so we don't start inside a tile (Raycast returns false when
        // the start cell is occupied).
        const float epsilon = 0.01f;
        var start = new Vector2(entity.X, feetY + epsilon);
        var end = new Vector2(entity.X, feetY - values.SlopeSnapDistance);

        if (!target.Raycast(start, end, out var hit, out var normal, out var hitShape))
        {
            DiagRaycastMissed(start, end);
            return;
        }

        float minNormalY = MathF.Cos(values.SlopeSnapMaxAngleDegrees * MathF.PI / 180f);
        if (normal.Y < minNormalY) { DiagTooSteep(normal.Y, minNormalY); return; }

        float entityYBefore = entity.Y;
        entity.Y = hit.Y - feetOffset;
        entity.VelocityY = 0f;
        IsOnGround = true;
        _snappedThisFrame = true;
        DiagSnapped(entityYBefore, entity.Y, start, end, hit, normal, target, hitShape);
    }

    /// <summary>
    /// Called by <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}"/> with
    /// <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/> to sample the
    /// slope of the surface beneath the entity's feet. Updates <see cref="CurrentSlope"/>.
    /// No-ops when airborne, after the first successful sample within a frame, or when
    /// <see cref="CollisionShape"/> is null.
    /// </summary>
    internal void ContributeSlopeProbe(Entity entity, TileShapeCollection target)
    {
        if (_slopeSampledThisFrame) return;
        if (CollisionShape == null) return;
        if (!_snappedThisFrame && entity.LastReposition.Y <= 0f) return;

        float feetY = CollisionShape.AbsoluteY - CollisionShape.Height / 2f;
        const float probeUp = 0.5f;
        const float probeDown = 2f;
        var start = new Vector2(entity.X, feetY + probeUp);
        var end = new Vector2(entity.X, feetY - probeDown);

        if (!target.Raycast(start, end, out _, out var normal, out _)) return;
        if (normal.Y <= 0f) return; // ignore ceilings / walls

        CurrentSlope = MathF.Atan2(-normal.X, normal.Y) * (180f / MathF.PI);
        _slopeSampledThisFrame = true;
    }

    // Best-effort ground determination for selecting the active values during a snap probe.
    // Since ConsiderSnappingTo runs before Update sets IsOnGround for the current frame,
    // we re-derive from LastReposition here.
    private static bool IsOnGroundForSnap(Entity entity) => entity.LastReposition.Y > 0;

    // All diagnostic messages include the current frame index so adjacent-frame output is
    // distinguishable from multiple-within-one-frame output in logs.
    private void Diag(string message) => OnSnapDiagnostic?.Invoke($"[f{_frameIndex}] {message}");
    private void DiagRising(float vy)
    {
        if (OnSnapDiagnostic == null) return;
        OnSnapDiagnostic($"[f{_frameIndex}] skip: rising (VelocityY={vy:F1})");
    }
    private void DiagRaycastMissed(Vector2 start, Vector2 end)
    {
        if (OnSnapDiagnostic == null) return;
        OnSnapDiagnostic(
            $"[f{_frameIndex}] skip: raycast missed probe=({start.X:F2},{start.Y:F2})->({end.X:F2},{end.Y:F2})");
    }
    private void DiagTooSteep(float ny, float threshold)
    {
        if (OnSnapDiagnostic == null) return;
        OnSnapDiagnostic($"[f{_frameIndex}] skip: surface too steep (normal.Y={ny:F2} < {threshold:F2})");
    }
    private void DiagSnapped(float yBefore, float yAfter, Vector2 start, Vector2 end,
        Vector2 hit, Vector2 normal, TileShapeCollection target, ICollidable? hitShape)
    {
        if (OnSnapDiagnostic == null) return;
        string shapeLabel = ClassifyHitShape(target, hitShape);
        // Hit point sits on a shape edge — nudge inward (opposite the surface normal) so the
        // floor-division cell lookup lands on the occupied cell, not its neighbor.
        var (col, row) = target.GetCellAt(hit - normal * 0.01f);
        OnSnapDiagnostic(
            $"[f{_frameIndex}] snap: entityY={yBefore:F2}->{yAfter:F2} " +
            $"probe=({start.X:F2},{start.Y:F2})->({end.X:F2},{end.Y:F2}) " +
            $"hit=({hit.X:F2},{hit.Y:F2}) cell=(col={col},row={row}) " +
            $"shape={shapeLabel} normal=({normal.X:F2},{normal.Y:F2})");
    }

    private static string ClassifyHitShape(TileShapeCollection target, ICollidable? hitShape)
    {
        if (hitShape == null) return "null";
        if (hitShape is Polygon) return "Polygon";
        if (hitShape is AxisAlignedRectangle rect)
        {
            // Distinguish full-cell tiles (stored in _tiles) from sub-cell rects by reference
            // equality — reflection-free access via the GetTileAtCell helper.
            var (col, row) = target.GetCellAt(new Vector2(rect.AbsoluteX, rect.AbsoluteY));
            var cellTile = target.GetTileAtCell(col, row);
            if (ReferenceEquals(cellTile, rect)) return "AxisAlignedRectangle(full-cell)";
            return "AxisAlignedRectangle(sub-cell)";
        }
        return hitShape.GetType().Name;
    }
}
