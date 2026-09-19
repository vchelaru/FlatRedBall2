using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// <see cref="AnimationEditor.Core.CommandsAndState.AppCommands.OpenProjectWorkflowAsync"/> is the
/// single dispatch point every tab-open path (<c>ActivateTabContentAsync</c>,
/// <c>MainWindow.LoadAnimationFileAsync</c>) should call through, so a <c>.tsx</c> tab needs no
/// changes to <c>TabKind</c>/<c>TabManager</c> -- <c>TabEntry.InferKind</c> already treats any
/// non-png extension as a full-editor tab.
/// </summary>
public class AppCommandsOpenProjectWorkflowDispatchTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public async Task OpenProjectWorkflowAsync_TsxExtension_RoutesToOpenTsxWorkflow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml);

        await ctx.AppCommands.OpenProjectWorkflowAsync(path);

        Assert.True(ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal("ID:0", ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name);
    }

    [Fact]
    public async Task OpenProjectWorkflowAsync_AchxExtension_RoutesToOpenAchxWorkflow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var path = Path.Combine(_dir.Path, "Hero.achx");
        var acls = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel,
        };
        acls.AnimationChains.Add(new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = "Walk" });
        acls.Save(path);

        await ctx.AppCommands.OpenProjectWorkflowAsync(path);

        Assert.False(ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal("Walk", ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name);
    }
}
