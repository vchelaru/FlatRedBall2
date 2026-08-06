using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #690 Phase 0/1: named automation peers so Browser Playwright (and desktop a11y) can find
/// History / Animations without coordinate clicking. Names must stay stable — UI drive scripts
/// and screen readers both depend on them.
/// </summary>
public class AutomationPropertiesNamesTests
{
    [AvaloniaFact]
    public void SidebarSurfaces_HaveStableAutomationNames()
    {
        var expectedHistoryTab = "History";
        var expectedUndoHistory = "Undo history";
        var expectedAnimations = "Animations";
        var expectedUndo = "Undo";
        var expectedRedo = "Redo";
        var expectedSidebar = "Sidebar";

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var historyTab = window.FindControl<TabItem>("HistoryTab")
                ?? throw new InvalidOperationException("HistoryTab not found");
            var historyList = window.FindControl<ItemsControl>("HistoryList")
                ?? throw new InvalidOperationException("HistoryList not found");
            var animTree = window.FindControl<TreeView>("AnimTree")
                ?? throw new InvalidOperationException("AnimTree not found");
            var undo = window.FindControl<Button>("HistoryUndoButton")
                ?? throw new InvalidOperationException("HistoryUndoButton not found");
            var redo = window.FindControl<Button>("HistoryRedoButton")
                ?? throw new InvalidOperationException("HistoryRedoButton not found");
            var sidebar = window.FindControl<TabControl>("SidebarTabs")
                ?? throw new InvalidOperationException("SidebarTabs not found");

            Assert.Equal(expectedHistoryTab, AutomationProperties.GetName(historyTab));
            Assert.Equal(expectedUndoHistory, AutomationProperties.GetName(historyList));
            Assert.Equal(expectedAnimations, AutomationProperties.GetName(animTree));
            Assert.Equal(expectedUndo, AutomationProperties.GetName(undo));
            Assert.Equal(expectedRedo, AutomationProperties.GetName(redo));
            Assert.Equal(expectedSidebar, AutomationProperties.GetName(sidebar));

            Assert.Equal("history-tab", AutomationProperties.GetAutomationId(historyTab));
            Assert.Equal("undo-history", AutomationProperties.GetAutomationId(historyList));
            Assert.Equal("animations-tree", AutomationProperties.GetAutomationId(animTree));
        }
        finally
        {
            window.Close();
        }
    }
}
