using AnimationEditor.Core.Rendering;
using Avalonia.Threading;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared smooth wheel-zoom driver (#425 / #451 / #802): owns the target, viewport-space pivot,
/// animating flag, and 60 fps <see cref="DispatcherTimer"/>. Both <see cref="TextureViewport"/>
/// and <see cref="PreviewControl"/> delegate to one of these so the settle-tick float-drift snap
/// and the Begin/Step/Settle/Cancel state machine exist once.
/// <para>
/// Lives in Views (not Core) because it drives an Avalonia timer. The easing math is the pure
/// <see cref="ZoomChase"/> in Core; the host's pivot-preserving apply is injected by the control
/// (<c>ZoomToward</c> / <c>ApplyZoomTowardPivot</c>).
/// </para>
/// </summary>
internal sealed class ZoomAnimator
{
    public const float IntervalSeconds = 1f / 60f;

    // Non-preset fallback multiplier for one wheel notch. Written once so zoom-in/out round-trip
    // (×1.25 then ×0.8) and the two former spellings (`1f/1.25f` vs `0.8f`) can't drift apart.
    private const float NonPresetFactor = 1.25f;

    private readonly Func<float> _getZoom;
    private readonly Action<float, float, float> _applyFactor;
    private readonly Action<float> _snapZoom;
    private readonly Func<int[]?> _getPresets;

    private DispatcherTimer? _timer;
    private bool _animating;
    private float _target;
    private float _pivotVpX;
    private float _pivotVpY;

    /// <param name="getZoom">Current zoom factor (1.0 = 100 %).</param>
    /// <param name="applyFactor">
    /// Applies a relative zoom factor around a viewport-space pivot
    /// (<c>pivotX, pivotY, factor</c>) — the control's pivot-preserving zoom path.
    /// </param>
    /// <param name="snapZoom">
    /// Writes the zoom scalar exactly (no pan recalc). Used on the settling tick to clear the
    /// float drift that <c>zoom * (next / zoom)</c> accumulates (#451).
    /// </param>
    /// <param name="getPresets">Live <see cref="IZoomTarget.WheelZoomPresets"/> (may be null).</param>
    public ZoomAnimator(
        Func<float> getZoom,
        Action<float, float, float> applyFactor,
        Action<float> snapZoom,
        Func<int[]?> getPresets)
    {
        _getZoom = getZoom;
        _applyFactor = applyFactor;
        _snapZoom = snapZoom;
        _getPresets = getPresets;
    }

    public bool IsAnimating => _animating;
    public float Target => _target;

    /// <summary>
    /// Retargets toward the next/previous preset from the given viewport-space pivot. A notch
    /// while already animating steps from the in-flight target so rapid spins accumulate.
    /// Does NOT start the timer — the live wheel handler calls <see cref="StartTimer"/>; tests
    /// drive <see cref="Step"/> directly.
    /// </summary>
    public void Begin(float pivotVpX, float pivotVpY, bool zoomIn)
    {
        float basis = _animating ? _target : _getZoom();
        _target = ComputeTargetZoom(basis, zoomIn);
        _pivotVpX = pivotVpX;
        _pivotVpY = pivotVpY;
        _animating = true;
    }

    /// <summary>
    /// Advances by <paramref name="dtSeconds"/>. Returns <c>true</c> while still animating.
    /// Clears the animating flag <em>before</em> applying the settling tick so hosts that gate
    /// persistence on <see cref="IsAnimating"/> write the companion file once on settle.
    /// </summary>
    public bool Step(float dtSeconds)
    {
        if (!_animating) return false;

        float zoom = _getZoom();
        float next = ZoomChase.Step(zoom, _target, dtSeconds);
        bool settling = ZoomChase.IsSettled(next, _target);

        if (settling)
        {
            _animating = false;
            StopTimer();
        }

        _applyFactor(_pivotVpX, _pivotVpY, next / zoom);

        // Snap the scalar exactly onto the target — factor multiplications drift (e.g. 1.5000001).
        if (settling) _snapZoom(_target);
        return !settling;
    }

    public void Settle()
    {
        for (int i = 0; _animating && i < 1000; i++)
            Step(IntervalSeconds);
    }

    public void Cancel()
    {
        if (!_animating) return;
        _animating = false;
        StopTimer();
    }

    public void StartTimer()
    {
        _timer ??= CreateTimer();
        _timer.Start();
    }

    public void StopTimer() => _timer?.Stop();

    private float ComputeTargetZoom(float basisZoom, bool zoomIn)
    {
        float targetPct = _getPresets() is { Length: > 0 } presets
            ? ZoomPresetStepper.StepToNextPreset(basisZoom * 100f, presets, zoomIn ? +1 : -1)
            : basisZoom * 100f * (zoomIn ? NonPresetFactor : 1f / NonPresetFactor);
        return Math.Clamp(targetPct / 100f, CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(IntervalSeconds) };
        timer.Tick += (_, _) => Step(IntervalSeconds);
        return timer;
    }
}
