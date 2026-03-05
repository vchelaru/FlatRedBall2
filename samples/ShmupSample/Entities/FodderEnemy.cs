using FlatRedBall2;
using Vector2 = System.Numerics.Vector2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

/// <summary>
/// Small fodder enemy. Follows a predefined waypoint path one node at a time.
/// One hit to kill. Fires the Escaped event if it completes its path without being destroyed.
/// </summary>
public class FodderEnemy : Entity
{
    public Circle CollisionCircle { get; private set; } = null!;

    private Vector2[] _waypoints = Array.Empty<Vector2>();
    private int _waypointIndex;
    private float _speed;

    // Flash on hit
    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.1f;
    private Color _normalColor;

    /// <summary>Fired when this enemy completes its path without being destroyed.</summary>
    public event Action? Escaped;

    public override void CustomInitialize()
    {
        _normalColor = new Color(120, 220, 120, 220);

        CollisionCircle = new Circle
        {
            Radius = 10,
            Color = _normalColor,
            IsFilled = true,
            Visible = true,
        };
        Add(CollisionCircle);
    }

    /// <summary>
    /// Sets the path this enemy will follow. The enemy is placed at the first waypoint immediately.
    /// Call right after Create().
    /// </summary>
    public void Launch(Vector2[] waypoints, float speed)
    {
        _waypoints = waypoints;
        _speed = speed;
        _waypointIndex = 1; // already at waypoints[0]; aim for [1] next
        if (waypoints.Length > 0)
        {
            X = waypoints[0].X;
            Y = waypoints[0].Y;
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        MoveAlongPath(time);
        UpdateHitFlash(time);
    }

    private void MoveAlongPath(FrameTime time)
    {
        if (_waypointIndex >= _waypoints.Length)
        {
            Escaped?.Invoke();
            Destroy();
            return;
        }

        var target = _waypoints[_waypointIndex];
        float dx = target.X - X;
        float dy = target.Y - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float step = _speed * time.DeltaSeconds;

        if (dist <= step)
        {
            // Snap to waypoint and advance
            X = target.X;
            Y = target.Y;
            VelocityX = 0f;
            VelocityY = 0f;
            _waypointIndex++;
        }
        else
        {
            VelocityX = dx / dist * _speed;
            VelocityY = dy / dist * _speed;
        }
    }

    private void UpdateHitFlash(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            CollisionCircle.Color = new Color(255, 255, 255, 255);
        }
        else
        {
            CollisionCircle.Color = _normalColor;
        }
    }

    public override void CustomDestroy()
    {
        CollisionCircle.Destroy();
    }
}
