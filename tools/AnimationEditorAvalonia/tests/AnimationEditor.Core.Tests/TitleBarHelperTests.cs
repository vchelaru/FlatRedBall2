using AnimationEditor.Core;
using Xunit;

namespace AnimationEditor.Core.Tests;

public sealed class TitleBarHelperTests
{
    [Fact]
    public void AppName_IsBrandedName()
    {
        Assert.Equal("AnimationEditor", TitleBarHelper.AppName);
    }

    [Fact]
    public void BuildActiveFolderDisplay_WhenNeitherPathSet_ReturnsPlaceholder()
    {
        Assert.Equal("No folder open", TitleBarHelper.BuildActiveFolderDisplay(null, null));
    }

    [Fact]
    public void BuildActiveFolderDisplay_WhenOnlyFilesPanelRootSet_ReturnsFilesPanelRoot()
    {
        var filesPanelRoot = @"C:\projects\sprites\Content\";
        Assert.Equal(filesPanelRoot, TitleBarHelper.BuildActiveFolderDisplay(null, filesPanelRoot));
    }

    [Fact]
    public void BuildActiveFolderDisplay_WhenProjectFolderPathSet_TakesPrecedenceOverFilesPanelRoot()
    {
        var projectFolderPath = @"C:\projects\sprites\";
        var filesPanelRoot = @"C:\projects\sprites\Content\";
        Assert.Equal(projectFolderPath, TitleBarHelper.BuildActiveFolderDisplay(projectFolderPath, filesPanelRoot));
    }

    [Fact]
    public void BuildWindowTitle_WhenNoFile_ReturnsAppNameOnly()
    {
        Assert.Equal("AnimationEditor", TitleBarHelper.BuildWindowTitle(null));
        Assert.Equal("AnimationEditor", TitleBarHelper.BuildWindowTitle(""));
    }

    [Fact]
    public void BuildWindowTitle_WhenFileOpen_ReturnsFileNameNotFullPath()
    {
        var title = TitleBarHelper.BuildWindowTitle(@"C:\projects\sprites\MyAnimation.achx");
        Assert.DoesNotContain(@"C:\", title);
        Assert.Contains("MyAnimation.achx", title);
    }

    [Fact]
    public void BuildWindowTitle_WhenFileOpen_FormatsAsAppNameDashFileName()
    {
        var title = TitleBarHelper.BuildWindowTitle(@"C:\projects\sprites\MyAnimation.achx");
        Assert.Equal("AnimationEditor - MyAnimation.achx", title);
    }
}
