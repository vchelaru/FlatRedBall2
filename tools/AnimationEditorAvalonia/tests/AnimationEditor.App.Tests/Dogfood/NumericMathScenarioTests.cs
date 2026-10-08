using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AnimationEditor.Views.Controls;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

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

    public enum Leave { Enter, Tab, ShiftTab, FocusElsewhere, EnterThenFocusElsewhere }

    // Every way an edit leaves a numeric box must commit it once.
    private static void TypeAndLeave(AnimationEditorHarness editor, string field, string text, Leave how)
    {
        bool flanker = field == "PropFrameLen";
        Key key = how is Leave.Tab or Leave.ShiftTab ? Key.Tab : Key.Enter;
        RawInputModifiers modifiers = how == Leave.ShiftTab ? RawInputModifiers.Shift : RawInputModifiers.None;
        if (how == Leave.FocusElsewhere)
        {
            // Type without committing, then move focus away.
            Control box = flanker ? editor.Control<FlankerNumericField>(field) : editor.Control<NumericUpDown>(field);
            TextBox text_ = box.GetVisualDescendants().OfType<TextBox>().First();
            text_.Focus();
            editor.Layout();
            text_.SelectAll();
            editor.Type(text);
        }
        else if (flanker)
        {
            editor.TypeFlanker(field, text, key, modifiers);
        }
        else
        {
            editor.TypeNumber(field, text, key, modifiers);
        }

        if (how is Leave.FocusElsewhere or Leave.EnterThenFocusElsewhere)
        {
            // Another field takes focus, as a click on it would.
            FocusField(editor, field.StartsWith("PropRect") ? "PropRectY" : "PropRelX");
        }
    }

    private static void FocusField(AnimationEditorHarness editor, string field)
    {
        editor.Control<NumericUpDown>(field).GetVisualDescendants().OfType<TextBox>().First().Focus().ShouldBeTrue();
        editor.Layout();
    }

    // Two frames whose red, green and alpha all differ, both selected, so every color field is mixed.
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave First, AnimationFrameSave Second)> OpenTwoMixedColorFrames()
    {
        var (editor, _, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave first = walk.Frames[0];
        AnimationFrameSave second = walk.Frames[1];
        first.Red = 100;
        second.Red = 200;
        first.Green = 10;
        second.Green = 20;
        first.Alpha = 30;
        second.Alpha = 100;
        editor.ClickRow(first);
        editor.ClickRow(second, RawInputModifiers.Control);
        return (editor, first, second);
    }

    [AvaloniaTheory]
    [InlineData(Leave.Enter)]
    [InlineData(Leave.Tab)]
    [InlineData(Leave.ShiftTab)]
    [InlineData(Leave.FocusElsewhere)]
    public async Task ColorChannel_MixedMultiSelect_RelativeEdit_AppliesToEachClampedAsOneUndoStep(Leave how)
    {
        var (editor, first, second) = await OpenTwoMixedColorFrames();
        using (editor)
        {
            TypeAndLeave(editor, "PropRed", "+ 100", how);

            first.Red.ShouldBe(200);
            second.Red.ShouldBe(255, "clamped to the channel maximum");
            first.Green.ShouldBe(10, "an untouched mixed channel survives");
            second.Green.ShouldBe(20, "an untouched mixed channel survives");
            editor.UndoLabels.Count.ShouldBe(1);

            TypeAndLeave(editor, "PropAlpha", "- 50", how);

            first.Alpha.ShouldBe(0, "clamped to the channel minimum");
            second.Alpha.ShouldBe(50);
            editor.UndoLabels.Count.ShouldBe(2);
        }
    }

    [AvaloniaFact]
    public async Task ColorChannel_MixedField_FocusLeavesWithoutTyping_LeavesChannelAlone()
    {
        var (editor, first, second) = await OpenTwoMixedColorFrames();
        using (editor)
        {
            FocusField(editor, "PropGreen");
            FocusField(editor, "PropRelX");

            first.Green.ShouldBe(10);
            second.Green.ShouldBe(20);
            editor.UndoLabels.ShouldBeEmpty();
        }
    }

    [AvaloniaTheory]
    [InlineData(Leave.Enter)]
    [InlineData(Leave.Tab)]
    [InlineData(Leave.ShiftTab)]
    [InlineData(Leave.FocusElsewhere)]
    [InlineData(Leave.EnterThenFocusElsewhere)]
    public async Task RectX_EveryWayOut_CommitsRelativeEditOnce(Leave how)
    {
        var (editor, first, second) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            editor.ClickRow(first);
            TypeAndLeave(editor, "PropRectX", "+ .5", how);
            first.X.ShouldBe(10.5f, "single select");

            editor.ClickRow(second, RawInputModifiers.Control);
            TypeAndLeave(editor, "PropRectX", "+ .5", how);
            first.X.ShouldBe(11f, "mixed multi-select, first");
            second.X.ShouldBe(20.5f, "mixed multi-select, second");

            TypeAndLeave(editor, "PropRectX", "3 + 4", how);
            first.X.ShouldBe(7f, "absolute over mixed");
            second.X.ShouldBe(7f, "absolute over mixed");
        }
    }

    [AvaloniaTheory]
    [InlineData(Leave.Enter)]
    [InlineData(Leave.Tab)]
    [InlineData(Leave.ShiftTab)]
    [InlineData(Leave.FocusElsewhere)]
    [InlineData(Leave.EnterThenFocusElsewhere)]
    public async Task FrameLength_EveryWayOut_CommitsRelativeEditOnce(Leave how)
    {
        var (editor, _, _) = await OpenTwoRects(firstX: 10, secondX: 20);
        using (editor)
        {
            AnimationChainSave walk = editor.ChainNamed("Walk");
            walk.Frames[0].FrameLength = 0.1f;
            walk.Frames[1].FrameLength = 0.25f;
            editor.ClickRow(walk.Frames[0]);
            TypeAndLeave(editor, "PropFrameLen", "+ .5", how);
            walk.Frames[0].FrameLength.ShouldBe(0.6f, 0.0001f, "single select");

            editor.ClickRow(walk.Frames[1], RawInputModifiers.Control);
            TypeAndLeave(editor, "PropFrameLen", "+ .5", how);
            walk.Frames[0].FrameLength.ShouldBe(1.1f, 0.0001f, "mixed multi-select, first");
            walk.Frames[1].FrameLength.ShouldBe(0.75f, 0.0001f, "mixed multi-select, second");
        }
    }
}
