using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Covers the crash-recovery restore-on-launch path wired through <c>MainWindow.OnOpened</c>.
/// A recovery file written by <see cref="IIoManager.WriteRecoveryFile"/> after an unclean
/// shutdown is restored into a tab automatically and announced with a dismissable banner
/// (issue #1020) — it used to be a blocking Restore/Delete modal that forced the user to
/// decide from memory, with no way to see the content first.
/// </summary>
public class StartupRecoveryTests
{
    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static void SeedRecoveryFile(TestServices ctx, string chainName)
    {
        var seed = new AnimationChainListSave();
        seed.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        ctx.IoManager.WriteRecoveryFile(seed);
    }

    [AvaloniaFact]
    public void NoRecoveryFile_StartsNormally_ShowsNoBanner()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(window.FindControl<Border>("RecoveredDocumentBanner")!.IsVisible);
            Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_BannerDismissed_HidesBanner()
    {
        var ctx = TestHelpers.BuildServices();
        SeedRecoveryFile(ctx, "Recovered");

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var banner = window.FindControl<Border>("RecoveredDocumentBanner")!;
            Assert.True(banner.IsVisible);

            window.FindControl<Button>("DismissRecoveredDocumentBtn")!
                  .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.False(banner.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_DeletesRecoveryFileAfterRestore()
    {
        // Otherwise the same recovery file re-announces itself on every subsequent launch,
        // whether or not the user goes on to Save As the restored content.
        var ctx = TestHelpers.BuildServices();
        SeedRecoveryFile(ctx, "Recovered");

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(ctx.IoManager.RecoveryFileExists());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_KeepsTabsFromPreviousSession()
    {
        // The old modal path early-returned out of HandleStartupAsync on a successful restore,
        // so accepting the recovery silently dropped every other tab the session had open.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var previousTabPath = Path.Combine(dir, "previous.achx");
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(previousTabPath);

        var ctx = TestHelpers.BuildServices();
        SeedRecoveryFile(ctx, "Recovered");

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { previousTabPath },
            ActiveTabPath = previousTabPath,
        }));

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var tabManager = GetTabManager(window);
            Assert.Equal(2, tabManager.Tabs.Count);
            Assert.Contains(tabManager.Tabs, t => t.Path == new FilePath(previousTabPath));
            // The recovered document is what the banner points at, so it wins the activation.
            // Compare against Path.Original: FullPath rewrites the untitled sentinel as a path.
            Assert.True(TabManager.IsUntitledSentinel(tabManager.ActiveTab!.Path.Original));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_RestoresContentIntoActiveTabWithoutPrompting()
    {
        // No dialog is stubbed anywhere in this class on purpose: a modal on the startup path
        // blocks the headless UI thread with nothing to close it, so this test reaching its
        // asserts at all is the proof that recovery no longer prompts.
        var ctx = TestHelpers.BuildServices();
        SeedRecoveryFile(ctx, "Recovered");

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Contains(ctx.ProjectManager.AnimationChainListSave!.AnimationChains, c => c.Name == "Recovered");
            Assert.Null(ctx.ProjectManager.FileName);

            var tabManager = GetTabManager(window);
            Assert.Single(tabManager.Tabs);
            Assert.Same(tabManager.Tabs[0], tabManager.ActiveTab);
            Assert.True(window.FindControl<Border>("RecoveredDocumentBanner")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_UnparseableContent_DeletesFileAndShowsNoBanner()
    {
        var ctx = TestHelpers.BuildServices();
        Directory.CreateDirectory(new FilePath(ctx.IoManager.RecoveryFilePath).GetDirectoryContainingThis().FullPath);
        File.WriteAllText(ctx.IoManager.RecoveryFilePath, "not an animation chain list");

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(ctx.IoManager.RecoveryFileExists());
            Assert.False(window.FindControl<Border>("RecoveredDocumentBanner")!.IsVisible);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }
}
