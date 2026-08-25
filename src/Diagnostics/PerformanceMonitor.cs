using System;
using System.Collections.Generic;
using System.Text;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Opt-in rolling-average performance instrumentation: FPS, per-phase frame timing, and a
/// FRB1-style collision relationship severity breakdown. Off by default — set
/// <see cref="IsEnabled"/> to <c>true</c> to start recording; when disabled, <see cref="Record"/>
/// is a no-op and adds no overhead. Access via <see cref="FlatRedBallService.Performance"/>.
/// </summary>
public class PerformanceMonitor
{
    private const int DefaultWindowSize = 120;

    private FrameProfile[] _window = new FrameProfile[DefaultWindowSize];
    private int _windowSize = DefaultWindowSize;
    private int _next;
    private int _count;
    private FrameProfile _current;
    private IReadOnlyList<ICollisionRelationship> _relationships = Array.Empty<ICollisionRelationship>();

    /// <summary>
    /// When <c>true</c>, every committed frame is fed into the rolling window by <see cref="Record"/>.
    /// Off by default — turn on to start collecting stats; leave off unless something is actually
    /// reading <see cref="GenerateReport"/> or the stat properties.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Number of most-recent frames kept in the rolling window backing every stat below.
    /// Changing this reallocates the window and discards any frames already recorded. Defaults
    /// to 120 (~2 seconds at 60 FPS).
    /// </summary>
    public int WindowSize
    {
        get => _windowSize;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "WindowSize must be positive.");
            _windowSize = value;
            _window = new FrameProfile[value];
            _next = 0;
            _count = 0;
        }
    }

    /// <summary>Rolling stats for the full Update+Draw frame time.</summary>
    public PerformanceStat FrameTotalMs => ComputeStat(static p => p.FrameTotalMs);

    /// <summary>Rolling stats for <see cref="FlatRedBallService.Update"/>.</summary>
    public PerformanceStat UpdateTotalMs => ComputeStat(static p => p.UpdateTotalMs);

    /// <summary>Rolling stats for <see cref="FlatRedBallService.Draw"/>.</summary>
    public PerformanceStat DrawTotalMs => ComputeStat(static p => p.DrawTotalMs);

    /// <summary>Rolling stats for every registered collision relationship's <c>RunCollisions</c> call.</summary>
    public PerformanceStat CollisionMs => ComputeStat(static p => p.CollisionMs);

    /// <summary>Rolling stats for the Gum service tree walk.</summary>
    public PerformanceStat GumUpdateMs => ComputeStat(static p => p.GumUpdateMs);

    /// <summary>
    /// Rolling frames-per-second, derived per-sample as <c>1000 / FrameTotalMs</c> (0 for a
    /// frame that measured 0ms, to avoid a divide-by-zero spike skewing the average).
    /// </summary>
    public PerformanceStat Fps => ComputeStat(static p => p.FrameTotalMs > 0 ? 1000.0 / p.FrameTotalMs : 0);

    /// <summary>
    /// Records one committed frame into the rolling window. Called by the engine at the single
    /// point a frame's <see cref="FrameProfile"/> is fully consistent — never call this yourself.
    /// No-op (no allocation) when <see cref="IsEnabled"/> is <c>false</c>.
    /// </summary>
    internal void Record(FrameProfile profile, IReadOnlyList<ICollisionRelationship> relationships)
    {
        if (!IsEnabled) return;

        _current = profile;
        _relationships = relationships;
        _window[_next] = profile;
        _next = (_next + 1) % _window.Length;
        if (_count < _window.Length) _count++;
    }

    /// <summary>
    /// Every active collision relationship as of the last recorded frame, ordered most-expensive
    /// first by <see cref="CollisionRelationshipReport.DeepCollisionCount"/> — mirrors FRB1's
    /// collision debugger. Empty until the first frame is recorded (requires <see cref="IsEnabled"/>).
    /// </summary>
    public IReadOnlyList<CollisionRelationshipReport> GetCollisionReport()
    {
        var report = new List<CollisionRelationshipReport>(_relationships.Count);
        foreach (var r in _relationships)
        {
            report.Add(new CollisionRelationshipReport
            {
                Name = r.DisplayName,
                DeepCollisionCount = r.DeepCollisionCount,
                IsPartitioned = r.IsPartitioned
            });
        }
        report.Sort((a, b) => b.DeepCollisionCount.CompareTo(a.DeepCollisionCount));
        return report;
    }

    /// <summary>
    /// Builds a human-readable performance summary — FPS, per-phase timing, and the collision
    /// relationship breakdown from <see cref="GetCollisionReport"/> — for the caller to log,
    /// print, or route into a diagnostics overlay. Pure string building: reads only the
    /// already-recorded window, does no I/O, and is safe to call from anywhere (e.g. every N
    /// frames from <see cref="Screen.CustomActivity"/>) without corrupting future measurements —
    /// the engine never does I/O on your behalf.
    /// </summary>
    public string GenerateReport()
    {
        var sb = new StringBuilder();
        var fps = Fps;
        sb.AppendLine(FormattableString.Invariant(
            $"FPS: {fps.Current:F1} (avg {fps.Average:F1}, min {fps.Min:F1}, max {fps.Max:F1})"));
        sb.AppendLine();
        sb.AppendLine("Phase             Current(ms)  Avg(ms)");
        AppendPhase(sb, "FrameTotal", FrameTotalMs);
        AppendPhase(sb, "UpdateTotal", UpdateTotalMs);
        AppendPhase(sb, "DrawTotal", DrawTotalMs);
        AppendPhase(sb, "Collision", CollisionMs);
        AppendPhase(sb, "GumUpdate", GumUpdateMs);

        var collisionReport = GetCollisionReport();
        if (collisionReport.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Collision relationships (most expensive first):");
            foreach (var r in collisionReport)
            {
                var partitionNote = r.IsPartitioned ? "partitioned" : "NOT PARTITIONED";
                sb.AppendLine(FormattableString.Invariant(
                    $"  {r.Name}: {r.DeepCollisionCount} deep checks [{partitionNote}]"));
            }
        }

        return sb.ToString();
    }

    private static void AppendPhase(StringBuilder sb, string name, PerformanceStat stat)
    {
        sb.AppendLine(FormattableString.Invariant(
            $"{name,-16}  {stat.Current,9:F2}  {stat.Average,7:F2}"));
    }

    private PerformanceStat ComputeStat(Func<FrameProfile, double> selector)
    {
        if (_count == 0) return default;

        double min = double.MaxValue, max = double.MinValue, sum = 0;
        for (int i = 0; i < _count; i++)
        {
            double v = selector(_window[i]);
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        return new PerformanceStat
        {
            Current = selector(_current),
            Min = min,
            Average = sum / _count,
            Max = max
        };
    }
}
