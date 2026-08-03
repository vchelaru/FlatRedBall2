using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace RiftboundSample.Entities;

public class OverworldEnemyEntity : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    /// <summary>Determines which enemy group is fought when the player touches this entity.</summary>
    public string EnemyGroupId { get; set; } = "default";

    /// <summary>The area level for this enemy, used for flee behavior when outleveled.</summary>
    public int AreaLevel { get; set; } = 1;

    /// <summary>When true, the enemy is fleeing from the player instead of patrolling.</summary>
    public bool IsFleeing { get; private set; }

    // Patrol state
    private float _patrolOriginX;
    private float _patrolDistance = 32f;
    private float _patrolSpeed = 40f;
    private int _patrolDirection = 1;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 16,
            Height = 16,
            Color = new Color(200, 50, 50),
            IsFilled = true,
            OutlineThickness = 0f,
            IsVisible = true,
        };
        Add(Rectangle);
    }

    /// <summary>
    /// Call after setting position to lock in the patrol center point.
    /// </summary>
    public void InitializePatrol(float distance = 32f, float speed = 40f)
    {
        _patrolOriginX = X;
        _patrolDistance = distance;
        _patrolSpeed = speed;
    }

    /// <summary>
    /// Sets the enemy to flee directly away from the given position at 1.5x patrol speed.
    /// </summary>
    public void StartFleeing(float playerX, float playerY)
    {
        IsFleeing = true;
        UpdateFleeDirection(playerX, playerY);
    }

    /// <summary>
    /// Returns the enemy to normal patrol behavior.
    /// </summary>
    public void StopFleeing()
    {
        IsFleeing = false;
        VelocityY = 0;
    }

    /// <summary>
    /// Updates the flee direction to move away from the player's current position.
    /// </summary>
    public void UpdateFleeDirection(float playerX, float playerY)
    {
        float dx = X - playerX;
        float dy = Y - playerY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 0.001f)
        {
            // Directly on top of player, flee in patrol direction
            VelocityX = _patrolSpeed * 1.5f * _patrolDirection;
            VelocityY = 0;
            return;
        }

        float fleeSpeed = _patrolSpeed * 1.5f;
        VelocityX = dx / dist * fleeSpeed;
        VelocityY = dy / dist * fleeSpeed;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (IsFleeing)
            return; // Flee velocity is set externally via UpdateFleeDirection

        // Simple horizontal patrol
        VelocityX = _patrolSpeed * _patrolDirection;
        VelocityY = 0;

        if (X > _patrolOriginX + _patrolDistance)
            _patrolDirection = -1;
        else if (X < _patrolOriginX - _patrolDistance)
            _patrolDirection = 1;
    }
}
