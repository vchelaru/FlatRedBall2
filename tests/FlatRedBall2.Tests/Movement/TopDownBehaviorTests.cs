using System;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class TopDownBehaviorTests
{
    private static FrameTime MakeFrame(float deltaSeconds)
        => new FrameTime(TimeSpan.FromSeconds(deltaSeconds), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void DirectionFacing_FourWay_DownInput_ReturnsDown()
    {
        var direction = TopDownBehavior.DirectionFromVector(0f, -1f, PossibleDirections.FourWay);
        direction.ShouldBe(TopDownDirection.Down);
    }

    [Fact]
    public void DirectionFacing_FourWay_RightInput_ReturnsRight()
    {
        var direction = TopDownBehavior.DirectionFromVector(1f, 0f, PossibleDirections.FourWay);
        direction.ShouldBe(TopDownDirection.Right);
    }

    [Fact]
    public void DirectionFacing_FourWay_UpInput_ReturnsUp()
    {
        var direction = TopDownBehavior.DirectionFromVector(0f, 1f, PossibleDirections.FourWay);
        direction.ShouldBe(TopDownDirection.Up);
    }

    [Fact]
    public void DirectionFacing_EightWay_DiagonalInput_ReturnsUpRight()
    {
        var direction = TopDownBehavior.DirectionFromVector(1f, 1f, PossibleDirections.EightWay);
        direction.ShouldBe(TopDownDirection.UpRight);
    }

    [Fact]
    public void Update_NoAcceleration_FullInput_SetsVelocityToMaxSpeed()
    {
        float maxSpeed = 200f;
        var values = new TopDownValues { MaxSpeed = maxSpeed };
        var behavior = new TopDownBehavior { MovementValues = values, MovementInput = new MockAxisInput(x: 1f, y: 0f) };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(maxSpeed, tolerance: 0.01f);
        entity.VelocityY.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void Update_NoAcceleration_NormalizedDiagonalInput_SetsVelocityToMaxSpeed()
    {
        float maxSpeed = 200f;
        var values = new TopDownValues { MaxSpeed = maxSpeed };
        // (1,1) will be normalized to ~(0.707, 0.707) — total magnitude should be maxSpeed
        var behavior = new TopDownBehavior { MovementValues = values, MovementInput = new MockAxisInput(x: 1f, y: 1f) };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        var speed = MathF.Sqrt(entity.VelocityX * entity.VelocityX + entity.VelocityY * entity.VelocityY);
        speed.ShouldBe(maxSpeed, tolerance: 0.01f);
    }

    [Fact]
    public void Update_NoInput_ZeroesVelocity()
    {
        var values = new TopDownValues { MaxSpeed = 200f };
        var behavior = new TopDownBehavior { MovementValues = values, MovementInput = new MockAxisInput(x: 0f, y: 0f) };
        var entity = new Entity { VelocityX = 100f, VelocityY = 50f };

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(0f, tolerance: 0.01f);
        entity.VelocityY.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void Update_WithAcceleration_DoesNotInstantlyReachMaxSpeed()
    {
        float maxSpeed = 200f;
        // 1-second ramp time — in one frame at 60fps, should not yet be at maxSpeed
        var values = new TopDownValues
        {
            MaxSpeed = maxSpeed,
            AccelerationTime = 1f,
            DecelerationTime = 1f,
        };
        var behavior = new TopDownBehavior { MovementValues = values, MovementInput = new MockAxisInput(x: 1f, y: 0f) };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBeLessThan(maxSpeed);
    }

    [Fact]
    public void Update_InputDisabled_DoesNotApplyInput()
    {
        float maxSpeed = 200f;
        var values = new TopDownValues { MaxSpeed = maxSpeed };
        var behavior = new TopDownBehavior
        {
            MovementValues = values,
            MovementInput = new MockAxisInput(x: 1f, y: 0f),
            InputEnabled = false,
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void Update_SpeedMultiplier_ScalesMaxSpeed()
    {
        float maxSpeed = 200f;
        float multiplier = 0.5f;
        var values = new TopDownValues { MaxSpeed = maxSpeed };
        var behavior = new TopDownBehavior
        {
            MovementValues = values,
            MovementInput = new MockAxisInput(x: 1f, y: 0f),
            SpeedMultiplier = multiplier,
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(maxSpeed * multiplier, tolerance: 0.01f);
    }

    private sealed class MockAxisInput : I2DInput
    {
        public float X { get; }
        public float Y { get; }

        public MockAxisInput(float x = 0f, float y = 0f)
        {
            X = x;
            Y = y;
        }
    }
}
