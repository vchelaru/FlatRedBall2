using System;
using System.Collections.Generic;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Glue;

/// <summary>
/// Evaluates Glue-authored platformer/top-down animation layers against live
/// <see cref="PlatformerBehavior"/>/<see cref="TopDownBehavior"/> state, matching FRB1's
/// <c>AnimationController</c> priority rule: layers are checked from the <b>bottom of the authored
/// list up</b> (last entry has the highest priority), and the first one whose condition is true wins.
/// </summary>
/// <remarks>
/// Sits on top of existing primitives rather than porting FRB1's <c>AnimationController</c> object
/// model — see <c>design/platformer-config-design.md</c>. A winning layer's
/// <see cref="GluePlatformerAnimationLayer.AnimationSpeedAssignment"/> /
/// <see cref="GlueTopDownAnimationLayer.AnimationSpeedAssignment"/> is applied to <c>sprite</c> as a
/// side effect before the resolved chain name is returned, matching FRB1. The caller is responsible
/// for calling <see cref="Sprite.PlayAnimation(string)"/> with the result — this method never touches
/// <c>CurrentChainName</c> itself, so a caller with no matching layer (a null result) leaves the
/// currently-playing animation untouched.
/// </remarks>
public static class GlueAnimationEvaluator
{
    /// <summary>
    /// Resolves the platformer animation chain name to play this frame, or null if no layer's
    /// condition is true. <paramref name="sprite"/> may be null to skip animation-speed assignment
    /// (useful for condition-only testing).
    /// </summary>
    public static string? EvaluatePlatformer(
        Entity entity,
        PlatformerBehavior platformer,
        Sprite? sprite,
        IReadOnlyList<GluePlatformerAnimationLayer> layers,
        string? currentMovementName)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (!PlatformerConditionMet(entity, platformer, layer, currentMovementName))
                continue;

            if (sprite is not null)
                ApplyPlatformerAnimationSpeed(entity, platformer, layer, sprite);

            string name = layer.AnimationName ?? string.Empty;
            if (layer.HasLeftAndRight)
                name += platformer.DirectionFacing == HorizontalDirection.Left ? "Left" : "Right";
            return name;
        }

        return null;
    }

    /// <summary>
    /// Resolves the top-down animation chain name to play this frame, or null if no layer's
    /// condition is true. <paramref name="sprite"/> may be null to skip animation-speed assignment.
    /// </summary>
    public static string? EvaluateTopDown(
        Entity entity,
        TopDownBehavior topDown,
        Sprite? sprite,
        IReadOnlyList<GlueTopDownAnimationLayer> layers,
        string? currentMovementName)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (!TopDownConditionMet(entity, topDown, layer, currentMovementName))
                continue;

            if (sprite is not null)
                ApplyTopDownAnimationSpeed(entity, topDown, layer, sprite);

            string name = layer.AnimationName ?? string.Empty;
            if (layer.IsDirectionFacingAppended)
                name += topDown.DirectionFacing.ToString();
            return name;
        }

        return null;
    }

    private static bool PlatformerConditionMet(
        Entity entity, PlatformerBehavior platformer, GluePlatformerAnimationLayer layer, string? currentMovementName)
    {
        if (!string.IsNullOrEmpty(layer.MovementName) && layer.MovementName != currentMovementName)
            return false;

        float absX = MathF.Abs(entity.VelocityX);
        float absInput = MathF.Abs(platformer.MovementInput?.X ?? 0f);
        float y = entity.VelocityY;

        if (layer.MinXVelocityAbsolute is float minX && absX < minX) return false;
        if (layer.MaxXVelocityAbsolute is float maxX && absX > maxX) return false;
        if (layer.MinYVelocity is float minY && y < minY) return false;
        if (layer.MaxYVelocity is float maxY && y > maxY) return false;
        if (layer.MinHorizontalInputAbsolute is float minIn && absInput < minIn) return false;
        if (layer.MaxHorizontalInputAbsolute is float maxIn && absInput > maxIn) return false;
        if (layer.OnGroundRequirement is bool onGround && platformer.IsOnGround != onGround) return false;

        return true;
    }

    private static bool TopDownConditionMet(
        Entity entity, TopDownBehavior topDown, GlueTopDownAnimationLayer layer, string? currentMovementName)
    {
        if (!string.IsNullOrEmpty(layer.MovementName) && layer.MovementName != currentMovementName)
            return false;

        float absVelocity = Magnitude(entity.VelocityX, entity.VelocityY);
        float absInput = topDown.MovementInput is { } input ? Magnitude(input.X, input.Y) : 0f;

        if (layer.MinVelocityAbsolute is float minV && absVelocity < minV) return false;
        if (layer.MaxVelocityAbsolute is float maxV && absVelocity > maxV) return false;
        if (layer.MinMovementInputAbsolute is float minIn && absInput < minIn) return false;
        if (layer.MaxMovementInputAbsolute is float maxIn && absInput > maxIn) return false;

        return true;
    }

    private static void ApplyPlatformerAnimationSpeed(
        Entity entity, PlatformerBehavior platformer, GluePlatformerAnimationLayer layer, Sprite sprite)
    {
        switch (layer.AnimationSpeedAssignment)
        {
            case GlueAnimationSpeedAssignment.ForceTo1:
                sprite.AnimationSpeed = 1f;
                break;

            case GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier:
                if (layer.AbsoluteXVelocityAnimationSpeedMultiplier is float xMultiplier)
                    sprite.AnimationSpeed = xMultiplier * MathF.Abs(entity.VelocityX);
                else if (layer.AbsoluteYVelocityAnimationSpeedMultiplier is float yMultiplier)
                    sprite.AnimationSpeed = yMultiplier * MathF.Abs(entity.VelocityY);
                break;

            case GlueAnimationSpeedAssignment.BasedOnMaxSpeedRatioMultiplier:
                var active = ActivePlatformerValues(platformer);
                if (layer.MaxSpeedXRatioMultiplier is float xRatio)
                    sprite.AnimationSpeed = active.MaxSpeedX == 0f
                        ? 1f : xRatio * MathF.Abs(entity.VelocityX) / active.MaxSpeedX;
                else if (layer.MaxSpeedYRatioMultiplier is float yRatio)
                    sprite.AnimationSpeed = active.MaxFallSpeed == 0f
                        ? 1f : yRatio * MathF.Abs(entity.VelocityY) / active.MaxFallSpeed;
                break;

            // NoAssignment: leave the sprite's current AnimationSpeed untouched.
            // BasedOnHorizontalInputMultiplier: parses but is not evaluated — see GlueAnimationLayerLoader.
        }
    }

    private static void ApplyTopDownAnimationSpeed(
        Entity entity, TopDownBehavior topDown, GlueTopDownAnimationLayer layer, Sprite sprite)
    {
        switch (layer.AnimationSpeedAssignment)
        {
            case GlueAnimationSpeedAssignment.ForceTo1:
                sprite.AnimationSpeed = 1f;
                break;

            case GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier:
                if (layer.AbsoluteVelocityAnimationSpeedMultiplier is float multiplier)
                    sprite.AnimationSpeed = multiplier * Magnitude(entity.VelocityX, entity.VelocityY);
                break;

            case GlueAnimationSpeedAssignment.BasedOnMaxSpeedRatioMultiplier:
                float maxSpeed = topDown.MovementValues?.MaxSpeed ?? 0f;
                if (layer.MaxSpeedRatioMultiplier is float ratio)
                    sprite.AnimationSpeed = maxSpeed == 0f
                        ? 1f : ratio * Magnitude(entity.VelocityX, entity.VelocityY) / maxSpeed;
                break;

            // NoAssignment / BasedOnInputMultiplier: see the platformer switch above.
        }
    }

    /// <summary>Mirrors <see cref="PlatformerBehavior.Update"/>'s private slot-selection logic using
    /// only the behavior's public state, so the evaluator never needs a new public "current values"
    /// surface on the behavior itself.</summary>
    private static PlatformerValues ActivePlatformerValues(PlatformerBehavior platformer) =>
        platformer.IsClimbing ? (platformer.ClimbingMovement ?? platformer.AirMovement)
        : platformer.IsOnGround ? (platformer.GroundMovement ?? platformer.AirMovement)
        : platformer.IsUsingAfterDoubleJumpSlot ? (platformer.AfterDoubleJump ?? platformer.AirMovement)
        : platformer.AirMovement;

    private static float Magnitude(float x, float y) => MathF.Sqrt(x * x + y * y);
}
