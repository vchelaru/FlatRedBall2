using System.Collections.Generic;

namespace FlatRedBall2.Collision;

/// <summary>
/// Owns all collision relationships for a Screen and runs them each frame.
/// </summary>
internal sealed class CollisionSystem
{
    private readonly List<ICollisionRelationship> _relationships = new();

    /// <summary>Every registered relationship, for diagnostics/reporting. Read-only view.</summary>
    internal IReadOnlyList<ICollisionRelationship> Relationships => _relationships;

    /// <summary>Adds a collision relationship to be run each frame.</summary>
    internal void Add(ICollisionRelationship relationship) => _relationships.Add(relationship);

    /// <summary>Removes a collision relationship.</summary>
    internal void Remove(ICollisionRelationship relationship) => _relationships.Remove(relationship);

    /// <summary>Clears all collision relationships.</summary>
    internal void Clear() => _relationships.Clear();

    /// <summary>Runs all registered collision relationships. Called once per frame after physics.</summary>
    internal void RunAllCollisions()
    {
        for (int i = 0; i < _relationships.Count; i++)
            _relationships[i].RunCollisions();
    }
}
