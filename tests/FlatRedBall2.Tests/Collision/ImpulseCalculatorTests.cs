using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class ImpulseCalculatorTests
{
    [Fact]
    public void ComputeStaticImpulseDelta_EntityMovingIntoWall_StopsAndReverses()
    {
        // Entity moving left (-X) into a wall with a left-facing normal (+X).
        var velocity = new Vector2(-10f, 0f);
        var normal = new Vector2(1f, 0f);  // wall normal points right
        float thisMass = 1f;
        float otherMass = 0f;              // static wall — absorbs impulse fully
        float elasticity = 0f;            // inelastic

        var delta = ImpulseCalculator.ComputeStaticImpulseDelta(velocity, normal, thisMass, otherMass, elasticity);

        // The delta should exactly cancel the leftward component
        (velocity + delta).X.ShouldBe(0f, tolerance: 0.0001f);
    }

    [Fact]
    public void ComputeDynamicImpulseDeltas_EqualMassElasticCollision_SwapsVelocities()
    {
        // Two equal-mass entities moving toward each other on X axis.
        // This entity is at -1 (moving right), other is at +1 (moving left).
        // Normal from other toward this points left: (-1, 0).
        var thisVel = new Vector2(5f, 0f);
        var otherVel = new Vector2(-5f, 0f);
        var normal = new Vector2(-1f, 0f);  // normal from other (right) toward this (left)
        float mass = 1f;
        float elasticity = 1f;            // fully elastic

        ImpulseCalculator.ComputeDynamicImpulseDeltas(thisVel, otherVel, normal, mass, mass, elasticity,
            out var thisDelta, out var otherDelta);

        (thisVel + thisDelta).X.ShouldBe(-5f, tolerance: 0.0001f);
        (otherVel + otherDelta).X.ShouldBe(5f, tolerance: 0.0001f);
    }

    [Fact]
    public void ComputeStaticImpulseDelta_AlreadySeparating_ReturnsZero()
    {
        // Entity moving away from the wall — no correction needed.
        var velocity = new Vector2(10f, 0f);
        var normal = new Vector2(1f, 0f);

        var delta = ImpulseCalculator.ComputeStaticImpulseDelta(velocity, normal,
            thisMass: 1f, otherMass: 0f, elasticity: 0f);

        delta.ShouldBe(Vector2.Zero);
    }
}
