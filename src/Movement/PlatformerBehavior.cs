using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;

namespace FlatRedBall2.Movement;

/// <summary>
/// Drives side-scrolling platformer movement for an <see cref="Entity"/>: horizontal acceleration,
/// gravity, jump (raw or derived from min/max heights with optional sustain), ground-snap across
/// downslope seams, slope-aware speed scaling, drop-through one-way platforms, and ladder/fence
/// climbing.
/// <para>
/// <b>Lifecycle:</b> assign <see cref="GroundMovement"/>/<see cref="AirMovement"/> (and optionally
/// <see cref="ClimbingMovement"/>), wire <see cref="JumpInput"/>/<see cref="MovementInput"/>, set
/// <see cref="CollisionShape"/> to the entity's body rectangle, and call <see cref="Update"/> from
/// <c>CustomActivity</c> <b>after</b> collision resolution. The behavior reads
/// <see cref="Entity.LastReposition"/> to detect ground contact, so it must run after the
/// collision pass that produced it.
/// </para>
/// <para>
/// <b>Tuning lives in <see cref="PlatformerValues"/></b> (one slot per movement state). Author
/// values directly in C# or load them from JSON via <see cref="PlatformerConfig"/> +
/// <see cref="PlatformerConfigExtensions.ApplyTo"/>.
/// </para>
/// <para>
/// <b>Implement <see cref="IPlatformerEntity"/></b> on the owning entity so collision relationships
/// in <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/> mode discover this
/// behavior automatically and contribute their <see cref="TileShapeCollection"/> as a snap target.
/// </para>
/// </summary>
public class PlatformerBehavior
{
    /// <summary>
    /// Movement values used while the entity is on the ground. Defaults to
    /// <see cref="AirMovement"/> if null.
    /// </summary>
    public PlatformerValues? GroundMovement { get; set; }

    /// <summary>
    /// Movement values used while the entity is airborne. Always non-null (defaults to a fresh
    /// <see cref="PlatformerValues"/>) — the airborne slot's <see cref="PlatformerValues.Gravity"/>
    /// governs the jump trajectory regardless of which slot initiated the jump, since collision
    /// cancels ground gravity the moment the entity leaves a surface. <see cref="GroundMovement"/>
    /// falls back to this when null.
    /// </summary>
    public PlatformerValues AirMovement { get; set; } = new();

    /// <summary>
    /// Movement values used while <see cref="IsClimbing"/> is true. Must be non-null before
    /// <see cref="Update"/> runs with <see cref="IsClimbing"/> = true, or
    /// <see cref="InvalidOperationException"/> is thrown. Consumed fields:
    /// <see cref="PlatformerValues.MaxSpeedX"/> / <see cref="PlatformerValues.AccelerationTimeX"/>
    /// / <see cref="PlatformerValues.DecelerationTimeX"/> (horizontal on ladder),
    /// <see cref="PlatformerValues.ClimbingSpeed"/> (vertical), and
    /// <see cref="PlatformerValues.JumpVelocity"/> / <see cref="PlatformerValues.JumpApplyLength"/>
    /// / <see cref="PlatformerValues.JumpApplyByButtonHold"/> (applied when the player presses
    /// jump to leave the ladder). Gravity, slope, and drop-through fields are ignored while
    /// climbing.
    /// </summary>
    public PlatformerValues? ClimbingMovement { get; set; }

    /// <summary>
    /// Game-set flag that enters/exits the climbing state. While true, <see cref="Update"/>
    /// selects <see cref="ClimbingMovement"/> as the active slot, suppresses gravity, drives
    /// <c>VelocityY</c> from <c>MovementInput.Y * ClimbingMovement.ClimbingSpeed</c>, skips
    /// fall-speed clamping and drop-through, and on a jump-press clears this flag and applies
    /// <see cref="ClimbingMovement"/>'s jump fields. Ladder detection, enter triggers, fall-off,
    /// and X-snap remain game-code concerns. To forbid jump-off entirely (rare), null
    /// <see cref="JumpInput"/> while this is true.
    /// </summary>
    public bool IsClimbing { get; set; }

    /// <summary>
    /// Optional world-Y top of the climb column honored while <see cref="IsClimbing"/>. When
    /// non-null, the entity is clamped so the climb-detection shape's bottom sits just inside
    /// the column's top cell (preserves overlap so the climb persists at the top instead of
    /// exiting and re-grabbing). Upward velocity is zeroed at the clamp. Leave null for
    /// "walk off the top" ladders where the game handles top-exit itself. Set automatically
    /// each frame to the top of the active column when <see cref="Ladders"/> or
    /// <see cref="Fences"/> drives entry.
    /// </summary>
    public float? TopOfLadderY { get; set; }

    /// <summary>
    /// Snapping ladders — single-column, vertical-only. When assigned, the behavior runs an
    /// overlap scan against the entity's <see cref="CollisionShape"/> each frame: pressing Up while
    /// overlapping (or pressing Down while grounded above a ladder) snaps X to the column center,
    /// sets <see cref="IsClimbing"/>, and re-pins X every frame so horizontal input does not drift
    /// the entity off the ladder. Leave null to disable.
    /// </summary>
    public TileShapeCollection? Ladders { get; set; }

    /// <summary>
    /// 2D fence climb surfaces (SMW-style). When assigned, overlapping + Up/Down enters the
    /// climbing state with X preserved; horizontal input remains active for L/R traversal across
    /// the fence. Leave null to disable.
    /// </summary>
    public TileShapeCollection? Fences { get; set; }

    /// <summary>
    /// Threshold on <see cref="MovementInput"/>.Y for triggering ladder/fence enter/exit.
    /// Default 0.5 matches digital keyboard/dpad input. Lower for analog sticks with a small
    /// deadzone if the default feels unresponsive.
    /// </summary>
    public float DirectionalInputThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Optional sub-shape used for <see cref="Ladders"/>/<see cref="Fences"/> overlap detection
    /// instead of <see cref="CollisionShape"/>. When null (default), the climb scan uses
    /// <see cref="CollisionShape"/> — same shape as wall/floor collision. Assign a separate
    /// rectangle to decouple climb feel from physical body extent: a small rect at the body
    /// center gives Mario-style "must be centered on the ladder" detection (player can hang over
    /// the edge before letting go); a wider rect gives a generous grab. Parent it to the entity
    /// so it follows the body — the scan reads <c>AbsoluteX</c>/<c>AbsoluteY</c>.
    /// </summary>
    public AxisAlignedRectangle? ClimbingShape { get; set; }

    /// <summary>True while climbing a snapping ladder (X-locked). False for fence climbs and when not climbing.</summary>
    public bool IsOnLadder => IsClimbing && _lockedLadderX.HasValue;

    /// <summary>True while climbing a fence (X free).</summary>
    public bool IsOnFence => IsClimbing && Fences != null && !_lockedLadderX.HasValue;

    // Non-null while climbing a snapping ladder; null on fences. Used to re-pin X post-Update.
    private float? _lockedLadderX;
    // One-frame guard so a grounded enter (Up pressed while standing under a ladder) does not
    // self-cancel via the IsOnGround exit condition on the same frame.
    private bool _enteredClimbThisFrame;
    // Set when the top-of-ladder clamp ran this frame. Triggers a post-clamp overlap re-scan in
    // the exit gate — without this, a frame where physics overshot the top would see stale
    // (pre-clamp) preLadderCol and spuriously exit even though the clamp pulled the entity back
    // into the ladder. Without the rescan: physics overshoot → exit → fall → re-grab → bounce.
    private bool _clampedAtTopThisFrame;
    // Last known surface + column the entity was climbing. Maintained so a single frame of
    // overshoot (physics moved the entity past the topmost cell) doesn't lose TopOfLadderY —
    // without the cache, the overshoot frame computes TopOfLadderY = null, the clamp doesn't
    // fire, and the entity sails past the top. Set on enter and refreshed each frame overlap
    // is found; cleared on exit.
    private TileShapeCollection? _activeClimbSurface;
    private int _activeClimbCol;

    /// <summary>
    /// Pressable input that triggers a jump on <c>WasJustPressed</c> and sustains the jump
    /// (raises height up to the max) while <c>IsDown</c> if
    /// <see cref="PlatformerValues.JumpApplyByButtonHold"/> is true. Null disables jumping —
    /// useful while in cutscenes or to forbid jump-off from a ladder.
    /// </summary>
    public IPressableInput? JumpInput { get; set; }

    /// <summary>
    /// 2D input driving horizontal movement (X) and ladder/fence climbing (Y). Y+ is up,
    /// matching world space — pressing up on a ladder produces positive Y. Null treats input
    /// as zero on both axes.
    /// </summary>
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

        if (IsClimbing && ClimbingMovement == null)
        {
            throw new InvalidOperationException(
                "PlatformerBehavior.IsClimbing is true but ClimbingMovement is null — " +
                "assign a PlatformerValues with MaxSpeedX and ClimbingSpeed before entering the climbing state.");
        }

        _frameIndex++;

        // Pre-Update climb gate. Runs before "A. Determine ground state" so the right movement
        // slot drives this frame's velocity. Short-circuits when no climb surfaces are assigned.
        int? preLadderCol = null;
        int? preFenceCol = null;
        bool hasClimbSurfaces = Ladders != null || Fences != null;
        if (hasClimbSurfaces)
        {
            if (CollisionShape == null)
            {
                throw new InvalidOperationException(
                    "PlatformerBehavior needs CollisionShape set to resolve body overlap against " +
                    "Ladders/Fences. Assign the entity's AxisAlignedRectangle to CollisionShape " +
                    "before entering the activity loop.");
            }

            _enteredClimbThisFrame = false;
            _clampedAtTopThisFrame = false;
            float climbInputY = MovementInput?.Y ?? 0f;

            preLadderCol = FindOverlappingColumn(Ladders, entity);
            preFenceCol = FindOverlappingColumn(Fences, entity);
            bool overlappingLadder = preLadderCol.HasValue;
            bool overlappingFence = preFenceCol.HasValue;

            if (!IsClimbing)
            {
                if (overlappingLadder && ShouldEnterLadder(entity, climbInputY))
                    EnterLadder(entity, Ladders!, preLadderCol!.Value);
                else if (!overlappingLadder && entity.LastReposition.Y > 0 && climbInputY < -DirectionalInputThreshold)
                {
                    // Climb-down from standing: grounded, pressing down, no body overlap yet —
                    // but a ladder tile exists directly below the feet. Uses LastReposition.Y
                    // (current-frame ground result) rather than IsOnGround (set later in step A).
                    // lostOverlap is suppressed this frame by _enteredClimbThisFrame.
                    int? belowCol = LadderColumnBelowFeet();
                    if (belowCol.HasValue)
                        EnterLadder(entity, Ladders!, belowCol.Value);
                }
                else if (overlappingFence && ShouldEnterFence(climbInputY))
                    EnterFence();
            }

            // Refresh cached active surface+column whenever the body overlaps something. The
            // cache lets TopOfLadderY survive a single-frame physics overshoot past the top of
            // the column — without it, the overshoot frame would compute TopOfLadderY=null,
            // the clamp would not fire, and the entity would sail past the top.
            if (overlappingLadder)
            {
                _activeClimbSurface = Ladders;
                _activeClimbCol = preLadderCol!.Value;
            }
            else if (overlappingFence)
            {
                _activeClimbSurface = Fences;
                _activeClimbCol = preFenceCol!.Value;
            }

            // Track TopOfLadderY every frame we're climbing — including the entry frame and
            // overshoot frames. For a fence with an uneven top edge, the value updates as the
            // player slides L/R.
            if (IsClimbing)
            {
                TopOfLadderY = _activeClimbSurface != null
                    ? ComputeTopOfColumnY(_activeClimbSurface, _activeClimbCol)
                    : null;
            }
        }

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

        var current = IsClimbing
            ? ClimbingMovement!
            : (IsOnGround ? (GroundMovement ?? AirMovement) : AirMovement);

        if (!IsOnGround || IsClimbing) CurrentSlope = 0f;
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

        float inputY = MovementInput?.Y ?? 0f;

        if (IsClimbing)
        {
            // C'. Climbing: suppress gravity, drive VelocityY directly from input, skip
            // fall-speed clamp and drop-through. Cancelling any active jump sustain here prevents
            // "jumped into a ladder then entered climb" from leaving IsApplyingJump stuck true
            // for the rest of the sustain window.
            entity.AccelerationY = 0f;
            entity.VelocityY = inputY * current.ClimbingSpeed;
            IsApplyingJump = false;

            // Jump-off: pressing jump while climbing exits climbing and applies the climbing
            // slot's jump fields (same shape as a ground jump, sustain included). JumpVelocity = 0
            // gives a "drop off without upward velocity" feel; games that want to forbid jump-off
            // should null JumpInput while IsClimbing.
            if (JumpInput?.WasJustPressed == true)
            {
                IsClimbing = false;
                entity.VelocityY = current.JumpVelocity;
                if (current.JumpApplyLength > TimeSpan.Zero)
                {
                    _jumpStartTime = time.SinceGameStart;
                    _jumpValues = current;
                    IsApplyingJump = true;
                }
            }
            else if (TopOfLadderY.HasValue)
            {
                // Clamp so the climb shape's bottom rests 0.5 inside the column's top cell. If we
                // clamped entity.Y to TopOfLadderY directly, the climb shape would sit just above
                // the top tile — next frame's overlap scan would return null and lostOverlap would
                // exit the climb, dropping the player back into the ladder for re-grab → bounce.
                // Manual state-machine callers (no CollisionShape, no ClimbingShape) get the simple
                // clamp at TopOfLadderY; they're driving IsClimbing themselves so the bounce
                // doesn't apply.
                var probe = ClimbingShape ?? CollisionShape;
                float maxEntityY = probe != null
                    ? TopOfLadderY.Value - (probe.AbsoluteY - probe.Height / 2f - entity.Y) - 0.5f
                    : TopOfLadderY.Value;
                if (entity.Y >= maxEntityY)
                {
                    entity.Y = maxEntityY;
                    if (entity.VelocityY > 0f) entity.VelocityY = 0f;
                    _clampedAtTopThisFrame = true;
                }
            }

            // Suppress one-way collision while descending so the player can climb down
            // through jump-through (cloud) platforms on the ladder.
            _suppressOneWay = inputY < -DirectionalInputThreshold;
            _dropThroughFrame = false;
        }
        else
        {
            // C. Apply gravity
            entity.AccelerationY = -current.Gravity;

            // E. Handle jump (before fall-speed clamp so jump velocity is applied first).
            // Down+Jump while grounded triggers drop-through instead of a regular jump, so the
            // entity falls through any jump-through platform it is standing on. Suppression lasts
            // one frame — after that, LastPosition is below the surface and the one-way gate's
            // positional check naturally prevents re-landing.
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
        }

        // G. Record ground state for next frame's snap gate, and reset per-frame flags.
        _wasOnGroundLastFrame = IsOnGround;
        _snappedThisFrame = false;
        _slopeSampledThisFrame = false;
        GroundHorizontalVelocity = _pendingGroundHorizontalVelocity;
        _pendingGroundHorizontalVelocity = 0f;

        // Post-Update climb cleanup. Runs after IsOnGround is set for the current frame so the
        // exit check sees this-frame ground state (the climbing branch above leaves IsOnGround
        // alone since LastReposition still reflects collision results).
        if (hasClimbSurfaces)
        {
            float climbInputY = MovementInput?.Y ?? 0f;
            float climbInputX = MovementInput?.X ?? 0f;

            if (IsClimbing)
            {
                // If the top-of-ladder clamp moved the entity back into the ladder this frame,
                // the start-of-frame preLadderCol/preFenceCol were computed at the overshot
                // position and may be null — re-scan at the clamped position so reaching the top
                // doesn't trigger a spurious exit (which would cause a fall/re-grab bounce).
                int? lostScanLadder = preLadderCol;
                int? lostScanFence = preFenceCol;
                if (_clampedAtTopThisFrame)
                {
                    lostScanLadder = FindOverlappingColumn(Ladders, entity);
                    lostScanFence = FindOverlappingColumn(Fences, entity);
                    // Refresh cache from rescan so next frame's TopOfLadderY uses the post-clamp column.
                    if (lostScanLadder.HasValue) { _activeClimbSurface = Ladders; _activeClimbCol = lostScanLadder.Value; }
                    else if (lostScanFence.HasValue) { _activeClimbSurface = Fences; _activeClimbCol = lostScanFence.Value; }
                }
                // Suppress lostOverlap on the entry frame — when entering via "below feet" the
                // body has no overlap yet and would otherwise exit on the same frame it entered.
                bool lostOverlap = !lostScanLadder.HasValue && !lostScanFence.HasValue && !_enteredClimbThisFrame;
                bool landedWhileDescending = IsOnGround && climbInputY <= 0f && !_enteredClimbThisFrame;
                bool steppedOffTop = _clampedAtTopThisFrame
                    && MathF.Abs(climbInputX) > DirectionalInputThreshold;

                if (lostOverlap || landedWhileDescending || steppedOffTop)
                {
                    IsClimbing = false;
                    if (steppedOffTop)
                    {
                        // Snap feet to ladder top so cloud/solid collision catches the player on the next frame.
                        var probe = ClimbingShape ?? CollisionShape;
                        float feetOffset = probe != null
                            ? probe.AbsoluteY - probe.Height / 2f - entity.Y
                            : 0f;
                        entity.Y = TopOfLadderY!.Value - feetOffset;
                        entity.VelocityY = 0f;
                    }
                }
            }

            // Update may have cleared IsClimbing (jump-off) or we just cleared it above.
            if (!IsClimbing)
            {
                _lockedLadderX = null;
                _activeClimbSurface = null;
            }
            else if (_lockedLadderX.HasValue)
            {
                entity.X = _lockedLadderX.Value;
                entity.VelocityX = 0f;
            }
        }
    }

    private bool ShouldEnterLadder(Entity entity, float inputY)
    {
        if (inputY > DirectionalInputThreshold) return true;
        // Climb-down-from-top: standing on ground with a ladder cell directly below the feet.
        if (IsOnGround && inputY < -DirectionalInputThreshold && IsLadderBelowFeet())
            return true;
        return false;
    }

    private bool ShouldEnterFence(float inputY)
        => inputY > DirectionalInputThreshold || inputY < -DirectionalInputThreshold;

    private void EnterLadder(Entity entity, TileShapeCollection surface, int col)
    {
        IsClimbing = true;
        entity.VelocityY = 0f;
        entity.X = surface.GetCellWorldPosition(col, 0).X;
        _lockedLadderX = entity.X;
        _enteredClimbThisFrame = true;
    }

    private void EnterFence()
    {
        IsClimbing = true;
        // VelocityY cleared so a mid-fall entry doesn't carry residual speed into the climbing
        // slot; Update overwrites VelocityY from inputY * ClimbingSpeed next anyway, but clearing
        // matches the ladder-enter semantics and makes inputY=0 entries stationary.
        _lockedLadderX = null;
        _enteredClimbThisFrame = true;
    }

    // Finds the TSC column overlapping the entity's body (on X) that contains a climb tile (on Y)
    // within the body's vertical span. Returns the column whose center is closest to the entity X
    // when multiple overlap. Using the player's *center* column via GetCellAt alone produces an
    // off-by-one snap when the player approaches a ladder from the side — the body overlaps the
    // ladder but X floors to the adjacent empty column.
    private int? FindOverlappingColumn(TileShapeCollection? surface, Entity entity)
    {
        if (surface == null) return null;

        var body = ClimbingShape ?? CollisionShape!;
        float halfW = body.Width / 2f;
        float halfH = body.Height / 2f;
        float centerX = body.AbsoluteX;
        float bodyBottomY = body.AbsoluteY - halfH;
        float bodyTopY = body.AbsoluteY + halfH - 0.5f;

        var (colLeft, _) = surface.GetCellAt(new Vector2(centerX - halfW + 0.5f, bodyBottomY));
        var (colRight, _) = surface.GetCellAt(new Vector2(centerX + halfW - 0.5f, bodyBottomY));
        var (_, rowBottom) = surface.GetCellAt(new Vector2(centerX, bodyBottomY));
        var (_, rowTop) = surface.GetCellAt(new Vector2(centerX, bodyTopY));

        int? best = null;
        float bestDist = float.MaxValue;
        for (int c = colLeft; c <= colRight; c++)
        {
            bool hasTile = false;
            for (int r = rowBottom; r <= rowTop; r++)
            {
                if (surface.GetTileAtCell(c, r) != null) { hasTile = true; break; }
            }
            if (!hasTile) continue;

            float cx = surface.GetCellWorldPosition(c, 0).X;
            float d = MathF.Abs(cx - centerX);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // Top-of-column Y: find the top edge of the contiguous climb-tile segment in this column.
    // Scans up from feet first (handles "feet at or below the ladder"), then down if nothing was
    // found above (handles physics-overshoot frames where feet just went past the top).
    private float? ComputeTopOfColumnY(TileShapeCollection surface, int col)
    {
        var body = ClimbingShape ?? CollisionShape!;
        float bodyBottomY = body.AbsoluteY - body.Height / 2f;
        var (_, feetRow) = surface.GetCellAt(new Vector2(body.AbsoluteX, bodyBottomY));
        const int MaxSearch = 256;

        int searchRow = feetRow;
        for (int i = 0; i < MaxSearch && surface.GetTileAtCell(col, searchRow) == null; i++)
            searchRow++;

        if (surface.GetTileAtCell(col, searchRow) == null)
        {
            searchRow = feetRow - 1;
            for (int i = 0; i < MaxSearch && surface.GetTileAtCell(col, searchRow) == null; i++)
                searchRow--;
            if (surface.GetTileAtCell(col, searchRow) == null) return null;
        }

        int topRow = searchRow;
        while (surface.GetTileAtCell(col, topRow + 1) != null) topRow++;
        return surface.GetCellWorldPosition(col, topRow).Y + surface.GridSize / 2f;
    }

    private bool IsLadderBelowFeet() => LadderColumnBelowFeet().HasValue;

    private int? LadderColumnBelowFeet()
    {
        if (Ladders == null) return null;
        var body = ClimbingShape ?? CollisionShape!;
        float bodyBottomY = body.AbsoluteY - body.Height / 2f;
        var (col, row) = Ladders.GetCellAt(new Vector2(body.AbsoluteX, bodyBottomY - 1f));
        return Ladders.GetTileAtCell(col, row) != null ? col : null;
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
