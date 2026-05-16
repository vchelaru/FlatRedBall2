using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class UndoRedoSelectionTests
{
    // ── AddChain ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddChain_Redo_SelectsAddedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.AddAnimationChainWithName("Walk");
        var chain = ctx.Acls.AnimationChains[0];
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void AddChain_Undo_RestoresPreAddSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.AddAnimationChainWithName("A");
        var chainA = ctx.Acls.AnimationChains[0];
        ctx.SelectedState.SelectedChain = chainA;

        ctx.AppCommands.AddAnimationChainWithName("B");
        ctx.UndoManager.Undo();

        Assert.Same(chainA, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void AddChain_Undo_SelectionIsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.AddAnimationChainWithName("Walk");

        ctx.UndoManager.Undo();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    // ── AddCircle ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddCircle_Redo_SelectsCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddCircle(frame);
        var circle = frame.ShapesSave!.CircleSaves.First();
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void AddCircle_Undo_ClearsCircleSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddCircle(frame);
        ctx.UndoManager.Undo();

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    // ── AddFrame ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddFrame_Redo_SelectsFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        ctx.AppCommands.AddFrame(chain);
        var frame = chain.Frames[0];
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void AddFrame_Undo_ClearsFrameSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        ctx.AppCommands.AddFrame(chain);
        ctx.UndoManager.Undo();

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }

    // ── AddRect ───────────────────────────────────────────────────────────────

    [Fact]
    public void AddRect_Redo_SelectsRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        var rect = frame.ShapesSave!.AARectSaves.First();
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void AddRect_Undo_ClearsRectSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.UndoManager.Undo();

        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    // ── DeleteChain ───────────────────────────────────────────────────────────

    [Fact]
    public void DeleteChain_Redo_ClearsChainSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void DeleteChain_Undo_SelectsRestoredChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });
        ctx.UndoManager.Undo();

        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }

    // ── DeleteCircle ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteCircle_Redo_ClearsCircleSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddCircle(frame);
        ctx.UndoManager.Clear();
        var circle = frame.ShapesSave!.CircleSaves.First();

        ctx.AppCommands.DeleteCircle(circle, frame);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void DeleteCircle_Undo_SelectsRestoredCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddCircle(frame);
        ctx.UndoManager.Clear();
        var circle = frame.ShapesSave!.CircleSaves.First();

        ctx.AppCommands.DeleteCircle(circle, frame);
        ctx.UndoManager.Undo();

        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    // ── DeleteFrames ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteFrames_Redo_ClearsFrameSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        ctx.SelectedState.SelectedChain = chain;
        var frame = chain.Frames[0];

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void DeleteFrames_Undo_SelectsRestoredFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        ctx.SelectedState.SelectedChain = chain;
        var frame = chain.Frames[0];

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
        ctx.UndoManager.Undo();

        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
    }

    // ── PasteChains ───────────────────────────────────────────────────────────

    [Fact]
    public void PasteChains_Undo_SelectionFallsBack()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var pastedChain = new AnimationChainSave { Name = "Pasted" };

        ctx.AppCommands.PasteChains(new List<AnimationChainSave> { pastedChain });
        ctx.UndoManager.Undo();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void PasteChains_UndoRedo_RedoReselectsFirstPastedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var pastedChain = new AnimationChainSave { Name = "Pasted" };

        ctx.AppCommands.PasteChains(new List<AnimationChainSave> { pastedChain });
        var firstChain = ctx.Acls.AnimationChains[0];
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Same(firstChain, ctx.SelectedState.SelectedChain);
    }

    // ── DeleteRect ────────────────────────────────────────────────────────────

    [Fact]
    public void DeleteRect_Redo_ClearsRectSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.UndoManager.Clear();
        var rect = frame.ShapesSave!.AARectSaves.First();

        ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void DeleteRect_Undo_SelectsRestoredRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.UndoManager.Clear();
        var rect = frame.ShapesSave!.AARectSaves.First();

        ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame);
        ctx.UndoManager.Undo();

        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);
    }
}