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
    public async Task DragOfAFrameWhoseOffsetKeyframeIsOff_DoesNotChangeTheOffsetPlaybackIgnores()
    {
        var (editor, path) = await OpenAsync(walk =>
        {
            walk.Frames[1].SkipOffsetKeyframe = true;
            walk.Frames[1].RelativeX = walk.Frames[0].RelativeX;
            walk.Frames[1].RelativeY = walk.Frames[0].RelativeY;
        });
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        editor.ClickRow(frame);

        editor.Drag(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(12, 12));

        (frame.RelativeX, frame.RelativeY).ShouldBe((0.5f, 2.25f));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DragOfAShapeWhoseOffsetKeyframeIsOff_DoesNotChangeTheOffsetPlaybackIgnores()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[1].ShapesSave!.AARectSaves.First().SkipOffsetKeyframe = true);
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.ClickRow(frame);

        editor.Drag(editor.PreviewPointAt(30, 20), editor.PreviewPointAt(42, 32));

        (rect.X, rect.Y).ShouldBe((30.5f, 20.25f));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ResizeOfAShapeWhoseSizeKeyframeIsOff_DoesNotChangeTheSizePlaybackIgnores()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[1].ShapesSave!.AARectSaves.First().SkipSizeKeyframe = true);
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);

        Point edge = editor.PreviewPointAt(rect.X + rect.ScaleX, rect.Y);
        editor.Drag(new Point(edge.X + 5, edge.Y), new Point(edge.X + 45, edge.Y));

        (rect.ScaleX, rect.ScaleY).ShouldBe((6f, 6f));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DragOfAnAnimation_SkipsFramesWhoseOffsetKeyframeIsOff()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[1].SkipOffsetKeyframe = true);
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Drag(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(12, 12));

        walk.Frames[0].RelativeX.ShouldNotBe(0.5f);
        (walk.Frames[1].RelativeX, walk.Frames[1].RelativeY).ShouldBe((0.5f, 2.25f));
    }

    [AvaloniaFact]
    public async Task DragOfTwoShapes_SkipsTheOneWhoseOffsetKeyframeIsOff()
    {
        var (editor, path) = await OpenAsync(walk =>
        {
            foreach (var frame in walk.Frames)
                frame.ShapesSave!.Add(new CircleSave { Name = "Orb", X = -30.5f, Y = 20.25f, Radius = 6 });
            walk.Frames[1].ShapesSave!.CircleSaves.First().SkipOffsetKeyframe = true;
        });
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var rect = frame.ShapesSave!.AARectSaves.First();
        var circle = frame.ShapesSave!.CircleSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);

        editor.Drag(editor.PreviewPointAt(30, 20), editor.PreviewPointAt(42, 32));

        rect.X.ShouldNotBe(30.5f);
        (circle.X, circle.Y).ShouldBe((-30.5f, 20.25f));
    }

    [AvaloniaFact]
    public async Task ResizeOfARectangleWhoseOffsetKeyframeIsOff_DoesNotChangeTheOffsetPlaybackIgnores()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[1].ShapesSave!.AARectSaves.First().SkipOffsetKeyframe = true);
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var rect = frame.ShapesSave!.AARectSaves.First();
        editor.Expand(frame);
        editor.ClickRow(rect);

        Point edge = editor.PreviewPointAt(rect.X + rect.ScaleX, rect.Y);
        editor.Drag(new Point(edge.X + 5, edge.Y), new Point(edge.X + 45, edge.Y));

        (rect.X, rect.ScaleX).ShouldBe((30.5f, 6f));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ResizeOfAShapeWhoseSizeKeyframeIsOff_OffersNoHandle()
    {
        var (editor, path) = await OpenAsync(walk => walk.Frames[1].ShapesSave!.AARectSaves.First().SkipSizeKeyframe = true);
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        editor.Expand(frame);
        editor.ClickRow(frame.ShapesSave!.AARectSaves.First());

        var shown = editor.Preview.GetShapeInfosForTest().Single();

        (shown.IsSelected, shown.HandlesLocked).ShouldBe((true, true));
    }

    [AvaloniaFact]
    public async Task ClickOnAShapeWhoseOffsetKeyframeIsOff_HitsWhereItIsDrawn()
    {
        var (editor, path) = await OpenAsync(walk =>
        {
            var rect = walk.Frames[1].ShapesSave!.AARectSaves.First();
            rect.SkipOffsetKeyframe = true;
            rect.X = -30.5f;
        });
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        editor.ClickRow(frame);

        editor.ClickAt(editor.PreviewPointAt(-30, 20));
        editor.Services.SelectedState.SelectedShape.ShouldBeNull();

        editor.ClickAt(editor.PreviewPointAt(30, 20));
        editor.Services.SelectedState.SelectedShape.ShouldBe(frame.ShapesSave!.AARectSaves.First());
    }

    [AvaloniaFact]
    public async Task VertexDragOfAPolygonWhoseOffsetKeyframeIsOff_MovesTheVertexWhereItIsDrawn()
    {
        var (editor, path) = await OpenAsync(walk =>
        {
            foreach (var frame in walk.Frames)
                frame.ShapesSave!.Add(new PolygonSave
                {
                    Name = "Tri", X = -30, Y = -20,
                    Points = [new() { X = 0, Y = 10 }, new() { X = 10, Y = -10 }, new() { X = -10, Y = -10 }, new() { X = 0, Y = 10 }],
                });
            var polygon = walk.Frames[1].ShapesSave!.PolygonSaves.First();
            polygon.SkipOffsetKeyframe = true;
            polygon.X = 40;
        });
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[1];
        var polygon = frame.ShapesSave!.PolygonSaves.First();
        editor.Expand(frame);
        editor.ClickRow(polygon);

        editor.Drag(editor.PreviewPointAt(-30, -10), editor.PreviewPointAt(-26, -10));

        (polygon.Points[0].X, polygon.Points[0].Y).ShouldBe((4f, 10f));
        polygon.X.ShouldBe(40f);
    }

    [AvaloniaFact]
    public async Task DragOfAnAnimationFromAFrameWhoseOffsetKeyframeIsOff_GrabsTheSpriteWhereItIsDrawn()
    {
        var (editor, path) = await OpenAsync(walk =>
        {
            walk.Frames[1].SkipOffsetKeyframe = true;
            walk.Frames[1].RelativeX = 200;
        });
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Preview.PauseAutoPlayback();
        editor.Preview.Playback.SeekToFrame(1);
        editor.Layout();

        editor.Drag(editor.PreviewPointAt(0, 2), editor.PreviewPointAt(12, 12));

        walk.Frames[0].RelativeX.ShouldNotBe(0.5f);
        walk.Frames[1].RelativeX.ShouldBe(200f);
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
