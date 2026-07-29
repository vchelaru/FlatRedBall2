using AnimationEditor.Core.Rendering;
using Avalonia.Threading;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared one-shot reveal timer host (#542 / #606 / #803): owns progress (0 = full bump, 1 =
/// settled), the <see cref="DispatcherTimer"/> at <see cref="RevealAnimation.DefaultIntervalSeconds"/>,
/// and Restart/Step/Settle/Stop. Both <see cref="WireframeControl"/> and
/// <see cref="PngPreviewControl"/> delegate to one of these; the curve itself remains
/// <see cref="RevealAnimation"/> in Core.
/// </summary>
internal sealed class RevealHost
{
    private readonly Action _onTick;
    private DispatcherTimer? _timer;
    private float _progress = 1f;

    public RevealHost(Action onTick) => _onTick = onTick;

    /// <summary>Reveal progress: 0 = full bump, 1 = settled.</summary>
    public float Progress => _progress;

    public bool IsAnimating => _progress < 1f;

    /// <summary>Resets progress to 0 and starts the timer.</summary>
    public void Restart()
    {
        _progress = 0f;
        _timer ??= CreateTimer();
        _timer.Start();
        _onTick();
    }

    /// <summary>
    /// Advances by <paramref name="dtSeconds"/>. Returns <c>true</c> while still animating.
    /// </summary>
    public bool Step(float dtSeconds)
    {
        if (_progress >= 1f) return false;

        _progress = RevealAnimation.StepProgress(_progress, dtSeconds);
        if (_progress >= 1f)
            _timer?.Stop();

        _onTick();
        return _progress < 1f;
    }

    public void Settle()
    {
        for (int i = 0; _progress < 1f && i < 1000; i++)
            Step(RevealAnimation.DefaultIntervalSeconds);
    }

    public void Stop() => _timer?.Stop();

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(RevealAnimation.DefaultIntervalSeconds)
        };
        timer.Tick += (_, _) => Step(RevealAnimation.DefaultIntervalSeconds);
        return timer;
    }
}
