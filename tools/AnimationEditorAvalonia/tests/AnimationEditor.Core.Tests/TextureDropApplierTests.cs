using AnimationEditor.Core.DragDrop;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TextureDropApplierTests
{
    [Fact]
    public void Apply_CreatedFrame_AddsFrameToChainAndSelectsIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        bool applied = TextureDropApplier.Apply(
            ctx.AppCommands, ctx.SelectedState,
            chain, targetFrame: null,
            TextureDropResult.CreatedFrame, "new.png");

        Assert.True(applied);
        var createdFrame = Assert.Single(chain.Frames);
        Assert.Equal("new.png", createdFrame.TextureName);
        Assert.Same(createdFrame, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void Apply_NotApplied_ReturnsFalseAndChangesNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];

        bool applied = TextureDropApplier.Apply(
            ctx.AppCommands, ctx.SelectedState,
            chain, frame,
            TextureDropResult.NotApplied, relativePath: null);

        Assert.False(applied);
        Assert.Equal("frame0.png", frame.TextureName);
    }

    [Fact]
    public void Apply_UpdatedChainFrames_SetsTextureOnEveryFrameAndSelectsChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 3);

        bool applied = TextureDropApplier.Apply(
            ctx.AppCommands, ctx.SelectedState,
            chain, targetFrame: null,
            TextureDropResult.UpdatedChainFrames, "sheet.png");

        Assert.True(applied);
        Assert.All(chain.Frames, f => Assert.Equal("sheet.png", f.TextureName));
        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void Apply_UpdatedFrame_SetsTextureNameAndSelectsFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];

        bool applied = TextureDropApplier.Apply(
            ctx.AppCommands, ctx.SelectedState,
            chain, frame,
            TextureDropResult.UpdatedFrame, "new.png");

        Assert.True(applied);
        Assert.Equal("new.png", frame.TextureName);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }
}
