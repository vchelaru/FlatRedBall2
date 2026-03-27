using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class PlatformCoin : Entity
{
    public Circle Circle { get; private set; } = null!;

    // Bobbing animation state
    private float _baseY;
    private float _bobTimer;

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 10,
            Color = new Color(255, 220, 50, 255),
            IsVisible = true,
            IsFilled = true,
        };
        Add(Circle);
    }

    /// <summary>
    /// Call after setting Y to lock in the bob center position.
    /// </summary>
    public void SetBaseY() => _baseY = Y;

    public override void CustomActivity(FrameTime time)
    {
        _bobTimer += time.DeltaSeconds * 3f;
        Y = _baseY + MathF.Sin(_bobTimer) * 4f;
    }
}
