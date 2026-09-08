using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1018 -- "New Animation File" on a Project-tree folder row. <c>ProjectPanelControlTests</c>
/// (Views.Tests) covers the context menu, the inline editor and the naming rules; these cover
/// MainWindow's half: writing the file into the right folder and opening it as a tab.
/// </summary>
public class NewAnimationFileTests
{
    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    [AvaloniaFact]
    public async Task CreateNewAnimationFileAsync_WritesFileIntoFolderAndOpensItAsTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "Sprites"));
        var expected = Path.Combine(dir, "Sprites", "Enemy.achj");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);

            await window.CreateNewAnimationFileAsync(new NewAnimationFileRequest("Sprites", "Enemy.achj"));

            Assert.True(File.Exists(expected));
            Assert.Contains(GetTabManager(window).Tabs, t => t.Path == new FilePath(expected));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    // The tree's scan can be stale (an external git pull, another editor), so the panel's own
    // collision check isn't the last word -- creating must never clobber a file already there.
    [AvaloniaFact]
    public async Task CreateNewAnimationFileAsync_FileAlreadyExists_LeavesItAloneAndShowsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var existing = Path.Combine(dir, "Enemy.achj");
        File.WriteAllText(existing, "original contents");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);

            await window.CreateNewAnimationFileAsync(new NewAnimationFileRequest("", "Enemy.achj"));

            Assert.Equal("original contents", File.ReadAllText(existing));
            Assert.True(window.Notifications.ErrorBanner.IsVisible);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
