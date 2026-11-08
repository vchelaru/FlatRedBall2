using System.Diagnostics;
using Avalonia.Threading;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dispatcher heartbeat: a 10 ms <see cref="DispatcherTimer"/> that records the largest gap between
/// ticks. Headless timers tick only while the test awaits, so a stall shows as one long gap: whatever
/// ran synchronously on the UI thread between two ticks. Gaps are tagged with the current
/// <see cref="Phase"/> so a report says which step froze the window.
/// </summary>
internal sealed class UiStallMeter : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTickMs;
    private readonly Dictionary<string, double> _maxGapByPhase = new();

    public UiStallMeter()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal, OnTick);
        _lastTickMs = _clock.Elapsed.TotalMilliseconds;
        _timer.Start();
    }

    /// <summary>The label the next gaps are filed under.</summary>
    public string Phase { get; set; } = "start";

    /// <summary>The largest gap seen in every phase, in milliseconds.</summary>
    public IReadOnlyDictionary<string, double> MaxGapByPhase => _maxGapByPhase;

    public double MaxGapMs => _maxGapByPhase.Count == 0 ? 0 : _maxGapByPhase.Values.Max();

    /// <summary>Forgets the gap that built up before now, so a phase starts clean.</summary>
    public void Mark(string phase)
    {
        Phase = phase;
        _lastTickMs = _clock.Elapsed.TotalMilliseconds;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        double gap = now - _lastTickMs;
        _lastTickMs = now;
        _maxGapByPhase[Phase] = _maxGapByPhase.TryGetValue(Phase, out double previous) ? Math.Max(previous, gap) : gap;
    }

    /// <summary>
    /// Ends the open stretch (the time since the last tick) in the current phase. Call right after a
    /// blocking step so its stall is recorded under the right label before the next phase starts.
    /// </summary>
    public void Flush() => OnTick(null, EventArgs.Empty);

    public void Dispose() => _timer.Stop();
}
