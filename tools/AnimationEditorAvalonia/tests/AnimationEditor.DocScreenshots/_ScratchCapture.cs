using System;
using System.IO;
using System.Linq;
using AnimationEditor.App;
using AnimationEditor.Core.Demo;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Ad-hoc visual proof template: drive a <see cref="FeatureDemos"/> scenario through
/// real <c>AppCommands</c>, open History, capture PNGs + <c>labels.txt</c> under
/// <see cref="ScreenshotOutput"/>.
/// </summary>
public class _ScratchCapture
{
    [AvaloniaFact]
    public void CaptureHistoryLabels_UndoLabelsDemo()
    {
        var outputDir = ScreenshotOutput.ResolveFeatureDir("issue-534-history");

        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 1000;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            ScenarioFixtures.UseCharacterSheetProject(ctx);
            ctx.ProjectManager.AnimationChainListSave ??= new AnimationChainListSave();
            Dispatcher.UIThread.RunJobs();

            Assert.True(FeatureDemos.TryRun(
                FeatureDemos.UndoLabels,
                ctx.AppCommands,
                ctx.UndoManager,
                ctx.ApplicationEvents,
                "characters.png"));

            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            EnlargeHistoryPanel(window);
            SelectHistoryTab(window);
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var labelsPath = Path.Combine(outputDir, "labels.txt");
            File.WriteAllLines(labelsPath,
                ctx.UndoManager.UndoHistory.Select((c, i) => $"{i + 1}. {c.Description}"));

            var scroll = window.FindControl<ScrollViewer>("HistoryScrollViewer")
                ?? throw new InvalidOperationException("HistoryScrollViewer not found");

            ScreenshotCapture.Capture(window, Path.Combine(outputDir, "01-window-history-tab.png"));

            scroll.Offset = new Vector(0, 0);
            Dispatcher.UIThread.RunJobs();
            ScreenshotCapture.Capture(scroll, Path.Combine(outputDir, "02-history-top.png"));

            double extent = scroll.Extent.Height;
            double viewport = scroll.Viewport.Height;
            if (extent > viewport + 1)
            {
                scroll.Offset = new Vector(0, Math.Max(0, (extent - viewport) / 2));
                Dispatcher.UIThread.RunJobs();
                ScreenshotCapture.Capture(scroll, Path.Combine(outputDir, "03-history-mid.png"));

                scroll.Offset = new Vector(0, Math.Max(0, extent - viewport));
                Dispatcher.UIThread.RunJobs();
                ScreenshotCapture.Capture(scroll, Path.Combine(outputDir, "04-history-bottom.png"));
            }

            Assert.True(File.Exists(labelsPath));
            Assert.True(ctx.UndoManager.UndoHistory.Count >= 15);
        }
        finally
        {
            window.Close();
        }
    }

    private static void SelectHistoryTab(MainWindow window)
    {
        var tabs = window.FindControl<TabControl>("SidebarTabs")
            ?? throw new InvalidOperationException("SidebarTabs not found");
        var history = window.FindControl<TabItem>("HistoryTab")
            ?? throw new InvalidOperationException("HistoryTab not found");
        tabs.SelectedItem = history;
    }

    private static void EnlargeHistoryPanel(MainWindow window)
    {
        if (window.FindControl<Grid>("LeftPanelGrid") is { } left
            && left.RowDefinitions.Count >= 3)
        {
            left.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            left.RowDefinitions[2].Height = new GridLength(4, GridUnitType.Star);
        }
    }
}
