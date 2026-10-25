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
    /// When <c>true</c>, playback bounces: forward to the last frame, then backward, without repeating the end
    /// frames at the turnaround (A B C B A B C...). One forward-and-back trip is one loop; with <see cref="Loop"/>
    /// off the chain plays one round trip and stops on the frame it started on. Defaults to <c>false</c> and is
    /// written only when <c>true</c>, so readers that predate it (Gum, FRB1) fall back to plain looping. Seeds the
    /// runtime's per-instance flag the same way <see cref="Loop"/> does (see e.g. <c>Sprite.IsPingPong</c>).
    /// </summary>
    public bool PingPong;

    /// <summary>
    /// When <c>true</c>, playback starts on the last frame and runs backward; with <see cref="PingPong"/> it goes
    /// backward first, then forward. Keyframe curves play time-reversed (see <see cref="KeyframeEvaluator"/>).
    /// Defaults to <c>false</c>, written only when <c>true</c>, and seeds the runtime's per-instance flag the same
    /// way <see cref="Loop"/> does (see e.g. <c>Sprite.IsReversed</c>).
    /// </summary>
    public bool Reverse;
}
