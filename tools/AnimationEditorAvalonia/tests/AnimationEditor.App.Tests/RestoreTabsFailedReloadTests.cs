using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// PR #1186 follow-up: a refused open (missing texture, declined UV conversion, unreadable file)
/// only dropped the ghost tab it left behind when it happened through <c>LoadAnimationFileAsync</c>
/// (File &gt; Open). <c>RestoreTabsAsync</c>, which restores the previous session's tabs on launch,
/// called the same <c>OpenProjectWorkflowAsync</c> directly and ignored its result, so a file that
/// stopped opening between sessions came back as an active tab showing nothing.
/// </summary>
public class RestoreTabsFailedReloadTests
{
    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window)!;

    private static void SeedSettings(TestServices ctx, IReadOnlyList<string> openTabPaths, string activeTabPath)
    {
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string>(openTabPaths),
            ActiveTabPath = activeTabPath,
        }));
    }

    [AvaloniaFact]
    public void OnlyRestoredTabFailsToReload_DropsIt_AndStartsWithABlankDocument()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var brokenPath = Path.Combine(dir, "broken.achx");
        File.WriteAllText(brokenPath, "not a valid achx file{{{");

        var ctx = TestHelpers.BuildServices();
        SeedSettings(ctx, new[] { brokenPath }, brokenPath);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var tabManager = GetTabManager(window);
            Assert.Empty(tabManager.Tabs);
            Assert.Null(tabManager.ActiveTab);
            Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void ActiveRestoredTabFailsToReload_DropsOnlyThatTab_AndLeavesTheOtherTabOpen()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var goodPath = Path.Combine(dir, "good.achx");
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(goodPath);
        var brokenPath = Path.Combine(dir, "broken.achx");
        File.WriteAllText(brokenPath, "not a valid achx file{{{");

        var ctx = TestHelpers.BuildServices();
        SeedSettings(ctx, new[] { goodPath, brokenPath }, brokenPath);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var tabManager = GetTabManager(window);
            Assert.Single(tabManager.Tabs);
            Assert.Equal(new FilePath(goodPath), tabManager.Tabs[0].Path);
            Assert.Same(tabManager.Tabs[0], tabManager.ActiveTab);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
