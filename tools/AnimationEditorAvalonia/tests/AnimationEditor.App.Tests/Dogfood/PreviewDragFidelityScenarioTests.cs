using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A preview drag changes only what it moves: not a cancelled drag's values, not the locked axis, not values playback ignores.</summary>
public class PreviewDragFidelityScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync(Action<AnimationChainSave>? setup = null)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        foreach (var frame in walk.Frames)
        {
            frame.RelativeX = 0.5f;
            frame.RelativeY = 2.25f;
            frame.ShapesSave = new ShapesSave();
            frame.ShapesSave!.Add(new AARectSave { Name = "Hit", X = 30.5f, Y = 20.25f, ScaleX = 6, ScaleY = 6 });
        }
        setup?.Invoke(walk);
        string path = editor.WriteAchx("hero.achx", walk);
        await editor.OpenAsync(path);
        editor.Expand(editor.ChainNamed("Walk"));
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task EscapeDuringAnAnimationDrag_PutsBackTheExactOffsets()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.DragHold(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(7, 9));
        walk.Frames[0].RelativeX.ShouldNotBe(0.5f);

        editor.Press(Key.Escape);
        editor.DragRelease(editor.PreviewPointAt(7, 9));

        walk.Frames.ShouldAllBe(f => f.RelativeX == 0.5f && f.RelativeY == 2.25f);
        walk.Frames.ShouldAllBe(f => f.ShapesSave!.AARectSaves.First().X == 30.5f && f.ShapesSave!.AARectSaves.First().Y == 20.25f);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAFrameDrag_PutsBackTheExactOffset()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);
        editor.DragHold(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(7, 9));

        editor.Press(Key.Escape);
        editor.DragRelease(editor.PreviewPointAt(7, 9));

        (frame.RelativeX, frame.RelativeY).ShouldBe((0.5f, 2.25f));
    }

    [AvaloniaFact]
    public async Task ShiftDragOfAFrame_LeavesTheLockedAxisAsItWas()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.Drag(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(12, 3), RawInputModifiers.Shift);

        frame.RelativeX.ShouldNotBe(0.5f);
        frame.RelativeY.ShouldBe(2.25f);
    }

    [AvaloniaFact]
    public async Task ShiftDragOfAShape_LeavesTheLockedAxisAsItWas()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.ClickRow(frame);

        editor.Drag(editor.PreviewPointAt(30, 20), editor.PreviewPointAt(42, 21), RawInputModifiers.Shift);

        rect.X.ShouldNotBe(30.5f);
        rect.Y.ShouldBe(20.25f);
    }

    [AvaloniaFact]
    public async Task ShiftDragOfAnAnimation_LeavesTheLockedAxisAsItWas()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Drag(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(12, 3), RawInputModifiers.Shift);

        walk.Frames.ShouldAllBe(f => f.RelativeX != 0.5f && f.RelativeY == 2.25f);
        walk.Frames.ShouldAllBe(f => f.ShapesSave!.AARectSaves.First().X != 30.5f && f.ShapesSave!.AARectSaves.First().Y == 20.25f);
    }

    [AvaloniaFact]
    public async Task ShiftDragOfTwoShapes_LeavesTheLockedAxisAsItWas()
    {
        var (editor, path) = await OpenAsync(walk =>
            walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Orb", X = -30.5f, Y = 20.25f, Radius = 6 }));
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        var rect = frame.ShapesSave!.AARectSaves.First();
        var circle = frame.ShapesSave!.CircleSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);

        editor.Drag(editor.PreviewPointAt(30, 20), editor.PreviewPointAt(42, 21), RawInputModifiers.Shift);

        rect.X.ShouldNotBe(30.5f);
        circle.X.ShouldNotBe(-30.5f);
        (rect.Y, circle.Y).ShouldBe((20.25f, 20.25f));
    }

    [AvaloniaFact]
    public async Task VertexDragAlongX_LeavesYAsItWas()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[0].ShapesSave!.Add(new PolygonSave
        {
            Name = "Tri", X = -30, Y = -20,
            Points = [new() { X = 0.5f, Y = 9.25f }, new() { X = 10, Y = -10 }, new() { X = -10, Y = -10 }, new() { X = 0.5f, Y = 9.25f }],
        }));
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        var polygon = frame.ShapesSave!.PolygonSaves.First();
        editor.Expand(frame);
        editor.ClickRow(polygon);

        // Grabbed off its center, as a hand does.
        editor.Drag(editor.PreviewPointAt(-29, -10), editor.PreviewPointAt(-19, -10));

        polygon.Points[0].X.ShouldNotBe(0.5f);
        polygon.Points[0].Y.ShouldBe(9.25f);
    }

    [AvaloniaFact]
    public async Task CornerResizeAlongX_LeavesTheHeightAsItWas()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[0].ShapesSave!.AARectSaves.First().ScaleY = 0.25f);
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);

        Point corner = editor.PreviewPointAt(rect.X + rect.ScaleX, rect.Y + rect.ScaleY);
        editor.Drag(new Point(corner.X + 5, corner.Y - 5), new Point(corner.X + 45, corner.Y - 5));

        rect.ScaleX.ShouldBeGreaterThan(6f);
        (rect.Y, rect.ScaleY).ShouldBe((20.25f, 0.25f));
    }

    [AvaloniaFact]
    public async Task GuideDragAlongTheGuide_LeavesItWhereItWas()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Preview.SetGuides([-40.5f], []);
        Point start = editor.PreviewPointAt(-30, 40.5f / editor.Services.AppState.OffsetMultiplier);

        editor.Drag(start, new Point(start.X + 20, start.Y));

        editor.Preview.HGuides.ShouldBe([-40.5f]);
    }

    [AvaloniaFact]
    public async Task ResizeOfAShape_ReachesTheHandle()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);

        Point edge = editor.PreviewPointAt(rect.X + rect.ScaleX, rect.Y);
        editor.Drag(new Point(edge.X + 5, edge.Y), new Point(edge.X + 45, edge.Y));

        rect.ScaleX.ShouldBeGreaterThan(6f);
        (rect.Y, rect.ScaleY).ShouldBe((20.25f, 6f));
        editor.UndoLabels.Count.ShouldBe(1);
    }
}
