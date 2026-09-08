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

    /// <summary>
    /// A cross-chain multi-selection (e.g. Ctrl+click frames from two different chains, then
    /// Delete) must still remove the unlocked chain's frames -- a locked chain among the
    /// selection must not silently veto the whole delete for every chain involved.
    /// </summary>
    [Fact]
    public void DeleteFrames_CrossChainSelectionWithLockedChain_DeletesOnlyUnlockedChainFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var lockedChain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        lockedChain.IsLocked = true;
        var unlockedChain = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 1);
        ctx.SelectedState.SelectedChain = lockedChain;
        var lockedFrame = lockedChain.Frames[0];
        var unlockedFrame = unlockedChain.Frames[0];

        ctx.AppCommands.DeleteFrames(new() { lockedFrame, unlockedFrame });

        Assert.Single(lockedChain.Frames);
        Assert.Empty(unlockedChain.Frames);
        Assert.True(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void PasteShapes_TargetFrameChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        chain.IsLocked = true;
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Copied" };

        ctx.AppCommands.PasteShapes(frame, new[] { rect }, System.Array.Empty<CircleSave>());

        Assert.Empty(frame.ShapesSave!.Shapes);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void PasteShapesCut_TargetFrameChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var sourceChain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var sourceFrame = sourceChain.Frames[0];
        var rect = new AARectSave { Name = "Original" };
        sourceFrame.ShapesSave!.Shapes.Add(rect);

        var targetChain = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 1);
        targetChain.IsLocked = true;
        var targetFrame = targetChain.Frames[0];

        ctx.AppCommands.PasteShapesCut(targetFrame, new[] { rect }, System.Array.Empty<CircleSave>(),
            new object[] { rect }, sourceFrame);

        Assert.Empty(targetFrame.ShapesSave!.Shapes);
        Assert.Single(sourceFrame.ShapesSave!.Shapes); // cut-from source must not happen either
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void PasteShapesCut_SourceFrameChainLocked_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var sourceChain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        sourceChain.IsLocked = true;
        var sourceFrame = sourceChain.Frames[0];
        var rect = new AARectSave { Name = "Original" };
        sourceFrame.ShapesSave!.Shapes.Add(rect);

        var targetChain = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 1);
        var targetFrame = targetChain.Frames[0];

        ctx.AppCommands.PasteShapesCut(targetFrame, new[] { rect }, System.Array.Empty<CircleSave>(),
            new object[] { rect }, sourceFrame);

        Assert.Single(sourceFrame.ShapesSave!.Shapes); // cut-from-locked-source stays blocked
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
