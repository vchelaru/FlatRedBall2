namespace FlatRedBall.AnimationChain;

/// <summary>
/// How a frame's per-frame color (<c>AnimationFrame.Red</c>/<c>AnimationFrame.Green</c>/
/// <c>AnimationFrame.Blue</c>) combines with the sprite's texture. A <c>null</c> operation
/// means none. Authored in the Animation Editor and stored in the .achx; whether a given
/// runtime applies it is that runtime's choice — <see cref="SpriteBatchExtensions.DrawAnimation"/>
/// does not apply it automatically.
/// </summary>
public enum ColorOperation
{
    /// <summary>Multiply the texture by the color (darken / colorize). White is the identity.</summary>
    Multiply,

    /// <summary>Add the color to the texture (brighten / glow / flash). Black is the identity.</summary>
    Add,
}
