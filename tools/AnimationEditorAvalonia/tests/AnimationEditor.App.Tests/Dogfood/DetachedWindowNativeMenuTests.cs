using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A window made by Detach to New Window gets its own live macOS menu bar mirror (#1692), same as the first window.
/// Only the AppKit rendering needs a real Mac.
/// </summary>
public class DetachedWindowNativeMenuTests
{
    private static string NewSettingsRoot() =>
        Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));

    // Closing a window deletes its recovery file, so each test window gets its own rather than the shared real one.
    private static MainWindow CreateDetached() =>
        App.CreateDetachedWindow(NewSettingsRoot(),
            Path.Combine(Path.GetTempPath(), "AnimationEditorAppTests", $"recovery_{Guid.NewGuid():N}.achx"));

    [AvaloniaFact]
    public void DetachedWindow_HasANativeMenu_ThatMirrorsItsInWindowMenu()
    {
        MainWindow window = CreateDetached();
        try
        {
            NativeMenu? native = NativeMenu.GetMenu(window);

            native.ShouldNotBeNull();
            NativeMenuParity.Differences(window.FindControl<Menu>("MainMenu")!, native, CommandModifier.Meta).ShouldBeEmpty();
        }
        finally
        {
            Dispose(window);
        }
    }

    [AvaloniaFact]
    public void TwoDetachedWindows_EachGetTheirOwnNativeMenu()
    {
        MainWindow first = CreateDetached();
        MainWindow second = CreateDetached();
        try
        {
            NativeMenu? firstMenu = NativeMenu.GetMenu(first);
            NativeMenu? secondMenu = NativeMenu.GetMenu(second);

            firstMenu.ShouldNotBeNull();
            secondMenu.ShouldNotBeNull();
            secondMenu.ShouldNotBeSameAs(firstMenu);
        }
        finally
        {
            Dispose(first);
            Dispose(second);
        }
    }

    // Close, as the harness does, so the window's watchers and posted work do not outlive the test.
    private static void Dispose(MainWindow window)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
