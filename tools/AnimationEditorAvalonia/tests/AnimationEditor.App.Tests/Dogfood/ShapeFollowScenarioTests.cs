using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// With a shape selected, a timeline click that lands on a frame holding a same-named shape
/// selects that shape there (#1924), so a hitbox can be followed across frames.
/// </summary>
public class ShapeFollowScenarioTests
{
    // Walk: three equal frames. Frames 0 and 1 hold a rectangle "Hit"; frame 2 holds none.
    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave Walk)> OpenAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        foreach (int i in new[] { 0, 1 })
        {
            walk.Frames[i].ShapesSave = new ShapesSave();
            walk.Frames[i].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        }
        await editor.OpenAsync(editor.WriteAchx("hero.achx", walk));
        AnimationChainSave loaded = editor.ChainNamed("Walk");
        editor.ClickRow(loaded);
        if (editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }
        return (editor, loaded);
    }

    private static void SelectShapeOnFrame0(AnimationEditorHarness editor, AnimationChainSave walk)
    {
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.ClickRow(walk.Frames[0].ShapesSave!.AARectSaves.Single());
    }

    private static void ClickTimelineFrame(AnimationEditorHarness editor, int frame, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        ItemsControl strip = editor.Control<ItemsControl>("TimelineStrip");
        Control cell = (Control)strip.ItemsPanelRoot!.Children[frame];
        editor.ClickAt(editor.PointIn(cell, cell.Bounds.Width / 2, cell.Bounds.Height / 2), modifiers);
    }

    [AvaloniaFact]
    public async Task TimelineClickOnAFrameWithTheSameNamedShape_SelectsThatShape()
    {
        var (editor, walk) = await OpenAsync();
        using (editor)
        {
            SelectShapeOnFrame0(editor, walk);

            ClickTimelineFrame(editor, 1);

            editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
            editor.Services.SelectedState.SelectedShape.ShouldBeSameAs(walk.Frames[1].ShapesSave!.AARectSaves.Single());
            editor.UndoLabels.ShouldBeEmpty("selection is not undoable");
        }
    }

    [AvaloniaFact]
    public async Task TimelineClickOnAFrameWithoutThatShape_SelectsJustTheFrame()
    {
        var (editor, walk) = await OpenAsync();
        using (editor)
        {
            SelectShapeOnFrame0(editor, walk);

            ClickTimelineFrame(editor, 2);

            editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[2]);
            editor.Services.SelectedState.SelectedShape.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public async Task TimelineClickWithNoShapeSelected_SelectsJustTheFrame()
    {
        var (editor, walk) = await OpenAsync();
        using (editor)
        {
            ClickTimelineFrame(editor, 1);

            editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
            editor.Services.SelectedState.SelectedShape.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public async Task ModifierClickOnTheTimeline_StillSelectsJustTheFrame()
    {
        var (editor, walk) = await OpenAsync();
        using (editor)
        {
            SelectShapeOnFrame0(editor, walk);

            ClickTimelineFrame(editor, 1, RawInputModifiers.Control);

            editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
            editor.Services.SelectedState.SelectedShape.ShouldBeNull();
        }
    }
}
