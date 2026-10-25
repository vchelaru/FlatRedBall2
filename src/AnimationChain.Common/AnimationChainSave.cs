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
    /// Tooling-only flag (AnimationEditor): when <c>true</c>, the chain's frames and shapes
    /// should not be edited. Not consumed by any runtime (see the "editor authors, runtimes
    /// interpret" rule in the AnimationEditor's own docs).
    /// </summary>
    public bool IsLocked;

    /// <summary>
    /// Whether this chain loops at runtime. Defaults to <c>true</c> so a file that omits this
    /// field (every .achx/.achj written before this field existed) keeps its prior behavior.
    /// Runtimes seed their own playback loop flag from this value when the chain starts playing,
    /// but that flag stays overridable per-instance afterward (see e.g. <c>Sprite.IsLooping</c>).
    /// </summary>
    public bool Loop = true;

    /// <summary>
    /// When <c>true</c>, each cycle plays the chain's forward timeline and then the same timeline mirrored, so a cycle
    /// is twice the chain's length, the end frames show for twice their length across the turnaround, and keyframe
    /// glides play backward on the way back. With <see cref="Loop"/> off one full cycle plays, then it stops. Defaults
    /// to <c>false</c> and is written only when <c>true</c>, so readers that predate it (Gum, FRB1) fall back to plain
    /// playback. Seeds the runtime's per-instance flag the same way <see cref="Loop"/> does (see e.g.
    /// <c>Sprite.IsPingPong</c>). The mapping is <see cref="PlaybackTimeline"/>.
    /// </summary>
    public bool PingPong;

    /// <summary>
    /// When <c>true</c>, playback shows the chain's forward timeline mirrored: time t shows forward time T - t, so it
    /// starts at the end of the last frame and keyframe glides play backward. With <see cref="PingPong"/> the mirrored
    /// half comes first. Defaults to <c>false</c>, written only when <c>true</c>, and seeds the runtime's per-instance
    /// flag the same way <see cref="Loop"/> does (see e.g. <c>Sprite.IsReversed</c>).
    /// </summary>
    public bool Reverse;
}
