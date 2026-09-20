using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1147: <c>ActivateUntitledTabContent</c> (the tab-switch path for an already-open
/// Untitled tab, distinct from <c>OpenAsNewUnsavedDocument</c> which creates one) used to assign
/// <c>AnimationChainListSave</c>/<c>FileName</c> directly without the <c>RestoreTsxState(null)</c>
/// reset <c>NewFile</c>/<c>CloseProject</c> already had -- switching back to an Untitled tab while
/// a native tsx tab was previously active left <c>IsNativeTsxProject</c> stuck true.
/// </summary>
public class ActivateUntitledTabTsxStateTests
{
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static async Task ActivateTabAsync(MainWindow window, TabEntry tab)
    {
        var method = typeof(MainWindow)
            .GetMethod("ActivateTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(window, [tab])!;
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    [AvaloniaFact]
    public async Task ActivateTabAsync_SwitchBackToUntitledTabAfterViewingTsxTab_ClearsNativeTsxState()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var tsxPath = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(tsxPath, TsxFixtureXml);
            await window.OpenFileAsTab(tsxPath);
            Dispatcher.UIThread.RunJobs();

            // File > New: registers the tsx tab in the background and opens/activates a new
            // Untitled tab -- IsNativeTsxProject goes false here via OpenAsNewUnsavedDocument.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(ctx.ProjectManager.IsNativeTsxProject);

            var tabManager = GetTabManager(window);
            var tsxTab = tabManager.Tabs.First(t => t.Path.FullPath == new AnimationEditor.Core.Paths.FilePath(tsxPath).FullPath);
            var untitledTab = tabManager.Tabs.First(t => TabManager.IsUntitledSentinel(t.Path.Original));

            // Switch to the tsx tab -- IsNativeTsxProject goes true again.
            await ActivateTabAsync(window, tsxTab);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctx.ProjectManager.IsNativeTsxProject);

            // Switch back to the Untitled tab -- this is the ActivateUntitledTabContent path.
            await ActivateTabAsync(window, untitledTab);
            Dispatcher.UIThread.RunJobs();

            Assert.False(ctx.ProjectManager.IsNativeTsxProject);
            Assert.Null(ctx.ProjectManager.TsxTileSize);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
