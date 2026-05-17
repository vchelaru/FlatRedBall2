using System.Numerics;

namespace FlatRedBall2.Collision;

/// <summary>
/// Stateless utility for computing velocity responses after collision separation.
/// Used by Entity collision response; available for custom types that need impulse math.
/// </summary>
public static class ImpulseCalculator
{
    /// <summary>
    /// Computes the velocity delta to apply to an entity after contact with static (immovable) geometry.
    /// Returns <see cref="Vector2.Zero"/> when already separating along the normal (no correction needed).
    /// </summary>
    /// <param name="velocity">The moving entity's current velocity.</param>
    /// <param name="normal">Unit collision normal pointing away from the static surface.</param>
    /// <param name="thisMass">Mass of the moving entity.</param>
    /// <param name="otherMass">Mass of the static obstacle (use 0 to make this entity fully absorb the impulse).</param>
    /// <param name="elasticity">Restitution coefficient: 0 = inelastic, 1 = elastic.</param>
    /// <returns>Velocity delta to add to the moving entity.</returns>
    public static Vector2 ComputeStaticImpulseDelta(
        Vector2 velocity, Vector2 normal, float thisMass, float otherMass, float elasticity)
    {
        float totalMass = thisMass + otherMass;
        if (totalMass == 0f) return Vector2.Zero;

        float thisRatio = otherMass == 0f ? 1f : otherMass / totalMass;
        float relVelAlongNormal = Vector2.Dot(velocity, normal);
        if (relVelAlongNormal >= 0f) return Vector2.Zero; // already separating

        float impulse = -(1f + elasticity) * relVelAlongNormal;
        Vector2 impulseVector = impulse * normal;
        return thisRatio * impulseVector;
    }

    /// <summary>
    /// Computes velocity deltas for both entities in a dynamic (entity vs. entity) collision.
    /// Outputs zero deltas when already separating along the normal.
    /// </summary>
    /// <param name="thisVelocity">Velocity of this entity.</param>
    /// <param name="otherVelocity">Velocity of the other entity.</param>
    /// <param name="normal">Unit collision normal pointing from other toward this entity.</param>
    /// <param name="thisMass">Mass of this entity.</param>
    /// <param name="otherMass">Mass of the other entity.</param>
    /// <param name="elasticity">Restitution coefficient: 0 = inelastic, 1 = elastic.</param>
    /// <param name="thisDelta">Velocity delta to add to this entity.</param>
    /// <param name="otherDelta">Velocity delta to add to the other entity.</param>
    public static void ComputeDynamicImpulseDeltas(
        Vector2 thisVelocity, Vector2 otherVelocity, Vector2 normal,
        float thisMass, float otherMass, float elasticity,
        out Vector2 thisDelta, out Vector2 otherDelta)
    {
        thisDelta = Vector2.Zero;
        otherDelta = Vector2.Zero;

        float totalMass = thisMass + otherMass;
        if (totalMass == 0f) return;

        float thisRatio = otherMass == 0f ? 1f : otherMass / totalMass;
        float otherRatio = thisMass == 0f ? 0f : thisMass / totalMass;

        float relVelAlongNormal = Vector2.Dot(thisVelocity - otherVelocity, normal);
        if (relVelAlongNormal >= 0f) return; // already separating

        float impulse = -(1f + elasticity) * relVelAlongNormal;
        Vector2 impulseVector = impulse * normal;
        thisDelta = thisRatio * impulseVector;
        otherDelta = -otherRatio * impulseVector;
    }
}
