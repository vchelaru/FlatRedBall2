using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Opt-in nestable timing for engine boot and game load — the "where did those 30 seconds go"
/// question. Phases are buffered silently and rendered as one indented report by
/// <see cref="GenerateReport"/>, rather than logged as they happen: interleaved log lines from a
/// boot that spans several subsystems are unreadable, and the per-line I/O would itself show up
/// in the measurement.
/// <para>
/// Off by default. Because the phases worth measuring run inside engine initialization, this has
/// to be enabled before that starts — see <c>EngineInitSettings</c> — rather than turned on after
/// the fact.
/// </para>
/// </summary>
public sealed class StartupProfiler
{
    // Two spaces per nesting level. Deep boots stay readable because the tree is shallow in
    // practice (engine -> subsystem -> asset kind).
    private const int IndentPerDepth = 2;

    private sealed class Phase
    {
        public required string Name { get; init; }
        public required long StartTicks { get; init; }
        public required int Depth { get; init; }
        public double? DurationMs { get; set; }
        public List<Phase> Children { get; } = [];
    }

    private readonly List<Phase> _roots = [];
    private readonly Stack<Phase> _openPhases = new();

    /// <summary>
    /// When <c>false</c> (the default) <see cref="BeginPhase"/> and <see cref="EndPhase"/> are
    /// no-ops and <see cref="GenerateReport"/> returns an empty string.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether any phase has been recorded. False when profiling was never enabled, which is the
    /// case a caller should check before printing a report that would otherwise be empty.
    /// </summary>
    public bool HasRecordedPhases => _roots.Count > 0;

    // Swapped out in tests so a phase can be given an exact duration.
    internal Func<long> TimestampProvider { get; set; } = Stopwatch.GetTimestamp;

    /// <summary>
    /// Opens a timing phase, nested inside whichever phase is currently open. Every call must be
    /// paired with an <see cref="EndPhase"/>; an unpaired one is reported as incomplete rather
    /// than silently dropped.
    /// </summary>
    public void BeginPhase(string name)
    {
        if (!IsEnabled) return;

        var phase = new Phase
        {
            Name = name,
            StartTicks = TimestampProvider(),
            Depth = _openPhases.Count,
        };

        if (_openPhases.Count > 0)
            _openPhases.Peek().Children.Add(phase);
        else
            _roots.Add(phase);

        _openPhases.Push(phase);
    }

    /// <summary>
    /// Closes the innermost open phase and records its duration. Called with no phase open — which
    /// an early return or a swallowed exception on a boot path can cause — it does nothing rather
    /// than throwing; losing one timing is preferable to taking the game down over a diagnostic.
    /// </summary>
    public void EndPhase()
    {
        if (!IsEnabled || _openPhases.Count == 0) return;

        var phase = _openPhases.Pop();
        phase.DurationMs = ProfileClock.Ms(phase.StartTicks, TimestampProvider());
    }

    /// <summary>
    /// Builds the consolidated indented report — one line per phase, with its duration and its
    /// share of the total. Pure string building: does no I/O, so printing or logging it is on the
    /// caller. Returns an empty string when nothing was recorded.
    /// </summary>
    public string GenerateReport()
    {
        if (!HasRecordedPhases) return string.Empty;

        // Only completed root phases count toward the total; an open one has no duration yet, and
        // measuring it to "now" would really be timing how long until someone asked for a report.
        double totalMs = 0;
        foreach (var root in _roots)
            totalMs += root.DurationMs ?? 0;

        var nameColumnWidth = 0;
        foreach (var root in _roots)
            nameColumnWidth = System.Math.Max(nameColumnWidth, MeasureNameColumn(root));

        var sb = new StringBuilder();
        sb.AppendLine(FormattableString.Invariant($"Startup timing — total {totalMs:F2}ms"));
        foreach (var root in _roots)
            AppendPhase(sb, root, totalMs, nameColumnWidth);

        return sb.ToString();
    }

    private static int MeasureNameColumn(Phase phase)
    {
        var width = IndentPerDepth * (phase.Depth + 1) + phase.Name.Length;
        foreach (var child in phase.Children)
            width = System.Math.Max(width, MeasureNameColumn(child));
        return width;
    }

    private static void AppendPhase(StringBuilder sb, Phase phase, double totalMs, int nameColumnWidth)
    {
        var label = new string(' ', IndentPerDepth * (phase.Depth + 1)) + phase.Name;

        if (phase.DurationMs is { } durationMs)
        {
            // A zero total would only happen if every root phase is open, in which case there is
            // no duration to take a percentage of anyway.
            var share = totalMs > 0 ? durationMs / totalMs * 100 : 0;
            sb.AppendLine(FormattableString.Invariant(
                $"{label.PadRight(nameColumnWidth)}  {durationMs,9:F2}ms  {share,5:F1}%"));
        }
        else
        {
            sb.AppendLine(FormattableString.Invariant(
                $"{label.PadRight(nameColumnWidth)}  {"(incomplete)",11}  {"",5}  [never ended]"));
        }

        foreach (var child in phase.Children)
            AppendPhase(sb, child, totalMs, nameColumnWidth);
    }
}
