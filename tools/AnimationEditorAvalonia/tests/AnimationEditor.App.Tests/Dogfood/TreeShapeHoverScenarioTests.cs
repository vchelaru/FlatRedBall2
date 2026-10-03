using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Issue #1297: hovering a shape row in the tree outlines that shape in the preview, the same
/// hover style as mousing over the shape itself.
/// </summary>
public class TreeShapeHoverScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame, AARectSave First, AARectSave Second)> OpenWithTwoRectsAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        AnimationChainSave chain = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32));
        AnimationFrameSave frame = chain.Frames[0];
        AARectSave first = new AARectSave { Name = "A", ScaleX = 4, ScaleY = 4 };
        AARectSave second = new AARectSave { Name = "B", X = 20, ScaleX = 4, ScaleY = 4 };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Add(first);
        frame.ShapesSave.Add(second);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", chain));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Expand(walk.Frames[0]);
        return (editor, walk.Frames[0], walk.Frames[0].ShapesSave!.AARectSaves.First(), walk.Frames[0].ShapesSave!.AARectSaves.Last());
    }

    [AvaloniaFact]
    public async Task HoveringAShapeRow_OutlinesThatShapeInThePreview()
    {
        (AnimationEditorHarness editor, _, AARectSave first, AARectSave second) = await OpenWithTwoRectsAsync();
        using (editor)
        {
            editor.Hover(editor.RowHeaderPoint(second));

            var hovered = editor.Preview.GetShapeInfosForTest().Where(i => i.IsHovered).ToList();
            hovered.Count.ShouldBe(1);
            hovered[0].X.ShouldBe(second.X);
        }
    }

    [AvaloniaFact]
    public async Task MovingOffTheShapeRow_ClearsThePreviewHover()
    {
        (AnimationEditorHarness editor, AnimationFrameSave frame, AARectSave first, _) = await OpenWithTwoRectsAsync();
        using (editor)
        {
            editor.Hover(editor.RowHeaderPoint(first));
            editor.Preview.GetShapeInfosForTest().ShouldContain(i => i.IsHovered);

            editor.Hover(editor.WireframeRectOf(frame).Center);

            editor.Preview.GetShapeInfosForTest().ShouldNotContain(i => i.IsHovered);
        }
    }

    [AvaloniaFact]
    public async Task HoveringTheSelectedShapesRow_AddsNoHover()
    {
        (AnimationEditorHarness editor, _, AARectSave first, _) = await OpenWithTwoRectsAsync();
        using (editor)
        {
            editor.ClickRow(first);

            editor.Hover(editor.RowHeaderPoint(first));

            editor.Preview.GetShapeInfosForTest().ShouldNotContain(i => i.IsHovered);
        }
    }
}
