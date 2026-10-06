using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #1325: numeric inspector fields evaluate arithmetic. A leading operator edits each selected
/// item's own value, so a mixed multi-selection stays mixed.
/// </summary>
public class NumericMathScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AARectSave First, AARectSave Second)> OpenTwoRects(float firstX, float secondX)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave source = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        source.Frames[0].ShapesSave = new ShapesSave();
        source.Frames[0].ShapesSave!.Add(new AARectSave { Name = "A", X = firstX, ScaleX = 4, ScaleY = 4 });
        source.Frames[1].ShapesSave = new ShapesSave();
        source.Frames[1].ShapesSave!.Add(new AARectSave { Name = "B", X = secondX, ScaleX = 4, ScaleY = 4 });
        await editor.OpenAsync(editor.WriteAchx("hero.achx", source));

        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.Expand(walk.Frames[1]);
        return (editor, walk.Frames[0].ShapesSave!.AARectSaves.First(), walk.Frames[1].ShapesSave!.AARectSaves.First());
    }

    [AvaloniaFact]
    public async Task RectX_SingleSelect_Expression_SetsResult()
    {
        var (editor, rect, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(rect);

            editor.TypeNumber("PropRectX", "3 + 4");

            rect.X.ShouldBe(7);
        }
    }

    [AvaloniaFact]
    public async Task RectX_SingleSelect_LeadingOperator_AppliesToCurrentValue()
    {
        var (editor, rect, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(rect);

            editor.TypeNumber("PropRectX", "* 3");

            rect.X.ShouldBe(30);
        }
    }

    [AvaloniaFact]
    public async Task RectX_MultiSelect_Expression_SetsEveryValue()
    {
        var (editor, first, second) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(first);
            editor.ClickRow(second, RawInputModifiers.Control);

            editor.TypeNumber("PropRectX", "3 + 4");

            first.X.ShouldBe(7);
            second.X.ShouldBe(7);
        }
    }

    [AvaloniaFact]
    public async Task RectX_MixedMultiSelect_LeadingOperator_AppliesToEachValue_AsOneUndoStep()
    {
        var (editor, first, second) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(first);
            editor.ClickRow(second, RawInputModifiers.Control);

            editor.TypeNumber("PropRectX", "+ 4");

            first.X.ShouldBe(14);
            second.X.ShouldBe(24);

            editor.Press(Key.Z, RawInputModifiers.Control);
            first.X.ShouldBe(10);
            second.X.ShouldBe(20);
        }
    }

    [AvaloniaFact]
    public async Task RectX_InvalidExpression_LeavesValueUnchanged()
    {
        var (editor, rect, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(rect);

            editor.TypeNumber("PropRectX", "3 +");

            rect.X.ShouldBe(10);
        }
    }

    [AvaloniaFact]
    public async Task FrameLength_MixedMultiSelect_LeadingOperator_AppliesToEachValue()
    {
        var (editor, _, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            AnimationChainSave walk = editor.ChainNamed("Walk");
            walk.Frames[0].FrameLength = 0.1f;
            walk.Frames[1].FrameLength = 0.25f;
            editor.ClickRow(walk.Frames[0]);
            editor.ClickRow(walk.Frames[1], RawInputModifiers.Control);

            editor.TypeFlanker("PropFrameLen", "* 2");

            walk.Frames[0].FrameLength.ShouldBe(0.2f);
            walk.Frames[1].FrameLength.ShouldBe(0.5f);
        }
    }
}
