using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Tracks that play another sequence of the file: a sequence dragged from the list onto the timeline, the right-click
/// menu, the expandable link row, and the list's selection waiting for the release so a drag can start from any row.
/// </summary>
public class SequenceReferenceScenarioTests
{
    // Walk: a 0.5 s cycle, Run: 0.2 s. Idle: Body (Walk x4 = 2 s). Earth: Ring (Run x5 = 1 s) and Dust. Action: Hit.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(Sequence("Idle", Track("Body", "Walk", 4)));
        save.Sequences.Add(Sequence("Earth", Track("Ring", "Run", 5), Track("Dust", "Walk", 1)));
        save.Sequences.Add(Sequence("Action", Track("Hit", "Run", 1)));
        save.Save(path);
        return path;
    }

    private static SequenceSave Sequence(string name, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave Track(string name, string chain, float loops) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Loops = loops } } };

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Control Row(AnimationEditorHarness editor, int row) => (Control)List(editor).ContainerFromIndex(row)!;

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static List<string> Selected(AnimationEditorHarness editor) =>
        List(editor).SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ToList();

    private static SequenceSave Saved(string path, string sequence) => SequenceListSave.FromFile(path).Sequences.First(s => s.Name == sequence);

    // A point on the lanes under the ruler, to the right of the headers.
    private static Point OnLanes(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), timeline.Viewport.LaneTop(lane) + TimelineLayout.BodyTop + 8);
    }

    private static Point Header(AnimationEditorHarness editor, int lane, double x)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, x, timeline.Viewport.LaneTop(lane) + TimelineLayout.HeaderCenterY);
    }

    [AvaloniaFact]
    public async Task DraggingASequenceRowOntoTheTimeline_AddsAReferenceAtThePlayhead_WithoutSwitchingAway()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        Timeline(editor).Playhead = 0.5;

        editor.Drag(editor.CenterOf(Row(editor, 1)), OnLanes(editor, 0, 1.0));

        var tracks = Saved(boss, "Idle").Tracks;
        tracks.Select(t => t.Name).ShouldBe(new[] { "Earth", "Body" }); // the new track lands in front
        tracks[0].Reference!.Sequence.ShouldBe("Earth");
        tracks[0].Reference!.Start.ShouldBe(0.5, "at the playhead, not where it was dropped");
        tracks[0].Reference!.Loops.ShouldBe(1f);
        editor.UndoLabels.ShouldBe(new[] { "Add Reference 'Earth'" });
        Selected(editor).ShouldBe(new[] { "Idle" }); // the drag did not select the dragged row
        Timeline(editor).SelectedTrack.ShouldBe(0);

        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(boss, "Idle").Tracks.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DraggingASequenceThatWouldCycle_ShowsNoDropTarget_AndAddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        // Idle onto Idle's own timeline.
        editor.DragHold(editor.CenterOf(Row(editor, 0)), OnLanes(editor, 0, 1.0));
        Timeline(editor).IsSequenceDropTarget.ShouldBeFalse();
        editor.DragRelease(OnLanes(editor, 0, 1.0));

        // A sequence that is fine shows the drop.
        editor.DragHold(editor.CenterOf(Row(editor, 2)), OnLanes(editor, 0, 1.0));
        Timeline(editor).IsSequenceDropTarget.ShouldBeTrue();
        editor.DragRelease(OnLanes(editor, 0, 1.0));

        Saved(boss, "Idle").Tracks.Select(t => t.Name).ShouldBe(new[] { "Action", "Body" });
        editor.UndoLabels.ShouldBe(new[] { "Add Reference 'Action'" });
    }

    [AvaloniaFact]
    public async Task DraggingSeveralSelectedRowsOntoTheTimeline_AddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        List(editor).SelectedItems!.Add(List(editor).Items.Cast<object>().ElementAt(1));
        List(editor).SelectedItems!.Count.ShouldBe(2);

        editor.Drag(editor.CenterOf(Row(editor, 1)), OnLanes(editor, 0, 1.0));

        Saved(boss, "Idle").Tracks.Count.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PressingARow_SelectsOnRelease_NotOnPress()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var point = editor.CenterOf(Row(editor, 2));

        editor.Window.MouseMove(point, RawInputModifiers.None);
        editor.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Selected(editor).ShouldBe(new[] { "Idle" }); // still the old selection while the button is down
        editor.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        Selected(editor).ShouldBe(new[] { "Action" });
    }

    [AvaloniaFact]
    public async Task DraggingAnUnselectedRowInTheList_StillReordersIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var above = editor.PointIn(Row(editor, 0), Row(editor, 0).Bounds.Width / 2, Row(editor, 0).Bounds.Height * 0.25);

        editor.Drag(editor.CenterOf(Row(editor, 2)), above);

        editor.SequenceNames.ShouldBe(new[] { "Action", "Idle", "Earth" });
        Selected(editor).ShouldBe(new[] { "Action" });
    }

    [AvaloniaFact]
    public async Task RightClickMenu_AddSequenceReference_ListsOnlySequencesThatDoNotCycle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.RightClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(3), timeline.Viewport.LaneTop(0) + 20));
        var parent = timeline.Menu.Items.OfType<MenuItem>().First(item => (string?)item.Header == "Add Sequence Reference");
        parent.Items.OfType<MenuItem>().Select(item => (string?)item.Header).ShouldBe(new[] { "Earth", "Action" });
        editor.PickMenuItem(timeline.Menu, "Add Sequence Reference", "Earth");

        Saved(boss, "Idle").Tracks[0].Reference!.Sequence.ShouldBe("Earth");

        // Earth now sits inside Idle, so Idle is no longer offered to Earth.
        editor.ClickAt(editor.CenterOf(Row(editor, 1)));
        editor.RightClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(3), timeline.Viewport.LaneTop(0) + 20));
        timeline.ReferenceCandidates.ShouldBe(new[] { "Action" });
        timeline.Menu.Close();
    }

    [AvaloniaFact]
    public async Task GoToSequence_FromTheTrackMenuAndTheInspector_SelectsTheTargetAndRecordsBack_AndIsDisabledForAMissingTarget()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        var file = SequenceListSave.FromFile(boss);
        file.Sequences[0].Tracks.Insert(0, new SequenceTrackSave { Name = "Ghost", Reference = new SequenceReferenceSave { Sequence = "Nope" } });
        file.Sequences[0].Tracks.Insert(0, new SequenceTrackSave { Name = "Earth", Reference = new SequenceReferenceSave { Sequence = "Earth" } });
        file.Save(boss);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        var back = editor.Control<MenuItem>("MenuNavBack");
        back.IsEnabled.ShouldBeFalse();

        // Track menu on the Earth reference (lane 0).
        editor.RightClickAt(Header(editor, 0, 60));
        var item = timeline.Menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Go to Sequence");
        item.IsEnabled.ShouldBeTrue();
        editor.PickMenuItem(timeline.Menu, "Go to Sequence");
        Selected(editor).ShouldBe(new[] { "Earth" });
        back.IsEnabled.ShouldBeTrue();
        editor.ClickMenu("MenuNavBack");
        Selected(editor).ShouldBe(new[] { "Idle" });

        // The Inspector button, with the reference track selected.
        editor.ClickAt(Header(editor, 0, 60));
        var button = editor.Control<Button>("GoToSequenceButton");
        button.IsEnabled.ShouldBeTrue();
        editor.Click(button);
        Selected(editor).ShouldBe(new[] { "Earth" });
        editor.ClickMenu("MenuNavBack");
        Selected(editor).ShouldBe(new[] { "Idle" });

        // A reference to a sequence the file lacks: both are disabled and say why.
        editor.RightClickAt(Header(editor, 1, 60));
        var ghost = timeline.Menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Go to Sequence");
        ghost.IsEnabled.ShouldBeFalse();
        ToolTip.GetTip(ghost).ShouldBe("There is no sequence named \"Nope\".");
        timeline.Menu.Close();
        editor.ClickAt(Header(editor, 1, 60));
        button = editor.Control<Button>("GoToSequenceButton");
        button.IsEnabled.ShouldBeFalse();
        ToolTip.GetTip(button).ShouldBe("There is no sequence named \"Nope\".");
    }

    [AvaloniaFact]
    public async Task AReferenceRow_IsCollapsedByDefault_AndTheChevronExpandsItToTheInheritedTracks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.AddSequenceReference("Earth").ShouldBeNull();
        editor.Layout();
        timeline.IsReferenceExpanded(0).ShouldBeFalse();
        double collapsed = timeline.Viewport.LaneHeightOf(0);

        editor.ClickAt(Header(editor, 0, 30));

        timeline.IsReferenceExpanded(0).ShouldBeTrue();
        timeline.Viewport.LaneHeightOf(0).ShouldBeGreaterThan(collapsed);
        timeline.References.Single().Rows.Select(r => r.Name).ShouldBe(new[] { "Ring", "Dust" });

        editor.ClickAt(Header(editor, 0, 30));
        timeline.IsReferenceExpanded(0).ShouldBeFalse();
        timeline.Viewport.LaneHeightOf(0).ShouldBe(collapsed);
    }

    [AvaloniaFact]
    public async Task DraggingAReferenceClip_MovesItsStart_AndItsRightEdgeChangesTheCycles_AndLengthensToTheClipEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.AddSequenceReference("Earth");
        editor.Layout();

        // Earth is 1 s: the clip spans 0..1 s. Drag its body 0.5 s later.
        editor.Drag(OnLanes(editor, 0, 0.4), OnLanes(editor, 0, 0.9), RawInputModifiers.Alt);
        Saved(boss, "Idle").Tracks[0].Reference!.Start.ShouldBe(0.5, 0.02);

        // Then its right edge from 1.5 s to 3.5 s: two more cycles.
        var reference = timeline.References.Single();
        double edge = timeline.Viewport.XAt(reference.End);
        var y = timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 8;
        editor.Drag(editor.PointIn(timeline, edge - 1, y), editor.PointIn(timeline, timeline.Viewport.XAt(reference.End + 2), y), RawInputModifiers.Alt);

        Saved(boss, "Idle").Tracks[0].Reference!.Loops.ShouldBe(3f, 0.05f);
        timeline.SequenceLength.ShouldBe(3.5, 0.05);   // the clip end, past Body's 2 s
        editor.UndoLabels.Count.ShouldBe(3);
    }
}
