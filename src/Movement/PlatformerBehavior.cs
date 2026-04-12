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

    private TimeSpan _jumpStartTime;
    private PlatformerValues? _jumpValues;
    private bool _wasOnGroundLastFrame;
    private bool _snappedThisFrame;
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

        if (!current.UsesAcceleration || (current.AccelerationTimeX == TimeSpan.Zero && current.DecelerationTimeX == TimeSpan.Zero))
        {
            entity.VelocityX = inputX * current.MaxSpeedX;
        }
        else
        {
            float targetSpeed = inputX * current.MaxSpeedX;
            float velocityX = entity.VelocityX;
            float diff = targetSpeed - velocityX;

            // Use AccelerationTimeX when pressing toward target; DecelerationTimeX when releasing or braking.
            // "Speeding up" = diff and target are in the same direction (both nonzero and same sign).
            bool speedingUp = targetSpeed != 0f && diff != 0f && MathF.Sign(diff) == MathF.Sign(targetSpeed);

            float accelMagnitude = speedingUp
                ? (current.AccelerationTimeX > TimeSpan.Zero ? current.MaxSpeedX / (float)current.AccelerationTimeX.TotalSeconds : float.MaxValue)
                : (current.DecelerationTimeX > TimeSpan.Zero ? current.MaxSpeedX / (float)current.DecelerationTimeX.TotalSeconds : float.MaxValue);

            float maxDeltaV = accelMagnitude * time.DeltaSeconds;
            float clampedDiff = MathF.Abs(diff) <= maxDeltaV ? diff : maxDeltaV * MathF.Sign(diff);
            entity.AccelerationX = clampedDiff / time.DeltaSeconds;
        }

        // C. Apply gravity
        entity.AccelerationY = -current.Gravity;

        // E. Handle jump (before fall-speed clamp so jump velocity is applied first)
        if (JumpInput?.WasJustPressed == true && IsOnGround)
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

        // F. Record ground state for next frame's snap gate, and reset the per-frame snap flag.
        _wasOnGroundLastFrame = IsOnGround;
        _snappedThisFrame = false;
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
