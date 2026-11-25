using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Stateless counterpart of <see cref="PlaybackController"/>: which frame a chain shows at an
/// absolute time, using the same frame lengths, looping, hold-at-end, and Reverse/PingPong mirroring
/// (<see cref="PlaybackTimeline"/>) rules.
/// </summary>
public static class ChainClock
{
    /// <summary>
    /// Length of one cycle in seconds (0 when every frame is skipped): the sum of the chain's frames, or twice that under
    /// <see cref="AnimationChainSave.PingPong"/>.
    /// </summary>
    public static double TotalLength(AnimationChainSave chain, FrameLengthUnit unit)
    {
        double total = 0;
        foreach (var frame in chain.Frames)
            total += PlaybackController.FrameSeconds(frame, unit);
        return PlaybackTimeline.CycleLength(total, chain.PingPong);
    }

    /// <summary>
    /// <see cref="TotalLength"/> as <see cref="SequenceTimeline"/> takes it: null for a chain that is missing or has no frames,
    /// which never plays, and 0 for one whose frames are all skipped.
    /// </summary>
    public static double? CycleLength(AnimationChainSave? chain, FrameLengthUnit unit) =>
        chain is { Frames.Count: > 0 } ? TotalLength(chain, unit) : null;

    /// <summary>
    /// The frame showing <paramref name="time"/> seconds after the chain starts, and the forward seconds into
    /// that frame to sample it at. A looping chain wraps; a non-looping chain holds its end.
    /// The chain must have at least one frame.
    /// </summary>
    public static (int FrameIndex, double Elapsed) Locate(AnimationChainSave chain, double time, FrameLengthUnit unit)
    {
        double local = PlaybackTimeline.Wrap(time, TotalLength(chain, unit), chain.Loop).CycleTime;
        var at = PlaybackTimeline.Locate(chain.Frames, f => PlaybackController.FrameSeconds(f, unit), local, chain.PingPong, chain.Reverse);
        return (at.FrameIndex, at.ElapsedInFrame);
    }
}
