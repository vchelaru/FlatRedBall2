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
        var acls = TestHelpers.SetupFreshAcls();
        await AppCommands.Self.AddAnimationChain();

        bool fired = false;
        void Handler() => fired = true;
        ApplicationEvents.Self.AnimationChainsChanged += Handler;
        try
        {
            UndoManager.Self.Undo();
            Assert.True(fired, "AnimationChainsChanged should fire on undo");
        }
        finally
        {
            ApplicationEvents.Self.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public async Task AddAnimationChain_Undo_RemovesChainFromAcls()
    {
        var acls = TestHelpers.SetupFreshAcls();
        await AppCommands.Self.AddAnimationChain();
        Assert.Single(acls.AnimationChains);

        UndoManager.Self.Undo();

        Assert.Empty(acls.AnimationChains);
    }

    [Fact]
    public async Task AddAnimationChain_UndoThenRedo_ReAddsChain()
    {
        var acls = TestHelpers.SetupFreshAcls();
        await AppCommands.Self.AddAnimationChain();
        UndoManager.Self.Undo();
        Assert.Empty(acls.AnimationChains);

        UndoManager.Self.Redo();

        Assert.Single(acls.AnimationChains);
    }
}
