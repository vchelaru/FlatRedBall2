using AnimationEditor.Core;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public sealed class TitleBarHelperTests
{
    [Fact]
    public void BuildWindowTitle_WhenNoFile_ReturnsAppNameOnly()
    {
        Assert.Equal("AnimationEditor", TitleBarHelper.BuildWindowTitle(null));
        Assert.Equal("AnimationEditor", TitleBarHelper.BuildWindowTitle(""));
    }

    [Fact]
    public void BuildWindowTitle_WhenFileOpen_ReturnsFileNameNotFullPath()
    {
        var filePath = Path.Combine("projects", "sprites", "MyAnimation.achx");
        var title = TitleBarHelper.BuildWindowTitle(filePath);
        Assert.DoesNotContain("sprites" + Path.DirectorySeparatorChar, title);
        Assert.Contains("MyAnimation.achx", title);
    }

    [Fact]
    public void BuildWindowTitle_WhenFileOpen_FormatsAsAppNameDashFileName()
    {
        var filePath = Path.Combine("projects", "sprites", "MyAnimation.achx");
        var title = TitleBarHelper.BuildWindowTitle(filePath);
        Assert.Equal("AnimationEditor - MyAnimation.achx", title);
    }
}
