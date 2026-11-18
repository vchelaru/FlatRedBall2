using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #919 -- right-click Delete on a Project-tree file row. <see cref="ProjectPanelControlTests"/>
/// (Views.Tests) covers the context menu and event-raising; these tests cover MainWindow's
/// <c>DeleteProjectFileAsync</c> confirm-gate and its handoff to the <c>DeleteToRecycleBin</c> seam.
/// The seam is stubbed in every test, so the real OS recycle-bin move (<see cref="RecycleBin"/>'s
/// platform branches) is never exercised here -- see <c>RecycleBinTests</c> for the pure guard
/// clauses that are exercised without touching a real recycle bin/trash.
///
/// <c>ConfirmAsync</c> must be re-stubbed on <c>ctx.AppCommands</c> AFTER
/// <see cref="TestServices.CreateMainWindow"/>, not before: the constructor's <c>WireAppCommands</c>
/// unconditionally overwrites it with the real modal dialog (see the animation-editor-testing
/// skill's "[AvaloniaFact] is a last resort" landmine on this exact delegate). <c>ctx.AppCommands</c>
/// and MainWindow's own <c>_appCommands</c> are the same instance, so mutating it post-construction
/// still reaches the window.
/// </summary>
public class ProjectFileDeleteTests
{
    private static string WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "walk.png", FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_UserDeclinesConfirm_DoesNotCallRecycleBin()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);
            string? recycledPath = null;
            window.DeleteToRecycleBin = path => { recycledPath = path; return null; };

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.Null(recycledPath);
            Assert.True(File.Exists(achx));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_UserConfirms_CallsRecycleBinWithAbsolutePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            string? recycledPath = null;
            window.DeleteToRecycleBin = path => { recycledPath = path; return null; };

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.Equal(achx, recycledPath);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    // Issue #919 follow-up: deleting a file that's open in a tab must close that tab -- otherwise
    // Save would silently resurrect the file outside the recycle bin, defeating the confirm dialog.
    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_FileOpenInTab_ClosesTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            await window.OpenFileAsTab(achx);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            window.DeleteToRecycleBin = _ => null;

            await window.DeleteProjectFileAsync("hero.achx");

            var tabManager = GetTabManager(window);
            Assert.DoesNotContain(tabManager.Tabs, t => t.Path == new FilePath(achx));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    // Delete failing (e.g. permission denied) must leave the open tab alone -- nothing was
    // actually removed from disk, so closing it would just lose the user's place for no reason.
    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_RecycleBinFails_LeavesTabOpen()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            await window.OpenFileAsTab(achx);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            window.DeleteToRecycleBin = _ => "Could not move to recycle bin: access denied.";

            await window.DeleteProjectFileAsync("hero.achx");

            var tabManager = GetTabManager(window);
            Assert.Contains(tabManager.Tabs, t => t.Path == new FilePath(achx));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_RecycleBinReturnsError_ShowsErrorBanner()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            window.DeleteToRecycleBin = _ => "Could not move to recycle bin: access denied.";

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.True(window.Notifications.ErrorBanner.IsVisible);
            Assert.Contains("access denied", window.Notifications.ErrorBannerText.Text, StringComparison.Ordinal);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
