using System.Collections.Generic;
using FlatRedBall2;
using FlatRedBall2.AI;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZombieSample.Entities;

public class Zombie : Entity
{
    private const float Speed              = 80f;
    private const float PathRefreshSeconds = 1f;
    private const float WaypointRadius     = 10f;   // distance to consider a waypoint reached

    private static readonly Color ZombieColor = new(80, 180, 60, 230);

    private Circle           _circle      = null!;
    private Player?          _target;
    private TileNodeNetwork? _nodeNetwork;

    private readonly List<Vector2> _path = new();
    private int   _waypointIndex;
    private float _pathRefreshTimer;   // starts at 0 so path is computed on first frame

    public void SetTarget(Player player)               => _target      = player;
    public void SetNodeNetwork(TileNodeNetwork network) => _nodeNetwork = network;

    public override void CustomInitialize()
    {
        _circle = new Circle { Radius = 18f, IsVisible = true, Color = ZombieColor };
        Add(_circle);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_target == null || _target.IsDead)
        {
            VelocityX = 0f;
            VelocityY = 0f;
            return;
        }

        _pathRefreshTimer -= time.DeltaSeconds;
        if (_pathRefreshTimer <= 0f)
        {
            RefreshPath();
            _pathRefreshTimer = PathRefreshSeconds;
        }

        FollowPath();
    }

    private void RefreshPath()
    {
        if (_nodeNetwork == null || _target == null) return;

        var startNode = _nodeNetwork.GetClosestNode(X, Y);
        var endNode   = _nodeNetwork.GetClosestNode(_target.X, _target.Y);

        if (startNode != null && endNode != null)
        {
            _nodeNetwork.GetPath(startNode, endNode, _path);
            _waypointIndex = 0;
        }
        else
        {
            _path.Clear();
        }
    }

    private void FollowPath()
    {
        // Advance past any waypoints we've already reached.
        while (_waypointIndex < _path.Count)
        {
            float dx = _path[_waypointIndex].X - X;
            float dy = _path[_waypointIndex].Y - Y;
            if (dx * dx + dy * dy <= WaypointRadius * WaypointRadius)
                _waypointIndex++;
            else
                break;
        }

        Vector2 destination;
        if (_waypointIndex < _path.Count)
        {
            destination = _path[_waypointIndex];
        }
        else
        {
            // Path exhausted or empty — steer directly toward the target.
            destination = new Vector2(_target!.X, _target.Y);
        }

        float ddx = destination.X - X;
        float ddy = destination.Y - Y;
        float len = MathF.Sqrt(ddx * ddx + ddy * ddy);

        if (len > 0.1f)
        {
            VelocityX = ddx / len * Speed;
            VelocityY = ddy / len * Speed;
        }
        else
        {
            VelocityX = 0f;
            VelocityY = 0f;
        }
    }

    public override void CustomDestroy()
    {
        _circle.Destroy();
    }
}
