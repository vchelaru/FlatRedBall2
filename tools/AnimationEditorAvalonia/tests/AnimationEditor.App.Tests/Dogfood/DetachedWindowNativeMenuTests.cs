using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

    [AvaloniaFact]
    public void DetachedWindow_HasANativeMenu_ThatMirrorsItsInWindowMenu()
    {
        MainWindow window = App.CreateDetachedWindow(NewSettingsRoot());

        NativeMenu? native = NativeMenu.GetMenu(window);

        native.ShouldNotBeNull();
        NativeMenuParity.Differences(window.FindControl<Menu>("MainMenu")!, native, CommandModifier.Meta).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void TwoDetachedWindows_EachGetTheirOwnNativeMenu()
    {
        MainWindow first = App.CreateDetachedWindow(NewSettingsRoot());
        MainWindow second = App.CreateDetachedWindow(NewSettingsRoot());

        NativeMenu? firstMenu = NativeMenu.GetMenu(first);
        NativeMenu? secondMenu = NativeMenu.GetMenu(second);

        firstMenu.ShouldNotBeNull();
        secondMenu.ShouldNotBeNull();
        secondMenu.ShouldNotBeSameAs(firstMenu);
    }
}
