using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One sprite's lane in a <see cref="SequenceSave"/>. Outside every chain on the track, the sprite is hidden. A track
/// has a name and a visibility flag, nothing else: the sprite sits at its frames' own offsets and draws with their own
/// flips (the animation's), so each comes from one place. A track has no depth value: the tracks of a sequence stack in
/// list order, the first in front and the last at the back, like layers in an image editor (the editor shows them as
/// lanes, the top lane in front).
/// </summary>
public class SequenceTrackSave
{
    /// <summary>Display name of the sprite.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Initial visibility of the track. The editor preview hides the track when false and runtimes may skip drawing
    /// it; game code may still enable it by name at runtime. Written to the file only when false, so every file
    /// without hidden tracks stays unchanged.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// When true, the track does not restart its chain at the wrap of a looping sequence if the chain playing at the wrap
    /// is the same chain (same source and chain name) as the track's first block, which must start at 0. It carries its
    /// playback phase across the wrap instead, so a chain stretched to a fractional number of cycles does not snap back.
    /// If a different chain is playing at the wrap, or none, the track restarts as usual. Tracks then drift against each
    /// other across passes; that is intended. On a <see cref="References"/> track the first clip's target keeps running across the wrap
    /// instead of restarting, when that clip starts at 0, the target loops and the clip playing at the wrap plays the same
    /// sequence (<see cref="SequenceTimeline.ReferenceCarryOver"/>); otherwise the flag is ignored. The chain rule is <see cref="SequenceTimeline.ActiveAtPass"/>. Written to the file
    /// only when true, so every file without it stays unchanged.
    /// </summary>
    public bool Continuous { get; set; }

    /// <summary>
    /// When not null, the track plays other sequences of the file instead of chains: a list of clips like a chain track's
    /// blocks (see <see cref="SequenceReferenceSave"/>), and <see cref="Chains"/> must stay empty: reading a file whose
    /// track has both throws. Null for a chain track; an empty list is a reference track with no clip yet. Playback orders
    /// the clips by <see cref="SequenceReferenceSave.Start"/>, list order breaking ties, and the next clip cuts the previous
    /// one (see <see cref="SequenceTimeline"/>). The targets' tracks are inlined under this track's name;
    /// <see cref="Visible"/> false hides all of them. JSON key <c>references</c>, always an array on write; a legacy single
    /// <c>reference</c> object reads as a one-clip list.
    /// </summary>
    public List<SequenceReferenceSave>? References { get; set; }

    /// <summary>
    /// Chain references in any order. Playback orders them by <see cref="SequenceChainReferenceSave.Start"/>, and list
    /// order breaks ties (see <see cref="SequenceTimeline"/>).
    /// </summary>
    public List<SequenceChainReferenceSave> Chains { get; set; } = new();

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
