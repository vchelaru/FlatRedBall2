using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Multi-select copy/paste/duplicate for issue #498.
/// </summary>
[Collection("SequentialSingletons")]
public class MultiCopyPasteTests
{
    [Fact]
    public void PasteFrames_MultiFrame_OneUndoRemovesAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var original = chain.Frames[0];
        var f1 = TestHelpers.MakeFrame("a.png");
        var f2 = TestHelpers.MakeFrame("b.png");
        var f3 = TestHelpers.MakeFrame("c.png");

        ctx.AppCommands.PasteFrames(chain, new List<AnimationFrameSave> { f1, f2, f3 });

        Assert.Equal(4, chain.Frames.Count);
        Assert.Equal(original.TextureName, chain.Frames[0].TextureName);
        Assert.Equal(new[] { "a.png", "b.png", "c.png" },
            chain.Frames.Skip(1).Select(f => f.TextureName));
        Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);

        ctx.UndoManager.Undo();
        Assert.Equal(new[] { original }, chain.Frames);
    }

    [Fact]
    public void PasteShapes_MultiShape_OneUndoRemovesAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var r1 = new AARectSave { Name = "R1" };
        var r2 = new AARectSave { Name = "R2" };
        var c1 = new CircleSave { Name = "C1", Radius = 2 };

        ctx.AppCommands.PasteShapes(frame, new[] { r1, r2 }, new[] { c1 });

        Assert.Equal(3, frame.ShapesSave!.Shapes.Count);
        Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);

        ctx.UndoManager.Undo();
        Assert.Empty(frame.ShapesSave!.Shapes);
    }

    [Fact]
    public void PasteShapes_NameCollision_UniquifiesEach()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.ShapesSave!.Shapes.Add(new AARectSave { Name = "Hit" });

        ctx.AppCommands.PasteShapes(frame, new[] { new AARectSave { Name = "Hit" } }, []);

        var names = frame.ShapesSave.AARectSaves.Select(r => r.Name).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("Hit", names);
        Assert.Contains("Hit2", names);
    }

    [Fact]
    public void DuplicateFrames_SameChain_AdjacentBlock_OneUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f1 = chain.Frames[1];
        var f3 = chain.Frames[3];

        ctx.SelectedState.SelectedNodes = new List<object> { f1, f3 };
        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Frame,
            Frames = new[] { f1, f3 },
        });

        Assert.Equal(6, chain.Frames.Count);
        Assert.Same(f1, chain.Frames[1]);
        Assert.NotSame(f1, chain.Frames[2]);
        Assert.Same(f3, chain.Frames[4]);
        Assert.NotSame(f3, chain.Frames[5]);
        Assert.Equal(2, ctx.SelectedState.SelectedNodes.Count);

        ctx.UndoManager.Undo();
        Assert.Equal(4, chain.Frames.Count);
    }

    [Fact]
    public void DuplicateFrames_CrossChain_EachAdjacentInSourceChain_OneUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var walkF0 = walk.Frames[0];
        var runF1  = run.Frames[1];

        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Frame,
            Frames = new[] { walkF0, runF1 },
        });

        Assert.Equal(3, walk.Frames.Count);
        Assert.Equal(3, run.Frames.Count);
        Assert.Same(walkF0, walk.Frames[0]);
        Assert.Same(runF1, run.Frames[1]);

        ctx.UndoManager.Undo();
        Assert.Equal(2, walk.Frames.Count);
        Assert.Equal(2, run.Frames.Count);
    }

    [Fact]
    public void DuplicateChains_TwoContiguousSources_InsertsBlockAfterRange_MatchingPaste()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c1 = TestHelpers.MakeChain(ctx.Acls, "Chain1");
        var c2 = TestHelpers.MakeChain(ctx.Acls, "Chain2");
        TestHelpers.MakeChain(ctx.Acls, "Chain3");
        TestHelpers.MakeChain(ctx.Acls, "Chain4");

        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Chain,
            Chains = new[] { c1, c2 },
        });

        Assert.Equal(new[] { "Chain1", "Chain2", "Chain1Copy", "Chain2Copy", "Chain3", "Chain4" },
            ctx.Acls.AnimationChains.Select(c => c.Name));
    }

    [Fact]
    public void PasteAndDuplicateChains_InsertCopiesAtSameIndices()
    {
        var pasteCtx = TestHelpers.SetupFreshAcls();
        var p1 = TestHelpers.MakeChain(pasteCtx.Acls, "Chain1");
        var p2 = TestHelpers.MakeChain(pasteCtx.Acls, "Chain2");
        TestHelpers.MakeChain(pasteCtx.Acls, "Chain3");
        TestHelpers.MakeChain(pasteCtx.Acls, "Chain4");
        var pasteClones = new[] { AnimationCloneHelper.CloneChain(p1), AnimationCloneHelper.CloneChain(p2) };
        pasteCtx.AppCommands.PasteChains(pasteClones);

        var dupCtx = TestHelpers.SetupFreshAcls();
        var d1 = TestHelpers.MakeChain(dupCtx.Acls, "Chain1");
        var d2 = TestHelpers.MakeChain(dupCtx.Acls, "Chain2");
        TestHelpers.MakeChain(dupCtx.Acls, "Chain3");
        TestHelpers.MakeChain(dupCtx.Acls, "Chain4");
        dupCtx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Chain,
            Chains = new[] { d1, d2 },
        });

        Assert.Equal(2, pasteCtx.Acls.AnimationChains.IndexOf(pasteClones[0]));
        Assert.Equal(3, pasteCtx.Acls.AnimationChains.IndexOf(pasteClones[1]));
        var dupCopies = dupCtx.SelectedState.SelectedChains;
        Assert.Equal(2, dupCtx.Acls.AnimationChains.IndexOf(dupCopies[0]));
        Assert.Equal(3, dupCtx.Acls.AnimationChains.IndexOf(dupCopies[1]));
    }

    [Fact]
    public void PasteChains_Twice_StacksAfterPreviousPaste_MatchingDuplicate()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a1 = TestHelpers.MakeChain(ctx.Acls, "Anim1");
        var a2 = TestHelpers.MakeChain(ctx.Acls, "Anim2");
        ctx.SelectedState.SelectedNodes = new List<object> { a1, a2 };

        static List<AnimationChainSave> FreshClip(AnimationChainSave x, AnimationChainSave y) =>
            new[] { AnimationCloneHelper.CloneChain(x), AnimationCloneHelper.CloneChain(y) }.ToList();

        ctx.AppCommands.PasteChains(FreshClip(a1, a2));
        var firstCopy0 = ctx.Acls.AnimationChains[2];
        var firstCopy1 = ctx.Acls.AnimationChains[3];

        ctx.AppCommands.PasteChains(FreshClip(a1, a2));

        Assert.Equal(6, ctx.Acls.AnimationChains.Count);
        Assert.Same(a1, ctx.Acls.AnimationChains[0]);
        Assert.Same(a2, ctx.Acls.AnimationChains[1]);
        Assert.Same(firstCopy0, ctx.Acls.AnimationChains[2]);
        Assert.Same(firstCopy1, ctx.Acls.AnimationChains[3]);
        Assert.NotSame(firstCopy0, ctx.Acls.AnimationChains[4]);
        Assert.NotSame(firstCopy1, ctx.Acls.AnimationChains[5]);
    }

    [Fact]
    public void PasteChains_Undo_RestoresMultiSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c1 = TestHelpers.MakeChain(ctx.Acls, "Chain1");
        var c2 = TestHelpers.MakeChain(ctx.Acls, "Chain2");
        ctx.SelectedState.SelectedNodes = new List<object> { c1, c2 };
        ctx.SelectedState.SelectedChain = c2;

        ctx.AppCommands.PasteChains(new List<AnimationChainSave>
        {
            AnimationCloneHelper.CloneChain(c1),
            AnimationCloneHelper.CloneChain(c2),
        });
        ctx.UndoManager.Undo();

        Assert.Equal(2, ctx.SelectedState.SelectedNodes.Count);
        Assert.Same(c1, ctx.SelectedState.SelectedNodes[0]);
        Assert.Same(c2, ctx.SelectedState.SelectedNodes[1]);
        Assert.Same(c2, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void PasteFrames_Undo_RestoresMultiSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        var f0 = chain.Frames[0];
        var f1 = chain.Frames[1];
        var f2 = chain.Frames[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f0, f1, f2 };
        ctx.SelectedState.SelectedFrame = f2;

        ctx.AppCommands.PasteFrames(chain, new[]
        {
            AnimationCloneHelper.CloneFrame(f0),
            AnimationCloneHelper.CloneFrame(f1),
            AnimationCloneHelper.CloneFrame(f2),
        }, insertIndex: 3);
        ctx.UndoManager.Undo();

        Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);
        Assert.Same(f0, ctx.SelectedState.SelectedNodes[0]);
        Assert.Same(f2, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void DuplicateChains_MultiChain_OneUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run");

        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Chain,
            Chains = new[] { walk, run },
        });

        Assert.Equal(4, ctx.Acls.AnimationChains.Count);
        ctx.UndoManager.Undo();
        Assert.Equal(2, ctx.Acls.AnimationChains.Count);
    }

    [Fact]
    public void DuplicateShapes_MultiShape_OneUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var r = new AARectSave { Name = "R" };
        var c = new CircleSave { Name = "C", Radius = 1 };
        frame.ShapesSave!.Shapes.Add(r);
        frame.ShapesSave.Shapes.Add(c);

        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Shape,
            Shapes = new object[] { r, c },
        });

        Assert.Equal(4, frame.ShapesSave.Shapes.Count);
        ctx.UndoManager.Undo();
        Assert.Equal(2, frame.ShapesSave.Shapes.Count);
    }

    [Fact]
    public void ClipboardPayload_MultiShape_RoundTrips()
    {
        var shapes = new List<object>
        {
            new AARectSave { Name = "A" },
            new CircleSave { Name = "B", Radius = 3 },
            new AARectSave { Name = "C" },
        };
        var xml = ClipboardPayload.SerializeShapes(shapes);

        Assert.True(ClipboardPayload.TryDeserialize(xml, out _, out _, out var rects, out var circles));
        Assert.Equal(2, rects!.Count);
        Assert.Single(circles!);
    }

    [Fact]
    public void SerializeFromPayload_CrossChainFrames_SortedByChainIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var walkF = walk.Frames[0];
        var runF  = run.Frames[0];

        Assert.True(SelectionCopyContext.TryGet(
            BuildFrameSelection(ctx, runF, walkF),
            ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));

        var xml = ClipboardPayload.SerializeFromPayload(payload);
        ClipboardPayload.TryDeserialize(xml, out _, out var frames, out _, out _);
        Assert.Equal(new[] { walkF.TextureName, runF.TextureName },
            frames!.Select(f => f.TextureName));
    }

    [Fact]
    public void PasteFrames_FiveFrameChain_InsertsContiguousBlockAfterLastSelected()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        for (int i = 0; i < 5; i++)
            chain.Frames[i].TextureName = $"f{i + 1}.png";

        var f0 = chain.Frames[0];
        var f1 = chain.Frames[1];
        var f2 = chain.Frames[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f0, f1, f2 };
        ctx.SelectedState.SelectedFrame = f2;

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));
        var xml = ClipboardPayload.SerializeFromPayload(payload);
        ClipboardPayload.TryDeserialize(xml, out _, out var clipboardFrames, out _, out _);

        var (target, insertIndex) = PastePlacementLogic.ResolveFramePasteTarget(
            ctx.Acls, f2, ctx.ObjectFinder, ctx.SelectedState);

        ctx.AppCommands.PasteFrames(target!, clipboardFrames!, insertIndex);

        Assert.Equal(8, chain.Frames.Count);
        Assert.Equal(new[] { "f1.png", "f2.png", "f3.png", "f1.png", "f2.png", "f3.png", "f4.png", "f5.png" },
            chain.Frames.Select(f => f.TextureName));
        Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);
        Assert.All(ctx.SelectedState.SelectedNodes, n => Assert.NotSame(f0, n));
    }

    [Fact]
    public void PasteFrames_DoesNotInterleaveWhenSourceReferencesAreReused()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        for (int i = 0; i < 5; i++)
            chain.Frames[i].TextureName = $"f{i + 1}.png";

        var sources = chain.Frames.Take(3).ToArray();
        ctx.AppCommands.PasteFrames(chain, sources, insertIndex: 3);

        Assert.Equal(new[] { "f1.png", "f2.png", "f3.png", "f1.png", "f2.png", "f3.png", "f4.png", "f5.png" },
            chain.Frames.Select(f => f.TextureName));
    }

    [Fact]
    public void DuplicateFrames_ContiguousSelection_InsertsBlockAfterRange()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        for (int i = 0; i < 5; i++)
            chain.Frames[i].TextureName = $"f{i + 1}.png";

        var sources = chain.Frames.Take(3).ToArray();
        ctx.SelectedState.SelectedNodes = sources.Cast<object>().ToList();
        ctx.AppCommands.DuplicateSelection(new CopySelectionPayload
        {
            Kind = CopySelectionKind.Frame,
            Frames = sources,
        });

        Assert.Equal(8, chain.Frames.Count);
        Assert.Equal(new[] { "f1.png", "f2.png", "f3.png", "f1.png", "f2.png", "f3.png", "f4.png", "f5.png" },
            chain.Frames.Select(f => f.TextureName));
        Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);
    }

    [Fact]
    public void PastePlacement_InsertsAfterSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var f1 = chain.Frames[1];

        var (target, index) = PastePlacementLogic.ResolveFramePasteTarget(
            ctx.Acls, f1, ctx.ObjectFinder);

        Assert.Same(chain, target);
        Assert.Equal(2, index);
    }

    [Fact]
    public void PastePlacement_MultiSelect_UsesLastFrameInChainOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        var f0 = chain.Frames[0];
        var f2 = chain.Frames[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f2, f0 };
        ctx.SelectedState.SelectedFrame = f0;

        var (_, index) = PastePlacementLogic.ResolveFramePasteTarget(
            ctx.Acls, f0, ctx.ObjectFinder, ctx.SelectedState);

        Assert.Equal(3, index);
    }

    private static SelectedState BuildFrameSelection(TestServices ctx, params AnimationFrameSave[] frames)
    {
        ctx.SelectedState.SelectedNodes = frames.Cast<object>().ToList();
        ctx.SelectedState.SelectedFrame = frames[^1];
        return ctx.SelectedState;
    }
}
