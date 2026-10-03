using System.Numerics;
using AnimationEditor.Core.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// A touchpad's two-finger scroll pans the canvas on macOS, Windows and Linux; Cmd+scroll on macOS,
/// Ctrl+scroll or pinch elsewhere, and a mouse wheel keep zooming (#1237, #1238). Expectations are
/// ported from Gum's <c>AvaloniaWheelMappingTests</c> (Gum#4988, #5490, #5495), with zoom expressed in
/// wheel notches (1.0 per notch, as Avalonia reports) instead of WPF's 120 per notch and pans in
/// device-independent pixels instead of physical ones.
/// </summary>
public class WheelGestureTests
{
    private static WheelGesture Interpret(WheelSource source, float x, float y, WheelModifiers modifiers = WheelModifiers.None,
        Vector2? touchpadPan = null) =>
        WheelGestureInterpreter.Interpret(new WheelReading(source, touchpadPan), new Vector2(x, y), modifiers);

    [Fact]
    public void MacTrackpadScroll_PansByThePointsTheFingersMoved()
    {
        // Avalonia divides a precise scroll's points by 50.
        WheelGesture gesture = Interpret(WheelSource.MacTrackpad, 0.2f, -0.1f);

        gesture.IsPan.ShouldBeTrue();
        gesture.PanX.ShouldBe(10f, 0.001f);
        gesture.PanY.ShouldBe(-5f, 0.001f);
        gesture.ZoomNotches.ShouldBe(0f);
    }

    [Fact]
    public void MacTrackpadScrollWithCmd_Zooms()
    {
        WheelGesture gesture = Interpret(WheelSource.MacTrackpad, 0, 0.5f, WheelModifiers.Meta);

        gesture.IsPan.ShouldBeFalse();
        gesture.ZoomNotches.ShouldBe(0.5f, 0.001f);
    }

    [Fact]
    public void MouseWheel_ZoomsOneNotchPerNotch()
    {
        WheelGesture gesture = Interpret(WheelSource.Wheel, 0, -1);

        gesture.IsPan.ShouldBeFalse();
        gesture.ZoomNotches.ShouldBe(-1f, 0.001f);
    }

    [Fact]
    public void MacMouseWheel_ZoomsOneNotchPerEventWhateverTheAcceleratedDelta()
    {
        // macOS reports one event per wheel click, scaled by scroll acceleration: about 0.02 for a
        // slow click and 3+ for a fast one (Gum#5010).
        Interpret(WheelSource.MacMouseWheel, 0, 0.02f).ZoomNotches.ShouldBe(1f);
        Interpret(WheelSource.MacMouseWheel, 0, -3.4f).ZoomNotches.ShouldBe(-1f);
    }

    [Fact]
    public void WindowsTouchpadScroll_PansOneHundredPixelsPerNotchOfDelta()
    {
        WheelGesture gesture = Interpret(WheelSource.WindowsTouchpad, 0.5f, -0.25f);

        gesture.IsPan.ShouldBeTrue();
        gesture.PanX.ShouldBe(50f, 0.001f);
        gesture.PanY.ShouldBe(-25f, 0.001f);
        gesture.ZoomNotches.ShouldBe(0f);
    }

    [Fact]
    public void WindowsTouchpadScrollWithFingerTravel_PansByTheTravelNotTheWheelDelta()
    {
        WheelGesture gesture = Interpret(WheelSource.WindowsTouchpad, 0, 0.5f, touchpadPan: new Vector2(12, 30));

        gesture.IsPan.ShouldBeTrue();
        gesture.PanX.ShouldBe(12f, 0.001f);
        gesture.PanY.ShouldBe(30f, 0.001f);
    }

    [Fact]
    public void WindowsTouchpadScrollWithCtrl_Zooms()
    {
        // Windows also reports a precision-touchpad pinch as Ctrl+wheel.
        WheelGesture gesture = Interpret(WheelSource.WindowsTouchpad, 0, 0.25f, WheelModifiers.Control);

        gesture.IsPan.ShouldBeFalse();
        gesture.ZoomNotches.ShouldBe(0.25f, 0.001f);
    }

    [Fact]
    public void LinuxTouchpadScroll_PansFifteenPixelsPerUnitOfDelta()
    {
        WheelGesture gesture = Interpret(WheelSource.LinuxTouchpad, 0.5f, -2);

        gesture.IsPan.ShouldBeTrue();
        gesture.PanX.ShouldBe(7.5f, 0.001f);
        gesture.PanY.ShouldBe(-30f, 0.001f);
        gesture.ZoomNotches.ShouldBe(0f);
    }

    [Fact]
    public void LinuxTouchpadScrollWithCtrl_Zooms()
    {
        WheelGesture gesture = Interpret(WheelSource.LinuxTouchpad, 0, 0.25f, WheelModifiers.Control);

        gesture.IsPan.ShouldBeFalse();
        gesture.ZoomNotches.ShouldBe(0.25f, 0.001f);
    }

    [Fact]
    public void Pinch_ZoomsOneNotchPerFifteenPercentOfMagnification()
    {
        PinchZoom.ToNotches(0.15).ShouldBe(1.0, 0.001);
        PinchZoom.ToNotches(-0.15).ShouldBe(-1.0, 0.001);
    }

    [Theory]
    [InlineData(true, true, WheelSource.MacTrackpad)]
    [InlineData(true, false, WheelSource.MacMouseWheel)]
    [InlineData(false, false, WheelSource.MacMouseWheel)]
    public void MacDetector_PanOnlyForPreciseEventsWithAGesturePhase(bool isPrecise, bool hasGesturePhase, WheelSource expected)
    {
        // Remote desktop injects precise pixel scrolls with no gesture phase for mouse-wheel clicks.
        var detector = new MacWheelSourceDetector(new FakeMacReader(isPrecise, hasGesturePhase));

        detector.Read(new Vector2(0, 0.1f), controlHeld: false).Source.ShouldBe(expected);
    }

    private sealed class FakeMacReader(bool isPrecise, bool hasGesturePhase) : IMacScrollEventReader
    {
        public void ReadCurrentEvent(out bool precise, out bool gesturePhase)
        {
            precise = isPrecise;
            gesturePhase = hasGesturePhase;
        }
    }

    [Theory]
    [InlineData(1000L, 950L, WheelSource.WindowsTouchpad)]
    [InlineData(1000L, 1000L, WheelSource.WindowsTouchpad)]
    [InlineData(1000L, 800L, WheelSource.Wheel)]
    [InlineData(1000L, null, WheelSource.Wheel)]
    public void WindowsDetector_TouchpadOnlyWhileItIsReportingContacts(long nowMs, long? lastTouchpadReportMs, WheelSource expected)
    {
        var state = new WindowsTouchpadState();
        if (lastTouchpadReportMs is { } last)
        {
            state.OnReport(last);
        }
        var detector = new WindowsWheelSourceDetector(() => nowMs, state);

        detector.Read(new Vector2(0, 1), controlHeld: false).Source.ShouldBe(expected);
    }

    [Fact]
    public void WindowsDetector_TouchpadScroll_CarriesTheFingerTravelAndConsumesIt()
    {
        var state = new WindowsTouchpadState();
        state.OnReport(1000);
        state.OnFrame([new TouchpadContact(1, 10, 10), new TouchpadContact(2, 30, 10)]);
        state.OnFrame([new TouchpadContact(1, 11, 12), new TouchpadContact(2, 31, 12)]);
        var detector = new WindowsWheelSourceDetector(() => 1010, state);

        WheelReading reading = detector.Read(new Vector2(0, 0.1f), controlHeld: false);

        reading.Source.ShouldBe(WheelSource.WindowsTouchpad);
        reading.TouchpadPan.ShouldNotBeNull();
        reading.TouchpadPan.Value.Y.ShouldBe(2 * TouchpadPanTracker.PixelsPerMillimeter, 0.001f);
        detector.Read(new Vector2(0, 0.1f), controlHeld: false).TouchpadPan.ShouldBe(Vector2.Zero);
    }

    [Fact]
    public void WindowsDetector_PinchWithCtrl_DiscardsFingerTravelSoItDoesNotPanAfterward()
    {
        var state = new WindowsTouchpadState();
        state.OnReport(1000);
        state.OnFrame([new TouchpadContact(1, 10, 10), new TouchpadContact(2, 30, 10)]);
        state.OnFrame([new TouchpadContact(1, 5, 10), new TouchpadContact(2, 35, 12)]);
        var detector = new WindowsWheelSourceDetector(() => 1010, state);

        detector.Read(new Vector2(0, 0.1f), controlHeld: true).TouchpadPan.ShouldBeNull();

        detector.Read(new Vector2(0, 0.1f), controlHeld: false).TouchpadPan.ShouldBe(Vector2.Zero);
    }

    [Theory]
    [InlineData(0.0f, -1.0f, WheelSource.Wheel)]
    [InlineData(0.0f, 3.0f, WheelSource.Wheel)]
    [InlineData(0.0f, 0.4f, WheelSource.LinuxTouchpad)]
    [InlineData(1.6f, 0.0f, WheelSource.LinuxTouchpad)]
    public void LinuxDetector_FractionalDeltaIsATouchpad(float x, float y, WheelSource expected)
    {
        var detector = new LinuxWheelSourceDetector(() => 0);

        detector.Read(new Vector2(x, y), controlHeld: false).Source.ShouldBe(expected);
    }

    [Theory]
    [InlineData(100L, WheelSource.LinuxTouchpad)]
    [InlineData(300L, WheelSource.Wheel)]
    public void LinuxDetector_AWholeStepMidGestureStillBelongsToTheTouchpad(long msAfterFraction, WheelSource expected)
    {
        long now = 1000;
        var detector = new LinuxWheelSourceDetector(() => now);
        detector.Read(new Vector2(0, 0.4f), controlHeld: false).Source.ShouldBe(WheelSource.LinuxTouchpad);

        now += msAfterFraction;

        detector.Read(new Vector2(0, 1), controlHeld: false).Source.ShouldBe(expected);
    }

    [Fact]
    public void NullDetector_EveryEventIsAMouseWheel()
    {
        NullWheelSourceDetector.Instance.Read(new Vector2(0, 0.3f), controlHeld: false).Source.ShouldBe(WheelSource.Wheel);
    }
}
