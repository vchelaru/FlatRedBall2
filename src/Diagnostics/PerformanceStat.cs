namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Current-frame value plus rolling min/average/max for one timing signal over
/// <see cref="PerformanceMonitor.WindowSize"/> frames. All fields are zero when no frame has
/// been recorded yet (i.e. <see cref="PerformanceMonitor.IsEnabled"/> was never turned on).
/// </summary>
public readonly struct PerformanceStat
{
    /// <summary>Value from the most recently recorded frame.</summary>
    public double Current { get; init; }

    /// <summary>Smallest value across the current rolling window.</summary>
    public double Min { get; init; }

    /// <summary>Average value across the current rolling window.</summary>
    public double Average { get; init; }

    /// <summary>Largest value across the current rolling window.</summary>
    public double Max { get; init; }
}
