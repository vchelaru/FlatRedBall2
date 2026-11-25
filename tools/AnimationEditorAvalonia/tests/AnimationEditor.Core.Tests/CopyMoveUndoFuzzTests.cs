using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Random copies, cuts, duplicates, moves and deletes: every undo and redo gives back the document it had, and the selection stays in it after each action.</summary>
public class CopyMoveUndoFuzzTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UndoAndRedo_GiveBackEveryState(int seed)
    {
        for (int run = 0; run < 8; run++)
            Run(new Random(seed * 1000 + run), $"seed {seed} run {run}");
    }

    private static void Run(Random random, string label)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        foreach (var name in new[] { "Walk", "Run", "Idle", "Walk2" })
        {
            var chain = TestHelpers.MakeChain(acls, name, random.Next(0, 4));
            chain.IsLocked = random.Next(5) == 0;
            foreach (var frame in chain.Frames)
                for (int i = random.Next(0, 3); i > 0; i--)
                    (frame.ShapesSave ??= new ShapesSave()).Add(random.Next(2) == 0 ? new AARectSave { Name = "R" + i } : new CircleSave { Name = "C" + i });
        }

        T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];
        List<T> Some<T>(IEnumerable<T> items) => items.Where(_ => random.Next(2) == 0).OrderBy(_ => random.Next()).ToList();
        List<AnimationFrameSave> Frames() => acls.AnimationChains.SelectMany(c => c.Frames).ToList();
        List<object> Shapes() => Frames().SelectMany(f => f.ShapesSave?.Shapes ?? []).ToList();

        var log = new List<string>();
        var states = new List<string> { Snapshot() };
        string Snapshot() => string.Join("|", acls.AnimationChains.Select(c => c.Name + (c.IsLocked ? "*" : "") + "[" + string.Join(",", c.Frames.Select(f =>
            f.TextureName + "(" + (f.ShapesSave is null ? "null" : string.Join(" ", f.ShapesSave.Shapes.Cast<ShapeSave>().Select(s => s.Name))) + ")")) + "]"));

        void CheckSelection(string when)
        {
            var all = new HashSet<object>(acls.AnimationChains.Cast<object>().Concat(Frames()).Concat(Shapes()), ReferenceEqualityComparer.Instance);
            var state = ctx.SelectedState;
            foreach (var node in state.SelectedNodes.Concat(new object?[] { state.SelectedChain, state.SelectedFrame, state.SelectedShape }).OfType<object>())
                all.Contains(node).ShouldBeTrue($"{label}: {when} leaves a selected {node.GetType().Name} that is not in the document; steps: {string.Join(" > ", log)}");
        }

        for (int step = 0; step < 8 && acls.AnimationChains.Count > 0; step++)
        {
            int undoBefore = ctx.UndoManager.UndoHistory.Count;
            Select();
            int action = random.Next(14);
            log.Add(Act(action));
            string after = Snapshot();
            int added = ctx.UndoManager.UndoHistory.Count - undoBefore;
            added.ShouldBeInRange(0, 1, $"{label}: {string.Join(" > ", log)}");
            if (added == 0)
                after.ShouldBe(states[^1], $"{label}: {string.Join(" > ", log)} changed the document without an undo entry");
            else
                states.Add(after);
            CheckSelection("the action");
        }

        for (int i = states.Count - 2; i >= 0; i--)
        {
            ctx.UndoManager.Undo();
            Snapshot().ShouldBe(states[i], $"{label}: undo to {i}; steps: {string.Join(" > ", log)}");
        }
        for (int i = 1; i < states.Count; i++)
        {
            ctx.UndoManager.Redo();
            Snapshot().ShouldBe(states[i], $"{label}: redo to {i}; steps: {string.Join(" > ", log)}");
        }
        for (int i = states.Count - 2; i >= 0; i--)
        {
            ctx.UndoManager.Undo();
            Snapshot().ShouldBe(states[i], $"{label}: second undo to {i}; steps: {string.Join(" > ", log)}");
        }

        void Select()
        {
            var state = ctx.SelectedState;
            state.SelectedNodes = new List<object>();
            switch (random.Next(3))
            {
                case 0:
                    var chains = Some(acls.AnimationChains);
                    if (chains.Count == 0) chains.Add(Pick(acls.AnimationChains));
                    state.SelectedNodes = chains.Cast<object>().ToList();
                    state.SelectedChain = chains[^1];
                    break;
                case 1 when Frames().Count > 0:
                    var frames = Some(Frames());
                    if (frames.Count == 0) frames.Add(Pick(Frames()));
                    state.SelectedNodes = frames.Cast<object>().ToList();
                    state.SelectedChain = acls.AnimationChains.First(c => c.Frames.Contains(frames[^1]));
                    state.SelectedFrame = frames[^1];
                    break;
                default:
                    state.SelectedNodes = new List<object> { Pick(acls.AnimationChains) };
                    state.SelectedChain = (AnimationChainSave)state.SelectedNodes[0];
                    break;
            }
        }

        string Act(int action)
        {
            var commands = ctx.AppCommands;
            switch (action)
            {
                case 0:
                    commands.PasteChains(Some(acls.AnimationChains).Select(AnimationCloneHelper.CloneChain).ToList());
                    return "PasteChains";
                case 1:
                {
                    var target = Pick(acls.AnimationChains);
                    commands.PasteFrames(target, Some(Frames()).Select(AnimationCloneHelper.CloneFrame).ToList(), random.Next(0, target.Frames.Count + 1));
                    return "PasteFrames";
                }
                case 2 when Frames().Count > 0:
                    commands.PasteShapes(Some(Frames()), Some(Shapes()));
                    return "PasteShapes";
                case 3:
                {
                    var cut = Some(acls.AnimationChains);
                    commands.PasteChainsCut(cut);
                    return $"CutChains {cut.Count}";
                }
                case 4:
                {
                    var target = Pick(acls.AnimationChains);
                    var cut = Some(Frames());
                    commands.PasteFramesCut(target, cut, random.Next(0, target.Frames.Count + 1));
                    return $"CutFrames {cut.Count}";
                }
                case 5 when Shapes().Count > 0:
                {
                    var source = Frames().First(f => f.ShapesSave?.Shapes.Count > 0);
                    var cut = Some(source.ShapesSave!.Shapes);
                    commands.PasteShapesCut(Pick(Frames()), cut);
                    return $"CutShapes {cut.Count}";
                }
                case 6:
                    commands.DuplicateChains(Some(acls.AnimationChains));
                    return "DuplicateChains";
                case 7:
                    commands.DuplicateSelection(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = Some(Frames()) });
                    return "DuplicateFrames";
                case 8:
                {
                    var source = Pick(acls.AnimationChains);
                    var target = Pick(acls.AnimationChains);
                    commands.MoveFrames(Some(source.Frames), source, target, random.Next(0, target.Frames.Count + 1));
                    return "MoveFrames";
                }
                case 9:
                    commands.MoveChainsToIndex(Some(acls.AnimationChains), random.Next(0, acls.AnimationChains.Count + 1));
                    return "MoveChains";
                case 10:
                    commands.HandleReorder(random.Next(2) == 0 ? -1 : 1);
                    return "Reorder";
                case 11:
                    commands.SortAnimationsAlphabetically();
                    return "Sort";
                case 12:
                    commands.DeleteSelection(Some(acls.AnimationChains).Take(1).ToList(), Some(Frames()), Some(Shapes()));
                    return "Delete";
                default:
                    if (Shapes().Count > 0)
                        commands.DuplicateShape(Pick(Shapes()));
                    return "DuplicateShape";
            }
        }
    }
}
