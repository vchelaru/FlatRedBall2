using AnimationEditor.Core;
using Xunit;

namespace AnimationEditor.Core.Tests;

public sealed class TitleBarHelperTests
{
    [Fact]
    public void AppName_IsBrandedName()
    {
        Assert.Equal("Animation Editor", TitleBarHelper.AppName);
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
