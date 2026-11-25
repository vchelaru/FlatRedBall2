using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Values committed one after the other in the inspector are separate undo steps.</summary>
public class UndoCoalescingScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Round", Radius = 4 });
        walk.Frames[0].ShapesSave!.Add(new PolygonSave { Name = "Outline", Points = [new() { X = 0, Y = 0 }, new() { X = 4, Y = 0 }, new() { X = 0, Y = 4 }, new() { X = 0, Y = 0 }] });
        string path = editor.WriteAchx("hero.achx", walk);
        await editor.OpenAsync(path);
        AnimationFrameSave frame = editor.ChainNamed("Walk").Frames[0];
        editor.Expand(editor.ChainNamed("Walk"));
        return (editor, frame, path);
    }

    [AvaloniaFact]
    public async Task RedThenGreen_UndoTakesBackOnlyGreen()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(frame);

        editor.TypeNumber("PropRed", "10");
        editor.TypeNumber("PropGreen", "20");
        frame.Green.ShouldBe(20);
        editor.UndoManager.Undo();

        frame.Red.ShouldBe(10);
    }

    [AvaloniaFact]
    public async Task AlphaTwice_UndoTakesBackOnlyTheSecond()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(frame);

        editor.TypeNumber("PropAlpha", "10");
        editor.TypeNumber("PropAlpha", "20");
        editor.UndoManager.Undo();

        frame.Alpha.ShouldBe(10);
    }

    [AvaloniaTheory]
    [InlineData(0, "Rect")]
    [InlineData(1, "Polygon")]
    [InlineData(2, "Circle")]
    public async Task ShapeNameThenX_UndoTakesBackOnlyX(int index, string kind)
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        var shape = (ShapeSave)frame.ShapesSave!.Shapes[index];
        editor.Expand(frame);
        editor.ClickRow(shape);

        editor.TypeText($"Prop{kind}Name", "Hurt");
        shape.Name.ShouldBe("Hurt");
        editor.TypeNumber($"Prop{kind}X", "7");
        editor.UndoManager.Undo();

        (shape.Name, shape.X).ShouldBe(("Hurt", 0f));
    }

    [AvaloniaFact]
    public async Task RenameInTheTreeTwice_ThenX_AreThreeSteps()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        var polygon = (PolygonSave)frame.ShapesSave!.Shapes[1];
        editor.Expand(frame);

        foreach (string name in new[] { "A", "B" })
        {
            editor.ClickRow(polygon);
            editor.Press(Key.F2);
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Type(name);
            editor.Press(Key.Enter);
        }
        editor.TypeNumber("PropPolygonX", "7");
        (polygon.Name, polygon.X).ShouldBe(("B", 7f));

        editor.UndoManager.Undo();
        (polygon.Name, polygon.X).ShouldBe(("B", 0f));
        editor.UndoManager.Undo();
        polygon.Name.ShouldBe("A");
    }
}
