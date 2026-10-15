using AnimationEditor.Core.Keyframes;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Points keyframe row of the polygon inspector (#1484): the diamond, the eight curve icons, and the read-only
/// effective vertex values while the keyframe is off.
/// </summary>
public class PolygonPointsInspectorScenarioTests
{
    private static PolygonSave Triangle(float dx)
    {
        var poly = new PolygonSave { Name = "Blade" };
        poly.Points.Add(new Vector2Save { X = dx, Y = 0 });
        poly.Points.Add(new Vector2Save { X = dx + 10, Y = 0 });
        poly.Points.Add(new Vector2Save { X = dx + 10, Y = 10 });
        return poly;
    }

    private static async Task<(AnimationEditorHarness Editor, string Path, PolygonSave[] Polys)> OpenSwingAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave authored = AnimationEditorHarness.Chain(
            "Swing", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        for (int i = 0; i < 3; i++)
        {
            authored.Frames[i].FrameLength = 1f;
            authored.Frames[i].ShapesSave = new ShapesSave();
            authored.Frames[i].ShapesSave!.Add(Triangle(i == 1 ? 999 : i * 45f));
        }
        authored.Frames[0].ShapesSave!.PolygonSaves.Single().PointsInterpolation = "Linear";
        string path = editor.WriteAchx("hero.achx", authored);
        await editor.OpenAsync(path);
        AnimationChainSave swing = editor.ChainNamed("Swing");
        editor.Expand(swing);
        foreach (var frame in swing.Frames) editor.Expand(frame);
        var loaded = swing.Frames.Select(f => f.ShapesSave!.PolygonSaves.Single()).ToArray();
        return (editor, path, loaded);
    }

    private static KeyframeControl PointsRow(AnimationEditorHarness editor) =>
        editor.Control<KeyframeControl>("PropPolygonPointsKeyframe");

    [AvaloniaFact]
    public async Task PolygonInspector_ForAKeyframe_ShowsAFilledDiamondAndTheStoredCurve()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(polys[0]);

            PointsRow(editor).DiamondButton.IsChecked.ShouldBe(true);
            PointsRow(editor).DiamondButton.IsEnabled.ShouldBeFalse("the first frame holding the name is always a keyframe");
            PointsRow(editor).SelectedCurve.ShouldBe(KeyframeCurveChoice.Linear);
            editor.Control<NumericUpDown>("PropPolygonVertex0X").IsReadOnly.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task ClickingThePointsDiamond_TurnsOnlyThePointsGroupOff_AndShowsTheGlidedVerticesReadOnly()
    {
        var (editor, path, polys) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(polys[1]);
            KeyframeControl row = PointsRow(editor);
            editor.ScrollIntoView(row);

            editor.Click(row.DiamondButton);

            polys[1].SkipPointsKeyframe.ShouldBeTrue();
            polys[1].SkipOffsetKeyframe.ShouldBeFalse("the groups are independent");
            editor.UndoLabels[^1].ShouldBe("Disable Shape Points Keyframe");
            row.CurvesVisible.ShouldBeFalse();
            NumericUpDown vertex = editor.Control<NumericUpDown>("PropPolygonVertex0X");
            vertex.IsReadOnly.ShouldBeTrue();
            vertex.Value.ShouldBe(45m, "the Linear glide from 0 to 90 across two frames, at the start of the middle frame");
            editor.Control<Button>("PropPolygonAddVertex").IsEnabled.ShouldBeFalse();
            polys[1].Points[0].X.ShouldBe(999f, "the polygon's own stored points are left alone");
            AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1].ShapesSave!.PolygonSaves.Single()
                .SkipPointsKeyframe.ShouldBeTrue();

            editor.Press(Key.Z, RawInputModifiers.Control);

            polys[1].SkipPointsKeyframe.ShouldBeFalse();
            PointsRow(editor).DiamondButton.IsChecked.ShouldBe(true);
            editor.Control<NumericUpDown>("PropPolygonVertex0X").IsReadOnly.ShouldBeFalse();
            editor.Control<Button>("PropPolygonAddVertex").IsEnabled.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public async Task ClickingACurveIcon_StoresThePair_ForThePointsGroupOnly()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(polys[1]);
            KeyframeControl row = PointsRow(editor);
            editor.ScrollIntoView(row);

            editor.Click(row.CurveButton(KeyframeCurveChoice.Overshoot));

            polys[1].PointsInterpolation.ShouldBe("Back");
            polys[1].PointsEasing.ShouldBe("Out");
            polys[1].OffsetInterpolation.ShouldBeNull();
            editor.UndoLabels[^1].ShouldBe("Set Shape Points Curve: Overshoot");
        }
    }

    [AvaloniaFact]
    public async Task KeyframeOff_TypingIntoAReadOnlyVertex_ChangesNothing()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            polys[1].SkipPointsKeyframe = true;
            editor.ClickRow(polys[1]);
            int undoCount = editor.UndoLabels.Count;

            editor.TypeNumber("PropPolygonVertex0X", "5");

            polys[1].Points[0].X.ShouldBe(999f);
            editor.UndoLabels.Count.ShouldBe(undoCount);
        }
    }

    [AvaloniaFact]
    public async Task EditingOffset_WhilePointsKeyframeIsOff_NeverWritesTheComputedVerticesBack()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            polys[1].SkipPointsKeyframe = true;
            editor.ClickRow(polys[1]);

            editor.TypeNumber("PropPolygonX", "33");

            polys[1].X.ShouldBe(33f);
            polys[1].Points[0].X.ShouldBe(999f, "the rows showed the computed 45, which must not become the stored value");
        }
    }

    [AvaloniaFact]
    public async Task SelectingAPolygon_ShowsItsOffsetAndPointsLanes_LabeledWithItsName()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(polys[1]);

            editor.Control<TextBlock>("FirstLaneLabel").Text.ShouldBe("Blade Offset");
            editor.Control<TextBlock>("SecondLaneLabel").Text.ShouldBe("Blade Points");
            editor.Control<KeyframeLaneControl>("SecondLane").Lane!.KeyframeXs.Count.ShouldBe(3);
            editor.Control<KeyframeLaneControl>("FirstLane").Lane.ShouldBeNull("the offsets never differ, so the Offset lane has nothing to show");
        }
    }

    [AvaloniaFact]
    public async Task TurningThePointsKeyframeOff_DropsItsDiamondFromThePointsLane()
    {
        var (editor, _, polys) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(polys[1]);
            KeyframeControl row = PointsRow(editor);
            editor.ScrollIntoView(row);

            editor.Click(row.DiamondButton);

            editor.Control<KeyframeLaneControl>("SecondLane").Lane!.KeyframeXs.Count.ShouldBe(2);
        }
    }
}
