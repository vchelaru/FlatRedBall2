using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

/// <summary>
/// Short-lived particle spawned when an enemy is destroyed.
/// Flies outward then fades out and self-destructs.
/// </summary>
public class DeathParticle : Entity
{
    private AxisAlignedRectangle _rect = null!;
    private float _lifetime;
    private float _totalLifetime;
    private Color _startColor;

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width = 6,
            Height = 6,
            Color = Color.White,
            IsFilled = true,
            Visible = true,
        };
        AddChild(_rect);
    }

    /// <summary>
    /// Configures particle appearance and lifetime. Call immediately after Create().
    /// </summary>
    public void Launch(Color color, float lifetime)
    {
        _startColor = color;
        _lifetime = lifetime;
        _totalLifetime = lifetime;
        _rect.Color = color;
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
        {
            Destroy();
            return;
        }

        // Fade out as lifetime expires
        float frac = _lifetime / _totalLifetime;
        byte alpha = (byte)(frac * 220);
        _rect.Color = new Color(_startColor.R, _startColor.G, _startColor.B, alpha);
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
