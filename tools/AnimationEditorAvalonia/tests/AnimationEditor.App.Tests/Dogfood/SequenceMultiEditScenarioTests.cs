using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Delete, duplicate, copy, cut and paste act on every sequence the list has selected, like the <c>.achx</c> tab acts on
/// every selected chain, through the Delete key, Ctrl+C/X/V/D, the Edit menu and the row menu alike. Each is one undo
/// entry for the whole set. Rename is available only with a single selection.
/// </summary>
public class SequenceMultiEditScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Control Row(AnimationEditorHarness editor, int row) => (Control)List(editor).ContainerFromIndex(row)!;

    private static void ClickRow(AnimationEditorHarness editor, int row, RawInputModifiers modifiers = RawInputModifiers.None) =>
        editor.Click(Row(editor, row), modifiers);

    private static void SelectRows(AnimationEditorHarness editor, params int[] rows)
    {
        ClickRow(editor, rows[0]);
        foreach (int row in rows.Skip(1)) ClickRow(editor, row, RawInputModifiers.Control);
    }

    private static List<string> Selected(AnimationEditorHarness editor) =>
        List(editor).SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ToList();

    private static string InspectorName(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox.Text!;

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static bool AnyRenameBoxOpen(AnimationEditorHarness editor) =>
        Enumerable.Range(0, editor.SequenceNames.Count).Any(row =>
            List(editor).ContainerFromIndex(row)?.GetVisualDescendants().OfType<InlineRenameBox>().Any(box => box.IsEditing) == true);

    /// <summary>hero.achx with a Walk chain; boss.seqj holds Idle, Attack, Run and Jump, each with a Body track using Walk.</summary>
    private static string WriteBoss(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        foreach (string name in new[] { "Idle", "Attack", "Run", "Jump" })
        {
            var sequence = new SequenceSave { Name = name };
            var body = new SequenceTrackSave { Name = "Body" };
            body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5, Loops = 2 });
            sequence.Tracks.Add(body);
            save.Sequences.Add(sequence);
        }
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        save.Save(path);
        return path;
    }

    private static string WriteOther(AnimationEditorHarness editor, string name)
    {
        string folder = Path.Combine(editor.ProjectFolder, "sub");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "other.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = name });
        save.Save(path);
        return path;
    }

    private static async Task<string?> ClipboardAsync(AnimationEditorHarness editor)
    {
        editor.Layout();
        return await editor.Window.Clipboard!.TryGetTextAsync();
    }

    private static async Task<List<string>> ClipboardSequenceNamesAsync(AnimationEditorHarness editor)
    {
        SequenceClipboard.TryRead(await ClipboardAsync(editor), out var content, out _).ShouldBeTrue();
        return content!.Sequences.Select(sequence => sequence.Name).ToList();
    }

    /// <summary>Presses Ctrl+key, then lets the fire-and-forget clipboard work finish.</summary>
    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    [AvaloniaFact]
    public async Task DeleteKey_OnSeveralSelected_DeletesThemAll_AsOneUndoEntry_AndUndoRestoresPositionsAndSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });

        editor.Press(Key.Delete);

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Jump" });
        SavedNames(boss).ShouldBe(new[] { "Attack", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Sequences" });
        Selected(editor).ShouldBe(new[] { "Attack" });
        InspectorName(editor).ShouldBe("Attack");

        Undo(editor);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
        InspectorName(editor).ShouldBe("Idle");

        editor.Press(Key.Y, RawInputModifiers.Control);

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Jump" });
        Selected(editor).ShouldBe(new[] { "Attack" });
    }

    [AvaloniaFact]
    public async Task DeleteKey_OnEverySequence_LeavesAnEmptyList_AndUndoBringsAllBackSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 3, RawInputModifiers.Shift);
        Selected(editor).Count.ShouldBe(4);

        editor.Press(Key.Delete);

        editor.SequenceNames.ShouldBeEmpty();
        editor.UndoLabels.ShouldBe(new[] { "Delete 4 Sequences" });

        Undo(editor);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        Selected(editor).ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
    }

    [AvaloniaFact]
    public async Task RowMenuDelete_OnARowInsideTheSelection_KeepsTheSelection_AndDeletesAllOfIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);

        editor.RightClick(Row(editor, 2));
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
        editor.PickMenuItem(List(editor).ContextMenu!, "Delete 2 Sequences");
        editor.Layout();

        SavedNames(boss).ShouldBe(new[] { "Attack", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Sequences" });
    }

    [AvaloniaFact]
    public async Task RowMenuDelete_OnARowOutsideTheSelection_SelectsOnlyThatRow_AndDeletesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 1);

        editor.RightClick(Row(editor, 3));
        Selected(editor).ShouldBe(new[] { "Jump" });
        editor.PickMenuItem(List(editor).ContextMenu!, "Delete");
        editor.Layout();

        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack", "Run" });
        editor.UndoLabels.ShouldBe(new[] { "Delete Sequence 'Jump'" });
    }

    [AvaloniaFact]
    public async Task RowMenuDuplicate_OnSeveralSelected_CopiesThemAsABlockAfterTheLast_SelectsTheCopies_AndOpensNoRenameBox()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);

        editor.RightClick(Row(editor, 0));
        editor.PickMenuItem(List(editor).ContextMenu!, "Duplicate 2 Sequences");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Idle 2", "Run 2", "Jump" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack", "Run", "Idle 2", "Run 2", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate 2 Sequences" });
        Selected(editor).ShouldBe(new[] { "Idle 2", "Run 2" });
        InspectorName(editor).ShouldBe("Idle 2");
        AnyRenameBoxOpen(editor).ShouldBeFalse();

        Undo(editor);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
    }

    [AvaloniaFact]
    public async Task CtrlD_AndEditMenuDuplicate_DuplicateTheWholeSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        SelectRows(editor, 1, 3);

        await CtrlAsync(editor, Key.D);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump", "Attack 2", "Jump 2" });
        Selected(editor).ShouldBe(new[] { "Attack 2", "Jump 2" });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate 2 Sequences" });

        SelectRows(editor, 0, 1);
        editor.ClickMenu("MenuDuplicate");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Idle 2", "Attack 3", "Run", "Jump", "Attack 2", "Jump 2" });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate 2 Sequences", "Duplicate 2 Sequences" });
    }

    [AvaloniaFact]
    public async Task CtrlC_ThenCtrlV_CopiesEverySelectedSequence_AndPastesThemAfterTheSelection_SelectingThePastedSet()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 1, 0);

        await CtrlAsync(editor, Key.C);

        (await ClipboardSequenceNamesAsync(editor)).ShouldBe(new[] { "Idle", "Attack" });
        editor.UndoLabels.ShouldBeEmpty();

        ClickRow(editor, 3);
        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldBeNull();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump", "Idle 2", "Attack 2" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack", "Run", "Jump", "Idle 2", "Attack 2" });
        editor.UndoLabels.ShouldBe(new[] { "Paste 2 Sequences" });
        Selected(editor).ShouldBe(new[] { "Idle 2", "Attack 2" });
        InspectorName(editor).ShouldBe("Idle 2");
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "hero.achx" });

        Undo(editor);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        Selected(editor).ShouldBe(new[] { "Jump" });
    }

    [AvaloniaFact]
    public async Task CtrlX_CopiesTheSelection_ThenDeletesItAsOneUndoEntry_AndPasteBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);

        await CtrlAsync(editor, Key.X);

        (await ClipboardSequenceNamesAsync(editor)).ShouldBe(new[] { "Idle", "Run" });
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Sequences" });
        Selected(editor).ShouldBe(new[] { "Attack" });

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Run", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Sequences", "Paste 2 Sequences" });
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
    }

    [AvaloniaFact]
    public async Task EditMenuCutAndPaste_ActOnTheWholeSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        SelectRows(editor, 0, 1);

        editor.ClickMenu("MenuCut");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.SequenceNames.ShouldBe(new[] { "Run", "Jump" });
        (await ClipboardSequenceNamesAsync(editor)).ShouldBe(new[] { "Idle", "Attack" });

        ClickRow(editor, 1);
        editor.ClickMenu("MenuPaste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.SequenceNames.ShouldBe(new[] { "Run", "Jump", "Idle", "Attack" });
        Selected(editor).ShouldBe(new[] { "Idle", "Attack" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Sequences", "Paste 2 Sequences" });
    }

    [AvaloniaFact]
    public async Task Paste_IntoAnotherSeqjTab_RewritesSourcesRelativeToIt_AsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        string other = WriteOther(editor, "Fly");
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 1);
        await CtrlAsync(editor, Key.C);
        await editor.OpenAsync(other);
        ClickRow(editor, 0);

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Fly", "Idle", "Attack" });
        Selected(editor).ShouldBe(new[] { "Idle", "Attack" });
        var saved = SequenceListSave.FromFile(other);
        saved.Sources.ShouldBe(new[] { "../hero.achx" });
        saved.Sequences.Skip(1).SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source)
            .ShouldBe(new[] { "../hero.achx", "../hero.achx" });
        editor.UndoLabels.ShouldBe(new[] { "Paste 2 Sequences" });

        Undo(editor);

        SequenceListSave.FromFile(other).Sources.ShouldBeEmpty();
        editor.SequenceNames.ShouldBe(new[] { "Fly" });
    }

    private static MenuItem RowMenuItem(AnimationEditorHarness editor, string header) =>
        List(editor).ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);

    private static List<string> RowMenuHeaders(AnimationEditorHarness editor) =>
        List(editor).ContextMenu!.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ToList();

    [AvaloniaFact]
    public async Task RowMenuRename_IsDisabled_WhileSeveralAreSelected_AndEnablesOnceOneIsLeft()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);

        editor.RightClick(Row(editor, 0));

        RowMenuItem(editor, "Rename…").IsEnabled.ShouldBeFalse();
        RowMenuItem(editor, "Duplicate 2 Sequences").IsEnabled.ShouldBeTrue();
        RowMenuItem(editor, "Delete 2 Sequences").IsEnabled.ShouldBeTrue();
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
        List(editor).ContextMenu!.Close();

        ClickRow(editor, 2, RawInputModifiers.Control);
        editor.RightClick(Row(editor, 0));

        Selected(editor).ShouldBe(new[] { "Idle" });
        RowMenuItem(editor, "Rename…").IsEnabled.ShouldBeTrue();
        editor.Dialogs.AnswerNextPrompt("Charge");
        editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
        editor.Layout();
        SavedNames(boss).ShouldBe(new[] { "Charge", "Attack", "Run", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Charge'" });
    }

    [AvaloniaFact]
    public async Task RenameHotkey_DoesNothing_WhileSeveralAreSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        SelectRows(editor, 0, 2);

        editor.Press(Key.F2);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();

        AnyRenameBoxOpen(editor).ShouldBeFalse();
        SavedNames(boss).ShouldBe(new[] { "Idle", "Attack", "Run", "Jump" });
        editor.UndoLabels.ShouldBeEmpty();
        Selected(editor).ShouldBe(new[] { "Idle", "Run" });
    }

    [AvaloniaFact]
    public async Task InspectorNameBox_IsDisabled_WhileSeveralAreSelected_AndEnablesOnceOneIsLeft()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        ClickRow(editor, 0);
        inspector.SequenceNameBox.IsEnabled.ShouldBeTrue();

        ClickRow(editor, 2, RawInputModifiers.Control);
        inspector.SequenceNameBox.IsEnabled.ShouldBeFalse();

        ClickRow(editor, 2, RawInputModifiers.Control);
        inspector.SequenceNameBox.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task RowMenuLabels_NameASingleSequence_AndCountSeveral()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 1);
        editor.RightClick(Row(editor, 1));

        RowMenuHeaders(editor).ShouldBe(new[] { "Copy", "Cut", "Paste", "Duplicate Sequence", "Rename…", "Copy Qualified Name", "Delete", "Add Sequence" });
        List(editor).ContextMenu!.Close();

        SelectRows(editor, 0, 1, 2);
        editor.RightClick(Row(editor, 1));

        RowMenuHeaders(editor).ShouldBe(new[] { "Copy 3 Sequences", "Cut 3 Sequences", "Paste", "Duplicate 3 Sequences", "Rename…", "Copy Qualified Name", "Delete 3 Sequences", "Add Sequence" });
    }

    [AvaloniaFact]
    public async Task Paste_OfASingleCopiedSequence_StillWorksAsBefore()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBoss(editor));
        ClickRow(editor, 1);
        await CtrlAsync(editor, Key.C);

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Attack 2", "Run", "Jump" });
        editor.UndoLabels.ShouldBe(new[] { "Paste Sequence 'Attack 2'" });
        Selected(editor).ShouldBe(new[] { "Attack 2" });
    }
}
