using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Rendering;

// Stub AnimationChain type referenced by Sprite
public class AnimationChain
{
    public string Name { get; set; } = string.Empty;
}

public class Sprite : IRenderable, IAttachable
{
    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // Rotation
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    // IRenderable
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    // Visual
    public Texture2D? Texture { get; set; }
    public float Width { get; set; } = 32f;
    public float Height { get; set; } = 32f;
    public Color Color { get; set; } = Color.White;
    public float Alpha { get; set; } = 1f;
    public bool IsVisible { get; set; } = true;

    // Sprite sheet
    public Rectangle? SourceRectangle { get; set; }

    // Flip
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }

    // Animation
    public AnimationChain? CurrentAnimation { get; private set; }

    public void PlayAnimation(string name)
    {
        // TODO: Implement ACHX animation format parsing and playback. See design/TODOS.md
    }

    public void PlayAnimation(AnimationChain chain)
    {
        // TODO: Implement ACHX animation format parsing and playback. See design/TODOS.md
        CurrentAnimation = chain;
    }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Texture == null) return;

        var effects = SpriteEffects.None;
        if (FlipHorizontal) effects |= SpriteEffects.FlipHorizontally;
        if (FlipVertical)   effects |= SpriteEffects.FlipVertically;

        var color = Color * Alpha;
        var origin = new Vector2(Texture.Width / 2f, Texture.Height / 2f);
        var scale = new Vector2(Width / Texture.Width, Height / Texture.Height);
        var position = new Vector2(AbsoluteX, AbsoluteY);

        spriteBatch.Draw(
            Texture,
            position,
            SourceRectangle,
            color,
            -AbsoluteRotation.Radians,
            origin,
            scale,
            effects,
            0f);
    }

    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.RemoveChild(this);
        else
            Parent = null;
    }
}
