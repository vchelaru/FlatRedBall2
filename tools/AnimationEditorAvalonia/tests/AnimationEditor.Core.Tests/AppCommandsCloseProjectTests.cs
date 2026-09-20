using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #948: File > Close Project must reset the editor to the same blank state as a fresh
// app launch -- ProjectManager, SelectedState, and the undo stack -- so a user can switch
// projects without relaunching, and so #949's before/after memory measurement gets a clean
// baseline between runs.
[Collection("SequentialSingletons")]
public class AppCommandsCloseProjectTests : IDisposable
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
    public void CloseProject_CreatesEmptyAcls()
    {
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
            new AnimationChainSave { Name = "Existing" });

        ctx.AppCommands.CloseProject();

        Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
        Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
    }

    [Fact]
    public void CloseProject_ClearsFileName()
    {
        ctx.ProjectManager.FileName = TestPaths.Abs("some", "file.achx");

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.ProjectManager.FileName);
    }

    [Fact]
    public void CloseProject_ClearsProjectFolderPath()
    {
        ctx.ProjectManager.ProjectFolderPath = TestPaths.AbsDir("some", "project");

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.ProjectManager.ProjectFolderPath);
    }

    [Fact]
    public void CloseProject_ClearsSelectedChain()
    {
        var chain = new AnimationChainSave { Name = "A" };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void CloseProject_ClearsUndoStack()
    {
        var chain = new AnimationChainSave { Name = "A" };
        ctx.AppCommands.AddFrame(chain); // pushes an undo entry
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.AppCommands.CloseProject();

        Assert.False(ctx.UndoManager.CanUndo);
        Assert.False(ctx.UndoManager.CanRedo);
    }

    [Fact]
    public void CloseProject_FiresRefreshTreeViewRequested()
    {
        bool fired = false;
        ctx.AppCommands.RefreshTreeViewRequested += () => fired = true;

        ctx.AppCommands.CloseProject();

        Assert.True(fired);
    }

    [Fact]
    public void CloseProject_DeletesRecoveryFile()
    {
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        ctx.AppCommands.CloseProject();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    // Fresh-eyes pass #7 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): same gap as
    // NewFile -- CloseProject sets AnimationChainListSave/FileName directly instead of going
    // through LoadAnimationChain, so it never clears ProjectManager's private native-tsx state.
    [Fact]
    public async Task CloseProject_AfterOpenTsxWorkflow_ClearsNativeTsxState()
    {
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(tsxPath, TsxFixtureXml);
        await ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);
        Assert.True(ctx.ProjectManager.IsNativeTsxProject);

        ctx.AppCommands.CloseProject();

        Assert.False(ctx.ProjectManager.IsNativeTsxProject);
    }
}
