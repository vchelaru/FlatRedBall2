using System.Collections.Generic;
using FlatRedBall2.Glue;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Pure condition/priority/suffix logic for Glue-authored animation layers, matching FRB1's
// AnimationController semantics (evaluate bottom-most-first, first true condition wins). No Glue
// project loading involved — these run straight against PlatformerBehavior/TopDownBehavior.
public class GlueAnimationEvaluatorTests
{
    // ── Platformer ──────────────────────────────────────────────────────────

    [Fact]
    public void EvaluatePlatformer_BottomMostTrueLayerWins()
    {
        var entity = new Entity();
        var platformer = new PlatformerBehavior();
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Idle", HasLeftAndRight = false },
            new() { AnimationName = "Walk", HasLeftAndRight = false },
        };

        string? result = GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: null);

        // Both layers have no conditions (always true) — the later (higher priority) one wins.
        result.ShouldBe("Walk");
    }

    [Fact]
    public void EvaluatePlatformer_MinXVelocityAbsolute_GatesLayerOnSpeed()
    {
        var entity = new Entity { VelocityX = 50f };
        var platformer = new PlatformerBehavior();
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Idle", HasLeftAndRight = false },
            new() { AnimationName = "Run", HasLeftAndRight = false, MinXVelocityAbsolute = 140f },
        };

        string? result = GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: null);

        // 50 < 140 → Run's condition fails, falls through to Idle.
        result.ShouldBe("Idle");
    }

    [Fact]
    public void EvaluatePlatformer_HasLeftAndRight_AppendsFacingSuffix()
    {
        var entity = new Entity();
        var platformer = new PlatformerBehavior { MovementInput = new FixedInput(-1f, 0f) };
        platformer.Update(entity, new FrameTime(System.TimeSpan.FromSeconds(1f / 60f), System.TimeSpan.FromSeconds(1f / 60f), System.TimeSpan.Zero, System.TimeSpan.Zero));
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Walk", HasLeftAndRight = true },
        };

        string? result = GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: null);

        result.ShouldBe("WalkLeft");
    }

    [Fact]
    public void EvaluatePlatformer_OnGroundRequirementTrue_RequiresGround()
    {
        var entity = new Entity();
        var platformer = new PlatformerBehavior(); // IsOnGround defaults to false
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Fall", HasLeftAndRight = false, OnGroundRequirement = true },
        };

        string? result = GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: null);

        result.ShouldBeNull();
    }

    [Fact]
    public void EvaluatePlatformer_MovementName_MatchesAgainstCurrentMovementName()
    {
        var entity = new Entity();
        var platformer = new PlatformerBehavior();
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Run", HasLeftAndRight = false, MovementName = "Sprint" },
        };

        GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: "Walk").ShouldBeNull();

        GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: "Sprint").ShouldBe("Run");
    }

    [Fact]
    public void EvaluatePlatformer_NoLayerConditionMet_ReturnsNull()
    {
        var entity = new Entity { VelocityX = 0f };
        var platformer = new PlatformerBehavior();
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Run", HasLeftAndRight = false, MinXVelocityAbsolute = 200f },
        };

        GlueAnimationEvaluator.EvaluatePlatformer(
            entity, platformer, sprite: null, layers, currentMovementName: null).ShouldBeNull();
    }

    [Fact]
    public void EvaluatePlatformer_AnimationSpeedAssignmentForceTo1_SetsSpriteSpeedToOne()
    {
        var entity = new Entity();
        var platformer = new PlatformerBehavior();
        var sprite = new Sprite { AnimationSpeed = 2.5f };
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new() { AnimationName = "Idle", HasLeftAndRight = false, AnimationSpeedAssignment = GlueAnimationSpeedAssignment.ForceTo1 },
        };

        GlueAnimationEvaluator.EvaluatePlatformer(entity, platformer, sprite, layers, currentMovementName: null);

        sprite.AnimationSpeed.ShouldBe(1f);
    }

    [Fact]
    public void EvaluatePlatformer_AnimationSpeedAssignmentBasedOnVelocityMultiplier_ScalesByVelocity()
    {
        var entity = new Entity { VelocityX = 80f };
        var platformer = new PlatformerBehavior();
        var sprite = new Sprite();
        var layers = new List<GluePlatformerAnimationLayer>
        {
            new()
            {
                AnimationName = "Walk", HasLeftAndRight = false,
                AnimationSpeedAssignment = GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier,
                AbsoluteXVelocityAnimationSpeedMultiplier = 0.1f,
            },
        };

        GlueAnimationEvaluator.EvaluatePlatformer(entity, platformer, sprite, layers, currentMovementName: null);

        sprite.AnimationSpeed.ShouldBe(8f, tolerance: 0.001f);
    }

    // ── Top-down ────────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateTopDown_BottomMostTrueLayerWins()
    {
        var entity = new Entity();
        var topDown = new TopDownBehavior();
        var layers = new List<GlueTopDownAnimationLayer>
        {
            new() { AnimationName = "Idle", IsDirectionFacingAppended = false },
            new() { AnimationName = "Walk", IsDirectionFacingAppended = false },
        };

        string? result = GlueAnimationEvaluator.EvaluateTopDown(
            entity, topDown, sprite: null, layers, currentMovementName: null);

        result.ShouldBe("Walk");
    }

    [Fact]
    public void EvaluateTopDown_IsDirectionFacingAppended_AppendsDirectionEnumName()
    {
        var entity = new Entity();
        var topDown = new TopDownBehavior { DirectionFacing = TopDownDirection.UpLeft };
        var layers = new List<GlueTopDownAnimationLayer>
        {
            new() { AnimationName = "Walk", IsDirectionFacingAppended = true },
        };

        string? result = GlueAnimationEvaluator.EvaluateTopDown(
            entity, topDown, sprite: null, layers, currentMovementName: null);

        result.ShouldBe("WalkUpLeft");
    }

    [Fact]
    public void EvaluateTopDown_MinVelocityAbsolute_GatesLayerOnCombinedXYSpeed()
    {
        var entity = new Entity { VelocityX = 30f, VelocityY = 40f }; // magnitude 50
        var topDown = new TopDownBehavior();
        var layers = new List<GlueTopDownAnimationLayer>
        {
            new() { AnimationName = "Idle", IsDirectionFacingAppended = false },
            new() { AnimationName = "Run", IsDirectionFacingAppended = false, MinVelocityAbsolute = 60f },
        };

        string? result = GlueAnimationEvaluator.EvaluateTopDown(
            entity, topDown, sprite: null, layers, currentMovementName: null);

        result.ShouldBe("Idle");
    }

    private sealed class FixedInput : I2DInput
    {
        public FixedInput(float x, float y) { X = x; Y = y; }
        public float X { get; }
        public float Y { get; }
    }
}
