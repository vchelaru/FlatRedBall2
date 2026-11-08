using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A track has a Name and a Visible flag, nothing else: the Inspector's track panel has no TRANSFORM section and no flip
/// button, for one track or for several.
/// </summary>
public class SequenceTrackNoFlipScenarioTests
{
    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static Point Header(AnimationEditorHarness editor, int lane)
    {
        var timeline = SequenceTimelineMultiSelectScenarioTests.Timeline(editor);
        return editor.PointIn(timeline, 70, timeline.HeaderCenters(lane).Name);
    }

    // What the user can see and operate in the track panel: the controls, not the layout around them.
    private static string[] VisibleControls(SequenceInspectorControl inspector) =>
        inspector.TrackPanel.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && (c is TextBox || c is ToggleButton || c is NumericUpDown || c is Button))
            .Select(c => c is CheckBox box ? "CheckBox:" + box.Content : c.GetType().Name)
            .ToArray();

    private static string[] VisibleHeadings(SequenceInspectorControl inspector) =>
        inspector.TrackPanel.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Classes.Contains("heading"))
            .Select(t => t.Text ?? "")
            .ToArray();

    [AvaloniaFact]
    public async Task OneTrack_PanelHasNameAndVisibleOnly_NoTransformSection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(SequenceTimelineMultiSelectScenarioTests.WriteFixture(editor));
        editor.ClickAt(Header(editor, 0));
        var inspector = Inspector(editor);

        inspector.TrackPanel.IsVisible.ShouldBeTrue();
        VisibleHeadings(inspector).ShouldBe(new[] { "TRACK" });
        VisibleControls(inspector).ShouldBe(new[] { "TextBox", "CheckBox:Visible" });
    }

    [AvaloniaFact]
    public async Task SeveralTracks_BulkPanelShowsVisibleOnly_NoTransformSection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(SequenceTimelineMultiSelectScenarioTests.WriteFixture(editor));
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        var inspector = Inspector(editor);

        inspector.TrackNameBox.IsEnabled.ShouldBeFalse("a name is for one track");
        VisibleHeadings(inspector).ShouldBe(new[] { "TRACK" });
        VisibleControls(inspector).ShouldBe(new[] { "TextBox", "CheckBox:Visible" });
    }
}
