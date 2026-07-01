namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Fixed-window rolling average over a stream of values, backed by a ring buffer (no per-sample
/// allocation). Feeds the preview draw-time diagnostics overlay (#514): the newest N frame
/// durations are averaged so the ms/frame readout is smooth instead of jittering every frame.
/// Not thread-safe — the caller confines <see cref="Add"/>/<see cref="Average"/> to one thread.
/// </summary>
public sealed class RollingAverage
{
    private readonly double[] _samples;
    private int _count;   // number of valid samples (≤ window), so a partly-filled buffer averages correctly
    private int _next;    // next write index (wraps)

    /// <param name="window">Number of most-recent samples to average over; must be positive.</param>
    public RollingAverage(int window)
    {
        if (window <= 0) throw new System.ArgumentOutOfRangeException(nameof(window));
        _samples = new double[window];
    }

    /// <summary>Records a sample, evicting the oldest once the window is full.</summary>
    public void Add(double value)
    {
        _samples[_next] = value;
        _next = (_next + 1) % _samples.Length;
        if (_count < _samples.Length) _count++;
    }

    /// <summary>Mean of the samples currently in the window; <c>0</c> when none have been added.</summary>
    public double Average
    {
        get
        {
            if (_count == 0) return 0d;
            double sum = 0d;
            for (int i = 0; i < _count; i++) sum += _samples[i];
            return sum / _count;
        }
    }
}
