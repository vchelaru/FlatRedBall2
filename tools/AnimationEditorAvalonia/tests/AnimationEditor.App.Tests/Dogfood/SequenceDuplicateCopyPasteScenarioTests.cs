using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json.Nodes;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Duplicate Sequence in the row menu, and Ctrl+C / Ctrl+V (and Edit &gt; Copy / Paste) on sequences while the
/// SEQUENCES list holds the keyboard focus. The timeline, text boxes and other tab kinds keep their own meaning of the keys.
/// </summary>
public class SequenceDuplicateCopyPasteScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static InlineRenameBox? BoxOf(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)?.GetVisualDescendants().OfType<InlineRenameBox>().SingleOrDefault();

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    /// <summary>hero.achx with a Walk chain, and boss.seqj listing it with Idle (one track using Walk) and Attack.</summary>
    private static string WriteBoss(AnimationEditorHarness editor, string folder = "")
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        var idle = new SequenceSave { Name = "Idle", Loop = true };
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5, Loops = 3 });
        body.Visible = false;
        body.UnknownProperties["tag"] = "boss";
        idle.Tracks.Add(body);
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(idle);
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        string path = Path.Combine(editor.ProjectFolder, folder, "boss.seqj");
        save.Save(path);
        return path;
    }

    private static string WriteOther(AnimationEditorHarness editor, params string[] sequenceNames)
    {
        string folder = Path.Combine(editor.ProjectFolder, "sub");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "other.seqj");
        var save = new SequenceListSave();
        save.Sequences.AddRange(sequenceNames.Select(name => new SequenceSave { Name = name }));
        save.Save(path);
        return path;
    }

    private static void ClickRow(AnimationEditorHarness editor, int row) => editor.Click((Control)List(editor).ContainerFromIndex(row)!);

    private static void ClickTimeline(AnimationEditorHarness editor) =>
        editor.ClickAt(editor.PointIn(Timeline(editor), 400, Timeline(editor).Viewport.LaneTop(3)));

    private static async Task SetClipboardAsync(AnimationEditorHarness editor, string text)
    {
        await editor.Window.Clipboard!.SetTextAsync(text);
        editor.Layout();
    }

    private static async Task<string?> ClipboardAsync(AnimationEditorHarness editor)
    {
        editor.Layout();
        return await editor.Window.Clipboard!.TryGetTextAsync();
    }

    /// <summary>Presses Ctrl+key, then lets the fire-and-forget clipboard work finish.</summary>
    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    private static void DuplicateFromRowMenu(AnimationEditorHarness editor, int row)
    {
        editor.RightClick((Control)List(editor).ContainerFromIndex(row)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Duplicate");
    }

    [AvaloniaFact]
    public async Task DuplicateSequence_FromTheRowMenu_AddsAnExactCopyRightAfter_SelectsIt_AndOpensItsNameForEditing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);

        DuplicateFromRowMenu(editor, 0);
        await editor.WaitUntilAsync(() => BoxOf(editor, 1) is { IsEditing: true }, TimeSpan.FromSeconds(1));

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        List(editor).SelectedIndex.ShouldBe(1);
        var box = BoxOf(editor, 1).ShouldNotBeNull();
        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("Idle 2");
        box.SelectedText.ShouldBe("Idle 2");
        box.IsFocused.ShouldBeTrue();
        editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox.Text.ShouldBe("Idle 2");
        editor.UndoLabels.ShouldBe(new[] { "Duplicate Sequence 'Idle'" });
        var saved = SequenceListSave.FromFile(boss);
        var original = saved.Sequences[0];
        var copy = saved.Sequences[1];
        (copy.Loop, copy.Tracks.Count).ShouldBe((true, 1));
        var track = copy.Tracks[0];
        track.Visible.ShouldBeFalse();
        track.UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        (track.Chains[0].Source, track.Chains[0].Chain, track.Chains[0].Start, track.Chains[0].Loops).ShouldBe(("hero.achx", "Walk", 0.5, 3f));
        copy.Name = original.Name;
        SequenceClipboard.Write(copy, "C:/x").ShouldBe(SequenceClipboard.Write(original, "C:/x"));
    }

    [AvaloniaFact]
    public async Task DuplicateSequence_ThenTypingAName_RenamesAsASecondUndoEntry_AndUndoRemovesTheCopy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);

        DuplicateFromRowMenu(editor, 0);
        await editor.WaitUntilAsync(() => BoxOf(editor, 1) is { IsEditing: true }, TimeSpan.FromSeconds(1));
        editor.Type("Charge");
        editor.Press(Key.Enter);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Charge", "Attack" });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate Sequence 'Idle'", "Rename Sequence 'Idle 2' to 'Charge'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack" });
        List(editor).SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task CtrlC_ThenCtrlV_OnTheFocusedList_PutsTheSequenceOnTheClipboardAsJson_AndPastesACopyAfterIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);

        await CtrlAsync(editor, Key.C);

        string? text = await ClipboardAsync(editor);
        SequenceClipboard.TryRead(text, out var content, out _).ShouldBeTrue();
        content!.Sequences.Single().Name.ShouldBe("Idle");
        var envelope = JsonNode.Parse(text!)!;
        envelope["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-sequence");
        content.Sources.ShouldBe(new[] { Path.Combine(editor.ProjectFolder, "hero.achx").Replace('\\', '/') });
        editor.UndoLabels.ShouldBeEmpty();

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldBeNull();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        List(editor).SelectedIndex.ShouldBe(1);
        SavedNames(boss).ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        editor.UndoLabels.ShouldBe(new[] { "Paste Sequence 'Idle 2'" });
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "hero.achx" });
        editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox.Text.ShouldBe("Idle 2");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
    }

    [AvaloniaFact]
    public async Task Paste_WithNothingSelected_GoesAtTheEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        await CtrlAsync(editor, Key.C);
        List(editor).SelectedIndex = -1;
        editor.Layout();
        List(editor).Focus();

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Idle 2" });
    }

    [AvaloniaFact]
    public async Task PasteIntoAnotherSeqj_AddsTheSequencesSourcesOnce_RelativeToThatFile_AsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        string other = WriteOther(editor, "Run");
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        await CtrlAsync(editor, Key.C);
        await editor.OpenAsync(other);
        ClickRow(editor, 0);

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Run", "Idle" });
        var saved = SequenceListSave.FromFile(other);
        saved.Sources.ShouldBe(new[] { "../hero.achx" });
        saved.Sequences[1].Tracks[0].Chains[0].Source.ShouldBe("../hero.achx");
        editor.UndoLabels.ShouldBe(new[] { "Paste Sequence 'Idle'" });

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Run", "Idle", "Idle 2" });
        SequenceListSave.FromFile(other).Sources.ShouldBe(new[] { "../hero.achx" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(other).Sources.ShouldBeEmpty();
        editor.SequenceNames.ShouldBe(new[] { "Run" });
    }

    [AvaloniaFact]
    public async Task Paste_OfTextThatIsNotACopy_ChangesNothing_AndSaysWhyInAProblemBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 0);

        foreach (string text in new[] { "hello", "{ \"a\": 1 }", "" })
        {
            await SetClipboardAsync(editor, text);
            await CtrlAsync(editor, Key.V);
            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Nothing to paste");
        }

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
        editor.UndoLabels.ShouldBeEmpty();
        editor.InfoText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Paste_OfAMalformedEnvelope_ShowsTheErrorBanner_AndChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 0);
        await SetClipboardAsync(editor, "{ \"kind\": \"frb2-animation-editor-sequence\", \"version\": 1, \"sequence\": 5 }");

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("Can't paste");
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlC_AndCtrlV_InTheRenameBox_EditTheText_NotTheSequences()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 0);
        await CtrlAsync(editor, Key.C);
        string envelope = (await ClipboardAsync(editor))!;
        DuplicateFromRowMenu(editor, 1);
        await editor.WaitUntilAsync(() => BoxOf(editor, 2) is { IsEditing: true }, TimeSpan.FromSeconds(1));
        var box = BoxOf(editor, 2).ShouldNotBeNull();
        string name = box.Text!;
        editor.SequenceNames.Count.ShouldBe(3);

        await CtrlAsync(editor, Key.C);
        (await ClipboardAsync(editor)).ShouldBe(name);
        await SetClipboardAsync(editor, envelope);
        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.Count.ShouldBe(3);
        editor.UndoLabels.ShouldBe(new[] { "Duplicate Sequence 'Attack'" });
        box.IsEditing.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task CtrlC_WithTheTimelineFocused_DoesNotCopyASequence_ButCtrlVStillPastesACopiedSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 0);
        await CtrlAsync(editor, Key.C);
        string envelope = (await ClipboardAsync(editor))!;
        ClickTimeline(editor);
        await SetClipboardAsync(editor, "sentinel");

        await CtrlAsync(editor, Key.C);
        // Nothing is selected on the timeline: Copy says so and empties the clipboard rather than leave an older copy to paste.
        (await ClipboardAsync(editor)).ShouldBeNullOrEmpty();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("Nothing to copy");
        await SetClipboardAsync(editor, envelope);
        await CtrlAsync(editor, Key.V);

        // Paste follows what the clipboard holds, so the copied sequence lands after the selected one even from the timeline.
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        editor.UndoLabels.ShouldBe(new[] { "Paste Sequence 'Idle 2'" });
    }

    [AvaloniaFact]
    public async Task OnAPngTab_CtrlC_AndCtrlV_DoNothingToTheSequences_OrTheClipboard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        string png = editor.WritePng("plain.png", 16, 16);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        await CtrlAsync(editor, Key.C);
        string envelope = (await ClipboardAsync(editor))!;
        await editor.OpenPngTabAsync(png);
        await SetClipboardAsync(editor, "sentinel");

        await CtrlAsync(editor, Key.C);
        (await ClipboardAsync(editor)).ShouldBe("sentinel");
        await SetClipboardAsync(editor, envelope);
        await CtrlAsync(editor, Key.V);

        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack" });
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task EditMenuCopyAndPaste_AreEnabledOnlyWhileTheListOrTheTimelineIsFocused_OnASeqjTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        var copy = editor.Control<MenuItem>("MenuCopy");
        var paste = editor.Control<MenuItem>("MenuPaste");

        (copy.IsEnabled, paste.IsEnabled).ShouldBe((false, false), "neither pane was touched yet");

        ClickRow(editor, 0);
        (copy.IsEnabled, paste.IsEnabled).ShouldBe((true, true));

        // The timeline copies chains and tracks, so it enables them too.
        ClickTimeline(editor);
        (copy.IsEnabled, paste.IsEnabled).ShouldBe((true, true));
        // A button takes the focus but not the ownership: the timeline is still the last pane touched.
        editor.Control<Button>("AddSequenceButton").Focus();
        editor.Layout();
        (copy.IsEnabled, paste.IsEnabled).ShouldBe((true, true));
        editor.Control<MenuItem>("MenuCut").IsEnabled.ShouldBeTrue();

        ClickRow(editor, 1);
        (copy.IsEnabled, paste.IsEnabled).ShouldBe((true, true));
    }

    [AvaloniaFact]
    public async Task EditMenuCopyThenPaste_OnTheFocusedList_CopiesAndPastesTheSelectedSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);

        editor.ClickMenu("MenuCopy");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        SequenceClipboard.TryRead(await ClipboardAsync(editor), out var content, out _).ShouldBeTrue();
        content!.Sequences.Single().Name.ShouldBe("Idle");

        ClickRow(editor, 1);
        editor.ClickMenu("MenuPaste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Idle 2" });
        List(editor).SelectedIndex.ShouldBe(2);
        editor.UndoLabels.ShouldBe(new[] { "Paste Sequence 'Idle 2'" });
    }

    [AvaloniaFact]
    public async Task EditMenuCopyAndPaste_OnAPngTab_AreDisabled()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("plain.png", 16, 16);
        await editor.OpenPngTabAsync(png);

        editor.Control<MenuItem>("MenuCopy").IsEnabled.ShouldBeFalse();
        editor.Control<MenuItem>("MenuPaste").IsEnabled.ShouldBeFalse();
    }
}
