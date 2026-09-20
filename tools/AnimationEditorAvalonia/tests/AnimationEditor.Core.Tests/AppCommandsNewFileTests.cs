using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsNewFileTests : IDisposable
{
    private readonly TestServices ctx = TestHelpers.SetupFreshAcls();
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string TsxFixtureXml = """
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

    [Fact]
    public void NewFile_CreatesEmptyAcls()
    {
        // Arrange – pre-populate
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
            new AnimationChainSave { Name = "Existing" });

        // Act
        ctx.AppCommands.NewFile();

        Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
        Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
    }

    [Fact]
    public void NewFile_ClearsFileName()
    {
        ctx.ProjectManager.FileName = TestPaths.Abs("some", "file.achx");

        ctx.AppCommands.NewFile();

        Assert.True(string.IsNullOrEmpty(ctx.ProjectManager.FileName));
    }

    [Fact]
    public void NewFile_ClearsSelectedChain()
    {
        var chain = new AnimationChainSave { Name = "A" };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.NewFile();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void NewFile_ClearsSelectedFrame()
    {
        var chain = new AnimationChainSave { Name = "A" };
        var frame = new AnimationFrameSave { FrameLength = 0.1f };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.NewFile();

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void NewFile_FiresRefreshTreeViewRequested()
    {
        bool fired = false;
        ctx.AppCommands.RefreshTreeViewRequested += () => fired = true;

        ctx.AppCommands.NewFile();

        Assert.True(fired);
    }

    [Fact]
    public void NewFile_FiresAnimationChainsChanged()
    {
        bool fired = false;
        ctx.ApplicationEvents.AnimationChainsChanged += () => fired = true;

        ctx.AppCommands.NewFile();

        Assert.True(fired);
    }

    [Fact]
    public void NewFile_CalledTwice_StillLeavesEmptyAcls()
    {
        ctx.AppCommands.NewFile();
        ctx.AppCommands.NewFile();

        Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
    }

    // Fresh-eyes pass #7 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): NewFile sets
    // IProjectManager.AnimationChainListSave/FileName/OnDiskCoordinateType directly, bypassing
    // LoadAnimationChain entirely -- so it never clears ProjectManager's private native-tsx
    // state (_tsxTileset and the two tracking dictionaries). File > New from an active tsx tab
    // must leave IsNativeTsxProject false, or a subsequent Save As offers only the "tsx" file
    // type choice and SaveCurrentAnimationChainList routes the brand-new (empty) document
    // through SaveTsxProject against the stale tileset instead of a plain achx/achj save.
    [Fact]
    public async Task NewFile_AfterOpenTsxWorkflow_ClearsNativeTsxState()
    {
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(tsxPath, TsxFixtureXml);
        await ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);
        Assert.True(ctx.ProjectManager.IsNativeTsxProject);

        ctx.AppCommands.NewFile();

        Assert.False(ctx.ProjectManager.IsNativeTsxProject);
    }
}
