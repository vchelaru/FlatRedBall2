using AnimationEditor.Views.Controls;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// With no tab open, content that lands in the editor gets its Untitled tab at once, so every way out asks about it. Covers
/// each way content can land, and that the others land nothing.
/// </summary>
public class ContentWithoutATabScenarioTests
{
    private const string Prompt = "save-discard-cancel: Unsaved Changes / \"Untitled\" has unsaved changes. Save before closing?";

    /// <summary>An editor with no tab whose clipboard holds the animation Walk, or its frame.</summary>
    private static async Task<AnimationEditorHarness> OpenWithCopyAsync(bool frame = false)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(frame ? walk.Frames[0] : walk);
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.TabLabels.ShouldBeEmpty();
        editor.Project.AnimationChains.ShouldBeEmpty();
        editor.AnimTree.Focus();
        return editor;
    }

    private static async Task PasteAsync(AnimationEditorHarness editor)
    {
        editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
    }

    [AvaloniaTheory]
    [InlineData("Ctrl+V")]
    [InlineData("Edit menu")]
    [InlineData("cut, then Ctrl+V")]
    [InlineData("Add Animation")]
    public async Task LandingContent_OpensAnUntitledTab_AndClosingTheWindowAsks(string way)
    {
        using var editor = way.StartsWith("cut") ? new AnimationEditorHarness() : await OpenWithCopyAsync();
        if (way.StartsWith("cut"))
        {
            editor.WritePng("sheet.png", 64, 64);
            await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
            editor.ClickRow(editor.ChainNamed("Walk"));
            editor.Press(Key.X, RawInputModifiers.Control);
            await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
            editor.CloseTab("hero.achx");
            await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
            editor.AnimTree.Focus();
        }
        switch (way)
        {
            case "Edit menu": editor.ClickMenu("MenuPaste"); break;
            case "Add Animation":
                editor.Click(editor.Control<Button>("AddChainBtn"));
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.Press(Key.Enter);
                break;
            default: editor.Press(Key.V, RawInputModifiers.Control); break;
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.TabLabels.ShouldBe(["Untitled"]);
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.VisibleChainHeaders.Count.ShouldBe(1);
        editor.Dialogs.Shown.Clear();
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);
        editor.Window.Close();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Dialogs.Shown.ShouldBe([Prompt]);
        editor.Window.IsVisible.ShouldBeTrue();
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.Services.IoManager.FindRecoveryFiles().ShouldHaveSingleItem();
        editor.Window.CloseWithoutPrompt();
    }

    [AvaloniaTheory]
    [InlineData(SaveDiscardCancelChoice.Save)]
    [InlineData(SaveDiscardCancelChoice.Discard)]
    public async Task ClosingTheWindow_SavesOrDropsThePastedAnimation(SaveDiscardCancelChoice choice)
    {
        using var editor = await OpenWithCopyAsync();
        await PasteAsync(editor);
        string target = Path.Combine(editor.ProjectFolder, "new.achx");
        editor.Dialogs.AnswerNextSaveDiscardCancel(choice);
        if (choice == SaveDiscardCancelChoice.Save) editor.Dialogs.AnswerNextSaveFile(target);

        editor.Window.Close();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.Window.IsVisible.ShouldBeFalse();
        File.Exists(target).ShouldBe(choice == SaveDiscardCancelChoice.Save);
        if (choice == SaveDiscardCancelChoice.Save)
            AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Walk");
        editor.Services.IoManager.FindRecoveryFiles().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task RestartingForAnUpdate_AsksAboutAPastedAnimation()
    {
        using var editor = await OpenWithCopyAsync();
        await PasteAsync(editor);
        editor.Dialogs.Shown.Clear();
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        editor.Window.FindControl<Button>("RestartForUpdateBtn")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Dialogs.Shown.ShouldBe([Prompt]);
        ((FakeApplicationUpdater)editor.Services.ApplicationUpdater).RestartCount.ShouldBe(0);
        editor.Window.CloseWithoutPrompt();
    }

    [AvaloniaFact]
    public void RestartingAfterACrash_WithOneRecoveryFile_OpensOneTab()
    {
        string folder = Directory.CreateTempSubdirectory("AeRecovery").FullName;
        try
        {
            string recovery = Path.Combine(folder, "recovery.achx");
            var recovered = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
            recovered.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
            recovered.Save(recovery);

            using var editor = new AnimationEditorHarness(Path.Combine(folder, "settings"), recovery);
            editor.Wait(TimeSpan.FromMilliseconds(200));

            editor.TabLabels.ShouldBe(["Untitled"]);
            editor.Project.AnimationChains.Single().Name.ShouldBe("Recovered");
            editor.Window.CloseWithoutPrompt();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // Opening a document does these before its tab exists. None is an edit, so none opens a tab of its own.
    [AvaloniaTheory]
    [InlineData("Clear")]
    [InlineData("RestoreSnapshot")]
    [InlineData("MarkSaved")]
    [InlineData("MarkSaveFailed")]
    public async Task AHistoryChangeThatIsNoEdit_OpensNoTab(string change)
    {
        using var editor = await OpenWithCopyAsync();
        await PasteAsync(editor);
        var snapshot = editor.UndoManager.TakeSnapshot();
        snapshot.UndoStack.ShouldNotBeEmpty();
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        editor.CloseTab("Untitled");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.Services.ProjectManager.AnimationChainListSave!.AnimationChains.Add(new AnimationChainSave { Name = "Loading" });

        switch (change)
        {
            case "Clear": editor.UndoManager.Clear(); break;
            case "RestoreSnapshot": editor.UndoManager.RestoreSnapshot(snapshot); break;
            case "MarkSaved": editor.UndoManager.MarkSaved(); break;
            case "MarkSaveFailed": editor.UndoManager.MarkSaveFailed(); break;
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        editor.TabLabels.ShouldBeEmpty();
        editor.Window.CloseWithoutPrompt();
    }

    [AvaloniaFact]
    public async Task UndoAndRedoOfThePaste_KeepTheOneTab()
    {
        using var editor = await OpenWithCopyAsync();
        await PasteAsync(editor);

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.ShouldBeEmpty();
        editor.TabLabels.ShouldBe(["Untitled"]);
        editor.Press(Key.Y, RawInputModifiers.Control);

        editor.Project.AnimationChains.Single().Name.ShouldBe("Walk");
        editor.TabLabels.ShouldBe(["Untitled"]);
        editor.Window.CloseWithoutPrompt();
    }

    // With no tab there is no animation and no undo history, so nothing here has a target.
    [AvaloniaTheory]
    [InlineData("paste a frame")]
    [InlineData("duplicate")]
    [InlineData("redo")]
    [InlineData("drop on the tree")]
    [InlineData("drop on the canvas")]
    public async Task AWayThatLandsNothing_OpensNoTab(string way)
    {
        using var editor = await OpenWithCopyAsync(frame: way == "paste a frame");
        switch (way)
        {
            case "paste a frame": editor.Press(Key.V, RawInputModifiers.Control); break;
            case "duplicate": editor.Press(Key.D, RawInputModifiers.Control); break;
            case "redo": editor.Press(Key.Y, RawInputModifiers.Control); break;
            default:
                Control target = way.EndsWith("tree") ? editor.AnimTree : editor.Wireframe;
                var file = await editor.Window.StorageProvider.TryGetFileFromPathAsync(Path.Combine(editor.ProjectFolder, "sheet.png"));
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateFile(file!));
                target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, target, new Avalonia.Point(10, 10), KeyModifiers.None));
                break;
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.Project.AnimationChains.ShouldBeEmpty();
        editor.TabLabels.ShouldBeEmpty();
    }
}
