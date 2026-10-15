using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The timeline's keyframe lanes follow the selection (#1489): a chain or frame shows the Sprite's Offset and
/// Color, a single shape shows that shape's Offset and Size labeled with its name, several shapes show none.
/// </summary>
public class ShapeTimelineLaneScenarioTests
{
    private const double SpriteDock = 52;
    private static readonly double TwoLanes = SpriteDock + 2 * KeyframeLaneBuilder.LaneHeight;

    // Three 1s frames. The Sprite's offset and color change (both Sprite lanes show), and "Sword" moves and grows
    // (both shape lanes show).
    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave Chain, AARectSave[] Rects)> OpenSwingAsync(bool nameShapes = true)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave authored = AnimationEditorHarness.Chain(
            "Swing", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        for (int i = 0; i < 3; i++)
        {
            authored.Frames[i].FrameLength = 1f;
            authored.Frames[i].RelativeX = i * 4;
            authored.Frames[i].Red = 100 + i * 50;
            authored.Frames[i].ShapesSave = new ShapesSave();
            authored.Frames[i].ShapesSave!.Add(new AARectSave { Name = nameShapes ? "Sword" : "", X = i * 30f, ScaleX = 10 + i * 10, ScaleY = 6 });
        }
        await editor.OpenAsync(editor.WriteAchx("hero.achx", authored));
        AnimationChainSave swing = editor.ChainNamed("Swing");
        editor.Expand(swing);
        foreach (var frame in swing.Frames) editor.Expand(frame);
        var loaded = swing.Frames.Select(f => f.ShapesSave!.AARectSaves.Single()).ToArray();
        return (editor, swing, loaded);
    }

    private static KeyframeLaneControl First(AnimationEditorHarness editor) => editor.Control<KeyframeLaneControl>("FirstLane");

    private static KeyframeLaneControl Second(AnimationEditorHarness editor) => editor.Control<KeyframeLaneControl>("SecondLane");

    private static string FirstLabel(AnimationEditorHarness editor) => editor.Control<TextBlock>("FirstLaneLabel").Text ?? "";

    private static string SecondLabel(AnimationEditorHarness editor) => editor.Control<TextBlock>("SecondLaneLabel").Text ?? "";

    private static double Dock(AnimationEditorHarness editor) => editor.Control<Grid>("TimelineDockGrid").Height;

    [AvaloniaFact]
    public async Task SelectingAChain_ShowsTheSpritesOffsetAndColorLanes()
    {
        var (editor, swing, _) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(swing);

            First(editor).IsVisible.ShouldBeTrue();
            Second(editor).IsVisible.ShouldBeTrue();
            FirstLabel(editor).ShouldBe("Offset");
            SecondLabel(editor).ShouldBe("Color");
            Dock(editor).ShouldBe(TwoLanes);
        }
    }

    [AvaloniaFact]
    public async Task SelectingAShape_ReplacesThemWithThatShapesOffsetAndSizeLanes_LabeledWithItsName()
    {
        var (editor, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[1]);

            FirstLabel(editor).ShouldBe("Sword Offset");
            SecondLabel(editor).ShouldBe("Sword Size");
            First(editor).Lane!.KeyframeXs.Count.ShouldBe(3);
            Second(editor).Lane!.KeyframeXs.Count.ShouldBe(3);
            Dock(editor).ShouldBe(TwoLanes);
        }
    }

    [AvaloniaFact]
    public async Task TheSameShapeLanesShowWhicheverFramesShapeIsSelected()
    {
        var (editor, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);
            var fromFirst = First(editor).Lane!.KeyframeXs.ToList();

            editor.ClickRow(rects[2]);

            First(editor).Lane!.KeyframeXs.ShouldBe(fromFirst);
            FirstLabel(editor).ShouldBe("Sword Offset");
        }
    }

    [AvaloniaFact]
    public async Task SelectingTheChainAgain_BringsTheSpriteLanesBack()
    {
        var (editor, swing, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);
            editor.ClickRow(swing);

            FirstLabel(editor).ShouldBe("Offset");
            SecondLabel(editor).ShouldBe("Color");

            editor.ClickRow(swing.Frames[1]);

            FirstLabel(editor).ShouldBe("Offset");
        }
    }

    [AvaloniaFact]
    public async Task SelectingTwoShapes_ShowsNoLanes_AndTheDockShrinks()
    {
        var (editor, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);
            editor.ClickRow(rects[1], RawInputModifiers.Control);

            First(editor).IsVisible.ShouldBeFalse();
            Second(editor).IsVisible.ShouldBeFalse();
            editor.Control<TextBlock>("FirstLaneLabel").IsVisible.ShouldBeFalse();
            Dock(editor).ShouldBe(SpriteDock);
        }
    }

    [AvaloniaFact]
    public async Task AnUnnamedShape_GetsNoLanes_NotTheSpriteLanes()
    {
        var (editor, _, rects) = await OpenSwingAsync(nameShapes: false);
        using (editor)
        {
            editor.ClickRow(rects[1]);

            First(editor).IsVisible.ShouldBeFalse();
            Second(editor).IsVisible.ShouldBeFalse();
            Dock(editor).ShouldBe(SpriteDock);
        }
    }

    [AvaloniaFact]
    public async Task AShapeKeyframeToggledOff_RemovesItsDiamondFromTheLane_AndUndoRestoresIt()
    {
        var (editor, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[1]);
            var offset = First(editor);
            offset.Lane!.KeyframeXs.Count.ShouldBe(3);
            KeyframeControl row = editor.Control<KeyframeControl>("PropRectOffsetKeyframe");
            editor.ScrollIntoView(row);

            editor.Click(row.DiamondButton);

            First(editor).Lane!.KeyframeXs.Count.ShouldBe(2);

            editor.Press(Key.Z, RawInputModifiers.Control);

            First(editor).Lane!.KeyframeXs.Count.ShouldBe(3);
        }
    }

    [AvaloniaFact]
    public async Task ALongShapeName_IsTrimmed_AndDoesNotWidenTheTransportColumn()
    {
        var (editor, _, rects) = await OpenSwingAsync();
        using (editor)
        {
            editor.ClickRow(rects[0]);
            Point cellsBefore = editor.Control<ItemsControl>("TimelineStrip").TranslatePoint(new Point(0, 0), editor.Window)!.Value;

            foreach (var frame in rects) frame.Name = "AVeryLongShapeNameThatCannotFitTheTransportColumn";
            editor.Services.ApplicationEvents.RaiseAnimationChainsChanged();
            editor.Layout();

            Point cellsAfter = editor.Control<ItemsControl>("TimelineStrip").TranslatePoint(new Point(0, 0), editor.Window)!.Value;
            FirstLabel(editor).ShouldBe("AVeryLongShapeNameThatCannotFitTheTransportColumn Offset");
            cellsAfter.X.ShouldBe(cellsBefore.X, 0.5);
        }
    }
}
