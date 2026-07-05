using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #573 follow-up — "View in Explorer" opened Explorer at a default folder instead of
/// selecting the file when the resolved path used forward slashes (e.g. from
/// <c>AnimationEditor.Core.Paths.FilePath</c>). explorer.exe's <c>/select,</c> switch mis-parses
/// forward slashes as extra switches, so the path must be backslash-normalized first.
/// </summary>
public class ShellExplorerTests
{
    [Fact]
    public void ToWindowsSelectPath_ForwardSlashes_ConvertsToBackslashes()
    {
        Assert.Equal(@"C:\proj\textures\hero.png", ShellExplorer.ToWindowsSelectPath("C:/proj/textures/hero.png"));
    }

    [Fact]
    public void ToWindowsSelectPath_AlreadyBackslashes_IsUnchanged()
    {
        Assert.Equal(@"C:\proj\textures\hero.png", ShellExplorer.ToWindowsSelectPath(@"C:\proj\textures\hero.png"));
    }

    [Fact]
    public void ToWindowsSelectPath_MixedSlashes_ConvertsAllToBackslashes()
    {
        Assert.Equal(@"C:\proj\textures\hero.png", ShellExplorer.ToWindowsSelectPath(@"C:/proj\textures/hero.png"));
    }
}
