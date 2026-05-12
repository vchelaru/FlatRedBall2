using System;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.Movement;

/// <summary>
/// Handles top-down movement logic for an entity, applying input to position and velocity.
/// </summary>
public class TopDownBehavior
{
    /// <summary>Movement parameters for this entity. Must be set before <see cref="Update"/> is called.</summary>
    public TopDownValues? MovementValues { get; set; }

    /// <summary>The 2D input controlling movement direction and magnitude.</summary>
    public I2DInput? MovementInput { get; set; }

    /// <summary>
    /// When false, input is ignored and the entity decelerates toward zero velocity.
    /// Velocity and acceleration are still applied by the physics system.
    /// </summary>
    public bool IsInputEnabled { get; set; } = true;

    /// <summary>
    /// Scales <see cref="TopDownValues.MaxSpeed"/> each frame.
    /// Use for slow/speed effects without modifying the values object.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Which discrete directions are available for <see cref="DirectionFacing"/>.</summary>
    public DirectionSnap DirectionSnap { get; set; } = DirectionSnap.EightWay;

    /// <summary>The direction the entity is currently facing. Updated each frame based on input or velocity.</summary>
    public TopDownDirection DirectionFacing { get; set; } = TopDownDirection.Right;

    /// <summary>
    /// True when the entity's velocity magnitude exceeds a small epsilon, recomputed each <see cref="Update"/> call.
    /// Prefer this over checking input for animation decisions: input is non-zero while the entity is held
    /// against a wall, but <see cref="IsMoving"/> correctly reports false because collision has zeroed velocity.
    /// Returns false before the first <see cref="Update"/> call.
    /// </summary>
    public bool IsMoving { get; private set; }

    private const float IsMovingEpsilonSquared = 0.25f; // 0.5 units/sec

    /// <summary>
    /// Applies top-down movement to <paramref name="entity"/> for the current frame.
    /// Call this from <c>CustomActivity</c> — after collision resolution, which resets
    /// <c>entity.LastReposition</c> and adjusts velocity.
    /// </summary>
    public void Update(Entity entity, FrameTime time)
    {
        if (MovementValues == null || time.DeltaSeconds == 0f) return;

        var currentVelocity = new Vector2(entity.VelocityX, entity.VelocityY);

        var desiredVelocity = Vector2.Zero;
        if (IsInputEnabled && MovementInput != null)
        {
            var input = new Vector2(MovementInput.X, MovementInput.Y);
            if (input.LengthSquared() > 1f)
                input = Vector2.Normalize(input);
            desiredVelocity = input * MovementValues.MaxSpeed * SpeedMultiplier;
        }

        var diff = desiredVelocity - currentVelocity;
        var diffLength = diff.Length();

        const float differenceEpsilon = 0.1f;

        if (diffLength > differenceEpsilon)
        {
            bool isMoving = currentVelocity.LengthSquared() > 0f;
            bool hasDesiredVelocity = desiredVelocity.LengthSquared() > 0f;

            // 0 = fully decelerating, 1 = fully accelerating. Blends between the two time values.
            float accelerationRatio;
            if (isMoving && !hasDesiredVelocity)
            {
                accelerationRatio = 0f;
            }
            else if (!isMoving || !hasDesiredVelocity)
            {
                accelerationRatio = 1f;
            }
            else
            {
                // Both moving and has a desired direction — blend by how aligned they are.
                var movementAngle = MathF.Atan2(currentVelocity.Y, currentVelocity.X);
                var desiredAngle = MathF.Atan2(diff.Y, diff.X);
                var angleDiff = AngleDifference(movementAngle, desiredAngle);
                accelerationRatio = 1f - MathF.Abs(angleDiff) / MathF.PI;
            }

            float secondsToTake = Lerp(
                (float)MovementValues.DecelerationTime.TotalSeconds,
                (float)MovementValues.AccelerationTime.TotalSeconds,
                accelerationRatio);

            if (secondsToTake == 0f)
            {
                entity.VelocityX = desiredVelocity.X;
                entity.VelocityY = desiredVelocity.Y;
                entity.AccelerationX = 0f;
                entity.AccelerationY = 0f;
            }
            else
            {
                float maxSpeed = MovementValues.MaxSpeed * SpeedMultiplier;
                float currentSpeed = currentVelocity.Length();
                float accelerationMagnitude;

                if (currentSpeed > maxSpeed && MovementValues.IsUsingCustomDeceleration)
                    accelerationMagnitude = MovementValues.CustomDecelerationValue;
                else if (currentSpeed > maxSpeed)
                    accelerationMagnitude = MovementValues.MaxSpeed / secondsToTake;
                else
                    accelerationMagnitude = maxSpeed / secondsToTake;

                var normalizedDiff = Vector2.Normalize(diff);
                var acceleration = normalizedDiff * accelerationMagnitude;

                // Prevent overshooting the desired velocity this frame.
                var expectedDeltaV = acceleration * time.DeltaSeconds;
                if (expectedDeltaV.Length() > diffLength)
                    acceleration *= diffLength / expectedDeltaV.Length();

                entity.AccelerationX = acceleration.X;
                entity.AccelerationY = acceleration.Y;
            }

            // Update facing direction.
            if (MovementValues.UpdateDirectionFromInput && hasDesiredVelocity)
                DirectionFacing = DirectionFromVector(desiredVelocity.X, desiredVelocity.Y, DirectionSnap);
            else if (MovementValues.UpdateDirectionFromVelocity)
            {
                if (!isMoving && hasDesiredVelocity)
                    DirectionFacing = DirectionFromVector(desiredVelocity.X, desiredVelocity.Y, DirectionSnap);
                else if (isMoving)
                    DirectionFacing = DirectionFromVector(currentVelocity.X, currentVelocity.Y, DirectionSnap);
            }
        }
        else
        {
            // Within epsilon of target — snap and clear acceleration.
            entity.VelocityX = desiredVelocity.X;
            entity.VelocityY = desiredVelocity.Y;
            entity.AccelerationX = 0f;
            entity.AccelerationY = 0f;
        }

        IsMoving = (entity.VelocityX * entity.VelocityX + entity.VelocityY * entity.VelocityY) > IsMovingEpsilonSquared;
    }

    private static float AngleDifference(float from, float to)
    {
        var diff = to - from;
        while (diff > MathF.PI) diff -= 2f * MathF.PI;
        while (diff < -MathF.PI) diff += 2f * MathF.PI;
        return diff;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    internal static TopDownDirection DirectionFromVector(float x, float y, DirectionSnap possibleDirections)
    {
        var angle = MathF.Atan2(y, x); // Y+ is up in world space

        if (possibleDirections == DirectionSnap.FourWay)
        {
            if (angle >= -MathF.PI / 4f && angle < MathF.PI / 4f) return TopDownDirection.Right;
            if (angle >= MathF.PI / 4f && angle < 3f * MathF.PI / 4f) return TopDownDirection.Up;
            if (angle < -3f * MathF.PI / 4f || angle >= 3f * MathF.PI / 4f) return TopDownDirection.Left;
            return TopDownDirection.Down;
        }

        // EightWay: 8 sectors of 45° each, centered on the cardinal/diagonal axes.
        var normalized = angle < 0f ? angle + 2f * MathF.PI : angle;
        int sector = (int)MathF.Round(normalized / (MathF.PI / 4f)) % 8;
        return sector switch
        {
            0 => TopDownDirection.Right,
            1 => TopDownDirection.UpRight,
            2 => TopDownDirection.Up,
            3 => TopDownDirection.UpLeft,
            4 => TopDownDirection.Left,
            5 => TopDownDirection.DownLeft,
            6 => TopDownDirection.Down,
            7 => TopDownDirection.DownRight,
            _ => TopDownDirection.Right,
        };
    }
}
