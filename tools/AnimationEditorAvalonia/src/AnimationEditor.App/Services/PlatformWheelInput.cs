using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using AnimationEditor.Core.Input;
using Avalonia.Controls;

namespace AnimationEditor.App.Services;

/// <summary>
/// The running OS's way of telling a touchpad scroll from a mouse wheel (#1237, #1238): macOS reads the
/// AppKit scroll event, Windows listens for the precision touchpad's raw HID reports, Linux guesses
/// from the delta. The decisions are the Core detectors; this just picks and wires the right one, so
/// the P/Invoke stays out of the shared controls. One instance serves every window.
/// <para>
/// Set <c>AE_LOG_SCROLL=1</c> to append each wheel event's delta and classification to
/// <c>animation-editor-scroll.log</c> in the temp folder, for tuning on machines the developers
/// don't have.
/// </para>
/// </summary>
internal sealed class PlatformWheelInput
{
    private readonly Action<TopLevel>? _attach;

    private PlatformWheelInput(IWheelSourceDetector detector, Action<TopLevel>? attach)
    {
        Detector = ScrollLog.IsEnabled ? new LoggingDetector(detector) : detector;
        _attach = attach;
    }

    /// <summary>Classifies wheel events for this OS.</summary>
    public IWheelSourceDetector Detector { get; }

    /// <summary>
    /// Lets the detector hook the window if it needs to (only Windows does, to receive raw touchpad
    /// input). Safe to call repeatedly; call it when a window activates.
    /// </summary>
    public void Attach(TopLevel topLevel) => _attach?.Invoke(topLevel);

    /// <summary>Builds the detector for the OS this process runs on.</summary>
    public static PlatformWheelInput CreateForHost()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new PlatformWheelInput(new MacWheelSourceDetector(new MacScrollEvent()), null);
        }

        if (OperatingSystem.IsWindows())
        {
            var state = new WindowsTouchpadState();
            var listener = new WindowsTouchpadListener(state, () => Environment.TickCount64);
            return new PlatformWheelInput(
                new WindowsWheelSourceDetector(() => Environment.TickCount64, state),
                listener.EnsureListening);
        }

        if (OperatingSystem.IsLinux())
        {
            return new PlatformWheelInput(new LinuxWheelSourceDetector(() => Environment.TickCount64), null);
        }

        return new PlatformWheelInput(NullWheelSourceDetector.Instance, null);
    }

    private sealed class LoggingDetector(IWheelSourceDetector inner) : IWheelSourceDetector
    {
        public WheelReading Read(Vector2 delta, bool controlHeld)
        {
            WheelReading reading = inner.Read(delta, controlHeld);
            ScrollLog.Write(delta, controlHeld, reading);
            return reading;
        }
    }
}

/// <summary>Appends wheel events to a temp-folder log when <see cref="EnvironmentVariable"/> is <c>1</c>.</summary>
internal static class ScrollLog
{
    public const string EnvironmentVariable = "AE_LOG_SCROLL";

    private static readonly string? Path =
        Environment.GetEnvironmentVariable(EnvironmentVariable) == "1"
            ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "animation-editor-scroll.log")
            : null;

    public static bool IsEnabled => Path != null;

    public static void Write(Vector2 delta, bool controlHeld, WheelReading reading)
    {
        if (Path == null)
        {
            return;
        }

        string pan = reading.TouchpadPan is { } p ? $"{p.X:R},{p.Y:R}" : "-";
        string line = string.Create(CultureInfo.InvariantCulture,
            $"{Environment.TickCount64}\t{delta.X:R}\t{delta.Y:R}\tctrl={controlHeld}\t{reading.Source}\tpan={pan}{Environment.NewLine}");
        try
        {
            File.AppendAllText(Path, line);
        }
        catch (IOException)
        {
        }
    }
}
