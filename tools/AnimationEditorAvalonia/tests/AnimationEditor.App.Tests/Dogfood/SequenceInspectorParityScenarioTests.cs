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
/// The sequence Inspector's TRACK fields are the .achx Inspector's frame TRANSFORM fields: same section header, the same
/// X / Y control with the same limits, format and keys, the same flip button. These compare the two live, so a change
/// to one that is not made to the other fails here.
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
    public async Task TrackOffsetFields_AreTheFrameOffsetFields_SameControlLimitsFormatAndLook()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, frame) = await OpenBothTabsAsync(editor);
        await SelectFrameAsync(editor, frame);
        var frameFields = new Control[] { editor.Control<NumericUpDown>("PropRelX"), editor.Control<NumericUpDown>("PropRelY") };
        var frameLook = frameFields.Select(f => (f.GetType(), Read(f, "Minimum"), Read(f, "Maximum"), Read(f, "Increment"),
            Read(f, "FormatString"), Read(f, "ShowButtonSpinner"), f.MinWidth)).ToList();
        await SelectTrackAsync(editor);

        var trackFields = new Control[] { Inspector(editor).TrackXField, Inspector(editor).TrackYField };
        var trackLook = trackFields.Select(f => (f.GetType(), Read(f, "Minimum"), Read(f, "Maximum"), Read(f, "Increment"),
            Read(f, "FormatString"), Read(f, "ShowButtonSpinner"), f.MinWidth)).ToList();

        trackLook.ShouldBe(frameLook);
        // The same one-line label column and wrapping row as the frame's.
        trackFields[0].FindAncestorOfType<WrapPanel>().ShouldNotBeNull();
        trackFields[0].FindAncestorOfType<Grid>()!.ColumnDefinitions[0].Width.Value.ShouldBe(
            frameFields[0].FindAncestorOfType<Grid>()!.ColumnDefinitions[0].Width.Value);
        trackFields[0].FindAncestorOfType<Grid>()!.Children.OfType<TextBlock>().Single().Text.ShouldBe("X");
        trackFields[1].FindAncestorOfType<Grid>()!.Children.OfType<TextBlock>().Single().Text.ShouldBe("Y");
    }

    [AvaloniaFact]
    public async Task TrackSections_UseTheFrameSectionHeaders_InTheSameStyle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, frame) = await OpenBothTabsAsync(editor);
        await SelectFrameAsync(editor, frame);
        var frameHeader = editor.Control<NumericUpDown>("PropRelX").FindAncestorOfType<Border>()!
            .GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "TRANSFORM");
        await SelectTrackAsync(editor);

        var headings = HeadingsIn(Inspector(editor)).ToList();
        headings.ShouldBe(new[] { "TRACK", "TRANSFORM" });
        var trackHeader = Inspector(editor).GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "TRANSFORM");
        (trackHeader.FontSize, trackHeader.FontWeight, trackHeader.Foreground).ShouldBe(
            (frameHeader.FontSize, frameHeader.FontWeight, frameHeader.Foreground));
    }

    [AvaloniaFact]
    public async Task TrackFlip_IsTheFrameFlipButton_IconToggleWithTheSameTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, frame) = await OpenBothTabsAsync(editor);
        await SelectFrameAsync(editor, frame);
        var frameFlip = editor.Control<ToggleButton>("PropFlipH");
        string? icon = frameFlip.GetVisualDescendants().OfType<SvgControl>().Single().Path;
        object? tip = ToolTip.GetTip(frameFlip);
        await SelectTrackAsync(editor);

        ToggleButton flip = Inspector(editor).TrackFlipBox;
        flip.GetType().ShouldBe(frameFlip.GetType());
        flip.GetVisualDescendants().OfType<SvgControl>().Single().Path.ShouldBe(icon);
        ToolTip.GetTip(flip).ShouldBe(tip);
        (flip.Width, flip.Height).ShouldBe((frameFlip.Width, frameFlip.Height));
    }

    [AvaloniaFact]
    public async Task TrackOffsetKeys_StepAndCommitLikeTheFrameOffsetFields()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (boss, frame) = await OpenBothTabsAsync(editor);
        await SelectFrameAsync(editor, frame);
        editor.TypeNumber("PropRelX", "5");
        editor.Press(Key.Up);
        editor.Control<NumericUpDown>("PropRelX").Value.ShouldBe(6m);
        await SelectTrackAsync(editor);

        // Typing commits as it goes and Enter ends the undo entry; Up steps by the increment; Ctrl+Z undoes one whole edit.
        var x = Inspector(editor).TrackXField;
        editor.TypeAndEnter(BoxOf(x), "5");
        editor.Press(Key.Up);
        x.Value.ShouldBe(6m);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].X.ShouldBe(6f);
        editor.Press(Key.Z, RawInputModifiers.Control);
        x.Value.ShouldBe(5m);
        editor.Press(Key.Z, RawInputModifiers.Control);
        x.Value.ShouldBe(0m);
        editor.UndoLabels.ShouldBeEmpty();
    }
}
