using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using AnimationEditor.Core.DragDrop;
using AnimationEditor.App.Controls;
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
/// Fixture: Walk has frames at (0,0) and (16,0), the first with a rectangle, a circle and a polygon; Idle has one frame at (32,32).
/// </summary>
public class AchxDragInterruptedScenarioTests
{
    private const string WireframeMove = "wireframe move";
    private const string WireframeHandle = "wireframe handle";
    private const string WireframeChain = "wireframe chain";
    private const string PreviewOffset = "preview offset";
    private const string PreviewShape = "preview shape";
    private const string PreviewChain = "preview chain";
    private const string PreviewShapes = "preview shapes";
    private const string PreviewVertex = "preview vertex";
    private const string WireframeAdd = "wireframe add frame";

    public static TheoryData<string> Drags => new()
    {
        WireframeMove, WireframeHandle, WireframeChain, WireframeAdd, PreviewOffset, PreviewShape, PreviewShapes, PreviewVertex, PreviewChain,
    };

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync(string drag)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", X = 20, Y = 20, ScaleX = 4, ScaleY = 4 });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Round", X = 20, Y = -20, Radius = 4 });
        walk.Frames[0].ShapesSave!.Add(new PolygonSave
        {
            Name = "Poly", X = -20, Y = -20,
            Points = { new() { X = -6, Y = -6 }, new() { X = -6, Y = 6 }, new() { X = 6, Y = 6 }, new() { X = 6, Y = -6 }, new() { X = -6, Y = -6 } },
        });
        string path = editor.WriteAchx("hero.achx", walk, AnimationEditorHarness.Chain("Idle", "sheet.png", (32, 32, 16, 16)));
        editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        var chain = editor.ChainNamed("Walk");
        editor.Expand(chain);
        if (drag is WireframeChain or PreviewChain) editor.ClickRow(chain);
        else editor.ClickRow(chain.Frames[0]);
        if (drag is PreviewShapes or PreviewVertex)
        {
            var shapes = chain.Frames[0].ShapesSave!;
            editor.Expand(chain.Frames[0]);
            editor.ClickRow(drag == PreviewVertex ? shapes.PolygonSaves.Single() : shapes.AARectSaves.Single());
            if (drag == PreviewShapes) editor.ClickRow(shapes.CircleSaves.Single(), RawInputModifiers.Control);
        }
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
            case WireframeAdd:
                return (editor.WireframePointAt(40, 8), editor.WireframePointAt(48, 16));
            case PreviewVertex:
                var polygon = frame.ShapesSave!.PolygonSaves.Single();
                var corner = polygon.Points[2];
                return (editor.PreviewPointAt(polygon.X + corner.X, polygon.Y + corner.Y), editor.PreviewPointAt(polygon.X + corner.X + 5, polygon.Y + corner.Y + 4));
            case PreviewShape:
            case PreviewShapes:
                var rect = frame.ShapesSave!.AARectSaves.First();
                return (editor.PreviewPointAt(rect.X, rect.Y), editor.PreviewPointAt(rect.X + 12, rect.Y + 9));
            default:
                return (editor.PreviewPointAt(frame.RelativeX, frame.RelativeY), editor.PreviewPointAt(frame.RelativeX + 12, frame.RelativeY + 9));
        }
    }

    private static string State(AnimationChainListSave list) => string.Join(" | ", list.AnimationChains.Select(chain =>
        chain.Name + ": " + string.Join("; ", chain.Frames.Select(f => string.Join(",", new[] { f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate,
            f.BottomCoordinate, f.RelativeX, f.RelativeY }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)))
            + string.Concat((f.ShapesSave?.AARectSaves ?? []).Select(r => $" {r.Name}@{r.X:0.###},{r.Y:0.###}"))
            + string.Concat((f.ShapesSave?.CircleSaves ?? []).Select(c => $" {c.Name}@{c.X:0.###},{c.Y:0.###}"))
            + string.Concat((f.ShapesSave?.PolygonSaves ?? []).Select(p => $" {p.Name}@{string.Join(" ", p.Points.Select(v => $"{v.X:0.###},{v.Y:0.###}"))}"))))));

    private static RawInputModifiers Modifiers(string drag) => drag == WireframeAdd ? RawInputModifiers.Control : RawInputModifiers.None;

    private static void ContinueAndRelease(AnimationEditorHarness editor, Point to, string drag = WireframeMove)
    {
        Should.NotThrow(() =>
        {
            editor.DragMove(new Point(to.X + 6, to.Y + 6), Modifiers(drag));
            editor.DragRelease(new Point(to.X + 6, to.Y + 6), Modifiers(drag));
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

        editor.DragHold(from, to, Modifiers(drag));
        ContinueAndRelease(editor, to, drag);

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
        editor.Drag(first.From, first.To, Modifiers(drag));
        editor.UndoLabels.Count.ShouldBe(1);
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to, Modifiers(drag));

        editor.Press(Key.Z, RawInputModifiers.Control);
        ContinueAndRelease(editor, to, drag);

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
        editor.DragHold(from, to, Modifiers(drag));

        editor.Press(Key.Delete);
        ContinueAndRelease(editor, to, drag);

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
        editor.DragHold(from, to, Modifiers(drag));

        string outsideState = await RewriteOutsideAsync(editor, path);
        ContinueAndRelease(editor, to, drag);

        State(AnimationEditorHarness.ReadSaved(path)).ShouldBe(outsideState);
        State(editor.Project).ShouldBe(outsideState);
        editor.UndoLabels.ShouldBeEmpty("the dragged objects are gone");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drags))]
    public async Task FileRewrittenOutsideDuringADrag_Escape_KeepsTheOutsideChange(string drag)
    {
        var (editor, path) = await OpenAsync(drag);
        using var _ = editor;
        var (from, to) = Points(editor, drag);
        editor.DragHold(from, to, Modifiers(drag));

        string outsideState = await RewriteOutsideAsync(editor, path);
        editor.Press(Key.Escape);
        ContinueAndRelease(editor, to, drag);

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
        editor.DragHold(from, to, Modifiers(drag));

        await editor.OpenAsync(other);
        ContinueAndRelease(editor, to, drag);

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

    // The OS drag cannot run headless, so this puts the source a held row drag has in place and raises the drop it ends with.
    // Windows takes the keyboard during that drag; a live reload and a file handed over by another window still arrive.
    [AvaloniaTheory]
    [InlineData("frame", false)]
    [InlineData("frame", true)]
    [InlineData("chain", false)]
    [InlineData("chain", true)]
    [InlineData("shape", false)]
    [InlineData("shape", true)]
    public async Task RowDrag_FileRewrittenOutsideOrAnotherTab_DropDoesNothing(string row, bool anotherTab)
    {
        var (editor, path) = await OpenAsync(WireframeMove);
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        var (kind, source) = row switch
        {
            "frame" => ("Frame", (object)new FrameDragSource(new[] { walk.Frames[0] }, walk, FrameDragValidity.Valid)),
            "chain" => ("Chain", new ChainDragSource(new[] { walk }, ChainDragValidity.Valid)),
            _ => ("Shape", (Shape: (object)walk.Frames[0].ShapesSave!.AARectSaves.Single(), Frame: walk.Frames[0])),
        };
        Set(editor, $"_pending{kind}Drag", source);
        string front = path;
        if (anotherTab)
        {
            front = Path.Combine(editor.ProjectFolder, "other.achx");
            await editor.OpenAsync(front);
        }
        else
        {
            await RewriteOutsideAsync(editor, path);
        }
        string expected = State(editor.Project);

        Drop(editor, kind, editor.Project.AnimationChains.Last());

        State(editor.Project).ShouldBe(expected, "the dragged row belongs to data that is gone");
        State(AnimationEditorHarness.ReadSaved(front)).ShouldBe(expected);
        editor.UndoLabels.ShouldBeEmpty();
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task RowDrag_NotInterrupted_DropMovesTheFrame()
    {
        var (editor, _) = await OpenAsync(WireframeMove);
        using var __ = editor;
        var walk = editor.ChainNamed("Walk");
        Set(editor, "_pendingFrameDrag", new FrameDragSource(new[] { walk.Frames[0] }, walk, FrameDragValidity.Valid));

        Drop(editor, "Frame", editor.ChainNamed("Idle"));

        editor.ChainNamed("Idle").Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task RowDrag_FileRewrittenOutside_RemovesTheDropLine()
    {
        var (editor, path) = await OpenAsync(WireframeMove);
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        var overlay = editor.Control<Canvas>("DragOverlayCanvas");
        Set(editor, "_pendingFrameDrag", new FrameDragSource(new[] { walk.Frames[0] }, walk, FrameDragValidity.Valid));
        Raise(editor, DragDrop.DragOverEvent, "Frame", editor.ChainNamed("Idle"));
        overlay.Children.Count.ShouldBe(1);

        await RewriteOutsideAsync(editor, path);

        overlay.Children.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PressedFrameRow_FileRewrittenOutside_MovingDoesNotRefuseADrag()
    {
        var (editor, path) = await OpenAsync(WireframeMove);
        using var _ = editor;
        await editor.WaitAsync(TimeSpan.FromSeconds(0.7)); // past the double-click time, so the press is a click of its own
        Control row = editor.RowFor(editor.ChainNamed("Walk").Frames[0]);
        Point press = editor.PointIn(row, row.Bounds.Width / 2, 8);
        editor.DragHold(press, press);

        await RewriteOutsideAsync(editor, path);
        ContinueAndRelease(editor, press);

        editor.ProblemText.ShouldBeNull("the pressed row is gone, so there is no drag to refuse");
    }

    private static void Set(AnimationEditorHarness editor, string field, object value) =>
        typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor.Window, value);

    private static void Drop(AnimationEditorHarness editor, string kind, AnimationChainSave target) =>
        Raise(editor, DragDrop.DropEvent, kind, target);

    private static void Raise(AnimationEditorHarness editor, RoutedEvent<DragEventArgs> dragEvent, string kind, AnimationChainSave target)
    {
        var format = typeof(MainWindow).GetField($"{kind}DragDataFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create((DataFormat<string>)format, kind.ToLowerInvariant()));
        editor.Layout();
        Control row = editor.RowFor(target);
        Point point = row.TranslatePoint(new Point(row.Bounds.Width / 2, 8), editor.AnimTree)!.Value;
        Should.NotThrow(() => editor.AnimTree.RaiseEvent(new DragEventArgs(dragEvent, data, editor.AnimTree, point, KeyModifiers.None)));
        editor.Layout();
    }

    [AvaloniaTheory]
    [InlineData("undo")]
    [InlineData("reload")]
    [InlineData("tab")]
    public async Task GuideDrag_Interrupted_PutsTheGuideBack(string interruption)
    {
        var (editor, path) = await OpenAsync(PreviewChain);
        using var _ = editor;
        Point ruler = editor.PointIn(editor.Preview, 2, 120);
        editor.Drag(ruler, editor.PointIn(editor.Preview, 60, 140));
        editor.Preview.HGuideCount.ShouldBe(1);
        float guide = editor.Preview.HGuides.Single(); // Y down, unlike the points below
        Point to = editor.PreviewPointAt(-20, -guide + 12);
        editor.DragHold(editor.PreviewPointAt(-20, -guide), to);
        editor.Preview.HGuides.Single().ShouldNotBe(guide);

        if (interruption == "undo") editor.Press(Key.Z, RawInputModifiers.Control);
        else if (interruption == "reload") await RewriteOutsideAsync(editor, path);
        else await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "other.achx"));
        ContinueAndRelease(editor, to);

        if (interruption == "tab")
        {
            editor.ClickTab("hero.achx");
            editor.Layout();
        }
        editor.Preview.HGuides.Single().ShouldBe(guide);
    }

    [AvaloniaFact]
    public async Task NewGuideDrag_FileRewrittenOutside_ReleaseDoesNotThrow_AndTheGuideIsGone()
    {
        var (editor, path) = await OpenAsync(PreviewChain);
        using var _ = editor;
        // Leaving the tab saves the companion, with no guides for the reload to put back.
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "other.achx"));
        editor.ClickTab("hero.achx");
        editor.Layout();
        Point to = editor.PointIn(editor.Preview, 60, 140);
        editor.DragHold(editor.PointIn(editor.Preview, 2, 120), to);
        editor.Preview.HGuideCount.ShouldBe(1);

        await RewriteOutsideAsync(editor, path);
        ContinueAndRelease(editor, to);

        editor.Preview.HGuideCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task SequencePreviewGuideDrag_UndoDuringIt_RemovesTheNewGuide()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(SequenceTimelineMultiSelectScenarioTests.WriteFixture(editor));
        var preview = editor.Control<PreviewControl>("SequencePreview");
        Point to = editor.PointIn(preview, 60, 100);
        editor.DragHold(editor.PointIn(preview, 2, 80), to);
        preview.HGuideCount.ShouldBe(1);

        editor.Press(Key.Z, RawInputModifiers.Control);
        ContinueAndRelease(editor, to);

        preview.HGuideCount.ShouldBe(0);
    }

    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static string Tiles(string tsx) =>
        string.Join(",", System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(tsx), "<frame tileid=\"(\\d+)\"").Select(m => m.Groups[1].Value));

    [AvaloniaTheory]
    [InlineData("undo")]
    [InlineData("delete")]
    [InlineData("reload")]
    [InlineData("tab")]
    public async Task Tsx_WireframeDragInterrupted_UndoLeadsBackToTheStart(string interruption)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("Heroes.png", 64, 64);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "Heroes.png", (0, 0, 16, 16)));
        string tsx = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(tsx, TsxXml);
        await editor.OpenAsync(tsx);
        var chain = editor.Project.AnimationChains.Single();
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);
        string start = State(editor.Project);
        if (interruption == "undo")
        {
            editor.Drag(editor.WireframePointAt(8, 8), editor.WireframePointAt(40, 8));
            Tiles(tsx).ShouldBe("2,1");
        }
        float x = chain.Frames[0].LeftCoordinate + 8;
        Point to = editor.WireframePointAt(x, 8 + 32);
        editor.DragHold(editor.WireframePointAt(x, 8), to);
        editor.PixelRectOf(chain.Frames[0]).Y.ShouldBe(32);

        string expected = "0,1";
        if (interruption == "undo") editor.Press(Key.Z, RawInputModifiers.Control);
        else if (interruption == "delete") editor.Press(Key.Delete);
        else if (interruption == "tab") await editor.OpenAsync(other);
        else
        {
            File.WriteAllText(tsx, TsxXml.Replace("<frame tileid=\"1\"", "<frame tileid=\"5\""));
            (await editor.WaitUntilAsync(() => editor.PixelRectOf(editor.Project.AnimationChains.Single().Frames[1]).Y == 16, TimeSpan.FromSeconds(8)))
                .ShouldBeTrue("the live reload should have read the outside change");
            expected = "0,5";
            start = State(editor.Project);
        }
        ContinueAndRelease(editor, to);

        if (interruption == "tab")
        {
            editor.UndoLabels.ShouldBeEmpty("the other document was not edited");
            editor.ClickTab("Heroes.tsx");
            editor.Layout();
        }
        UndoAll(editor);
        State(editor.Project).ShouldBe(start);
        Tiles(tsx).ShouldBe(expected);
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
