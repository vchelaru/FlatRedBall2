namespace FlatRedBall2.Animation;

/// <summary>
/// How a frame's per-frame color (<c>AnimationFrame.Red</c>/<c>AnimationFrame.Green</c>/
/// <c>AnimationFrame.Blue</c>) combines with the sprite's texture. A frame with a <c>null</c> operation
/// inherits the most recent earlier frame in its chain that sets one (see
/// <c>FlatRedBall2.AnimationEditorCommon.EffectiveFrameColor</c>); if none does, R/G/B are not applied.
/// FRB2 and the FlatRedBall.AnimationChain.MonoGame/KNI packages apply it; other <c>.achx</c> consumers
/// (Gum, FRB1) decide for themselves.
/// </summary>
public enum ColorOperation
{
    /// <summary>Multiply the texture by the color (darken / colorize). White is the identity.</summary>
    Multiply,

    /// <summary>Add the color to the texture (brighten / glow / flash). Black is the identity.</summary>
    Add,
}
