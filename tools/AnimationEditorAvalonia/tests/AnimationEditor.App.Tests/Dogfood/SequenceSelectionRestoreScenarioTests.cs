using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab remembers its selected sequence by name across a restart (#1651). It is view state: it is
/// kept in the editor settings, never in the file or the undo history.
/// </summary>
public class SequenceSelectionRestoreScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static void ClickRow(AnimationEditorHarness editor, int row) => editor.Click((Control)List(editor).ContainerFromIndex(row)!);

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    private static void ShouldShowSequence(AnimationEditorHarness editor, int index, string name)
    {
        List(editor).SelectedIndex.ShouldBe(index);
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
        Inspector(editor).SequenceNameBox.Text.ShouldBe(name);
    }

    private static void WriteSeqj(string path, params string[] names)
    {
        SequenceListSave save = new SequenceListSave();
        save.Sequences.AddRange(names.Select(name => new SequenceSave { Name = name }));
        save.Save(path);
    }

    /// <summary>A folder holding boss.seqj and a settings root, which outlive each editor window so a second one can "restart" on them.</summary>
    private sealed class Session : IDisposable
    {
        public Session(params string[] names)
        {
            Folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
            SettingsRoot = Path.Combine(Folder, "settings");
            Directory.CreateDirectory(Folder);
            Boss = Path.Combine(Folder, "boss.seqj");
            WriteSeqj(Boss, names);
        }

        public string Folder { get; }
        public string SettingsRoot { get; }
        public string Boss { get; }

        public AnimationEditorHarness Start() => new AnimationEditorHarness(SettingsRoot);

        /// <summary>Starts the editor on the saved session and waits for its tab to come back.</summary>
        public AnimationEditorHarness Restart()
        {
            var editor = Start();
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            editor.Layout();
            return editor;
        }

        public AppSettingsModel StoredSettings() =>
            JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(AppSettingsLocation.ForApplicationDataRoot(SettingsRoot).FullPath))!;

        public void Dispose()
        {
            try { Directory.Delete(Folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task TheSelectedSequence_IsStillSelectedAfterARestart_WithNoUndoEntryAndTheFileUntouched()
    {
        using Session session = new Session("Idle", "Action", "Death");
        byte[] before = File.ReadAllBytes(session.Boss);
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            ClickRow(editor, 1);
            ShouldShowSequence(editor, 1, "Action");
            editor.UndoLabels.ShouldBeEmpty();
        }

        using (AnimationEditorHarness editor = session.Restart())
        {
            ShouldShowSequence(editor, 1, "Action");
            editor.UndoLabels.ShouldBeEmpty();
        }

        File.ReadAllBytes(session.Boss).ShouldBe(before);
        session.StoredSettings().SelectedSequences[new FilePath(session.Boss).Standardized].ShouldBe("Action");
    }

    [AvaloniaFact]
    public async Task ReopeningTheFile_AfterClosingItsTab_SelectsTheRememberedSequence()
    {
        using Session session = new Session("Idle", "Action", "Death");
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        ClickRow(editor, 2);

        editor.CloseTab("boss.seqj");
        await editor.OpenAsync(session.Boss);

        ShouldShowSequence(editor, 2, "Death");
    }

    [AvaloniaFact]
    public async Task RenamingTheSelectedSequence_KeepsItSelectedAfterARestart()
    {
        using Session session = new Session("Idle", "Action", "Death");
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            editor.RightClick(List(editor).ContainerFromIndex(1)!);
            editor.Dialogs.AnswerNextPrompt("Strike");
            editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
            editor.Layout();
            ShouldShowSequence(editor, 1, "Strike");
        }

        using (AnimationEditorHarness editor = session.Restart())
            ShouldShowSequence(editor, 1, "Strike");
    }

    [AvaloniaFact]
    public async Task DeletingTheSelectedSequence_RemembersTheOneThatTookItsPlace()
    {
        using Session session = new Session("Idle", "Action", "Death");
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            ClickRow(editor, 1);
            editor.Press(Key.Delete);
            ShouldShowSequence(editor, 1, "Death");
        }

        using (AnimationEditorHarness editor = session.Restart())
            ShouldShowSequence(editor, 1, "Death");
    }

    [AvaloniaFact]
    public async Task ARememberedSequenceDeletedOutsideTheEditor_FallsBackToTheFirst()
    {
        using Session session = new Session("Idle", "Action", "Death");
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            ClickRow(editor, 1);
        }
        WriteSeqj(session.Boss, "Idle", "Death");

        using (AnimationEditorHarness editor = session.Restart())
        {
            ShouldShowSequence(editor, 0, "Idle");
            editor.UndoLabels.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public async Task UndoAndRedoOfAnAdd_MoveTheSelection_AndARestartFollowsWhereTheyLeftIt()
    {
        using Session session = new Session("Idle", "Action");
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            ClickRow(editor, 1);
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            ShouldShowSequence(editor, 2, "Sequence");

            Undo(editor);
            ShouldShowSequence(editor, 1, "Action");
            editor.Press(Key.Y, RawInputModifiers.Control);
            ShouldShowSequence(editor, 2, "Sequence");
            Undo(editor);
            ShouldShowSequence(editor, 1, "Action");
        }

        using (AnimationEditorHarness editor = session.Restart())
        {
            ShouldShowSequence(editor, 1, "Action");
            editor.UndoLabels.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public async Task EachTab_KeepsItsOwnSelection_WhenSwitchingBetweenTwoFiles()
    {
        using Session session = new Session("Idle", "Action", "Death");
        string other = Path.Combine(session.Folder, "minion.seqj");
        WriteSeqj(other, "Walk", "Bite");
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        ClickRow(editor, 2);
        await editor.OpenAsync(other);
        ClickRow(editor, 1);

        editor.ClickTab("boss.seqj");
        ShouldShowSequence(editor, 2, "Death");
        editor.ClickTab("minion.seqj");
        ShouldShowSequence(editor, 1, "Bite");
    }
}
