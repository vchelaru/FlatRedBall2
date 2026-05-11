using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppStateTests
{
    [Fact]
    public void WireframeZoomValue_DefaultIs100()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.Equal(100, ctx.AppState.WireframeZoomValue);
    }

    [Fact]
    public void WireframeZoomValue_WhenSet_StoresNewValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        ctx.AppState.WireframeZoomValue = 200;

        Assert.Equal(200, ctx.AppState.WireframeZoomValue);
    }

    [Fact]
    public void WireframeZoomValue_WhenSet_FiresAfterZoomChange()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.AfterZoomChange += () => fired = true;

        ctx.AppState.WireframeZoomValue = 150;

        Assert.True(fired);
    }

    [Fact]
    public void UnitType_WhenSet_StoresValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        ctx.AppState.UnitType = UnitType.TextureCoordinate;

        Assert.Equal(UnitType.TextureCoordinate, ctx.AppState.UnitType);
    }

    [Fact]
    public void UnitType_WhenSet_FiresWireframeTextureChange()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.WireframeTextureChange += () => fired = true;

        ctx.AppState.UnitType = UnitType.TextureCoordinate;

        Assert.True(fired);
    }

    [Fact]
    public void GridSize_DefaultIs16()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.Equal(16, ctx.AppState.GridSize);
    }

    [Fact]
    public void GridSize_WhenSet_StoresValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        ctx.AppState.GridSize = 32;

        Assert.Equal(32, ctx.AppState.GridSize);
    }

    [Fact]
    public void IsSnapToGridChecked_DefaultIsFalse()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.False(ctx.AppState.IsSnapToGridChecked);
    }

    [Fact]
    public void IsSnapToGridChecked_WhenSet_StoresValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        ctx.AppState.IsSnapToGridChecked = true;

        Assert.True(ctx.AppState.IsSnapToGridChecked);
    }

    [Fact]
    public void CurrentFrame_DelegatesToSelectedStateSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 1);
        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        Assert.Same(ctx.SelectedState.SelectedFrame, ctx.AppState.CurrentFrame);
    }

    [Fact]
    public void CurrentFrame_WhenNoFrameSelected_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.Null(ctx.AppState.CurrentFrame);
    }

    [Fact]
    public void ProjectFolder_WhenSet_StoresValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        ctx.AppState.ProjectFolder = "C:/MyGame/Content";

        Assert.Equal("C:/MyGame/Content", ctx.AppState.ProjectFolder);
    }
}
