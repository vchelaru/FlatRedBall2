using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsNewFileTests
{
    private readonly TestServices ctx = TestHelpers.SetupFreshAcls();

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
        ctx.ProjectManager.FileName = @"C:\some\file.achx";

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
}
