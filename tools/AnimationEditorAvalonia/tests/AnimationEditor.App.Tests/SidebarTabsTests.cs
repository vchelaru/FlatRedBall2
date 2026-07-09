using System.Collections.Generic;
using System.Linq;
using AnimationEditor.App.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #544: the left sidebar's lower region is a single tab strip (Inspector / Files /
/// History) sitting below the always-visible ANIMATIONS tree. Inspector is the default tab,
/// and the ANIMATIONS tree is never nested behind a tab.
/// </summary>
public class SidebarTabsTests
{
    [AvaloniaFact]
    public void SidebarTabs_OnLaunch_SelectsInspectorTab()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var tabs = window.FindControl<TabControl>("SidebarTabs");
            Assert.NotNull(tabs);
            Assert.Equal("Inspector", ((TabItem)tabs!.SelectedItem!).Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SidebarTabs_AnimationsTree_IsNotNestedInsideTheTabStrip()
    {
        // The hard requirement of #544: the ANIMATIONS tree must stay uncovered — it lives
        // above the tab strip, never inside it.
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var tabs = window.FindControl<TabControl>("SidebarTabs");
            var tree = window.FindControl<TreeView>("AnimTree");
            Assert.NotNull(tabs);
            Assert.NotNull(tree);
            Assert.DoesNotContain(tabs!, tree!.GetVisualAncestors());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuShowHistory_Clicked_SelectsHistoryTab()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("MenuShowHistory")!;
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });

            var tabs = window.FindControl<TabControl>("SidebarTabs");
            Assert.NotNull(tabs);
            Assert.Equal("History", ((TabItem)tabs!.SelectedItem!).Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ShortcutsTab_OnLaunch_IsHiddenAndMenuUnchecked()
    {
        // #633: the Shortcuts tab is a hidden-by-default toggleable tab, not always visible like
        // Inspector/History/Files.
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var tab = window.FindControl<TabItem>("ShortcutsTab")!;
            var menu = window.FindControl<MenuItem>("MenuShowShortcuts")!;
            Assert.False(tab.IsVisible);
            Assert.False(menu.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuShowShortcuts_ClickedOnce_ShowsAndSelectsShortcutsTabAndChecksMenu()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("MenuShowShortcuts")!;
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });

            var tabs = window.FindControl<TabControl>("SidebarTabs")!;
            var tab = window.FindControl<TabItem>("ShortcutsTab")!;
            Assert.True(tab.IsVisible);
            Assert.Same(tab, tabs.SelectedItem);
            Assert.True(menu.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuShowShortcuts_ClickedTwice_HidesShortcutsTabAndFallsBackToInspector()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("MenuShowShortcuts")!;
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });

            var tabs = window.FindControl<TabControl>("SidebarTabs")!;
            var tab = window.FindControl<TabItem>("ShortcutsTab")!;
            var inspector = window.FindControl<TabItem>("InspectorTab")!;
            Assert.False(tab.IsVisible);
            Assert.Same(inspector, tabs.SelectedItem);
            Assert.False(menu.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ShortcutsTabCloseButton_Clicked_HidesShortcutsTabAndFallsBackToInspector()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("MenuShowShortcuts")!;
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });

            var closeButton = window.FindControl<Button>("ShortcutsTabCloseButton")!;
            closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = closeButton });

            var tabs = window.FindControl<TabControl>("SidebarTabs")!;
            var tab = window.FindControl<TabItem>("ShortcutsTab")!;
            var inspector = window.FindControl<TabItem>("InspectorTab")!;
            Assert.False(tab.IsVisible);
            Assert.Same(inspector, tabs.SelectedItem);
            Assert.False(menu.IsChecked);
        }
        finally { window.Close(); }
    }

    // Raises a middle-button-release directly on the header, rather than simulating a physical
    // click through the window: the header only exists once ToggleableSidebarTab makes the tab
    // visible mid-test, and the headless compositor's hit-test scene lags behind that late
    // layout change, so a coordinate-based click can miss the freshly-shown control. Raising the
    // routed event directly still exercises the same handler a real middle-click would invoke.
    private static void RaiseMiddleClick(Control target)
    {
        var pointer = new Pointer(0, PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.MiddleButtonReleased);
        var args = new PointerReleasedEventArgs(
            target, pointer, target, new Point(0, 0), 0, properties, KeyModifiers.None, MouseButton.Middle);
        target.RaiseEvent(args);
    }

    [AvaloniaFact]
    public void ShortcutsTabHeader_MiddleClicked_HidesShortcutsTabAndFallsBackToInspector()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("MenuShowShortcuts")!;
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });

            var header = window.FindControl<Control>("ShortcutsTabHeader")!;
            RaiseMiddleClick(header);

            var tabs = window.FindControl<TabControl>("SidebarTabs")!;
            var tab = window.FindControl<TabItem>("ShortcutsTab")!;
            var inspector = window.FindControl<TabItem>("InspectorTab")!;
            Assert.False(tab.IsVisible);
            Assert.Same(inspector, tabs.SelectedItem);
            Assert.False(menu.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ShortcutsList_OnLaunch_IsPopulatedFromTheHotkeyRegistry()
    {
        // #634: content is sourced from the same registry the KeyDown dispatch uses (#632), so
        // it can never drift from what a keypress actually does.
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var list = window.FindControl<ItemsControl>("ShortcutsList")!;
            var categories = ((IEnumerable<HotkeyCategoryVm>)list.ItemsSource!).ToList();

            var undo = categories.SelectMany(c => c.Hotkeys).First(h => h.Description == "Undo");
            Assert.Equal("Ctrl+Z", undo.Gesture);

            // #632's registry names these keys "OemPlus"/"OemMinus" (Avalonia's Key enum, needed
            // so MenuItem.InputGesture stays parseable) — the panel spells them out instead.
            var zoomIn = categories.SelectMany(c => c.Hotkeys).First(h => h.Description == "Zoom In (Focused Panel)");
            Assert.Equal("Ctrl+Plus", zoomIn.Gesture);
            var zoomOut = categories.SelectMany(c => c.Hotkeys).First(h => h.Description == "Zoom Out (Focused Panel)");
            Assert.Equal("Ctrl+Minus", zoomOut.Gesture);

            int totalRows = categories.Sum(c => c.Hotkeys.Count);
            Assert.Equal(window.Hotkeys.Count, totalRows);
        }
        finally { window.Close(); }
    }
}
