using System;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class PhysicsTests
{
    private static FrameTime MakeFrame(float deltaSeconds)
        => new FrameTime(TimeSpan.FromSeconds(deltaSeconds), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void ChildEntity_DoesNotRunPhysicsIndependently()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);
        child.VelocityX = 50f;

        parent.PhysicsUpdate(MakeFrame(1f / 60f));

        child.X.ShouldBe(0f);
    }

    [Fact]
    public void ConstantAcceleration_UsesSecondOrderKinematics()
    {
        float accelerationX = 100f;
        float dt = 1f / 60f;
        float expectedX = accelerationX * (dt * dt / 2f);
        float expectedVx = accelerationX * dt;

        var entity = new Entity();
        entity.AccelerationX = accelerationX;

        entity.PhysicsUpdate(MakeFrame(dt));

        entity.X.ShouldBe(expectedX, tolerance: 0.0001f);
        entity.VelocityX.ShouldBe(expectedVx, tolerance: 0.0001f);
    }

    [Fact]
    public void ConstantVelocity_MovesExpectedDistance()
    {
        float velocityX = 100f;
        float deltaSeconds = 1f / 60f;
        float expectedX = velocityX * deltaSeconds * 3f;

        var entity = new Entity();
        entity.VelocityX = velocityX;

        var frame = MakeFrame(deltaSeconds);
        entity.PhysicsUpdate(frame);
        entity.PhysicsUpdate(frame);
        entity.PhysicsUpdate(frame);

        entity.X.ShouldBe(expectedX, tolerance: 0.0001f);
    }

    [Fact]
    public void RotationVelocity_ChildDoesNotRotateIndependently()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);
        child.RotationVelocity = Angle.FromDegrees(90f);

        parent.PhysicsUpdate(MakeFrame(1f));

        child.Rotation.Degrees.ShouldBe(0f);
    }

    [Fact]
    public void RotationVelocity_RotatesEntityEachFrame()
    {
        float degreesPerSecond = 90f;
        float dt = 1f / 60f;
        float expectedDegrees = degreesPerSecond * dt;

        var entity = new Entity();
        entity.RotationVelocity = Angle.FromDegrees(degreesPerSecond);

        entity.PhysicsUpdate(MakeFrame(dt));

        entity.Rotation.Degrees.ShouldBe(expectedDegrees, tolerance: 0.001f);
    }

    [Fact]
    public void Drag_ReducesVelocityEachFrame()
    {
        float initialVelocity = 100f;
        float drag = 1f;
        float dt = 1f / 60f;
        float expectedVx = initialVelocity - initialVelocity * drag * dt;

        var entity = new Entity();
        entity.VelocityX = initialVelocity;
        entity.Drag = drag;

        entity.PhysicsUpdate(MakeFrame(dt));

        entity.VelocityX.ShouldBe(expectedVx, tolerance: 0.00001f);
    }
}
