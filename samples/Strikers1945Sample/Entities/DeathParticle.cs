using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Strikers1945Sample.Entities;

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
            Width = 8,
            Height = 8,
            Color = Color.White,
            IsFilled = true,
            Visible = true,
        };
        Add(_rect);
    }

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

        float frac = _lifetime / _totalLifetime;

        // Fade: bright orange/yellow -> red -> transparent
        byte r, g, b;
        if (frac > 0.5f)
        {
            // First half: start color -> red
            float t = (frac - 0.5f) * 2f; // 1..0
            r = (byte)MathF.Min(255, _startColor.R + (1f - t) * (255 - _startColor.R));
            g = (byte)(_startColor.G * t);
            b = (byte)(_startColor.B * t * 0.3f);
        }
        else
        {
            // Second half: red -> dark, fading out
            float t = frac * 2f; // 1..0
            r = (byte)(200 * t);
            g = 0;
            b = 0;
        }
        byte alpha = (byte)(frac * 255);
        _rect.Color = new Color(r, g, b, alpha);
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
