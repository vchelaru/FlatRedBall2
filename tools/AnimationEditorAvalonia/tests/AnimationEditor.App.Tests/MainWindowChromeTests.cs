using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace AnimationEditor.App.Tests;

public class MainWindowChromeTests
{
    // ── Non-macOS: custom chrome ──────────────────────────────────────────────

    [AvaloniaFact]
    public void OnNonMacOS_WindowDecorations_IsNone()
    {
        if (OperatingSystem.IsMacOS()) return; // macOS uses system decorations — tested separately

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TitleBar_ContainsMenuAndAppIdentity()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.NotNull(window.FindControl<Border>("TitleBarBorder"));
            Assert.NotNull(window.FindControl<Menu>("MainMenu"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TitleBar_HasWindowControlButtons()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.NotNull(window.FindControl<Button>("MinimizeBtn"));
            Assert.NotNull(window.FindControl<Button>("MaximizeBtn"));
            Assert.NotNull(window.FindControl<Button>("CloseBtn"));
        }
        finally
        {
            window.Close();
        }
    }

    // ── Regression #583: title bar drag must work over the file name text ─────

    [AvaloniaFact]
    public void DoubleTapOnTitleFileName_TogglesMaximize()
    {
        // TitleFileName sits on top of (not inside) the drag-region Border, so it
        // must wire the same DoubleTapped handler itself. Before the fix, double-
        // tapping the file name text did nothing because no handler was attached.
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var titleFileName = window.FindControl<TextBlock>("TitleFileName");
            Assert.NotNull(titleFileName);
            Assert.Equal(WindowState.Normal, window.WindowState);

            // Construct TappedEventArgs without calling its constructor (which requires
            // a live PointerEventArgs) — same approach used elsewhere in this test suite
            // for synthesizing gesture events. The handler only cares about the event
            // having reached it, not its payload.
            var fakeArgs = (TappedEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(TappedEventArgs));
            fakeArgs.RoutedEvent = InputElement.DoubleTappedEvent;
            fakeArgs.Source = titleFileName;
            titleFileName!.RaiseEvent(fakeArgs);

            Assert.Equal(WindowState.Maximized, window.WindowState);
        }
        finally
        {
            window.Close();
        }
    }

    // ── macOS: native traffic-light chrome ────────────────────────────────────

    [AvaloniaFact]
    public void OnMacOS_WindowDecorations_IsFull()
    {
        if (!OperatingSystem.IsMacOS()) return; // Windows/Linux use custom chrome — tested separately

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OnMacOS_TitleBarBorder_IsHidden()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var titleBar = window.FindControl<Border>("TitleBarBorder");
            Assert.NotNull(titleBar);
            Assert.False(titleBar!.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OnMacOS_ResizeGrips_AreHidden()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            string[] gripNames = ["GripN", "GripS", "GripW", "GripE", "GripNW", "GripNE", "GripSW", "GripSE"];
            foreach (var name in gripNames)
            {
                var grip = window.FindControl<Border>(name);
                Assert.NotNull(grip);
                Assert.False(grip!.IsVisible, $"{name} should be hidden on macOS");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
