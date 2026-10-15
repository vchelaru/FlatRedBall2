using AnimationEditor.Core.Keyframes;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Transform and Color keyframe rows of the frame inspector (#1478): the diamond, the eight curve
/// icons, and the read-only effective values while a keyframe is off.
/// </summary>
public class KeyframeInspectorScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path, AnimationChainSave Chain)> OpenWalkAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave authored = AnimationEditorHarness.Chain(
            "Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        authored.Frames[0].RelativeX = 0;
        authored.Frames[1].RelativeX = 999;
        authored.Frames[2].RelativeX = 90;
        authored.Frames[0].OffsetInterpolation = "Linear";
        authored.Frames[0].Red = 0;
        authored.Frames[2].Red = 200;
        authored.Frames[0].ColorInterpolation = "Linear";
        string path = editor.WriteAchx("hero.achx", authored);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        return (editor, path, walk);
    }

    private static KeyframeControl Transform(AnimationEditorHarness editor) =>
        editor.Control<KeyframeControl>("PropTransformKeyframe");

    private static KeyframeControl ColorRow(AnimationEditorHarness editor) =>
        editor.Control<KeyframeControl>("PropColorKeyframe");

    private static void ScrollTo(AnimationEditorHarness editor, Control control) => editor.ScrollIntoView(control);

    [AvaloniaFact]
    public async Task TransformRow_ForAKeyframe_ShowsAFilledDiamond_AndAllEightCurves_NoneForALinearFrame()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);

            KeyframeControl row = Transform(editor);
            row.DiamondButton.IsChecked.ShouldBe(true);
            row.CurvesVisible.ShouldBeTrue();
            row.CurveButtons.Count.ShouldBe(8);
            row.SelectedCurve.ShouldBe(KeyframeCurveChoice.Snap, "an omitted curve is Snap");
            editor.Control<NumericUpDown>("PropRelX").IsReadOnly.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task FirstFrame_DiamondIsFilledAndDisabled()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[0]);

            KeyframeControl row = Transform(editor);
            row.DiamondButton.IsChecked.ShouldBe(true);
            row.DiamondButton.IsEnabled.ShouldBeFalse();
            row.SelectedCurve.ShouldBe(KeyframeCurveChoice.Linear);
        }
    }

    [AvaloniaFact]
    public async Task XAndY_ShareOneWrappingRow()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);

            WrapPanel x = editor.Control<NumericUpDown>("PropRelX").FindAncestorOfType<WrapPanel>()!;
            WrapPanel y = editor.Control<NumericUpDown>("PropRelY").FindAncestorOfType<WrapPanel>()!;
            y.ShouldBeSameAs(x);
            editor.PointIn(editor.Control<NumericUpDown>("PropRelX"), 0, 0).Y
                .ShouldBe(editor.PointIn(editor.Control<NumericUpDown>("PropRelY"), 0, 0).Y);
        }
    }

    [AvaloniaFact]
    public async Task ClickingTheDiamond_TurnsTheKeyframeOff_HidesCurves_AndShowsTheValueInEffect_ReadOnly()
    {
        var (editor, path, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);
            KeyframeControl row = Transform(editor);
            ScrollTo(editor, row);

            editor.Click(row.DiamondButton);

            walk.Frames[1].SkipOffsetKeyframe.ShouldBeTrue();
            walk.Frames[1].SkipColorKeyframe.ShouldBeFalse("the groups are independent");
            editor.UndoLabels[^1].ShouldBe("Disable Offset Keyframe");
            row.DiamondButton.IsChecked.ShouldBe(false);
            row.CurvesVisible.ShouldBeFalse();
            NumericUpDown x = editor.Control<NumericUpDown>("PropRelX");
            x.IsReadOnly.ShouldBeTrue();
            x.Value.ShouldBe(45m, "the Linear glide from 0 to 90 across two frames, at the start of the middle frame");
            walk.Frames[1].RelativeX.ShouldBe(999f, "the frame's own stored value is left alone");
            AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1].SkipOffsetKeyframe.ShouldBeTrue();

            editor.Press(Key.Z, RawInputModifiers.Control);

            walk.Frames[1].SkipOffsetKeyframe.ShouldBeFalse();
            Transform(editor).DiamondButton.IsChecked.ShouldBe(true);
            editor.Control<NumericUpDown>("PropRelX").IsReadOnly.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task KeyframeOff_TypingIntoTheReadOnlyField_ChangesNothing()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            walk.Frames[1].SkipOffsetKeyframe = true;
            editor.ClickRow(walk.Frames[1]);
            int undoCount = editor.UndoLabels.Count;

            editor.TypeNumber("PropRelX", "5");

            walk.Frames[1].RelativeX.ShouldBe(999f);
            editor.UndoLabels.Count.ShouldBe(undoCount);
        }
    }

    [AvaloniaFact]
    public async Task ClickingACurveIcon_WritesThePair_AndSnapWritesNothing()
    {
        var (editor, path, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);
            KeyframeControl row = Transform(editor);
            ScrollTo(editor, row);

            editor.Click(row.CurveButton(KeyframeCurveChoice.EaseInOut));

            walk.Frames[1].OffsetInterpolation.ShouldBe("Quadratic");
            walk.Frames[1].OffsetEasing.ShouldBe("InOut");
            editor.UndoLabels[^1].ShouldBe("Set Offset Curve: Ease In-Out");
            Transform(editor).SelectedCurve.ShouldBe(KeyframeCurveChoice.EaseInOut);
            var saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1];
            saved.OffsetInterpolation.ShouldBe("Quadratic");
            saved.OffsetEasing.ShouldBe("InOut");

            editor.Click(row.CurveButton(KeyframeCurveChoice.Snap));

            walk.Frames[1].OffsetInterpolation.ShouldBeNull();
            walk.Frames[1].OffsetEasing.ShouldBeNull();
            Transform(editor).SelectedCurve.ShouldBe(KeyframeCurveChoice.Snap);
        }
    }

    [AvaloniaFact]
    public async Task AStoredCurveOutsideTheEight_ShowsNothingSelected_AndSurvivesOtherEdits()
    {
        var (editor, path, walk) = await OpenWalkAsync();
        using (editor)
        {
            walk.Frames[1].OffsetInterpolation = "Cubic";
            walk.Frames[1].OffsetEasing = "In";
            editor.ClickRow(walk.Frames[1]);

            KeyframeControl row = Transform(editor);
            row.SelectedCurve.ShouldBeNull();
            row.CurveNote.ShouldBe("Cubic In");
            row.CurveButtons.ShouldAllBe(button => button.IsChecked != true);

            editor.TypeNumber("PropRelX", "12");

            walk.Frames[1].RelativeX.ShouldBe(12f);
            var saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1];
            saved.OffsetInterpolation.ShouldBe("Cubic");
            saved.OffsetEasing.ShouldBe("In");
            Transform(editor).SelectedCurve.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public async Task ColorRow_DiamondIsIndependent_AndOffMakesTheChannelsReadOnlyWithTheGlide()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);
            KeyframeControl row = ColorRow(editor);
            ScrollTo(editor, row);

            editor.Click(row.DiamondButton);

            walk.Frames[1].SkipColorKeyframe.ShouldBeTrue();
            walk.Frames[1].SkipOffsetKeyframe.ShouldBeFalse();
            editor.UndoLabels[^1].ShouldBe("Disable Color Keyframe");
            editor.Control<NumericUpDown>("PropRed").IsReadOnly.ShouldBeTrue();
            editor.Control<NumericUpDown>("PropAlpha").IsReadOnly.ShouldBeTrue();
            editor.Control<NumericUpDown>("PropRed").Value.ShouldBe(100m);
            ColorRow(editor).CurvesVisible.ShouldBeFalse();
            Transform(editor).CurvesVisible.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public async Task MultiSelect_DiamondClick_AppliesToEveryFrameAsOneUndoStep()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            editor.ClickRow(walk.Frames[1]);
            editor.ClickRow(walk.Frames[2], RawInputModifiers.Control);
            KeyframeControl row = Transform(editor);
            ScrollTo(editor, row);
            int before = editor.UndoLabels.Count;

            editor.Click(row.DiamondButton);

            walk.Frames[1].SkipOffsetKeyframe.ShouldBeTrue();
            walk.Frames[2].SkipOffsetKeyframe.ShouldBeTrue();
            editor.UndoLabels.Count.ShouldBe(before + 1);
            editor.UndoLabels[^1].ShouldBe("Disable Offset Keyframe");
        }
    }

    [AvaloniaFact]
    public async Task MultiSelect_MixedKeyframes_ShowAnIndeterminateDiamond_AndTheCurveIconsStay()
    {
        var (editor, _, walk) = await OpenWalkAsync();
        using (editor)
        {
            walk.Frames[2].SkipOffsetKeyframe = true;
            editor.ClickRow(walk.Frames[1]);
            editor.ClickRow(walk.Frames[2], RawInputModifiers.Control);

            KeyframeControl row = Transform(editor);
            row.DiamondButton.IsChecked.ShouldBeNull();
            row.CurvesVisible.ShouldBeTrue();
        }
    }
}
