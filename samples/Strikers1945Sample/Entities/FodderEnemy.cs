using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace Strikers1945Sample.Entities;

public class FodderEnemy : Entity
{
    private Sprite _sprite = null!;
    public Circle CollisionCircle { get; private set; } = null!;

    private Vector2[] _waypoints = Array.Empty<Vector2>();
    private int _waypointIndex;
    private float _speed;

    /// <summary>When true, the enemy maintains its X offset from the first waypoint throughout the path.</summary>
    public bool IsFormation { get; set; }

    /// <summary>X offset applied to all waypoints when <see cref="IsFormation"/> is true.</summary>
    public float FormationOffsetX { get; set; }

    public event Action? Escaped;

    private static readonly string[] SpriteNames = { "ship_0008", "ship_0009", "ship_0010", "ship_0011" };

    public override void CustomInitialize()
    {
        // Pick a random fodder sprite for variety
        var name = SpriteNames[Engine.Random.Next(SpriteNames.Length)];
        var texture = Engine.ContentManager.Load<Texture2D>(name);
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2f,
            FlipVertical = true, // face downward (enemies fly toward player)
        };
        Add(_sprite);

        CollisionCircle = new Circle
        {
            Radius = 12,
            Visible = false,
        };
        Add(CollisionCircle);
    }

    public void Launch(Vector2[] waypoints, float speed)
    {
        _waypoints = waypoints;
        _speed = speed;
        _waypointIndex = 1;
        if (waypoints.Length > 0)
        {
            X = waypoints[0].X;
            Y = waypoints[0].Y;
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_waypointIndex >= _waypoints.Length)
        {
            Escaped?.Invoke();
            Destroy();
            return;
        }

        var target = _waypoints[_waypointIndex];
        float targetX = IsFormation ? target.X + FormationOffsetX : target.X;
        float dx = targetX - X;
        float dy = target.Y - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float step = _speed * time.DeltaSeconds;

        if (dist <= step)
        {
            X = targetX;
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

    public void Flash()
    {
        _sprite.Color = new Color(255, 255, 255, 255);
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionCircle.Destroy();
    }
}
