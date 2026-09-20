using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies the last remaining tab can be closed (issue #608): the tab bar's close
/// affordance must stay reachable at a single open tab, and closing it must land the
/// editor back in a fresh, untitled, unsaved state.
/// </summary>
public class CloseLastTabTests
{
    private static string WriteAchx(string dir, string fileName, params string[] chainNames)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var name in chainNames)
        {
            var chain = new AnimationChainSave { Name = name };
            chain.Frames.Add(new AnimationFrameSave { TextureName = name + ".png", FrameLength = 0.1f });
            acls.AnimationChains.Add(chain);
        }
        acls.Save(path);
        return path;
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static async System.Threading.Tasks.Task CloseTabAsync(MainWindow window, TabEntry tab)
    {
        var method = typeof(MainWindow)
            .GetMethod("CloseTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (System.Threading.Tasks.Task)method.Invoke(window, [tab])!;
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task SingleOpenTab_TabBarShowsCloseAffordance()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = WriteAchx(dir, "a.achx", "Walk");
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            Assert.Single(tabManager.Tabs);

            var tabBar = window.FindControl<Border>("TabBarBorder");
            Assert.NotNull(tabBar);
            Assert.True(tabBar!.IsVisible);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task ClosingLastTab_ReturnsToFreshUnsavedState()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = WriteAchx(dir, "a.achx", "Walk");
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            var tabA = tabManager.Tabs.First(t => t.Path == new FilePath(pathA));

            await CloseTabAsync(window, tabA);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(tabManager.Tabs);
            Assert.Null(ctx.ProjectManager.FileName);

            var tabBar = window.FindControl<Border>("TabBarBorder");
            Assert.NotNull(tabBar);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // Issue #1147: CloseTabCore's "all tabs closed -- start fresh" branch used to assign
    // AnimationChainListSave/FileName directly, bypassing the RestoreTsxState(null) reset
    // NewFile/CloseProject already had -- closing the last tab of a native tsx project left
    // IsNativeTsxProject stuck true for the brand-new blank document that replaced it.
    [AvaloniaFact]
    public async System.Threading.Tasks.Task ClosingLastTsxTab_ClearsNativeTsxState()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var tsxPath = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(tsxPath,
                """
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
                """);
            await window.OpenFileAsTab(tsxPath);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctx.ProjectManager.IsNativeTsxProject);

            var tabManager = GetTabManager(window);
            var tsxTab = tabManager.Tabs.First(t => t.Path == new FilePath(tsxPath));

            await CloseTabAsync(window, tsxTab);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(tabManager.Tabs);
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
