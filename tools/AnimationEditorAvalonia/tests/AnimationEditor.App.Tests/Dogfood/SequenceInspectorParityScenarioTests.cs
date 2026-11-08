using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using SvgControl = global::Avalonia.Svg.Skia.Svg;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The sequence Inspector's track section uses the .achx Inspector's section header style (a track has a Name and a Visible flag, nothing else).
/// These compare the two live, so a change to one that is not made to the other fails here.
/// </summary>
public class SequenceInspectorParityScenarioTests
{
    // Walk has one frame, so selecting it shows the frame's TRANSFORM section.
    private static async Task<(string Boss, AnimationFrameSave Frame)> OpenBothTabsAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(boss);
        await editor.OpenAsync(hero);
        var frame = editor.ChainNamed("Walk").Frames[0];
        await editor.OpenAsync(boss);
        return (boss, frame);
    }

    private static async Task SelectFrameAsync(AnimationEditorHarness editor, AnimationFrameSave frame)
    {
        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.Expand(editor.ChainNamed("Walk"));
        editor.ClickRow(frame);
    }

    private static async Task SelectTrackAsync(AnimationEditorHarness editor)
    {
        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        editor.ClickAt(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(0) + 10));
    }

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static object? Read(Control control, string property) => control.GetType().GetProperty(property)?.GetValue(control);

    private static TextBox BoxOf(Control field) => field.GetVisualDescendants().OfType<TextBox>().First();

    private static IEnumerable<string> HeadingsIn(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "")
            .Where(t => t.Length > 2 && t == t.ToUpperInvariant() && t.All(char.IsLetter));

    [AvaloniaFact]
    public async Task TrackSection_UsesTheFrameSectionHeaderStyle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, frame) = await OpenBothTabsAsync(editor);
        await SelectFrameAsync(editor, frame);
        var frameHeader = editor.Control<NumericUpDown>("PropRelX").FindAncestorOfType<Border>()!
            .GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "TRANSFORM");
        await SelectTrackAsync(editor);

        var headings = HeadingsIn(Inspector(editor)).ToList();
        headings.ShouldBe(new[] { "TRACK" });
        var trackHeader = Inspector(editor).GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "TRACK");
        (trackHeader.FontSize, trackHeader.FontWeight, trackHeader.Foreground).ShouldBe(
            (frameHeader.FontSize, frameHeader.FontWeight, frameHeader.Foreground));
    }

}
