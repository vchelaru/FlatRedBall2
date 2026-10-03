using System.Numerics;

namespace AnimationEditor.Core.Input;

/// <summary>Where a wheel event came from, which decides whether it zooms or pans and how far.</summary>
public enum WheelSource
{
    /// <summary>A mouse wheel on Windows or Linux, reporting 1 per notch.</summary>
    Wheel,
    /// <summary>A mouse wheel on macOS: one event per click, delta scaled by acceleration.</summary>
    MacMouseWheel,
    /// <summary>A gesture on a precise-delta device on macOS (trackpad, Magic Mouse).</summary>
    MacTrackpad,
    /// <summary>A scroll or pinch on a Windows precision touchpad, reporting 1 per notch.</summary>
    WindowsTouchpad,
    /// <summary>A two-finger scroll on a Linux touchpad, reporting fractional steps.</summary>
    LinuxTouchpad,
}

/// <summary>The modifier keys that change how a wheel event is read.</summary>
[Flags]
public enum WheelModifiers
{
    None = 0,
    Control = 1,
    /// <summary>Cmd on macOS.</summary>
    Meta = 2,
}

/// <summary>
/// A classified wheel event: its <see cref="Source"/>, plus, for a Windows touchpad, the pan in
/// device-independent pixels read from the fingers' own movement (null when it isn't known).
/// </summary>
public readonly record struct WheelReading(WheelSource Source, Vector2? TouchpadPan = null);

/// <summary>
/// What a wheel event does to the canvas: pan by <see cref="PanX"/>/<see cref="PanY"/> device-independent
/// pixels (content moves that way) or zoom by <see cref="ZoomNotches"/> wheel notches.
/// </summary>
public readonly record struct WheelGesture(bool IsPan, float PanX, float PanY, float ZoomNotches);

/// <summary>
/// Decides whether a classified wheel event pans or zooms and by how much, so the wireframe, preview
/// and PNG viewer all read touchpads the same way. Constants and rules are Gum's
/// (<c>AvaloniaMouseMapping.ApplyWheelDelta</c>), converted from physical pixels and 120-per-notch
/// to device-independent pixels and 1-per-notch.
/// </summary>
public static class WheelGestureInterpreter
{
    // Avalonia's macOS backend divides a precise scroll's points by 50 (AvnView.mm).
    private const float PrecisePointsPerDelta = 50;

    // Browsers scroll 100 pixels per wheel notch; Avalonia reports a notch as 1.
    private const float WindowsTouchpadPixelsPerDelta = 100;

    // xf86-input-libinput's default scroll distance: one unit of touchpad delta is 15 pixels of finger travel.
    private const float LinuxTouchpadPixelsPerDelta = 15;

    /// <summary>Reads one wheel event whose vertical/horizontal notch delta is <paramref name="delta"/>.</summary>
    public static WheelGesture Interpret(WheelReading reading, Vector2 delta, WheelModifiers modifiers)
    {
        bool control = modifiers.HasFlag(WheelModifiers.Control);

        switch (reading.Source)
        {
            case WheelSource.MacTrackpad when !modifiers.HasFlag(WheelModifiers.Meta):
                return Pan(delta * PrecisePointsPerDelta);

            // Ctrl+scroll zooms, and so does a pinch, which Windows reports as Ctrl+wheel.
            case WheelSource.WindowsTouchpad when !control:
                return Pan(reading.TouchpadPan ?? delta * WindowsTouchpadPixelsPerDelta);

            case WheelSource.LinuxTouchpad when !control:
                return Pan(delta * LinuxTouchpadPixelsPerDelta);

            case WheelSource.MacMouseWheel:
                // macOS sends one event per click but scales its delta by scroll acceleration (about
                // 0.02 for a slow click), so only the direction counts (Gum#5010).
                return Zoom(Math.Sign(delta.Y));

            default:
                return Zoom(delta.Y);
        }
    }

    private static WheelGesture Pan(Vector2 pan) => new(true, pan.X, pan.Y, 0);

    private static WheelGesture Zoom(float notches) => new(false, 0, 0, notches);
}

/// <summary>Converts a macOS trackpad pinch into the same notch unit a wheel zoom uses.</summary>
public static class PinchZoom
{
    // One zoom step per 15% of pinch, about the ratio between neighboring zoom levels.
    private const double PinchPerZoomStep = 0.15;

    /// <summary>Magnification (0.15 = spread 15%) to wheel notches.</summary>
    public static double ToNotches(double magnification) => magnification / PinchPerZoomStep;
}
