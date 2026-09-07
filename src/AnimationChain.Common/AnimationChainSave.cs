using System.Collections.Generic;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Deserialized representation of an animation chain within a .achx file.
/// </summary>
public class AnimationChainSave
{
    /// <summary>The name of the animation chain.</summary>
    public string Name = string.Empty;

    /// <summary>The list of frames in this chain.</summary>
    public List<AnimationFrameSave> Frames = new();

    /// <summary>
    /// Tooling-only flag (Animation Editor): when <c>true</c>, the chain's frames and shapes
    /// should not be edited. Not consumed by any runtime (see the "editor authors, runtimes
    /// interpret" rule in the Animation Editor's own docs).
    /// </summary>
    public bool IsLocked;
}
