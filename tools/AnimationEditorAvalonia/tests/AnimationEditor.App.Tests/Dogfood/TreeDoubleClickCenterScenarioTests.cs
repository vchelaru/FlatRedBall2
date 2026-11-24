using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Double-clicking a tree row brings the preview to that row's object (#1925).</summary>
public class TreeDoubleClickCenterScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame, AARectSave Rect)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        rect.X = 30; rect.Y = -10;
        frame.RelativeX = 12; frame.RelativeY = 7;
        editor.Expand(frame);
        return (editor, frame, rect);
    }

    /// <summary>A point on the row's empty space left of the name (the icon), where a double-click is not on the label.</summary>
    private static Avalonia.Point RowBlankPoint(AnimationEditorHarness editor, object data)
    {
        var row = editor.RowFor(data);
        var label = row.GetVisualDescendants().OfType<TextBlock>().First(b => b.Name == "RowHeaderLabel" && ReferenceEquals(b.DataContext, row.DataContext));
        var y = editor.RowHeaderPoint(data).Y;
        var x = label.TranslatePoint(new Avalonia.Point(-8, 0), editor.Window)!.Value.X; // the icon, left of the name text
        return new Avalonia.Point(x, y);
    }

    [AvaloniaFact]
    public async Task DoubleClickShapeLabel_StartsRename_AndDoesNotCenter()
    {
        var (editor, _, rect) = await OpenAsync();
        using var _e = editor;
        editor.DoubleClickRow(rect);
        editor.Nodes.Single(n => ReferenceEquals(n.Data, rect)).IsEditing.ShouldBeTrue();
        editor.Preview.PanOffset.ShouldBe((0f, 0f));
    }

    [AvaloniaFact]
    public async Task DoubleClickFrameRow_CentersPreviewOnFrameOffset()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _e = editor;
        editor.DoubleClickRow(frame); editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Preview.PanOffset.X.ShouldBe(-12f * editor.Preview.Zoom, 0.01f);
        editor.Preview.PanOffset.Y.ShouldBe(7f * editor.Preview.Zoom, 0.01f);
    }

    [AvaloniaFact]
    public async Task DoubleClickShapeRowBlankSpace_CentersPreviewOnShape()
    {
        var (editor, _, rect) = await OpenAsync();
        using var _e = editor;
        editor.DoubleClickAt(RowBlankPoint(editor, rect));
        editor.Preview.PanOffset.X.ShouldBe(-30f * editor.Preview.Zoom, 0.01f);
        editor.Preview.PanOffset.Y.ShouldBe(-10f * editor.Preview.Zoom, 0.01f);
    }

    [AvaloniaFact]
    public async Task DoubleClickPolygonRowBlankSpace_CentersPreviewOnItsPoints_NotItsOrigin()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _e = editor;
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Polygon");
        PolygonSave poly = frame.ShapesSave!.PolygonSaves.Single();
        poly.X = 0; poly.Y = 0;
        poly.Points.Clear();
        poly.Points.Add(new Vector2Save { X = 40, Y = 20 });
        poly.Points.Add(new Vector2Save { X = 60, Y = 20 });
        poly.Points.Add(new Vector2Save { X = 60, Y = 40 });
        editor.Expand(frame);
        editor.DoubleClickAt(RowBlankPoint(editor, poly));
        editor.Preview.PanOffset.X.ShouldBe(-50f * editor.Preview.Zoom, 0.01f);
        editor.Preview.PanOffset.Y.ShouldBe(30f * editor.Preview.Zoom, 0.01f);
    }
}
