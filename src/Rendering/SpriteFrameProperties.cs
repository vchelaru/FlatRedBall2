using System;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Groups of <see cref="Sprite"/> properties that animation frames can control. Combine with
/// <c>|</c>/<c>&amp;</c> and assign to <see cref="Sprite.FrameControlledProperties"/>.
/// A group that is not set is owned by game code: frames never write it.
/// </summary>
[Flags]
public enum SpriteFrameProperties
{
    /// <summary>No optional group is frame-controlled. Texture and source rectangle still follow the frame.</summary>
    None = 0,

    /// <summary><see cref="Sprite.FlipHorizontal"/>, <see cref="Sprite.FlipVertical"/> and <see cref="Sprite.FlipDiagonal"/>.</summary>
    Flip = 1,

    /// <summary><see cref="Sprite.X"/> and <see cref="Sprite.Y"/>, from the frame's <c>RelativeX</c>/<c>RelativeY</c>.</summary>
    Offset = 2,

    /// <summary>
    /// The frame's color channels, alpha and color operation. Multiply color and alpha are applied at
    /// draw time; an Add operation is assigned to <see cref="Sprite.AddColor"/>.
    /// </summary>
    Color = 4,

    /// <summary>Every group (the default).</summary>
    All = Flip | Offset | Color,
}
