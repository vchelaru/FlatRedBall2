using System.Numerics;

namespace AnimationEditor.Core.Input;

/// <summary>
/// Tells a touchpad scroll from a mouse wheel. Avalonia reports both as a pointer wheel event, so
/// each OS needs its own trick; the tricks that need P/Invoke live behind small interfaces in the
/// App head, and the decisions live here where tests reach them.
/// </summary>
public interface IWheelSourceDetector
{
    /// <summary>
    /// Classifies the wheel event being dispatched right now. <paramref name="delta"/> is its
    /// Avalonia delta; <paramref name="controlHeld"/> says Ctrl was down, which makes a Windows
    /// touchpad's event a pinch rather than a scroll.
    /// </summary>
    WheelReading Read(Vector2 delta, bool controlHeld);

    /// <summary>
    /// True while the OS is likely feeding the editor a touchpad pinch. Windows presses a real Ctrl key
    /// for a pinch, which the editor must not mistake for the user holding Ctrl; only Windows says yes.
    /// </summary>
    bool IsPinchInProgress => false;
}

/// <summary>Treats every event as a mouse wheel: the fallback on an OS with no touchpad detector, and the default for tests.</summary>
public sealed class NullWheelSourceDetector : IWheelSourceDetector
{
    public static readonly NullWheelSourceDetector Instance = new();

    public WheelReading Read(Vector2 delta, bool controlHeld) => new(WheelSource.Wheel);
}

/// <summary>Reads the macOS scroll event AppKit is dispatching (implemented over Objective-C in the App head).</summary>
public interface IMacScrollEventReader
{
    /// <summary>
    /// Whether the event reports precise deltas, and whether it belongs to a gesture (a non-zero
    /// <c>phase</c> or <c>momentumPhase</c>).
    /// </summary>
    void ReadCurrentEvent(out bool isPrecise, out bool hasGesturePhase);
}

/// <summary>
/// macOS: only a trackpad or Magic Mouse gesture carries a phase; remote-desktop tools inject a mouse
/// wheel's clicks as precise scrolls with none, so those zoom.
/// </summary>
public sealed class MacWheelSourceDetector(IMacScrollEventReader reader) : IWheelSourceDetector
{
    public WheelReading Read(Vector2 delta, bool controlHeld)
    {
        reader.ReadCurrentEvent(out bool isPrecise, out bool hasGesturePhase);
        return new WheelReading(isPrecise && hasGesturePhase ? WheelSource.MacTrackpad : WheelSource.MacMouseWheel);
    }
}

/// <summary>
/// What the Windows raw-input listener learned from the precision touchpad's HID reports: when the
/// last one arrived, and the fingers' travel. Fed by the App head, read by
/// <see cref="WindowsWheelSourceDetector"/>.
/// </summary>
public sealed class WindowsTouchpadState
{
    /// <summary>Clock reading when the last touchpad report arrived, or null if none has.</summary>
    public long? LastReportMs { get; private set; }

    /// <summary>The two-finger travel read from the touchpad's reports.</summary>
    public TouchpadPanTracker PanTracker { get; } = new();

    /// <summary>Notes that a touchpad report arrived at <paramref name="nowMs"/>.</summary>
    public void OnReport(long nowMs) => LastReportMs = nowMs;

    /// <summary>Records a complete frame of the fingers on the pad.</summary>
    public void OnFrame(IReadOnlyList<TouchpadContact> contacts) => PanTracker.OnFrame(contacts);
}

/// <summary>
/// Windows: a wheel event is from a precision touchpad if the touchpad sent a contact report within
/// the last <see cref="ReportWindowMs"/> (it reports at 100+ Hz while touched). Its pan then comes from
/// the fingers' movement, because the wheel messages lock a diagonal swipe to one axis at first.
/// </summary>
public sealed class WindowsWheelSourceDetector(Func<long> clockMs, WindowsTouchpadState state) : IWheelSourceDetector
{
    /// <summary>A gap this long means no fingers are on the pad.</summary>
    public const long ReportWindowMs = 100;

    /// <summary>Two fingers on a pad that is still reporting: a pinch, or the start of a two-finger scroll.</summary>
    public bool IsPinchInProgress => IsTouchpadReporting() && state.PanTracker.HasTwoFingers;

    private bool IsTouchpadReporting() => state.LastReportMs is { } last && clockMs() - last <= ReportWindowMs;

    public WheelReading Read(Vector2 delta, bool controlHeld)
    {
        if (!IsTouchpadReporting())
        {
            return new WheelReading(WheelSource.Wheel);
        }

        if (controlHeld)
        {
            // A pinch: drop its finger travel so it doesn't pan afterward.
            state.PanTracker.Discard();
            return new WheelReading(WheelSource.WindowsTouchpad);
        }

        return new WheelReading(WheelSource.WindowsTouchpad, state.PanTracker.TakePan(delta));
    }
}

/// <summary>
/// Linux: Avalonia's X11 backend doesn't say which device sent a wheel event, so it's guessed from the
/// delta: a notched wheel scrolls whole steps, a touchpad fractions. A whole step within
/// <see cref="GestureGapMs"/> of the last touchpad event is still the touchpad.
/// </summary>
public sealed class LinuxWheelSourceDetector(Func<long> clockMs) : IWheelSourceDetector
{
    public const long GestureGapMs = 200;

    private long? _lastTouchpadMs;

    public WheelReading Read(Vector2 delta, bool controlHeld)
    {
        long now = clockMs();
        bool touchpad = !IsWhole(delta.X) || !IsWhole(delta.Y)
            || (_lastTouchpadMs is { } last && now - last <= GestureGapMs);
        if (!touchpad)
        {
            return new WheelReading(WheelSource.Wheel);
        }

        _lastTouchpadMs = now;
        return new WheelReading(WheelSource.LinuxTouchpad);
    }

    private static bool IsWhole(float value) => Math.Abs(value - Math.Round(value)) < 1e-6;
}
