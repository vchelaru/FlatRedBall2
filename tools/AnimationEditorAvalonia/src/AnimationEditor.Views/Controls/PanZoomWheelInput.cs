using AnimationEditor.Core.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Numerics;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared wheel and pinch handling for every pannable/zoomable canvas (#1237, #1238): a touchpad's
/// two-finger scroll pans, a pinch or Ctrl/Cmd+scroll zooms, and a mouse wheel zooms. Like
/// <see cref="ZoomAnimator"/>, <see cref="TextureViewport"/> and <see cref="PreviewControl"/> each own
/// one of these and supply the camera operations, since they share no base class.
/// <para>
/// Telling a touchpad from a mouse wheel is the per-OS part, supplied by an
/// <see cref="IWheelSourceDetector"/> the host sets; the default treats everything as a wheel.
/// </para>
/// </summary>
internal sealed class PanZoomWheelInput
{
    // A scroll pan raises PanChanged once the fingers stop, so the companion file isn't rewritten
    // on every one of a touchpad's 60+ events per second.
    private static readonly TimeSpan PanSettleDelay = TimeSpan.FromMilliseconds(250);

    private readonly Action<float, float> _panBy;
    private readonly Action<float, float, double> _zoomByNotches;
    private readonly Action _panSettled;
    private DispatcherTimer? _settleTimer;

    /// <param name="panBy">Moves the content by (dx, dy) device-independent pixels.</param>
    /// <param name="zoomByNotches">Zooms toward a control-space pivot by a (possibly fractional) number of wheel notches.</param>
    /// <param name="panSettled">Runs after the last pan event of a scroll gesture.</param>
    public PanZoomWheelInput(Action<float, float> panBy, Action<float, float, double> zoomByNotches, Action panSettled)
    {
        _panBy = panBy;
        _zoomByNotches = zoomByNotches;
        _panSettled = panSettled;
    }

    /// <summary>Classifies wheel events; set by the host from the running OS.</summary>
    public IWheelSourceDetector Detector { get; set; } = NullWheelSourceDetector.Instance;

    /// <summary>Routes one wheel event to <paramref name="panBy"/> or the zoom; call from <c>OnPointerWheelChanged</c>.</summary>
    public void HandleWheel(PointerWheelEventArgs e, Visual relativeTo)
    {
        var delta = new Vector2((float)e.Delta.X, (float)e.Delta.Y);
        WheelModifiers modifiers = ToWheelModifiers(e.KeyModifiers);
        WheelReading reading = Detector.Read(delta, modifiers.HasFlag(WheelModifiers.Control));
        WheelGesture gesture = WheelGestureInterpreter.Interpret(reading, delta, modifiers);

        if (gesture.IsPan)
        {
            _panBy(gesture.PanX, gesture.PanY);
            ScheduleSettle();
        }
        else
        {
            Point pivot = e.GetPosition(relativeTo);
            _zoomByNotches((float)pivot.X, (float)pivot.Y, gesture.ZoomNotches);
        }
        e.Handled = true;
    }

    /// <summary>Zooms for a macOS trackpad pinch; call from the magnify gesture handler.</summary>
    public void HandlePinch(PointerDeltaEventArgs e, Visual relativeTo)
    {
        Point pivot = e.GetPosition(relativeTo);
        _zoomByNotches((float)pivot.X, (float)pivot.Y, PinchZoom.ToNotches(e.Delta.X));
        e.Handled = true;
    }

    /// <summary>Hooks the pinch gesture, which Avalonia raises only on macOS (Windows sends Ctrl+wheel).</summary>
    public void AttachPinch(InputElement control, Visual relativeTo) =>
        control.AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent,
            (object? _, PointerDeltaEventArgs e) => HandlePinch(e, relativeTo));

    private void ScheduleSettle()
    {
        _settleTimer ??= CreateSettleTimer();
        _settleTimer.Stop();
        _settleTimer.Start();
    }

    private DispatcherTimer CreateSettleTimer()
    {
        var timer = new DispatcherTimer { Interval = PanSettleDelay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _panSettled();
        };
        return timer;
    }

    private static WheelModifiers ToWheelModifiers(KeyModifiers modifiers)
    {
        var result = WheelModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control)) result |= WheelModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Meta)) result |= WheelModifiers.Meta;
        return result;
    }
}
