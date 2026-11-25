namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// The timing rules of a <see cref="SequenceSave"/>, shared by the editor and any runtime player so both agree.
/// <list type="bullet">
/// <item>A chain reference lasts its <see cref="SequenceChainReferenceSave.Length"/> (one cycle when unset), or until the next chain on
/// its track starts, whichever is first. The cut part never plays and is not resumed. The cut is not stored: move the
/// next chain away and the earlier one plays in full again.</item>
/// <item>What fills that length follows the chain's own Loop flag: a looping chain repeats, a non-looping one plays one
/// cycle and holds its last frame (the final frame of <see cref="PlaybackTimeline"/>'s cycle) to the end.</item>
/// <item>Frame events fire when playback enters their frame, in every cycle of a reference's block, not only the first
/// (a walk repeated 3 times fires its footstep 3 times). A block of a chain that does not loop has one cycle; its hold
/// raises nothing. Never from the part of a block the next chain cuts off, and never from a hidden track (turning it on
/// does not replay frames already passed). A ping-pong's turnaround and loop point raise nothing: only a changed frame
/// does. A large time step raises every skipped frame's events in order. A sequence with Loop off raises none during its
/// hold.</item>
/// <item>The next chain is the one with the next later start. On a tie, list order decides: of two chains starting
/// together, the earlier one in <see cref="SequenceTrackSave.Chains"/> plays for zero seconds.</item>
/// <item>A missing chain (cycle length 0) never plays, but it still cuts the chain before it.</item>
/// <item>Outside every chain, the track's sprite is hidden. That includes the time before the first chain starts,
/// on purpose: it is how a sprite appears partway through a sequence. If it showed before its chain, it would always
/// be visible.</item>
/// <item>A reference track (<see cref="SequenceTrackSave.References"/>) holds clips like a chain track holds blocks, with the
/// same rules: clips play in order of <see cref="SequenceReferenceSave.Start"/>, list order breaks ties (the earlier one in the
/// list plays for zero seconds); a clip lasts its <see cref="SequenceReferenceSave.Length"/> (one run of its target when
/// unset) or until the next clip starts, whichever is first, so clips never overlap and the cut part never plays; outside every
/// clip the track's sprites are hidden; a clip whose target is missing, empty or part of a cycle plays nothing but still cuts
/// the clip before it. A clip's target restarts at the clip's start (<see cref="ReferenceTime"/>). The sequence lasts to the
/// latest end of any clip. The tracks of every clip inline under the reference track's name, so same-named tracks from
/// different clips are one sprite (<see cref="SequenceReferences.Inline"/>).</item>
/// <item>A <see cref="SequenceTrackSave.Continuous"/> reference track carries its first clip's target phase across the wrap of a looping holder (<see cref="ReferenceCarryOver"/>): the target keeps running instead of restarting.</item>
/// <item>A sequence with <see cref="SequenceSave.Loop"/> off does not end by going blank: it stops at its length and holds
/// its final pose (<see cref="PoseTime"/>). The hold is the composite of the last instant (<see cref="LastInstant"/>), so a
/// track whose chains finished earlier stays hidden. With Loop on the sequence repeats from 0.</item>
/// <item>A time within the slack (<see cref="BoundaryTolerance"/>) before a loop's end is the start of the next loop, pass
/// count included, so a time on a multiple of a length that summed to a hair more never lands on the hidden end
/// (<see cref="WrapTime(double, double, out int)"/>). A player wraps its time that way before it samples: a looping sequence
/// sampled at its own end is hidden. With Loop off such a time is the end, and holds.</item>
/// </list>
/// Cycle lengths come from the caller (a chain's frame lengths summed, doubled for ping-pong), so this class needs no
/// chain data. Sum each length as the decimal the file holds: the float nearest 0.1 is a hair over it, and after a few
/// hundred cycles a time on a frame's start would show the frame before.
/// </summary>
public static class SequenceTimeline
{
    /// <summary>
    /// The shortest length in seconds <see cref="LengthForEdge"/> returns, so a block never shrinks to nothing. Under a
    /// frame at 60 fps.
    /// </summary>
    public const double MinimumLength = 0.01;

    /// <summary>
    /// Slack in seconds (at least this, and proportional to the time beyond 1 s) when comparing a time to a block's start or end. Cycle lengths sum float frame lengths, so a block
    /// whose end is 2.4 on paper ends a hair after it; without the slack a playhead on that end still shows the block.
    /// A start is inclusive and an end exclusive, within this slack.
    /// </summary>
    public const double BoundaryTolerance = 1e-6;

    // Float error in a summed block end grows with the number of cycles, so the slack scales with the time.
    private static double Slack(double time) => BoundaryTolerance * Math.Max(1, Math.Abs(time));

    /// <summary>
    /// Authored length in seconds: <paramref name="length"/> when set, else one <paramref name="cycleLength"/>. Never
    /// negative, and 0 when <paramref name="cycleLength"/> is not positive (a missing chain or target never plays).
    /// </summary>
    public static double FullLength(double? length, double cycleLength) =>
        cycleLength > 0 ? Math.Max(0, length ?? cycleLength) : 0;

    /// <summary>
    /// Seconds a chain actually plays: <paramref name="fullLength"/>, cut where <paramref name="nextStart"/> begins.
    /// Pass null for <paramref name="nextStart"/> when the chain is last on its track.
    /// </summary>
    public static double PlayedLength(double start, double fullLength, double? nextStart) =>
        nextStart is { } next ? Math.Clamp(next - start, 0, fullLength) : fullLength;

    /// <summary>Seconds <paramref name="reference"/> plays on <paramref name="track"/> after the cut rule.</summary>
    public static double PlayedLength(SequenceTrackSave track, SequenceChainReferenceSave reference, double cycleLength) =>
        PlayedLength(reference.Start, FullLength(reference.Length, cycleLength), NextStart(track, reference));

    /// <summary>The track's references in playback order: by start, ties kept in list order.</summary>
    public static IReadOnlyList<SequenceChainReferenceSave> Ordered(SequenceTrackSave track) =>
        track.Chains.Select((reference, index) => (reference, index))
            .OrderBy(pair => pair.reference.Start).ThenBy(pair => pair.index)
            .Select(pair => pair.reference).ToList();

    /// <summary>
    /// When the chain after <paramref name="reference"/> on <paramref name="track"/> starts, or null when
    /// <paramref name="reference"/> is last or not on the track.
    /// </summary>
    public static double? NextStart(SequenceTrackSave track, SequenceChainReferenceSave reference)
    {
        var ordered = Ordered(track);
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            if (ReferenceEquals(ordered[i], reference)) return ordered[i + 1].Start;
        }
        return null;
    }

    /// <summary>
    /// The reference playing at <paramref name="time"/> on <paramref name="track"/>, or null when the sprite is hidden.
    /// </summary>
    /// <param name="track">The track to sample.</param>
    /// <param name="time">Seconds from the sequence start.</param>
    /// <param name="cycleLength">A reference's cycle length in seconds; 0 for a missing chain.</param>
    /// <param name="localTime">Seconds since the returned reference's start; 0 when none plays. The caller maps it onto the
    /// chain: wrapped when the chain loops, held at the end of the cycle when it does not.</param>
    public static SequenceChainReferenceSave? ActiveAt(SequenceTrackSave track, double time,
        Func<SequenceChainReferenceSave, double> cycleLength, out double localTime)
    {
        localTime = 0;
        var ordered = Ordered(track);
        // The latest chain that has started is the only candidate: any earlier one is cut at its start.
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var reference = ordered[i];
            if (reference.Start > time + Slack(time)) continue;

            double? next = i + 1 < ordered.Count ? ordered[i + 1].Start : null;
            double played = PlayedLength(reference.Start, FullLength(reference.Length, cycleLength(reference)), next);
            if (time + Slack(time) >= reference.Start + played) return null;

            localTime = Math.Max(0, time - reference.Start);
            return reference;
        }
        return null;
    }

    /// <summary>
    /// Seconds of playback a <see cref="SequenceTrackSave.Continuous"/> track's first block has already advanced when pass
    /// <paramref name="pass"/> (0 = the first run through, 1 = after the first wrap) begins: 0 for pass 0, and for a track
    /// that restarts at the wrap. At each wrap the carry becomes the playback time of the block playing at the <see cref="LastInstant"/>
    /// (<paramref name="sequenceLength"/> minus its start, plus its own carry when it is the first block), if that block
    /// is the same chain (source and name) as the first block and the first block starts at 0; otherwise 0. The block at the
    /// wrap is the same in every pass, so this is <paramref name="pass"/> times the sequence length when it is the first
    /// block, and the same after every wrap when it is a later one. Passes are
    /// counted by the player: the number of times the looping sequence has wrapped.
    /// </summary>
    public static double CarryOver(SequenceTrackSave track, double sequenceLength, int pass,
        Func<SequenceChainReferenceSave, double> cycleLength)
    {
        if (!track.Continuous || pass <= 0 || sequenceLength <= 0 || track.Chains.Count == 0) return 0;
        var first = Ordered(track)[0];
        if (first.Start > Slack(0)) return 0;

        // The block playing just before the end of a pass: the wrap instant itself belongs to the next pass.
        var atWrap = ActiveAt(track, LastInstant(sequenceLength), cycleLength, out _);
        if (atWrap is null || atWrap.Source != first.Source || atWrap.Chain != first.Chain) return 0;
        return (ReferenceEquals(atWrap, first) ? pass : 1) * (sequenceLength - atWrap.Start);
    }

    /// <summary>
    /// <see cref="ActiveAt"/> for pass <paramref name="pass"/> of a looping sequence of <paramref name="sequenceLength"/>
    /// seconds, where <paramref name="time"/> is the time inside that pass (0 to the length). A
    /// <see cref="SequenceTrackSave.Continuous"/> track's first block does not restart at the wrap: its
    /// <paramref name="localTime"/> includes <see cref="CarryOver"/>, so the caller maps it onto the chain as usual (wrapped
    /// when the chain loops). Every other block, and every other track, is exactly <see cref="ActiveAt"/>. A player counts
    /// the wraps itself (a seek or restart makes it pass 0 again).
    /// </summary>
    public static SequenceChainReferenceSave? ActiveAtPass(SequenceTrackSave track, double time, int pass, double sequenceLength,
        Func<SequenceChainReferenceSave, double> cycleLength, out double localTime)
    {
        var active = ActiveAt(track, time, cycleLength, out localTime);
        if (active is not null && track.Continuous && pass > 0 && ReferenceEquals(active, Ordered(track)[0]))
            localTime += CarryOver(track, sequenceLength, pass, cycleLength);
        return active;
    }

    /// <summary>
    /// Seconds until the sequence ends: the latest end of any clip on any track. A chain ends where it stops playing; a
    /// reference clip ends its <see cref="SequenceReferenceSave.Length"/> (one run of its target when unset) after its start,
    /// measured the same way, through every level. A reference that does not <see cref="SequenceReferences.Plays"/> (a
    /// missing target or a cycle) adds nothing. 0 when nothing plays.
    /// </summary>
    /// <param name="sequences">Every sequence of the file, to resolve references by name.</param>
    /// <param name="sequence">The sequence to measure.</param>
    /// <param name="cycleLength">A chain reference's cycle length in seconds; 0 for a missing chain.</param>
    public static double Length(IReadOnlyList<SequenceSave> sequences, SequenceSave sequence, Func<SequenceChainReferenceSave, double> cycleLength) =>
        LengthWithin(sequences, sequence, cycleLength, new List<SequenceSave> { sequence });

    private static double LengthWithin(IReadOnlyList<SequenceSave> sequences, SequenceSave sequence,
        Func<SequenceChainReferenceSave, double> cycleLength, List<SequenceSave> stack)
    {
        double end = 0;
        foreach (var track in sequence.Tracks)
        {
            foreach (var reference in track.Chains)
                end = Math.Max(end, reference.Start + PlayedLength(track, reference, cycleLength(reference)));

            foreach (var clip in track.References ?? [])
            {
                // The stack check is a backstop for duplicate names, which Plays (by name) cannot tell apart.
                if (!SequenceReferences.Plays(sequences, sequence.Name, clip.Sequence)
                    || SequenceReferences.Find(sequences, clip.Sequence) is not { } target || stack.Contains(target)) continue;
                stack.Add(target);
                double targetLength = LengthWithin(sequences, target, cycleLength, stack);
                stack.RemoveAt(stack.Count - 1);
                end = Math.Max(end, clip.Start + PlayedLength(clip.Start, FullLength(clip.Length, targetLength), NextStart(track, clip)));
            }
        }
        return end;
    }

    /// <summary>
    /// The sequence time to show for an elapsed <paramref name="time"/>: wrapped when <paramref name="loop"/>, otherwise
    /// held at <paramref name="length"/>. Negative time shows 0.
    /// </summary>
    public static double WrapTime(double time, double length, bool loop) =>
        loop ? WrapTime(time, length, out _) : Math.Clamp(time, 0, Math.Max(0, length));

    /// <summary>
    /// The time inside the current loop of a looping sequence of <paramref name="length"/> seconds after
    /// <paramref name="time"/> seconds of playback, and in <paramref name="passes"/> the number of loops completed by then.
    /// Within the slack (<see cref="BoundaryTolerance"/>, for <paramref name="length"/>) before a loop's end the next loop
    /// has begun: the result is 0 and the pass is counted. Negative time, and an empty sequence, is 0 in pass 0.
    /// </summary>
    public static double WrapTime(double time, double length, out int passes)
    {
        passes = 0;
        if (length <= 0 || time <= 0) return 0;
        double inLoop = time % length;
        // From the remainder, not time / length, which can round up to a whole number the remainder has not reached.
        passes = (int)Math.Round((time - inLoop) / length);
        if (inLoop + Slack(length) < length) return inLoop;
        passes++;
        return 0;
    }

    /// <summary>How far before its end a sequence under 100 s has its <see cref="LastInstant"/>, in seconds.</summary>
    public const double HoldEpsilon = 1e-4;

    /// <summary>
    /// The last instant, in seconds, before the end of a sequence of <paramref name="length"/> seconds: what plays there is
    /// what a non-looping sequence holds (<see cref="PoseTime"/>) and what a continuous track carries across the wrap
    /// (<see cref="CarryOver"/>, <see cref="ReferenceCarryOver"/>). It is <see cref="HoldEpsilon"/> before the end, or twice
    /// the slack (<see cref="BoundaryTolerance"/>, for <paramref name="length"/>) before it from 100 s on, where the slack
    /// reaches <see cref="HoldEpsilon"/> and would hide everything. Never negative.
    /// </summary>
    public static double LastInstant(double length) =>
        Math.Max(0, length - (length < 100 ? HoldEpsilon : 2 * Slack(length)));

    /// <summary>
    /// The time to sample a sequence's pose at for a playhead at <paramref name="time"/>. A sequence with
    /// <paramref name="loop"/> off stops at its end and holds its final pose: at or past <paramref name="length"/>, or within
    /// the slack (<see cref="BoundaryTolerance"/>) before it, the pose is the one at the <see cref="LastInstant"/> before the end, so a track whose chains already
    /// finished stays hidden and the rest keep their last frame. Otherwise, and for an empty sequence, the time is unchanged.
    /// </summary>
    public static double PoseTime(double time, double length, bool loop) =>
        !loop && length > 0 && time + Slack(length) >= length ? LastInstant(length) : time;

    /// <summary>The track's reference clips in playback order: by start, ties kept in list order.</summary>
    public static IReadOnlyList<SequenceReferenceSave> OrderedClips(SequenceTrackSave track) =>
        (track.References ?? []).Select((clip, index) => (clip, index))
            .OrderBy(pair => pair.clip.Start).ThenBy(pair => pair.index)
            .Select(pair => pair.clip).ToList();

    /// <summary>
    /// When the clip after <paramref name="clip"/> on <paramref name="track"/> starts, or null when
    /// <paramref name="clip"/> is last or not on the track. That start cuts <paramref name="clip"/> short.
    /// </summary>
    public static double? NextStart(SequenceTrackSave track, SequenceReferenceSave clip)
    {
        var ordered = OrderedClips(track);
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            if (ReferenceEquals(ordered[i], clip)) return ordered[i + 1].Start;
        }
        return null;
    }

    /// <summary>
    /// The clip playing at <paramref name="time"/> on a reference <paramref name="track"/>, or null when it shows nothing:
    /// before the first clip, after the last one ends, or when the latest clip started has a missing or empty target. Same
    /// rule as <see cref="ActiveAt"/> for chains: the latest clip that has started is the only candidate, and it plays until
    /// its length (<see cref="FullLength"/>) or the next clip's start, whichever is first.
    /// </summary>
    public static SequenceReferenceSave? ActiveClipAt(SequenceTrackSave track, double time, Func<SequenceReferenceSave, double> targetLength)
    {
        var ordered = OrderedClips(track);
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var clip = ordered[i];
            if (clip.Start > time + Slack(time)) continue;
            double? next = i + 1 < ordered.Count ? ordered[i + 1].Start : null;
            double played = PlayedLength(clip.Start, FullLength(clip.Length, targetLength(clip)), next);
            return time + Slack(time) >= clip.Start + played ? null : clip;
        }
        return null;
    }

    /// <summary>
    /// The time to sample <paramref name="target"/> at when the sequence holding <paramref name="reference"/> is at
    /// <paramref name="parentTime"/>, or null when the clip is not playing: before its start, after
    /// its <see cref="SequenceReferenceSave.Length"/> (one <paramref name="targetLength"/> when unset) or at
    /// <paramref name="nextStart"/> (the start of the next clip on the track), whichever is first,
    /// or when the target has no length.
    /// The target restarts at the clip's start. A looping target wraps at its
    /// length (<see cref="WrapTime(double, double, out int)"/>); one with Loop off plays once and then holds its final pose (<see cref="PoseTime"/>) until the clip ends. The
    /// holder lasts at least to the clip's end (<see cref="Length"/>), so a looping holder restarts the target only after it.
    /// </summary>
    public static double? ReferenceTime(SequenceReferenceSave reference, SequenceSave target, double targetLength, double parentTime,
        double? nextStart = null)
    {
        if (targetLength <= 0) return null;
        if (reference.Start > parentTime + Slack(parentTime)) return null;
        double local = Math.Max(0, parentTime - reference.Start);
        double played = PlayedLength(reference.Start, FullLength(reference.Length, targetLength), nextStart);
        if (parentTime + Slack(parentTime) >= reference.Start + played) return null;
        return PoseTime(WrapTime(local, targetLength, target.Loop), targetLength, target.Loop);
    }

    /// <summary>
    /// Seconds of playback a <see cref="SequenceTrackSave.Continuous"/> reference track's first clip has already advanced
    /// when pass <paramref name="pass"/> (0 = the first run through, 1 = after the first wrap, counted by the player) of a
    /// looping holder of <paramref name="sequenceLength"/> seconds begins: 0 for pass 0, for a track that restarts at the
    /// wrap, and unless all of these hold: the first clip starts at 0, its target loops (<paramref name="loops"/>), and the
    /// clip playing at the holder's <see cref="LastInstant"/> (<see cref="ActiveClipAt"/>) plays the same sequence as the first clip.
    /// At each wrap the carry becomes the time that clip has played (<paramref name="sequenceLength"/> minus its start, plus
    /// its own carry when it is the first clip, so <paramref name="pass"/> times that); otherwise 0. A clip that ends before the holder does is not playing at the
    /// wrap, so a default-length clip carries only when its target is as long as the holder; to carry a shorter looping
    /// target, set the clip's length to the holder's length. Only the first clip carries; every other clip restarts at the
    /// wrap. The chain rule is <see cref="CarryOver"/>.
    /// </summary>
    public static double ReferenceCarryOver(SequenceTrackSave track, double sequenceLength, int pass,
        Func<SequenceReferenceSave, double> targetLength, Func<SequenceReferenceSave, bool> loops)
    {
        if (!track.Continuous || pass <= 0 || sequenceLength <= 0 || OrderedClips(track) is not { Count: > 0 } ordered) return 0;
        var first = ordered[0];
        if (first.Start > Slack(0) || targetLength(first) <= 0 || !loops(first)) return 0;

        // The clip playing just before the end of a pass: the wrap instant itself belongs to the next pass.
        var atWrap = ActiveClipAt(track, LastInstant(sequenceLength), targetLength);
        if (atWrap is null || atWrap.Sequence != first.Sequence) return 0;
        return (ReferenceEquals(atWrap, first) ? pass : 1) * (sequenceLength - atWrap.Start);
    }

    /// <summary>
    /// <see cref="ReferenceTime"/> for <paramref name="clip"/> of <paramref name="track"/> (the next clip cuts it) in pass
    /// <paramref name="pass"/> of a looping holder of <paramref name="sequenceLength"/> seconds (counted by the player),
    /// where <paramref name="parentTime"/> is the time inside that pass. The clip's start and end are checked against that
    /// in-pass time, as in pass 0. For the track's first clip, when <see cref="ReferenceCarryOver"/> is above 0 the target's
    /// clock does not restart at the wrap: it is the carry plus <paramref name="parentTime"/> (the clip starts at 0). The
    /// target is then a looping sequence playing continuously: its own wraps count as passes, returned in
    /// <paramref name="targetPass"/> and used for the target's own continuous tracks, and the returned time is inside the
    /// current one. Otherwise <paramref name="targetPass"/> is 0 and this is exactly <see cref="ReferenceTime"/>: a played
    /// sequence runs at pass 0. Null when the clip is not playing.
    /// </summary>
    public static double? ReferenceTimeAtPass(SequenceTrackSave track, SequenceReferenceSave clip, SequenceSave target, double targetLength,
        double parentTime, int pass, double sequenceLength, Func<SequenceReferenceSave, double> targetLengthOf, out int targetPass)
    {
        targetPass = 0;
        double? plain = ReferenceTime(clip, target, targetLength, parentTime, NextStart(track, clip));
        if (plain is null || pass <= 0 || !ReferenceEquals(clip, OrderedClips(track)[0])) return plain;

        double carry = ReferenceCarryOver(track, sequenceLength, pass, targetLengthOf, _ => target.Loop);
        if (carry <= 0) return plain;
        return WrapTime(carry + parentTime, targetLength, out targetPass);
    }

    /// <summary>The earliest a block's right edge can sit: <see cref="MinimumLength"/> after <paramref name="start"/>.</summary>
    public static double MinimumEdge(double start) => start + MinimumLength;

    /// <summary>
    /// The length that puts a block's right edge at <paramref name="edgeTime"/>, never below <see cref="MinimumLength"/>,
    /// rounded to 1e-6 s so <c>4.3 - 0.1</c> is 4.2 rather than 4.1999999999999993. Snapping the edge is
    /// <see cref="TimelineSnap"/>'s job; this only converts.
    /// </summary>
    public static double LengthForEdge(double start, double edgeTime) =>
        Math.Max(MinimumLength, Math.Round(edgeTime - start, 6));
}
