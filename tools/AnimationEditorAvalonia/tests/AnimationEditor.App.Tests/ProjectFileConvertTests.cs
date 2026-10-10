using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1376 -- right-click "Convert to .achj" on a Project-tree file or folder row.
/// <see cref="ProjectPanelControlTests"/> (Views.Tests) covers the menu items and events; these
/// cover MainWindow's confirm gate and the hand-off of each converted original to the
/// <c>DeleteToRecycleBin</c> seam (stubbed, same as <see cref="ProjectFileDeleteTests"/>).
/// </summary>
public class ProjectFileConvertTests
{
    private static string WriteAchx(string dir, string fileName)
    {
        Directory.CreateDirectory(dir);
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

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [AvaloniaFact]
    public async Task ConvertProjectFileToAchjAsync_UserDeclinesConfirm_WritesNothingAndRecyclesNothing()
    {
        var dir = NewTempDir();
        WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);
            var recycled = new List<string>();
            window.DeleteToRecycleBin = path => { recycled.Add(path); return null; };

            await window.ConvertProjectFileToAchjAsync("hero.achx");

            Assert.Empty(recycled);
            Assert.False(File.Exists(Path.Combine(dir, "hero.achj")));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task ConvertProjectFileToAchjAsync_UserConfirms_WritesAchjAndRecyclesTheAchx()
    {
        var dir = NewTempDir();
        var achx = WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            var recycled = new List<string>();
            window.DeleteToRecycleBin = path => { recycled.Add(path); return null; };

            await window.ConvertProjectFileToAchjAsync("hero.achx");

            Assert.True(File.Exists(Path.Combine(dir, "hero.achj")));
            Assert.Equal(new[] { achx }, recycled);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    // The achj already exists: the converter skips it, so the original must NOT be recycled --
    // nothing was converted, and recycling would leave the user with only the older .achj.
    [AvaloniaFact]
    public async Task ConvertProjectFileToAchjAsync_AchjAlreadyExists_DoesNotRecycleTheAchx()
    {
        var dir = NewTempDir();
        WriteAchx(dir, "hero.achx");
        File.WriteAllText(Path.Combine(dir, "hero.achj"), "keep me");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            var recycled = new List<string>();
            window.DeleteToRecycleBin = path => { recycled.Add(path); return null; };

            await window.ConvertProjectFileToAchjAsync("hero.achx");

            Assert.Empty(recycled);
            Assert.Equal("keep me", File.ReadAllText(Path.Combine(dir, "hero.achj")));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    // Same reason as Delete (#919): a tab left open on the recycled .achx would let Save
    // resurrect it next to the new .achj.
    [AvaloniaFact]
    public async Task ConvertProjectFileToAchjAsync_FileOpenInTab_ClosesTab()
    {
        var dir = NewTempDir();
        var achx = WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            await window.OpenFileAsTab(achx);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            window.DeleteToRecycleBin = _ => null;

            await window.ConvertProjectFileToAchjAsync("hero.achx");

            Assert.DoesNotContain(GetTabManager(window).Tabs, t => t.Path == new FilePath(achx));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task ConvertProjectFolderToAchjAsync_UserConfirms_ConvertsAndRecyclesEveryAchxUnderIt()
    {
        var dir = NewTempDir();
        var top = WriteAchx(Path.Combine(dir, "Sprites"), "a.achx");
        var nested = WriteAchx(Path.Combine(dir, "Sprites", "Enemies"), "b.achx");
        var outside = WriteAchx(dir, "c.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            var recycled = new List<string>();
            window.DeleteToRecycleBin = path => { recycled.Add(path); return null; };

            await window.ConvertProjectFolderToAchjAsync("Sprites");

            Assert.Equal(new[] { top, nested }.OrderBy(p => p), recycled.OrderBy(p => p));
            Assert.True(File.Exists(Path.Combine(dir, "Sprites", "a.achj")));
            Assert.True(File.Exists(Path.Combine(dir, "Sprites", "Enemies", "b.achj")));
            Assert.False(File.Exists(Path.Combine(dir, "c.achj")));
            Assert.True(File.Exists(outside));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
