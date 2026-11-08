using System;
using System.Diagnostics;
using System.Threading;

namespace AnimationEditor.Core.Logging;

/// <summary>
/// Explains a process that should be gone but is not. Armed when shutdown starts, it waits on a background thread and, if the
/// process is still alive, writes a snapshot of what is keeping it so (then again every <c>interval</c>, up to
/// <c>maxReports</c> times). It only reports: it never exits or kills anything.
/// </summary>
public sealed class LingerWatchdog : IDisposable
{
    private readonly TimeSpan _firstDelay;
    private readonly TimeSpan _interval;
    private readonly int _maxReports;
    private readonly Action<string> _sink;
    private readonly Func<string> _snapshot;
    private readonly ManualResetEventSlim _cancelled = new(false);
    private int _armed;

    public LingerWatchdog(TimeSpan firstDelay, TimeSpan interval, int maxReports, Action<string> sink, Func<string> snapshot)
    {
        _firstDelay = firstDelay;
        _interval = interval;
        _maxReports = maxReports;
        _sink = sink;
        _snapshot = snapshot;
    }

    /// <summary>Starts the countdown; later calls are ignored, so every shutdown signal can call it.</summary>
    public void Arm(string reason)
    {
        if (Interlocked.Exchange(ref _armed, 1) == 1)
            return;

        // Background, so the watchdog never keeps the process it is watching alive.
        new Thread(() => Run(reason)) { IsBackground = true, Name = "AE.LingerWatchdog" }.Start();
    }

    private void Run(string reason)
    {
        var clock = Stopwatch.StartNew();
        for (int report = 1; report <= _maxReports; report++)
        {
            if (_cancelled.Wait(report == 1 ? _firstDelay : _interval))
                return;

            string snapshot;
            try { snapshot = _snapshot(); }
            catch (Exception ex) { snapshot = $"(snapshot failed: {ex.GetType().Name}: {ex.Message})"; }

            try
            {
                _sink($"Process still alive {clock.Elapsed.TotalSeconds:0}s after: {reason} (report {report}/{_maxReports})" +
                      Environment.NewLine + snapshot);
            }
            catch
            {
                // Diagnostics only.
            }
        }
    }

    public void Dispose() => _cancelled.Set();
}
