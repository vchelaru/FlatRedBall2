using FlatRedBall2.AnimationEditorCommon;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The SEQUENCES list's right-click menu follows what was hit: a sequence row offers Copy, Cut, Paste, Duplicate Sequence, Rename,
/// Copy Qualified Name, Delete and Add Sequence; blank space offers Add Sequence and Paste. (The popup is not part of the headless back-buffer, so the
/// menus are checked by their items rather than by screenshot.)
/// </summary>
public class SequenceListMenuScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static List<string> Headers(ListBox list) =>
        list.ContextMenu!.Items.Select(item => item is MenuItem menuItem ? (string)menuItem.Header! : "-").ToList();

    private static void RightClickBlankSpace(AnimationEditorHarness editor)
    {
        ListBox list = List(editor);
        editor.RightClickAt(editor.PointIn(list, 20, list.Bounds.Height - 10));
    }

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    [AvaloniaFact]
    public async Task RightClickingBlankSpace_OffersAddSequenceAndPaste()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle", "Attack"));

        RightClickBlankSpace(editor);

        List(editor).ContextMenu!.IsOpen.ShouldBeTrue();
        Headers(List(editor)).ShouldBe(new[] { "Add Sequence", "-", "Paste" });
    }

    [AvaloniaFact]
    public async Task RightClickingARow_SelectsIt_AndOffersCopyCutPasteDuplicate_RenameCopyName_Delete_ThenAddSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle", "Attack"));
        List(editor).SelectedIndex.ShouldBe(0);

        editor.RightClick(List(editor).ContainerFromIndex(1)!);

        List(editor).SelectedIndex.ShouldBe(1);
        Headers(List(editor)).ShouldBe(new[] { "Copy", "Cut", "Paste", "Duplicate Sequence", "-", "Rename…", "Copy Qualified Name", "-", "Delete", "-", "Add Sequence" });
    }

    [AvaloniaFact]
    public async Task CopyQualifiedName_FromARowMenu_CopiesTheRightClickedSequenceInItsFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Heavy Attack");
        await editor.OpenAsync(boss);
        editor.RightClick(List(editor).ContainerFromIndex(1)!);

        editor.PickMenuItem(List(editor).ContextMenu!, "Copy Qualified Name");
        string? copied = await editor.Window.Clipboard!.TryGetTextAsync();

        copied.ShouldBe($"Heavy Attack in {boss.Replace('\\', '/')}");
    }

    [AvaloniaFact]
    public async Task TheMenuFollowsEachRightClick_BlankThenRowThenBlank()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));

        RightClickBlankSpace(editor);
        Headers(List(editor)).Count.ShouldBe(3);
        List(editor).ContextMenu!.Close();
        editor.RightClick(List(editor).ContainerFromIndex(0)!);
        Headers(List(editor)).Count.ShouldBe(11);
        List(editor).ContextMenu!.Close();
        RightClickBlankSpace(editor);
        Headers(List(editor)).ShouldBe(new[] { "Add Sequence", "-", "Paste" });
    }

    [AvaloniaFact]
    public async Task AddSequence_FromTheBlankSpaceMenu_AddsAndSelectsOne_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        RightClickBlankSpace(editor);

        editor.PickMenuItem(List(editor).ContextMenu!, "Add Sequence");

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        List(editor).SelectedIndex.ShouldBe(1);
        SavedNames(boss).ShouldBe(new[] { "Idle", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task AddSequence_FromARowMenu_AddsOneAtTheEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle", "Attack"));
        editor.RightClick(List(editor).ContainerFromIndex(0)!);

        editor.PickMenuItem(List(editor).ContextMenu!, "Add Sequence");

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Sequence" });
        editor.UndoLabels.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task RenameAndDelete_FromARowMenu_ActOnTheRightClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");
        await editor.OpenAsync(boss);
        editor.RightClick(List(editor).ContainerFromIndex(1)!);

        editor.Dialogs.AnswerNextPrompt("Charge");
        editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
        editor.Layout();
        SavedNames(boss).ShouldBe(new[] { "Idle", "Charge" });

        editor.RightClick(List(editor).ContainerFromIndex(0)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Delete");
        editor.Layout();
        SavedNames(boss).ShouldBe(new[] { "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Attack' to 'Charge'", "Delete Sequence 'Idle'" });
    }
}
