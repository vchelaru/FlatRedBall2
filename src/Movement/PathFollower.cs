using System;
using FlatRedBall2.Math;

namespace FlatRedBall2.Movement;

/// <summary>
/// Advances an entity along a <see cref="Path"/> at a constant speed.
/// Call <see cref="Activity"/> every frame from the entity's <c>CustomActivity</c>.
/// </summary>
/// <remarks>
/// <c>PathFollower</c> only updates <c>X</c>, <c>Y</c>, and optionally <c>Rotation</c>
/// on the entity passed to <see cref="Activity"/>. The path can be swapped at runtime,
/// and multiple followers can share the same path.
/// </remarks>
public class PathFollower
{
    private float _distanceTraveled;
    private bool _completed;
    private int _lastSegmentIndex = -1;

    /// <summary>The path being followed. Can be swapped at runtime to redirect the entity.</summary>
    public Path Path { get; set; }

    /// <summary>Traversal speed in world units per second.</summary>
    public float Speed { get; set; } = 100f;

    /// <summary>
    /// When true, the follower wraps back to the start after reaching the end.
    /// Independent of <see cref="Path.IsLooped"/> — either flag enables looping.
    /// </summary>
    public bool Loops { get; set; }

    /// <summary>
    /// When true, sets the entity's <c>Rotation</c> to face the direction of travel each frame.
    /// Uses the FlatRedBall2 <see cref="Angle"/> convention: 0 = up (+Y), positive = clockwise.
    /// </summary>
    public bool FaceDirection { get; set; }

    /// <summary>Current distance from the path start, in world units.</summary>
    public float DistanceTraveled => _distanceTraveled;

    /// <summary>
    /// True after the follower has reached the end of an open (non-looping) path.
    /// Reset with <see cref="Reset"/>.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Fired when the follower enters a new path segment.
    /// Argument is the zero-based index of the segment just entered (Move segments are not counted).
    /// </summary>
    public event Action<int>? WaypointReached;

    /// <summary>Fired once when the follower reaches the end of an open (non-looping) path.</summary>
    public event Action? PathCompleted;

    /// <summary>Creates a <c>PathFollower</c> targeting the given path.</summary>
    public PathFollower(Path path) => Path = path;

    /// <summary>
    /// Advances the follower and updates the entity's position (and optionally rotation).
    /// Call every frame from <c>CustomActivity</c>.
    /// </summary>
    public void Activity(Entity entity, float deltaSeconds)
    {
        if (_completed && !Loops && !Path.IsLooped) return;

        float total = Path.TotalLength;
        if (total < 1e-6f) return;

        _distanceTraveled += Speed * deltaSeconds;

        bool looping = Loops || Path.IsLooped;
        if (_distanceTraveled >= total)
        {
            if (looping)
                _distanceTraveled = ((_distanceTraveled % total) + total) % total;
            else
            {
                _distanceTraveled = total;
                if (!_completed)
                {
                    _completed = true;
                    PathCompleted?.Invoke();
                }
            }
        }

        int segIndex = Path.GetSegmentIndexAtLength(_distanceTraveled);
        if (segIndex != _lastSegmentIndex)
        {
            _lastSegmentIndex = segIndex;
            WaypointReached?.Invoke(segIndex);
        }

        var pos = Path.PointAtLength(_distanceTraveled);
        entity.X = pos.X;
        entity.Y = pos.Y;

        if (FaceDirection)
        {
            var tangent = Path.TangentAtLength(_distanceTraveled);
            // Standard math convention: 0 = right (1, 0), positive = CCW.
            entity.Rotation = Angle.FromRadians(MathF.Atan2(tangent.Y, tangent.X));
        }
    }

    /// <summary>
    /// Resets the follower to the path start and clears the <see cref="IsCompleted"/> flag.
    /// </summary>
    public void Reset()
    {
        _distanceTraveled = 0f;
        _completed = false;
        _lastSegmentIndex = -1;
    }
}
