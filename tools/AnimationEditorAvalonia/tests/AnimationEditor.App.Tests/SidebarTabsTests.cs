using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
