using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// End-to-end load path: AppCommands.LoadAnimationChain points at a real .achx, and the
// ProjectManager.AnimationChainListSave that the tree-view builds from should hold the
// parsed chains. Mirrors the user flow "File -> Load .achx -> tree populates".
public class AppCommandsLoadAnimationChainTests
{
    private const string TwoChainAchx =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>Pixel</CoordinateType>
          <AnimationChain>
            <Name>Idle</Name>
            <Frame>
              <TextureName>Sheet.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate><RightCoordinate>32</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>32</BottomCoordinate>
            </Frame>
          </AnimationChain>
          <AnimationChain>
            <Name>Walk</Name>
            <Frame>
              <TextureName>Sheet.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>32</LeftCoordinate><RightCoordinate>64</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>32</BottomCoordinate>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    [Fact]
    public void LoadAnimationChain_RealAchxOnDisk_PopulatesAnimationChainListSaveWithChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var temp = new TestHelpers.TempDir();
        var achxPath = Path.Combine(temp.Path, "test.achx");
        File.WriteAllText(achxPath, TwoChainAchx, Encoding.UTF8);

        ctx.AppCommands.LoadAnimationChain(achxPath);

        Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
        Assert.Equal(2, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
        Assert.Equal("Idle", ctx.ProjectManager.AnimationChainListSave.AnimationChains[0].Name);
        Assert.Equal("Walk", ctx.ProjectManager.AnimationChainListSave.AnimationChains[1].Name);
    }

    [Fact]
    public void LoadAnimationChain_RealAchxOnDisk_FiresRefreshTreeViewRequested()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var temp = new TestHelpers.TempDir();
        var achxPath = Path.Combine(temp.Path, "test.achx");
        File.WriteAllText(achxPath, TwoChainAchx, Encoding.UTF8);

        int refreshCount = 0;
        ctx.AppCommands.RefreshTreeViewRequested += () => refreshCount++;

        ctx.AppCommands.LoadAnimationChain(achxPath);

        Assert.True(refreshCount > 0, "RefreshTreeViewRequested must fire so the UI rebuilds the tree.");
    }
}
