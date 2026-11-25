using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Text typed in the Inspector and not committed yet only ever applies to what it was typed for, when the file changes outside,
/// an edit is undone, or the user selects something else.
/// </summary>
public class SequenceInspectorOutsideChangeScenarioTests
{
    [AvaloniaFact]
    public async Task NameTypedInTheInspector_TheSequenceIsRemovedOutside_DoesNotRenameAnother()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(seqj);
        var box = editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox;
        box.Focus();
        box.SelectAll();
        editor.Type("Slam");

        editor.WriteSeqj("boss.seqj", "Idle");
        (await editor.WaitUntilAsync(() => editor.SequenceNames.Count() == 1, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(seqj).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle" });
    }

    private static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(8);

    private static SequenceTrackSave Track(string name, params (string Chain, double Start, double? Length)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.AddRange(chains.Select(c => new SequenceChainReferenceSave { Source = "hero.achx", Chain = c.Chain, Start = c.Start, Length = c.Length }));
        return track;
    }

    private static SequenceTrackSave Plays(string name, string sequence) =>
        new SequenceTrackSave { Name = name, References = [new SequenceReferenceSave { Sequence = sequence, Start = 1 }] };

    private static string Write(AnimationEditorHarness editor, string name, params SequenceTrackSave[] tracks)
    {
        string path = editor.WriteSeqj(name, "Attack", "Idle");
        return Rewrite(path, save => save.Sequences[0].Tracks.AddRange(tracks));
    }

    private static string Rewrite(string path, Action<SequenceListSave> change)
    {
        var save = SequenceListSave.FromFile(path);
        change(save);
        save.Save(path);
        return path;
    }

    private static async Task<SequenceInspectorControl> OpenAsync(AnimationEditorHarness editor, string path, TimelineChainId? chain, int? track)
    {
        await editor.OpenAsync(path);
        editor.Control<SequenceTimelineControl>("SequenceTimeline").Select(chain, track);
        editor.Layout();
        return editor.Control<SequenceInspectorControl>("SequenceInspector");
    }

    private static void TypeInto(AnimationEditorHarness editor, TextBox box, string text)
    {
        box.Focus();
        box.SelectAll();
        editor.Type(text);
    }

    // Changes the file outside and waits for the editor to have read it.
    private static async Task ChangeOutsideAsync(AnimationEditorHarness editor, string path, Action<SequenceListSave> change)
    {
        var list = editor.Control<ListBox>("SequenceList");
        var rows = list.ItemsSource;
        Rewrite(path, change);
        (await editor.WaitUntilAsync(() => !ReferenceEquals(list.ItemsSource, rows), ReloadTimeout)).ShouldBeTrue();
    }

    private static TextBox BoxOf(FlankerNumericField field) => field.GetVisualDescendants().OfType<TextBox>().First();

    // The blocks share their length, so the field's value does not change when the other one takes the index.
    [AvaloniaFact]
    public async Task LengthTyped_AnotherBlockTakesTheIndexOutside_DoesNotChangeIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2), ("Run", 4, 2)));
        var inspector = await OpenAsync(editor, path, new TimelineChainId(0, 0), null);
        TypeInto(editor, BoxOf(inspector.ChainLengthField), "3");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks[0].Chains.RemoveAt(0));
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Chains.Select(c => (c.Chain, c.Start, c.Length)).ShouldBe(new[] { ("Run", 4.0, (double?)2) });
    }

    [AvaloniaFact]
    public async Task TrackNameTyped_TracksReorderedOutside_DoesNotRenameAnother()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body"), Track("Arms"));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, inspector.TrackNameBox, "Legs");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks.Reverse());
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Arms", "Body" });
    }

    [AvaloniaFact]
    public async Task ClipStartTyped_TheTrackPlaysAnotherSequenceOutside_DoesNotMoveItsClip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Plays("Sub", "Idle"));
        Rewrite(path, save => save.Sequences.Add(new SequenceSave { Name = "Run" }));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, BoxOf(inspector.ReferenceStartField), "5");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks[0].References![0].Sequence = "Run");
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].References![0].Start.ShouldBe(1);
    }

    // The section of the field typed in is gone, so the field loses the focus with the text still in it.
    [AvaloniaFact]
    public async Task ClipStartTyped_AChainTrackTakesTheIndexOutside_NothingIsEdited()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Plays("Sub", "Idle"));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, BoxOf(inspector.ReferenceStartField), "5");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks[0] = Track("Body"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.Press(Key.Enter);

        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task NameTyped_TheSequenceIsMovedOutside_StillRenamesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        var inspector = await OpenAsync(editor, path, null, null);
        TypeInto(editor, inspector.SequenceNameBox, "Slam");

        await ChangeOutsideAsync(editor, path, save => save.Sequences.Reverse());
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Slam" });
    }

    [AvaloniaFact]
    public async Task StartTyped_TheLengthOfTheBlockChangesOutside_IsKept()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2)));
        var inspector = await OpenAsync(editor, path, new TimelineChainId(0, 0), null);
        TypeInto(editor, BoxOf(inspector.ChainStartField), "7");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks[0].Chains[0].Length = 3);
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Chains.Select(c => (c.Start, c.Length)).ShouldBe(new[] { (7.0, (double?)3) });
    }

    [AvaloniaFact]
    public async Task TrackNameTyped_ItsClipMovesOutside_IsKept()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Plays("Sub", "Idle"));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, inspector.TrackNameBox, "Legs");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks[0].References![0].Start = 3);

        inspector.TrackNameBox.Text.ShouldBe("Legs");
    }

    [AvaloniaFact]
    public async Task StartTyped_TheLengthOfTheBlockIsUndone_IsKept()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2)));
        var inspector = await OpenAsync(editor, path, new TimelineChainId(0, 0), null);
        editor.TypeAndEnter(BoxOf(inspector.ChainLengthField), "3");
        TypeInto(editor, BoxOf(inspector.ChainStartField), "7");

        editor.UndoManager.Undo();

        BoxOf(inspector.ChainStartField).Text.ShouldBe("7");
    }

    [AvaloniaFact]
    public async Task TrackNameTyped_OneOfTwoTracksOfThatNameIsRemovedOutside_IsDropped()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2)), Track("Body", ("Run", 1, 3)));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, inspector.TrackNameBox, "Legs");

        await ChangeOutsideAsync(editor, path, save => save.Sequences[0].Tracks.RemoveAt(0));
        editor.Press(Key.Enter);

        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body" });
    }

    private static async Task ClickRowAsync(AnimationEditorHarness editor, int row)
    {
        editor.Click((Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(row)!);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
    }

    [AvaloniaFact]
    public async Task NameTyped_ClickOnAnotherRow_RenamesTheOneItWasTypedFor_AndTheClickedRowStaysSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj");
        var inspector = await OpenAsync(editor, path, null, null);
        TypeInto(editor, inspector.SequenceNameBox, "Slam");

        await ClickRowAsync(editor, 1);

        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Slam", "Idle" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Attack' to 'Slam'" });
        editor.Control<ListBox>("SequenceList").SelectedIndex.ShouldBe(1);
        inspector.SequenceNameBox.Text.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task StartTyped_ClickOnAnotherRow_MovesTheBlockItWasTypedFor_AndTheClickedRowStaysSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2)));
        Rewrite(path, save => save.Sequences[1].Tracks.Add(Track("Body", ("Walk", 0, 2))));
        var inspector = await OpenAsync(editor, path, new TimelineChainId(0, 0), null);
        TypeInto(editor, BoxOf(inspector.ChainStartField), "7");

        await ClickRowAsync(editor, 1);

        SequenceListSave.FromFile(path).Sequences.Select(s => s.Tracks[0].Chains[0].Start).ShouldBe(new[] { 7.0, 0.0 });
        editor.Control<ListBox>("SequenceList").SelectedIndex.ShouldBe(1);
        inspector.SequenceNameBox.Text.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task TrackNameTyped_ClickOnTheSameRow_RenamesTheTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body"));
        var inspector = await OpenAsync(editor, path, null, 0);
        TypeInto(editor, inspector.TrackNameBox, "Legs");

        await ClickRowAsync(editor, 0);

        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Legs" });
        inspector.SequencePanel.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task StartTyped_ClickOnAnotherTab_MovesTheBlockItWasTypedFor()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Write(editor, "boss.seqj", Track("Body", ("Walk", 0, 2)));
        string other = Write(editor, "other.seqj", Track("Body", ("Walk", 0, 2)));
        await editor.OpenAsync(other);
        var inspector = await OpenAsync(editor, path, new TimelineChainId(0, 0), null);
        TypeInto(editor, BoxOf(inspector.ChainStartField), "7");

        editor.ClickTab("other.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));

        new[] { path, other }.Select(file => SequenceListSave.FromFile(file).Sequences[0].Tracks[0].Chains[0].Start).ShouldBe(new[] { 7.0, 0.0 });
    }
}
