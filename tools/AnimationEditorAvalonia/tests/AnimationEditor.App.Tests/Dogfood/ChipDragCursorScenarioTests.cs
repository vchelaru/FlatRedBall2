using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>While a library chip is dragged, the mouse cursor and a floating card say whether a drop would do anything.</summary>
public class ChipDragCursorScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static Point Lane(AnimationEditorHarness editor, int lane)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        double y = timeline.Viewport.LaneTop(lane) + TimelineViewport.LaneHeight / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(1.0), y);
    }

    [AvaloniaFact]
    public async Task DraggingAChip_ShowsCopyOverALane_NotAllowedOffTheTimeline_AndClearsAfterTheDrop()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Walk")!;
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        Point start = editor.CenterOf(chip);

        editor.Window.MouseMove(start, RawInputModifiers.None);
        editor.Window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);

        editor.Window.MouseMove(editor.PointIn(chip, chip.Bounds.Width / 2, chip.Bounds.Height + 40), RawInputModifiers.LeftMouseButton);
        chip.DragCursor.ShouldBe(ChipDragCursor.NotAllowed);

        editor.Window.MouseMove(Lane(editor, 0), RawInputModifiers.LeftMouseButton);
        chip.DragCursor.ShouldBe(ChipDragCursor.Copy);
        FloatingCardText(editor)!.ShouldContain("Add to Body");

        editor.Window.MouseMove(Lane(editor, 1), RawInputModifiers.LeftMouseButton);
        FloatingCardText(editor)!.ShouldContain("Add as a new track");

        editor.Window.MouseMove(editor.PointIn(timeline, 300, 5), RawInputModifiers.LeftMouseButton);
        chip.DragCursor.ShouldBe(ChipDragCursor.NotAllowed);

        editor.Window.MouseUp(Lane(editor, 0), MouseButton.Left, RawInputModifiers.None);
        editor.Layout();
        chip.DragCursor.ShouldBeNull();
        FloatingCardText(editor).ShouldBeNull();
    }

    // The ghost card is the only overlay child that reads "Add ..." or "Drop on a track".
    private static string? FloatingCardText(AnimationEditorHarness editor)
    {
        var overlay = OverlayLayerOf(editor);
        var texts = overlay.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>()
            .Select(t => t.Text).Where(t => t is not null && (t.StartsWith("Add ") || t.StartsWith("Drop on"))).ToList();
        return texts.Count == 0 ? null : string.Join("|", texts);
    }

    private static Avalonia.Controls.Primitives.OverlayLayer OverlayLayerOf(AnimationEditorHarness editor) =>
        Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(editor.Window)!;
}
