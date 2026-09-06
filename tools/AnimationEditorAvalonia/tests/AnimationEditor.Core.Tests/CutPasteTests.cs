using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Cut (Ctrl+X) clipboard semantics for issue #499.
/// </summary>
[Collection("SequentialSingletons")]
public class CutPasteTests
{
    [Fact]
    public void PendingCutState_SetClearAndContains()
    {
        var cut = new PendingCutState();
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var payload = new CopySelectionPayload
        {
            Kind = CopySelectionKind.Chain,
            Chains = new[] { chain },
        };

        cut.Set(payload, acls);
        Assert.True(cut.IsActive);
        Assert.Equal(CopySelectionKind.Chain, cut.Kind);
        Assert.Same(acls, cut.SourceDocument);
        Assert.True(cut.Contains(chain));
        Assert.False(cut.Contains(new AnimationChainSave { Name = "Other" }));

        cut.Clear();
        Assert.False(cut.IsActive);
        Assert.Null(cut.SourceDocument);
        Assert.False(cut.Contains(chain));
    }

    [Fact]
    public void PendingCutState_NewCutReplacesPrevious()
    {
        var cut = new PendingCutState();
        var acls = new AnimationChainListSave();
        var c1 = new AnimationChainSave { Name = "A" };
        var c2 = new AnimationChainSave { Name = "B" };
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { c1 } }, acls);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { c2 } }, acls);

        Assert.False(cut.Contains(c1));
        Assert.True(cut.Contains(c2));
    }

    [Fact]
    public void PendingCutState_WireframeFrames_IncludesChainChildren()
    {
        var cut = new PendingCutState();
        var acls = new AnimationChainListSave();
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, acls);

        Assert.Equal(2, cut.WireframeFrames.Count);
        Assert.Contains(chain.Frames[0], cut.WireframeFrames);
        Assert.Contains(chain.Frames[1], cut.WireframeFrames);
    }

    [Fact]
    public void PasteChainsCut_OneUndoRestoresSourcesAndRemovesPasted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c1 = TestHelpers.MakeChain(ctx.Acls, "Chain1");
        var c2 = TestHelpers.MakeChain(ctx.Acls, "Chain2");
        TestHelpers.MakeChain(ctx.Acls, "Chain3");
        ctx.SelectedState.SelectedNodes = new List<object> { c1, c2 };

        var pasted = new List<AnimationChainSave>
        {
            AnimationCloneHelper.CloneChain(c1),
            AnimationCloneHelper.CloneChain(c2),
        };

        ctx.AppCommands.PasteChainsCut(pasted, new[] { c1, c2 });

        Assert.Equal(3, ctx.Acls.AnimationChains.Count);
        Assert.DoesNotContain(c1, ctx.Acls.AnimationChains);
        Assert.DoesNotContain(c2, ctx.Acls.AnimationChains);

        ctx.UndoManager.Undo();
        Assert.Equal(3, ctx.Acls.AnimationChains.Count);
        Assert.Contains(c1, ctx.Acls.AnimationChains);
        Assert.Contains(c2, ctx.Acls.AnimationChains);
    }

    [Fact]
    public void PasteFramesCut_OneUndoRestoresSourcesAndRemovesPasted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var f1 = chain.Frames[1];
        var pasted = new[] { AnimationCloneHelper.CloneFrame(f1) };

        ctx.AppCommands.PasteFramesCut(chain, pasted, insertIndex: 2, sourcesToRemove: new[] { f1 });

        Assert.Equal(3, chain.Frames.Count);
        Assert.DoesNotContain(f1, chain.Frames);
        Assert.Equal("frame1.png", chain.Frames[1].TextureName);

        ctx.UndoManager.Undo();
        Assert.Equal(3, chain.Frames.Count);
        Assert.Same(f1, chain.Frames[1]);
    }

    [Fact]
    public void PasteShapesCut_OneUndoRestoresSourcesAndRemovesPasted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hit" };
        frame.ShapesSave!.Shapes.Add(rect);

        var pastedRect = (AARectSave)AnimationCloneHelper.CloneShape(rect)!;
        ctx.AppCommands.PasteShapesCut(frame, new[] { pastedRect }, [], new[] { rect }, frame);

        Assert.Single(frame.ShapesSave.Shapes);
        Assert.DoesNotContain(rect, frame.ShapesSave.Shapes);

        ctx.UndoManager.Undo();
        Assert.Single(frame.ShapesSave.Shapes);
        Assert.Same(rect, frame.ShapesSave.Shapes[0]);
    }

    [Fact]
    public void PasteFramesCut_CrossChainDelete_OneUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var walkF0 = walk.Frames[0];
        var runF1  = run.Frames[1];
        var pasted = new[]
        {
            AnimationCloneHelper.CloneFrame(walkF0),
            AnimationCloneHelper.CloneFrame(runF1),
        };

        ctx.AppCommands.PasteFramesCut(walk, pasted, insertIndex: null,
            sourcesToRemove: new[] { walkF0, runF1 });

        Assert.Equal(3, walk.Frames.Count);
        Assert.Single(run.Frames);

        ctx.UndoManager.Undo();
        Assert.Equal(2, walk.Frames.Count);
        Assert.Equal(2, run.Frames.Count);
        Assert.Same(walkF0, walk.Frames[0]);
        Assert.Same(runF1, run.Frames[1]);
    }

    [Fact]
    public void ResolveCompletion_SourceInDifferentOpenDocument_ReturnsCrossDocument()
    {
        // Cut happened while tab A was active; the user then switched to tab B and pastes there.
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var ctxB = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctxA.Acls, "Walk", 1);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, ctxA.Acls);

        Assert.Equal(CutCompletion.CrossDocument, cut.ResolveCompletion(ctxB.Acls));
    }

    [Fact]
    public void ResolveCompletion_SourceInActiveDocument_ReturnsSameDocument()
    {
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctxA.Acls, "Walk", 1);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, ctxA.Acls);

        Assert.Equal(CutCompletion.SameDocument, cut.ResolveCompletion(ctxA.Acls));
    }

    [Fact]
    public void ResolveCompletion_SourceGoneFromBothDocuments_ReturnsStale()
    {
        // Simulates the source tab having been closed (and its document discarded) since the cut.
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var ctxB = TestHelpers.SetupFreshAcls();
        var chain = new AnimationChainSave { Name = "Walk" }; // never added to any acls
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, ctxA.Acls);

        Assert.Equal(CutCompletion.Stale, cut.ResolveCompletion(ctxB.Acls));
    }

    [Fact]
    public void ResolveCompletion_NoCutPending_ReturnsNone()
    {
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();

        Assert.Equal(CutCompletion.None, cut.ResolveCompletion(ctxA.Acls));
    }

    [Fact]
    public void RemoveSourcesFrom_Chain_RemovesFromGivenDocumentEvenWhenNotActive()
    {
        // Simulates a cut made in tab A while tab B is now the active document (#1026):
        // the source chain lives in A's document, not the currently active one.
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctxA.Acls, "Walk", 1);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, ctxA.Acls);

        bool removed = cut.RemoveSourcesFrom(cut.SourceDocument!);

        Assert.True(removed);
        Assert.DoesNotContain(chain, ctxA.Acls.AnimationChains);
    }

    [Fact]
    public void RemoveSourcesFrom_Frame_RemovesFromChainInGivenDocument()
    {
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctxA.Acls, "Walk", 2);
        var frame = chain.Frames[0];
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = new[] { frame } }, ctxA.Acls);

        bool removed = cut.RemoveSourcesFrom(cut.SourceDocument!);

        Assert.True(removed);
        Assert.DoesNotContain(frame, chain.Frames);
    }

    [Fact]
    public void SourcesBelongToProject_FalseWhenSourceFromOtherAcls()
    {
        var cut = new PendingCutState();
        var ctxA = TestHelpers.SetupFreshAcls();
        var ctxB = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctxA.Acls, "Walk", 1);
        cut.Set(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } }, ctxA.Acls);

        Assert.True(cut.SourcesBelongToProject(ctxA.Acls));
        Assert.False(cut.SourcesBelongToProject(ctxB.Acls));
    }
}
