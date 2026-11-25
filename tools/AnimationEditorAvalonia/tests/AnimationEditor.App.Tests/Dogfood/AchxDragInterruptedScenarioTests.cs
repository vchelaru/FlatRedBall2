using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AnimationEditor.Core.DragDrop;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Something else changes the .achx while the mouse holds a drag on the wireframe, the preview or a frame row: an undo, Delete,
/// the file rewritten outside, or another tab. Whatever the drag does, memory and the file agree afterwards, undo leads back to
/// where it began, and an outside change is never overwritten.
/// Fixture: Walk has frames at (0,0) and (16,0), the first with a rectangle; Idle has one frame at (32,32).
/// </summary>
public class AchxDragInterruptedScenarioTests
{
    private const string WireframeMove = "wireframe move";
    private const string WireframeHandle = "wireframe handle";
    private const string WireframeChain = "wireframe chain";
    private const string PreviewOffset = "preview offset";
    private const string PreviewShape = "preview shape";
    private const string PreviewChain = "preview chain";

    public static TheoryData<string> Drags => new() { WireframeMove, WireframeHandle, WireframeChain, PreviewOffset, PreviewShape, PreviewChain };

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync(string drag)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", X = 20, Y = 20, ScaleX = 4, ScaleY = 4 });
        string path = editor.WriteAchx("hero.achx", walk, AnimationEditorHarness.Chain("Idle", "sheet.png", (32, 32, 16, 16)));
        editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        var chain = editor.ChainNamed("Walk");
        editor.Expand(chain);
        if (drag is WireframeChain or PreviewChain) editor.ClickRow(chain);
        else editor.ClickRow(chain.Frames[0]);
        return (editor, path);
    }

    private static (Point From, Point To) Points(AnimationEditorHarness editor, string drag)
    {
        var frame = editor.ChainNamed("Walk").Frames[0];
        Rect box = editor.WireframeRectOf(frame);
        Point body = editor.WireframePointAt(frame.LeftCoordinate + 5, frame.TopCoordinate + 5);
        switch (drag)
        {
            case WireframeHandle:
                return (new Point(box.Right, box.Bottom), editor.WireframePointAt(frame.RightCoordinate + 7, frame.BottomCoordinate + 9));
            case WireframeMove:
            case WireframeChain:
                return (body, editor.WireframePointAt(frame.LeftCoordinate + 5 + 8, frame.TopCoordinate + 5 + 6));
            case PreviewShape:
                var rect = frame.ShapesSave!.AARectSaves.First();
                return (editor.PreviewPointAt(rect.X, rect.Y), editor.PreviewPointAt(rect.X + 12, rect.Y + 9));
            default:
                return (editor.PreviewPointAt(frame.RelativeX, frame.RelativeY), editor.PreviewPointAt(frame.RelativeX + 12, frame.RelativeY + 9));
        }
    }

    private static string State(AnimationChainListSave list) => string.Join(" | ", list.AnimationChains.Select(chain =>
        chain.Name + ": " + string.Join("; ", chain.Frames.Select(f => string.Join(",", new[] { f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate,
            f.BottomCoordinate, f.RelativeX, f.RelativeY }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)))
            + string.Concat((f.ShapesSave?.AARectSaves ?? []).Select(r => $" {r.Name}@{r.X:0.###},{r.Y:0.###}"))))));

    private static void ContinueAndRelease(AnimationEditorHarness editor, Point to)
    {
        Should.NotThrow(() =>
        {
            editor.DragMove(new Point(to.X + 6, to.Y + 6));
            editor.DragRelease(new Point(to.X + 6, to.Y + 6));
        });
        editor.ThrowIfErrorShown();
    }

    private static void UndoAll(AnimationEditorHarness editor)
    {
        for (int i = 0; i < 20 && editor.UndoLabels.Count > 0; i++) editor.Press(Key.Z, RawInputModifiers.Control);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task NotInterrupted_TheDragMovesSomething(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        string start = State(editor.Project);
        var (from, to) = Points(editor, drag);

        editor.DragHold(from, to);
        ContinueAndRelease(editor, to);

        State(editor.Project).ShouldNotBe(start);
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(State(editor.Project));
        UndoAll(editor);
        State(editor.Project).ShouldBe(start);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task UndoDuringADrag_UndoLeadsBackToTheStart(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        string start = State(editor.Project);
        var first = Points(editor, drag);
        editor.Drag(first.From, first.To);
        editor.UndoLabels.Count.ShouldBe(1);
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to);

        editor.Press(Key.Z, RawInputModifiers.Control);
        ContinueAndRelease(editor, to);

        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(State(editor.Project), "memory and the file agree");
        UndoAll(editor);
        State(editor.Project).ShouldBe(start, "undo leads back to the start");
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(start);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task DeleteDuringADrag_UndoLeadsBackToTheStart(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        string start = State(editor.Project);
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to);

        editor.Press(Key.Delete);
        ContinueAndRelease(editor, to);

        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(State(editor.Project), "memory and the file agree");
        UndoAll(editor);
        State(editor.Project).ShouldBe(start, "undo leads back to the start");
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(start);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task FileRewrittenOutsideDuringADrag_KeepsTheOutsideChange(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to);

        string outsideState = await RewriteOutsideAsync(editor, path);
        ContinueAndRelease(editor, to);

        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(State(editor.Project), "memory and the file agree");
        UndoAll(editor);
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(outsideState, "undo leads back to the outside change");
        State(editor.Project).ShouldBe(outsideState);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task FileRewrittenOutsideDuringADrag_Escape_KeepsTheOutsideChange(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to);

        string outsideState = await RewriteOutsideAsync(editor, path);
        editor.Press(Key.Escape);
        ContinueAndRelease(editor, to);

        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(outsideState);
        State(editor.Project).ShouldBe(outsideState);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task AnotherTabDuringADrag_LeavesTheOtherTabAlone_AndMemoryMatchesTheFile(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        string start = State(editor.Project);
        string other = Path.Combine(editor.ProjectFolder, "other.achx");
        string otherState = State(AnimationEditorHarness.ReadSaved(other));
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to);

        await editor.OpenAsync(other);
        ContinueAndRelease(editor, to);

        State(editor.Project).ShouldBe(otherState, "the other document is untouched");
        State(AnimationEditorHarness.ReadSaved(other)).ShouldBe(otherState);
        editor.UndoLabels.ShouldBeEmpty("the other document was not edited");
        editor.ClickTab("hero.achx");
        editor.Layout();
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(State(editor.Project), "memory and the file agree");
        UndoAll(editor);
        State(editor.Project).ShouldBe(start, "undo leads back to the start");
        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(start);
    }

    // The OS drag cannot run headless, so this puts the source a held frame row drag has in place and raises the drop it ends with.
    // Windows takes the keyboard during that drag; a live reload and a file handed over by another window still arrive.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrameRowDrag_FileRewrittenOutsideOrAnotherTab_DropKeepsTheFile(bool anotherTab)
    {
        var (editor, path) = await OpenAsync(WireframeMove);
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        var pending = typeof(MainWindow).GetField("_pendingFrameDrag", BindingFlags.Instance | BindingFlags.NonPublic)!;
        pending.SetValue(editor.Window, new FrameDragSource(new[] { walk.Frames[0] }, walk, FrameDragValidity.Valid));
        string expected;
        AnimationChainSave target;
        string front = path;
        if (anotherTab)
        {
            front = Path.Combine(editor.ProjectFolder, "other.achx");
            expected = State(AnimationEditorHarness.ReadSaved(front));
            await editor.OpenAsync(front);
            target = editor.ChainNamed("Other");
        }
        else
        {
            expected = await RewriteOutsideAsync(editor, path);
            target = editor.ChainNamed("Idle");
        }

        var format = typeof(MainWindow).GetField("FrameDragDataFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create((DataFormat<string>)format, "frame"));
        editor.Layout();
        Control row = editor.RowFor(target);
        Point point = row.TranslatePoint(new Point(row.Bounds.Width / 2, 8), editor.AnimTree)!.Value;
        Should.NotThrow(() => editor.AnimTree.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, editor.AnimTree, point, KeyModifiers.None)));
        editor.Layout();

        State(AnimationEditorHarness.ReadSaved(front)).ShouldBe(State(editor.Project), "memory and the file agree");
        State(editor.Project).ShouldBe(expected, "the dragged frame belongs to data that is gone");
        UndoAll(editor);
        State(AnimationEditorHarness.ReadSaved(front)).ShouldBe(expected);
    }

    private static async Task<string> RewriteOutsideAsync(AnimationEditorHarness editor, string path)
    {
        var outside = AnimationEditorHarness.ReadSaved(path);
        outside.AnimationChains.Add(AnimationEditorHarness.Chain("Outside", "sheet.png", (48, 48, 16, 16)));
        outside.Save(path);
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Any(c => c.Name == "Outside"), TimeSpan.FromSeconds(8)))
            .ShouldBeTrue("the live reload should have read the outside change");
        return State(outside);
    }
}
