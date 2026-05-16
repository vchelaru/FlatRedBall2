using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Guardrail tests for undo coverage across <see cref="IAppCommands"/>.
///
/// <para><see cref="CompleteRoster_EveryIAppCommandsMethodIsCategorized"/> uses reflection so a
/// NEW method added to <see cref="IAppCommands"/> fails the build until it is consciously
/// categorized in <see cref="Roster"/>. That is the mechanism that catches "a feature shipped
/// without an undo decision" — the failure forces the author to classify the method.</para>
///
/// <para><see cref="UndoableCommand_RoundTrips"/> then verifies, for every method categorized
/// <see cref="Category.MutatingUndoable"/>, that invoking it records an entry and that Undo/Redo
/// restore the project state exactly (compared via .achx serialization).</para>
/// </summary>
[Collection("SequentialSingletons")]
public class UndoCoverageRosterTests
{
    private enum Category
    {
        /// <summary>Mutates the project and must push an undo entry.</summary>
        MutatingUndoable,
        /// <summary>Mutates the project but is deliberately not undoable (e.g. clears the stack).</summary>
        MutatingNotUndoable,
        /// <summary>Does not mutate project state (refresh/notify only).</summary>
        NonMutating,
    }

    // Every method on IAppCommands must appear here. See the class summary for why.
    private static readonly Dictionary<string, Category> Roster = new()
    {
        // Mutating, deliberately NOT undoable -------------------------------------
        [nameof(IAppCommands.OpenAchxWorkflowAsync)]              = Category.MutatingNotUndoable, // loads a file; clears the undo stack
        [nameof(IAppCommands.LoadAnimationChain)]                 = Category.MutatingNotUndoable, // loads a file; clears the undo stack
        [nameof(IAppCommands.NewFile)]                            = Category.MutatingNotUndoable, // resets the project; clears the undo stack
        [nameof(IAppCommands.SaveCurrentAnimationChainList)]      = Category.MutatingNotUndoable, // writes a file; no model change
        [nameof(IAppCommands.SaveCurrentAnimationChainListAsync)] = Category.MutatingNotUndoable, // writes a file; no model change

        // Non-mutating -----------------------------------------------------------
        [nameof(IAppCommands.RefreshTreeNode)]              = Category.NonMutating,
        [nameof(IAppCommands.RefreshAnimationFrameDisplay)] = Category.NonMutating,
        [nameof(IAppCommands.RefreshWireframe)]             = Category.NonMutating,
        [nameof(IAppCommands.RefreshTreeView)]              = Category.NonMutating,

        // Mutating + undoable ----------------------------------------------------
        [nameof(IAppCommands.DeleteAnimationChains)]        = Category.MutatingUndoable,
        [nameof(IAppCommands.AddAxisAlignedRectangle)]      = Category.MutatingUndoable,
        [nameof(IAppCommands.AddCircle)]                    = Category.MutatingUndoable,
        [nameof(IAppCommands.MatchRectangleToFrame)]        = Category.MutatingUndoable,
        [nameof(IAppCommands.MatchCircleToFrame)]           = Category.MutatingUndoable,
        [nameof(IAppCommands.DeleteCircle)]                 = Category.MutatingUndoable,
        [nameof(IAppCommands.DeleteAxisAlignedRectangle)]   = Category.MutatingUndoable,
        [nameof(IAppCommands.AskToDeleteRectangles)]        = Category.MutatingUndoable,
        [nameof(IAppCommands.AskToDeleteCircles)]           = Category.MutatingUndoable,
        [nameof(IAppCommands.AskToDeleteShapes)]            = Category.MutatingUndoable,
        [nameof(IAppCommands.DeleteShapes)]                 = Category.MutatingUndoable,
        [nameof(IAppCommands.AskToDeleteAnimationChains)]   = Category.MutatingUndoable,
        [nameof(IAppCommands.DeleteFrames)]                 = Category.MutatingUndoable,
        [nameof(IAppCommands.AddAnimationChain)]            = Category.MutatingUndoable,
        [nameof(IAppCommands.AddAnimationChainWithName)]    = Category.MutatingUndoable,
        [nameof(IAppCommands.RenameChain)]                  = Category.MutatingUndoable,
        [nameof(IAppCommands.AddFrame)]                     = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveChain)]                    = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveChainToTop)]               = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveChainToBottom)]            = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveFrame)]                    = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveFrameToTop)]               = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveFrameToBottom)]            = Category.MutatingUndoable,
        [nameof(IAppCommands.MoveShape)]                    = Category.MutatingUndoable,
        [nameof(IAppCommands.HandleReorder)]                = Category.MutatingUndoable,
        [nameof(IAppCommands.FlipFrameHorizontally)]        = Category.MutatingUndoable,
        [nameof(IAppCommands.FlipFrameVertically)]          = Category.MutatingUndoable,
        [nameof(IAppCommands.FlipChainHorizontally)]        = Category.MutatingUndoable,
        [nameof(IAppCommands.FlipChainVertically)]          = Category.MutatingUndoable,
        [nameof(IAppCommands.InvertFrameOrder)]             = Category.MutatingUndoable,
        [nameof(IAppCommands.SetAllFrameLengths)]           = Category.MutatingUndoable,
        [nameof(IAppCommands.DuplicateChain)]               = Category.MutatingUndoable,
        [nameof(IAppCommands.SortAnimationsAlphabetically)] = Category.MutatingUndoable,
        [nameof(IAppCommands.AdjustOffsetsJustifyBottom)]   = Category.MutatingUndoable,
        [nameof(IAppCommands.AdjustOffsetsAdjustAll)]       = Category.MutatingUndoable,
        [nameof(IAppCommands.ScaleFrameTimesProportional)]  = Category.MutatingUndoable,
        [nameof(IAppCommands.ScaleFrameTimesSetAllSame)]    = Category.MutatingUndoable,
        [nameof(IAppCommands.AddMultipleFrames)]            = Category.MutatingUndoable,
        [nameof(IAppCommands.AdjustUVAfterResize)]          = Category.MutatingUndoable,
        [nameof(IAppCommands.AddFrameFromPixelBounds)]      = Category.MutatingUndoable,
        [nameof(IAppCommands.SetFrameTextureName)]          = Category.MutatingUndoable,
        [nameof(IAppCommands.PasteChains)]                  = Category.MutatingUndoable,
        [nameof(IAppCommands.PasteFrames)]                  = Category.MutatingUndoable,
        [nameof(IAppCommands.PasteRectangle)]               = Category.MutatingUndoable,
        [nameof(IAppCommands.PasteCircle)]                  = Category.MutatingUndoable,

        // Hot reload — mutates the project but deliberately not undoable (reloads from disk)
        [nameof(IAppCommands.WireHotReloadWatcher)]    = Category.NonMutating,
        [nameof(IAppCommands.ReloadAchxFromDisk)]      = Category.MutatingNotUndoable,
        [nameof(IAppCommands.ReloadPngFromDisk)]       = Category.NonMutating,
        [nameof(IAppCommands.SyncHotReloadWatcher)]    = Category.NonMutating,
    };

    // ── Reflection guardrails ─────────────────────────────────────────────────

    [Fact]
    public void CompleteRoster_EveryIAppCommandsMethodIsCategorized()
    {
        var missing = IAppCommandsMethodNames()
            .Where(name => !Roster.ContainsKey(name))
            .ToList();

        Assert.True(missing.Count == 0,
            "IAppCommands methods not categorized in the undo-coverage Roster — add each to " +
            "Roster with a deliberate Category: " + string.Join(", ", missing));
    }

    [Fact]
    public void CompleteRoster_HasNoStaleEntries()
    {
        var existing = IAppCommandsMethodNames().ToHashSet();
        var stale = Roster.Keys.Where(name => !existing.Contains(name)).ToList();

        Assert.True(stale.Count == 0,
            "Roster entries that no longer exist on IAppCommands: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryUndoableMethod_HasARoundTripInvocation()
    {
        var invoked = UndoableInvocations().Select(row => (string)row[0]).ToHashSet();
        var missing = Roster
            .Where(kv => kv.Value == Category.MutatingUndoable)
            .Select(kv => kv.Key)
            .Where(name => !invoked.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            "MutatingUndoable methods with no round-trip invocation in UndoableInvocations(): " +
            string.Join(", ", missing));
    }

    // ── Round-trip correctness for every undoable command ─────────────────────

    [Theory]
    [MemberData(nameof(UndoableInvocations))]
    public async Task UndoableCommand_RoundTrips(string methodName, object invokeObj)
    {
        var invoke = (Func<TestServices, Task>)invokeObj;
        var ctx = TestHelpers.SetupFreshAcls();
        Arrange(ctx);
        string before = Serialize(ctx.Acls);

        await invoke(ctx);

        Assert.True(ctx.UndoManager.CanUndo, $"{methodName} did not record an undo entry");
        string afterCommand = Serialize(ctx.Acls);
        Assert.True(before != afterCommand,
            $"{methodName} did not change project state — the round-trip test cannot verify undo");

        ctx.UndoManager.Undo();
        Assert.Equal(before, Serialize(ctx.Acls));

        ctx.UndoManager.Redo();
        Assert.Equal(afterCommand, Serialize(ctx.Acls));
    }

    public static IEnumerable<object[]> UndoableInvocations()
    {
        yield return Row(nameof(IAppCommands.DeleteAnimationChains),
            ctx => Sync(() => ctx.AppCommands.DeleteAnimationChains(new() { Alpha(ctx) })));
        yield return Row(nameof(IAppCommands.AddAxisAlignedRectangle),
            ctx => Sync(() => ctx.AppCommands.AddAxisAlignedRectangle(Zebra(ctx).Frames[1])));
        yield return Row(nameof(IAppCommands.AddCircle),
            ctx => Sync(() => ctx.AppCommands.AddCircle(Zebra(ctx).Frames[1])));
        yield return Row(nameof(IAppCommands.MatchRectangleToFrame),
            ctx => Sync(() => ctx.AppCommands.MatchRectangleToFrame(Rect(ctx), Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.MatchCircleToFrame),
            ctx => Sync(() => ctx.AppCommands.MatchCircleToFrame(Circle(ctx), Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.DeleteCircle),
            ctx => Sync(() => ctx.AppCommands.DeleteCircle(Circle(ctx), Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.DeleteAxisAlignedRectangle),
            ctx => Sync(() => ctx.AppCommands.DeleteAxisAlignedRectangle(Rect(ctx), Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.AskToDeleteRectangles),
            ctx => ctx.AppCommands.AskToDeleteRectangles(new() { Rect(ctx) }));
        yield return Row(nameof(IAppCommands.AskToDeleteCircles),
            ctx => ctx.AppCommands.AskToDeleteCircles(new() { Circle(ctx) }));
        yield return Row(nameof(IAppCommands.AskToDeleteShapes),
            ctx => ctx.AppCommands.AskToDeleteShapes(new() { Rect(ctx) }, new() { Circle(ctx) }));
        yield return Row(nameof(IAppCommands.DeleteShapes),
            ctx => Sync(() => ctx.AppCommands.DeleteShapes(Zebra(ctx).Frames[0], new() { Rect(ctx) }, new() { Circle(ctx) })));
        yield return Row(nameof(IAppCommands.AskToDeleteAnimationChains),
            ctx => ctx.AppCommands.AskToDeleteAnimationChains(new() { Alpha(ctx) }));
        yield return Row(nameof(IAppCommands.DeleteFrames),
            ctx => Sync(() => ctx.AppCommands.DeleteFrames(new() { Zebra(ctx).Frames[1] })));
        yield return Row(nameof(IAppCommands.AddAnimationChain),
            ctx => ctx.AppCommands.AddAnimationChain());
        yield return Row(nameof(IAppCommands.AddAnimationChainWithName),
            ctx => Sync(() => ctx.AppCommands.AddAnimationChainWithName("Brand New")));
        yield return Row(nameof(IAppCommands.RenameChain),
            ctx => Sync(() => ctx.AppCommands.RenameChain(Zebra(ctx), "Renamed")));
        yield return Row(nameof(IAppCommands.AddFrame),
            ctx => Sync(() => ctx.AppCommands.AddFrame(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.MoveChain),
            ctx => Sync(() => ctx.AppCommands.MoveChain(Zebra(ctx), +1)));
        yield return Row(nameof(IAppCommands.MoveChainToTop),
            ctx => Sync(() => ctx.AppCommands.MoveChainToTop(Alpha(ctx))));
        yield return Row(nameof(IAppCommands.MoveChainToBottom),
            ctx => Sync(() => ctx.AppCommands.MoveChainToBottom(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.MoveFrame),
            ctx => Sync(() => ctx.AppCommands.MoveFrame(Zebra(ctx).Frames[0], Zebra(ctx), +1)));
        yield return Row(nameof(IAppCommands.MoveFrameToTop),
            ctx => Sync(() => ctx.AppCommands.MoveFrameToTop(Zebra(ctx).Frames[2], Zebra(ctx))));
        yield return Row(nameof(IAppCommands.MoveFrameToBottom),
            ctx => Sync(() => ctx.AppCommands.MoveFrameToBottom(Zebra(ctx).Frames[0], Zebra(ctx))));
        yield return Row(nameof(IAppCommands.MoveShape),
            ctx => Sync(() => ctx.AppCommands.MoveShape(Rect(ctx), Zebra(ctx).Frames[0], +1)));
        yield return Row(nameof(IAppCommands.HandleReorder),
            ctx => Sync(() => ctx.AppCommands.HandleReorder(+1))); // selection is set up by Arrange
        yield return Row(nameof(IAppCommands.FlipFrameHorizontally),
            ctx => Sync(() => ctx.AppCommands.FlipFrameHorizontally(Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.FlipFrameVertically),
            ctx => Sync(() => ctx.AppCommands.FlipFrameVertically(Zebra(ctx).Frames[0])));
        yield return Row(nameof(IAppCommands.FlipChainHorizontally),
            ctx => Sync(() => ctx.AppCommands.FlipChainHorizontally(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.FlipChainVertically),
            ctx => Sync(() => ctx.AppCommands.FlipChainVertically(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.InvertFrameOrder),
            ctx => Sync(() => ctx.AppCommands.InvertFrameOrder(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.SetAllFrameLengths),
            ctx => Sync(() => ctx.AppCommands.SetAllFrameLengths(Zebra(ctx), 0.5f)));
        yield return Row(nameof(IAppCommands.DuplicateChain),
            ctx => Sync(() => ctx.AppCommands.DuplicateChain(Zebra(ctx))));
        yield return Row(nameof(IAppCommands.SortAnimationsAlphabetically),
            ctx => Sync(() => ctx.AppCommands.SortAnimationsAlphabetically())); // Zebra,Alpha -> Alpha,Zebra
        yield return Row(nameof(IAppCommands.AdjustOffsetsJustifyBottom),
            ctx => Sync(() => ctx.AppCommands.AdjustOffsetsJustifyBottom(Zebra(ctx), _ => 32f)));
        yield return Row(nameof(IAppCommands.AdjustOffsetsAdjustAll),
            ctx => Sync(() => ctx.AppCommands.AdjustOffsetsAdjustAll(Zebra(ctx), 5f, 5f, relative: true)));
        yield return Row(nameof(IAppCommands.ScaleFrameTimesProportional),
            ctx => Sync(() => ctx.AppCommands.ScaleFrameTimesProportional(Zebra(ctx), 3f)));
        yield return Row(nameof(IAppCommands.ScaleFrameTimesSetAllSame),
            ctx => Sync(() => ctx.AppCommands.ScaleFrameTimesSetAllSame(Zebra(ctx), 3f)));
        yield return Row(nameof(IAppCommands.AddMultipleFrames),
            ctx => Sync(() => ctx.AppCommands.AddMultipleFrames(Zebra(ctx), 2, incrementUV: false)));
        yield return Row(nameof(IAppCommands.AdjustUVAfterResize),
            ctx => Sync(() => ctx.AppCommands.AdjustUVAfterResize("sheet.png", 64, 64, 128, 128)));
        yield return Row(nameof(IAppCommands.AddFrameFromPixelBounds),
            ctx => Sync(() => ctx.AppCommands.AddFrameFromPixelBounds(Zebra(ctx), "sheet.png", 0, 0, 8, 8, 64, 64)));
        yield return Row(nameof(IAppCommands.SetFrameTextureName),
            ctx => Sync(() => ctx.AppCommands.SetFrameTextureName(Zebra(ctx).Frames[0], "set.png")));
        yield return Row(nameof(IAppCommands.PasteChains),
            ctx => Sync(() => ctx.AppCommands.PasteChains(
                new List<AnimationChainSave> { new() { Name = "Pasted" } })));
        yield return Row(nameof(IAppCommands.PasteFrames),
            ctx => Sync(() => ctx.AppCommands.PasteFrames(Zebra(ctx),
                new List<AnimationFrameSave> { new() { ShapesSave = new ShapesSave() } })));
        yield return Row(nameof(IAppCommands.PasteRectangle),
            ctx => Sync(() => ctx.AppCommands.PasteRectangle(
                Zebra(ctx).Frames[1], new AARectSave { Name = "Pasted" })));
        yield return Row(nameof(IAppCommands.PasteCircle),
            ctx => Sync(() => ctx.AppCommands.PasteCircle(
                Zebra(ctx).Frames[1], new CircleSave { Name = "Pasted" })));
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a standard two-chain project: "Zebra" (3 distinguishable frames, with a
    /// rectangle and circle on frame 0) and "Alpha" (1 frame). Selection points at Zebra
    /// and its first frame. Chain names are deliberately out of alphabetical order so
    /// SortAnimationsAlphabetically actually reorders.
    /// </summary>
    private static void Arrange(TestServices ctx)
    {
        var zebra = new AnimationChainSave { Name = "Zebra" };
        for (int i = 0; i < 3; i++)
        {
            zebra.Frames.Add(new AnimationFrameSave
            {
                TextureName      = "sheet.png",
                Name             = $"Frame{i}",
                FrameLength      = 0.1f * (i + 1),
                LeftCoordinate   = 0.25f,
                RightCoordinate  = 0.5f,
                TopCoordinate    = 0.25f,
                BottomCoordinate = 0.5f,
                RelativeX        = 10f + i,
                RelativeY        = 20f + i,
                ShapesSave       = new ShapesSave(),
            });
        }
        zebra.Frames[0].ShapesSave!.Shapes.Add(
            new AARectSave { Name = "Rect", X = 0f, Y = 0f, ScaleX = 4f, ScaleY = 4f });
        zebra.Frames[0].ShapesSave!.Shapes.Add(
            new AARectSave { Name = "Rect2", X = 8f, Y = 8f, ScaleX = 2f, ScaleY = 2f });
        zebra.Frames[0].ShapesSave!.Shapes.Add(
            new CircleSave { Name = "Circle", X = 0f, Y = 0f, Radius = 4f });
        zebra.Frames[0].ShapesSave!.Shapes.Add(
            new CircleSave { Name = "Circle2", X = 8f, Y = 8f, Radius = 2f });

        var alpha = new AnimationChainSave { Name = "Alpha" };
        alpha.Frames.Add(new AnimationFrameSave
        {
            TextureName      = "sheet.png",
            Name             = "Frame0",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.1f,
            RightCoordinate  = 0.2f,
            TopCoordinate    = 0.1f,
            BottomCoordinate = 0.2f,
            ShapesSave       = new ShapesSave(),
        });

        ctx.Acls.AnimationChains.Add(zebra);
        ctx.Acls.AnimationChains.Add(alpha);
        ctx.SelectedState.SelectedChain = zebra;
        ctx.SelectedState.SelectedFrame = zebra.Frames[0];
    }

    private static AnimationChainSave Zebra(TestServices ctx) => ctx.Acls.AnimationChains[0];
    private static AnimationChainSave Alpha(TestServices ctx) => ctx.Acls.AnimationChains[1];
    private static AARectSave Rect(TestServices ctx) => Zebra(ctx).Frames[0].ShapesSave!.AARectSaves.First();
    private static CircleSave Circle(TestServices ctx) => Zebra(ctx).Frames[0].ShapesSave!.CircleSaves.First();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<string> IAppCommandsMethodNames() =>
        typeof(IAppCommands).GetMethods()
            .Where(m => !m.IsSpecialName) // exclude property/event accessors
            .Select(m => m.Name)
            .Distinct();

    /// <summary>Serializes the project to .achx text — the comparison key for round-trip checks.</summary>
    private static string Serialize(AnimationChainListSave acls)
    {
        using var dir = new TestHelpers.TempDir();
        var path = Path.Combine(dir.Path, "state.achx");
        acls.Save(path);
        return File.ReadAllText(path);
    }

    private static object[] Row(string methodName, Func<TestServices, Task> invoke) =>
        new object[] { methodName, invoke };

    /// <summary>Adapts a synchronous command call to the <see cref="Task"/>-returning invoke shape.</summary>
    private static Task Sync(Action call)
    {
        call();
        return Task.CompletedTask;
    }
}
