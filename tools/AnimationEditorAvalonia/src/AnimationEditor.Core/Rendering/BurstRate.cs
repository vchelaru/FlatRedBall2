namespace AnimationEditor.Core.Rendering;

/// <summary>
/// How fast events arrive while they are arriving: the rate over the latest burst of events, ignoring idle time. A rate
/// averaged over whole seconds mixes a short scroll with the quiet around it and reads far lower than the scroll was;
/// this reads the cadence of the scroll itself. A burst ends when the gap to the next event exceeds
/// <see cref="BurstGapSeconds"/>; until the next burst has <see cref="MinimumEvents"/> events the previous burst's rate stays.
/// Not thread-safe: one writer.
/// </summary>
public sealed class BurstRate
{
    public const double BurstGapSeconds = 0.25;
    public const int MinimumEvents = 3;
    private const int Window = 30;

    private readonly double[] _times = new double[Window];
    private int _count;
    private int _next;
    private double _last = double.NegativeInfinity;

    /// <summary>Events per second over the latest burst; 0 until a burst of <see cref="MinimumEvents"/> has been seen.</summary>
    public double Hertz { get; private set; }

    /// <summary>The longest gap between two events in the latest burst, in milliseconds.</summary>
    public double WorstGapMilliseconds { get; private set; }

    /// <summary>Records an event at <paramref name="seconds"/> (any monotonic clock, in seconds).</summary>
    public void Add(double seconds)
    {
        if (seconds - _last > BurstGapSeconds)
        {
            _count = 0;
            _next = 0;
        }
        _last = seconds;
        _times[_next] = seconds;
        _next = (_next + 1) % Window;
        if (_count < Window) _count++;
        if (_count < MinimumEvents) return;

        int oldest = _count < Window ? 0 : _next;
        double span = seconds - _times[oldest];
        if (span <= 0) return;
        Hertz = (_count - 1) / span;
        double worst = 0;
        for (int i = 1; i < _count; i++)
        {
            double gap = _times[(oldest + i) % Window] - _times[(oldest + i - 1) % Window];
            if (gap > worst) worst = gap;
        }
        WorstGapMilliseconds = worst * 1000;
    }
}
