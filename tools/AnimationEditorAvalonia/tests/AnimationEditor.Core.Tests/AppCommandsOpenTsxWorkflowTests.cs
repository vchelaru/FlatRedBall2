using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AppCommandsOpenTsxWorkflowTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string PlainFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private const string WangsetFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Terrain" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Terrain.png" width="64" height="64"/>
         <wangsets>
          <wangset name="Ground" type="corner" tile="-1">
           <wangcolor name="Grass" color="#00ff00" tile="0" probability="1"/>
           <wangtile tileid="0" wangid="0,1,0,1,0,1,0,1"/>
          </wangset>
         </wangsets>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public async Task OpenTsxWorkflowAsync_ValidTsx_PopulatesProjectAndFiresCurrentFileChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        string? changedTo = null;
        ctx.ApplicationEvents.CurrentFileChanged += p => changedTo = p;

        await ctx.AppCommands.OpenTsxWorkflowAsync(path);

        Assert.True(ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal("ID:0", ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal(path, changedTo);
    }

    [Fact]
    public async Task OpenTsxWorkflowAsync_UnsupportedConstruct_FiresLoadFailedAndLeavesProjectUntouched()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var path = WriteFixture(WangsetFixtureXml, "Terrain.tsx");
        Exception? failure = null;
        ctx.AppCommands.LoadFailed += (_, ex) => failure = ex;

        await ctx.AppCommands.OpenTsxWorkflowAsync(path);

        Assert.NotNull(failure);
        Assert.Contains("wangsets", failure!.Message);
        Assert.False(ctx.ProjectManager.IsNativeTsxProject);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainList_NativeTsxProject_WritesBackToTsxFile()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        await ctx.AppCommands.OpenTsxWorkflowAsync(path);

        ctx.AppCommands.SaveCurrentAnimationChainList();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var tile = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], tile.Animation.Select(f => (f.TileID, f.Duration)));
    }
}
