using System.Numerics;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class KinematicIntegratorTests
{
    [Fact]
    public void Integrate_ConstantVelocity_PositionChangesLinearly()
    {
        var pos = Vector2.Zero;
        var vel = new Vector2(10f, 0f);
        float dt = 0.5f;
        float expectedX = 10f * dt; // pure velocity, no acceleration

        KinematicIntegrator.Integrate(ref pos, ref vel, Vector2.Zero, drag: 0f, dt);

        pos.X.ShouldBe(expectedX, tolerance: 0.0001f);
        pos.Y.ShouldBe(0f, tolerance: 0.0001f);
    }

    [Fact]
    public void Integrate_Drag_ReducesVelocityTowardZero()
    {
        var pos = Vector2.Zero;
        var vel = new Vector2(100f, 0f);
        float drag = 1f;
        float dt = 0.1f;
        float expectedVx = 100f - 100f * drag * dt; // vel -= vel * drag * dt

        KinematicIntegrator.Integrate(ref pos, ref vel, Vector2.Zero, drag, dt);

        vel.X.ShouldBe(expectedVx, tolerance: 0.0001f);
    }

    [Fact]
    public void Integrate_ZeroVelocityAndAcceleration_NoMovement()
    {
        var pos = new Vector2(5f, 7f);
        var vel = Vector2.Zero;

        KinematicIntegrator.Integrate(ref pos, ref vel, Vector2.Zero, drag: 0f, dt: 1f);

        pos.X.ShouldBe(5f, tolerance: 0.0001f);
        pos.Y.ShouldBe(7f, tolerance: 0.0001f);
    }

    [Fact]
    public void Integrate_ConstantAcceleration_AddsHalfDtSquaredToPosition()
    {
        var pos = Vector2.Zero;
        var vel = Vector2.Zero;
        var acc = new Vector2(10f, 0f);
        float dt = 1f;
        // second-order: pos += vel*dt + acc*0.5*dt^2 = 0 + 10*0.5 = 5
        float expectedX = 0.5f * 10f * dt * dt;

        KinematicIntegrator.Integrate(ref pos, ref vel, acc, drag: 0f, dt);

        pos.X.ShouldBe(expectedX, tolerance: 0.0001f);
    }

    [Fact]
    public void IntegrateRotation_ConstantRotationVelocity_RotationChangesLinearly()
    {
        float rotation = 0f;
        float rotVel = 2f;
        float dt = 0.5f;
        float expectedRotation = rotVel * dt; // no acceleration

        KinematicIntegrator.IntegrateRotation(ref rotation, ref rotVel, rotationAcceleration: 0f, dt);

        rotation.ShouldBe(expectedRotation, tolerance: 0.0001f);
    }
}
