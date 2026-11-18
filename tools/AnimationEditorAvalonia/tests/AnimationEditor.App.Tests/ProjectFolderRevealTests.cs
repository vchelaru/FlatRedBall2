using Avalonia.Headless.XUnit;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #841 follow-up -- right-clicking a folder in the Project tab's tree offers "View in
/// Explorer". <see cref="ProjectPanelControlTests"/> (Views.Tests) covers the context menu and
/// event-raising; these tests cover MainWindow's <c>ResolveProjectFolderAbsolutePath</c>, the pure
/// resolution extracted from <c>RevealProjectFolderInExplorer</c> so the actual OS shell launch
/// (<c>Process.Start</c>) is the only untested residue, matching this codebase's existing
/// <c>OnTitleFileOpenFolderClick</c>/<c>ShellExplorer.RevealFile</c> precedent -- neither has (or
/// needs) a test around its own <c>Process.Start</c> call.
/// </summary>
public class ProjectFolderRevealTests
{
    [AvaloniaFact]
    public async Task ResolveProjectFolderAbsolutePath_NoFolderOpened_ReturnsNull()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.Null(window.ResolveProjectFolderAbsolutePath("Sprites"));
        }
        finally { window.Close(); }
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task ResolveProjectFolderAbsolutePath_EmptyRelativePath_ReturnsRootItself()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);

            Assert.Equal(dir, window.ResolveProjectFolderAbsolutePath(string.Empty));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task ResolveProjectFolderAbsolutePath_NestedFolderRelativePath_CombinesAgainstRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);

            var expected = Path.Combine(dir, "Sprites", "Enemies");
            Assert.Equal(expected, window.ResolveProjectFolderAbsolutePath("Sprites/Enemies"));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
