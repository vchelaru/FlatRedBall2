using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
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

    private static void CloseTab(MainWindow window, TabEntry tab)
    {
        var method = typeof(MainWindow)
            .GetMethod("CloseTab", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(window, [tab]);
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

            CloseTab(window, tabA);
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
}
