using AnimationEditor.Core.Keyframes;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Offset and Size keyframe rows of the shape inspector (#1478): the diamond, the eight curve icons, and the
/// read-only effective values while a keyframe is off.
/// </summary>
public class ShapeKeyframeInspectorScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path, AnimationChainSave Chain, AARectSave[] Rects)> OpenSwingAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave authored = AnimationEditorHarness.Chain(
            "Swing", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        var rects = new AARectSave[3];
        for (int i = 0; i < 3; i++)
        {
            authored.Frames[i].FrameLength = 1f;
            rects[i] = new AARectSave { Name = "Sword", X = i == 1 ? 999 : i * 45f, ScaleX = 10 + i * 10, ScaleY = 6 };
            authored.Frames[i].ShapesSave = new ShapesSave();
            authored.Frames[i].ShapesSave!.Add(rects[i]);
        }
        rects[0].OffsetInterpolation = "Linear";
        rects[0].SizeInterpolation = "Quadratic";
        rects[0].SizeEasing = "In";
        string path = editor.WriteAchx("hero.achx", authored);
        await editor.OpenAsync(path);
        AnimationChainSave swing = editor.ChainNamed("Swing");
        editor.Expand(swing);
        foreach (var frame in swing.Frames) editor.Expand(frame);
        // The harness reloads the file, so address the loaded shapes rather than the authored ones.
        var loaded = swing.Frames.Select(f => f.ShapesSave!.AARectSaves.Single()).ToArray();
        return (editor, path, swing, loaded);
    }

    private static KeyframeControl OffsetRow(AnimationEditorHarness editor) =>
        editor.Control<KeyframeControl>("PropRectOffsetKeyframe");

    private static KeyframeControl SizeRow(AnimationEditorHarness editor) =>
        editor.Control<KeyframeControl>("PropRectSizeKeyframe");

    [AvaloniaFact]
    public async Task RectInspector_ForAKeyframe_ShowsFilledDiamondsAndTheStoredCurves()
    {
        var (editor, _, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);

            OffsetRow(editor).DiamondButton.IsChecked.ShouldBe(true);
            OffsetRow(editor).SelectedCurve.ShouldBe(KeyframeCurveChoice.Linear);
            SizeRow(editor).SelectedCurve.ShouldBe(KeyframeCurveChoice.EaseIn);
            editor.Control<NumericUpDown>("PropRectX").IsReadOnly.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task FirstFrameHoldingTheName_DiamondIsFilledAndDisabled()
    {
        var (editor, _, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);

            OffsetRow(editor).DiamondButton.IsEnabled.ShouldBeFalse();
            SizeRow(editor).DiamondButton.IsEnabled.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task ClickingTheOffsetDiamond_TurnsOnlyThatGroupOff_AndShowsTheValueInEffect_ReadOnly()
    {
        var (editor, path, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[1]);
            KeyframeControl row = OffsetRow(editor);
            editor.ScrollIntoView(row);

            editor.Click(row.DiamondButton);

            rects[1].SkipOffsetKeyframe.ShouldBeTrue();
            rects[1].SkipSizeKeyframe.ShouldBeFalse("the groups are independent");
            editor.UndoLabels[^1].ShouldBe("Disable Shape Offset Keyframe");
            row.CurvesVisible.ShouldBeFalse();
            NumericUpDown x = editor.Control<NumericUpDown>("PropRectX");
            x.IsReadOnly.ShouldBeTrue();
            x.Value.ShouldBe(45m, "the Linear glide from 0 to 90 across two frames, at the start of the middle frame");
            editor.Control<NumericUpDown>("PropRectScaleX").IsReadOnly.ShouldBeFalse();
            rects[1].X.ShouldBe(999f, "the shape's own stored value is left alone");
            AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1].ShapesSave!.AARectSaves.Single()
                .SkipOffsetKeyframe.ShouldBeTrue();

            editor.Press(Key.Z, RawInputModifiers.Control);

            rects[1].SkipOffsetKeyframe.ShouldBeFalse();
            OffsetRow(editor).DiamondButton.IsChecked.ShouldBe(true);
            editor.Control<NumericUpDown>("PropRectX").IsReadOnly.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task ClickingACurveIcon_StoresThePair_ForThatGroupOnly()
    {
        var (editor, _, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[1]);
            KeyframeControl row = SizeRow(editor);
            editor.ScrollIntoView(row);

            editor.Click(row.CurveButton(KeyframeCurveChoice.Overshoot));

            rects[1].SizeInterpolation.ShouldBe("Back");
            rects[1].SizeEasing.ShouldBe("Out");
            rects[1].OffsetInterpolation.ShouldBeNull();
            editor.UndoLabels[^1].ShouldBe("Set Shape Size Curve: Overshoot");
        }
    }

    [AvaloniaFact]
    public async Task KeyframeOff_TypingIntoTheReadOnlyField_ChangesNothing()
    {
        var (editor, _, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            rects[1].SkipOffsetKeyframe = true;
            editor.ClickRow(rects[1]);
            int undoCount = editor.UndoLabels.Count;

            editor.TypeNumber("PropRectX", "5");

            rects[1].X.ShouldBe(999f);
            editor.UndoLabels.Count.ShouldBe(undoCount);
        }
    }

    [AvaloniaFact]
    public async Task EditingSize_WhileOffsetKeyframeIsOff_NeverWritesTheComputedOffsetBack()
    {
        var (editor, _, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            rects[1].SkipOffsetKeyframe = true;
            editor.ClickRow(rects[1]);

            editor.TypeNumber("PropRectScaleX", "33");

            rects[1].ScaleX.ShouldBe(33f);
            rects[1].X.ShouldBe(999f, "the field showed the computed 45, which must not become the stored value");
        }
    }
}
