using System.Collections.Generic;

namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Deserialized representation of an animation chain within a .achx file.
/// </summary>
public class AnimationChainSave
{
    /// <summary>The name of the animation chain.</summary>
    public string Name = string.Empty;

    /// <summary>The list of frames in this chain.</summary>
    public List<AnimationFrameSave> Frames = new();
}
