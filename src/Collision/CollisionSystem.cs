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

    // Removals requested while RunAllCollisions is iterating (a CollisionOccurred handler removing a
    // relationship or a map). Applied when the pass ends so the index loop never skips a relationship
    // and a relationship mid-run finishes its pairs before its contacts are ended.
    private List<ICollisionRelationship>? _pendingRemoval;
    private bool _isRunning;

    /// <summary>
    /// Removes a collision relationship. Idempotent. Called during <see cref="RunAllCollisions"/>,
    /// the removal takes effect at the end of the pass and the relationship is skipped if it has not run yet.
    /// </summary>
    internal void Remove(ICollisionRelationship relationship)
    {
        if (!_relationships.Contains(relationship)) return;
        if (_isRunning)
        {
            _pendingRemoval ??= new List<ICollisionRelationship>();
            if (!_pendingRemoval.Contains(relationship))
                _pendingRemoval.Add(relationship);
            return;
        }
        _relationships.Remove(relationship);
        relationship.OnRemoved();
    }

    /// <summary>Clears all collision relationships.</summary>
    internal void Clear() => _relationships.Clear();

    /// <summary>
    /// Runs every enabled collision relationship. Called once per frame after physics; relationships
    /// with <see cref="ICollisionRelationship.IsEnabled"/> set to <c>false</c> are skipped.
    /// </summary>
    internal void RunAllCollisions()
    {
        _isRunning = true;
        try
        {
            for (int i = 0; i < _relationships.Count; i++)
            {
                var rel = _relationships[i];
                if (rel.IsEnabled && (_pendingRemoval == null || !_pendingRemoval.Contains(rel)))
                    rel.RunCollisions();
            }
        }
        finally
        {
            _isRunning = false;
            if (_pendingRemoval != null && _pendingRemoval.Count > 0)
            {
                var pending = _pendingRemoval;
                _pendingRemoval = null;
                for (int i = 0; i < pending.Count; i++)
                    Remove(pending[i]);
            }
        }
    }
}
