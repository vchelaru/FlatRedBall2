using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AddChainUndoTests
{
    // ── AddAnimationChain + Undo ──────────────────────────────────────────────

    [Fact]
    public async Task AddAnimationChain_Undo_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        await ctx.AppCommands.AddAnimationChain();

        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.UndoManager.Undo();
            Assert.True(fired, "AnimationChainsChanged should fire on undo");
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public async Task AddAnimationChain_Undo_RemovesChainFromAcls()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        await ctx.AppCommands.AddAnimationChain();
        Assert.Single(acls.AnimationChains);

        ctx.UndoManager.Undo();

        Assert.Empty(acls.AnimationChains);
    }

    [Fact]
    public async Task AddAnimationChain_UndoThenRedo_ReAddsChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        await ctx.AppCommands.AddAnimationChain();
        ctx.UndoManager.Undo();
        Assert.Empty(acls.AnimationChains);

        ctx.UndoManager.Redo();

        Assert.Single(acls.AnimationChains);
    }
}
