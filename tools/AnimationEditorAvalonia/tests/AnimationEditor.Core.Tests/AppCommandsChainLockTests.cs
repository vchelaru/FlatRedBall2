using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Locking a chain (issue #1032) blocks frame/shape content edits on that chain while leaving
/// chain-level container operations (rename, delete, reorder, duplicate) unaffected.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsChainLockTests
{
    [Fact]
    public void AddAxisAlignedRectangle_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        var frame = chain.Frames[0];

        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        Assert.Empty(frame.ShapesSave!.Shapes);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void AddFrame_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;

        ctx.AppCommands.AddFrame(chain);

        Assert.Single(chain.Frames);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void AddFrame_ChainUnlocked_WorksNormally()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);

        ctx.AppCommands.AddFrame(chain);

        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public void AddFrame_AfterUnlockingPreviouslyLockedChain_WorksNormally()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;

        ctx.AppCommands.SetChainLocked(chain, false);
        ctx.AppCommands.AddFrame(chain);

        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public void DeleteAnimationChains_ChainLocked_StillDeletesChain()
    {
        // Chain-level container operations are not blocked by the lock.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;

        ctx.AppCommands.DeleteAnimationChains(new() { chain });

        Assert.Empty(ctx.Acls.AnimationChains);
    }

    [Fact]
    public void DeleteFrames_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        ctx.SelectedState.SelectedChain = chain;
        var frame = chain.Frames[0];

        ctx.AppCommands.DeleteFrames(new() { frame });

        Assert.Single(chain.Frames);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void MoveShape_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        var frame = chain.Frames[0];
        frame.ShapesSave = new ShapesSave();
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave.Shapes.Add(rectA);
        frame.ShapesSave.Shapes.Add(rectB);

        ctx.AppCommands.MoveShape(rectA, frame, delta: 1);

        Assert.Same(rectA, frame.ShapesSave.Shapes[0]);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void RenameChain_ChainLocked_StillRenames()
    {
        // Chain-level container operations are not blocked by the lock.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 0);
        chain.IsLocked = true;

        bool result = ctx.AppCommands.RenameChain(chain, "Run");

        Assert.True(result);
        Assert.Equal("Run", chain.Name);
    }

    [Fact]
    public void SetChainLocked_SetsIsLockedAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 0);

        ctx.AppCommands.SetChainLocked(chain, true);

        Assert.True(chain.IsLocked);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.False(chain.IsLocked);
    }

    [Fact]
    public void SetFrameColor_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        var frame = chain.Frames[0];

        ctx.AppCommands.SetFrameColor(new[] { frame }, red: 200, green: null, blue: null);

        Assert.Null(frame.Red);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameTextureName_ChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        var frame = chain.Frames[0];
        var originalTexture = frame.TextureName;

        ctx.AppCommands.SetFrameTextureName(frame, "new.png");

        Assert.Equal(originalTexture, frame.TextureName);
        Assert.False(ctx.UndoManager.CanUndo);
    }
}
