using AnimationEditor.Core.Demo;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class DemoQueryTests
{
    [Fact]
    public void TryGetDemoName_NoQuery_ReturnsNull()
    {
        string? expected = null;
        var actual = DemoQuery.TryGetDemoName("http://127.0.0.1:49990/");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetDemoName_DemoParamPresent_ReturnsName()
    {
        var expected = "undo-labels";
        var actual = DemoQuery.TryGetDemoName(
            "http://127.0.0.1:49990/?demo=undo-labels");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetDemoName_DemoAmongOtherParams_ReturnsName()
    {
        var expected = "undo-labels";
        var actual = DemoQuery.TryGetDemoName(
            "http://127.0.0.1:49990/?arg=--urls&demo=undo-labels&x=1");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetDemoName_EmptyDemoValue_ReturnsNull()
    {
        string? expected = null;
        var actual = DemoQuery.TryGetDemoName("http://127.0.0.1:49990/?demo=");

        Assert.Equal(expected, actual);
    }
}
