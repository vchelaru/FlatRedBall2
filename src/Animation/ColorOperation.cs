namespace FlatRedBall2.Animation;

/// <summary>
/// How a frame's per-frame color (<see cref="AnimationFrame.Red"/>/<see cref="AnimationFrame.Green"/>/
/// <see cref="AnimationFrame.Blue"/>) combines with the sprite's texture. A <c>null</c> operation
/// means none. Like the channels themselves, this is authored in the Animation Editor and stored in
/// the <c>.achx</c>, but whether a given runtime applies it is that runtime's choice — the <c>.achx</c>
/// is a general-purpose format consumed by several renderers (Gum, MonoGame/FNA, FRB1, FRB2).
/// </summary>
public enum ColorOperation
{
    /// <summary>Multiply the texture by the color (darken / colorize). White is the identity.</summary>
    Multiply,

    /// <summary>Add the color to the texture (brighten / glow / flash). Black is the identity.</summary>
    Add,
}
