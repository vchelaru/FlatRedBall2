using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The deleted toast's Undo undoes that deletion only: the toast is gone once something else would be undone (#2183).</summary>
public class DeletedToastUndoScenarioTests
{
    private static async Task<(string Hero, string Other)> OpenAndDeleteRunAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        string hero = editor.WriteAchx("hero.achx",
            walk, AnimationEditorHarness.Chain("Run", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        editor.Services.AppCommands.AddAnimationChainWithName("Added");
        await editor.OpenAsync(hero);
        editor.Services.AppCommands.AddAnimationChainWithName("First");
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        editor.DeletedToastText.ShouldBe("\"Run\" deleted");
        return (hero, other);
    }

    [AvaloniaTheory]
    [InlineData("tab")]
    [InlineData("png tab")]
    [InlineData("close")]
    [InlineData("reload")]
    [InlineData("another edit")]
    [InlineData("undo")]
    [InlineData("save as to another folder")] // which clears the undo history
    public async Task WhenTheDeletionIsNotTheLastStepInFront_TheToastIsGone(string since)
    {
        using var editor = new AnimationEditorHarness();
        var (hero, _) = await OpenAndDeleteRunAsync(editor);

        switch (since)
        {
            case "tab": editor.ClickTab("other.achx"); break;
            case "png tab": await editor.OpenPngTabAsync(Path.Combine(editor.ProjectFolder, "sheet.png")); break;
            case "close": editor.CloseTab("hero.achx"); break;
            case "reload":
                File.WriteAllText(hero, File.ReadAllText(hero).Replace("Walk", "Stroll"));
                (await editor.WaitUntilAsync(() => editor.Project.AnimationChains[0].Name == "Stroll", TimeSpan.FromSeconds(3))).ShouldBeTrue();
                break;
            case "another edit": editor.Services.AppCommands.AddAnimationChainWithName("Second"); break;
            case "save as to another folder":
                editor.Dialogs.AnswerNextSaveFile(Path.Combine(Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "moved")).FullName, "hero.achx"));
                editor.ClickMenu("MenuSaveAs");
                await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
                break;
            case "undo": editor.Press(Key.Z, RawInputModifiers.Control); break;
        }
        editor.Layout();

        editor.DeletedToastText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task AfterSaveAs_TheToastStays_AndUndoesTheDeletion()
    {
        using var editor = new AnimationEditorHarness();
        await OpenAndDeleteRunAsync(editor);
        string path = Path.Combine(editor.ProjectFolder, "hero2.achx");

        editor.Dialogs.AnswerNextSaveFile(path);
        editor.ClickMenu("MenuSaveAs");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        editor.Click(editor.Notifications.ItemDeletedToastUndoBtn);
        editor.Layout();

        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(c => c.Name).ShouldBe(["Walk", "Run", "First"]);
    }

    [AvaloniaFact]
    public async Task AfterASelectionAndASave_TheToastStays_AndUndoesTheDeletion()
    {
        using var editor = new AnimationEditorHarness();
        var (hero, _) = await OpenAndDeleteRunAsync(editor);

        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Services.AppCommands.SaveCurrentAnimationChainList();
        editor.Layout();
        editor.Click(editor.Notifications.ItemDeletedToastUndoBtn);
        editor.Layout();

        AnimationEditorHarness.ReadSaved(hero).AnimationChains.Select(c => c.Name).ShouldBe(["Walk", "Run", "First"]);
        editor.DeletedToastText.ShouldBeNull();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeqjTabInFront_ToastIsGone_AndSequenceKept(bool edit)
    {
        using var editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.SequenceNames.Count.ShouldBe(2);
        await OpenAndDeleteRunAsync(editor);

        editor.ClickTab("boss.seqj");
        if (edit) editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.Layout();
        editor.DeletedToastText.ShouldBeNull();
        RaiseUndoClick(editor);

        editor.SequenceNames.Count.ShouldBe(edit ? 3 : 2);
    }

    [AvaloniaFact]
    public async Task SeqjOpenedFresh_ToastIsGone()
    {
        using var editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await OpenAndDeleteRunAsync(editor);

        await editor.OpenAsync(boss);

        editor.DeletedToastText.ShouldBeNull();
    }

    // Raised rather than clicked: a click takes the focus, which commits the name before Undo runs.
    [AvaloniaTheory]
    [InlineData("tree")]
    [InlineData("tree-unchanged")]
    [InlineData("inspector")]
    public async Task NameBeingTyped_ToastUndo(string where)
    {
        using var editor = new AnimationEditorHarness();
        var (hero, _) = await OpenAndDeleteRunAsync(editor);
        var walk = editor.ChainNamed("Walk");
        if (where == "inspector")
        {
            editor.Expand(walk);
            editor.Expand(walk.Frames[0]);
            editor.ClickRow(walk.Frames[0].ShapesSave!.AARectSaves.First());
            var box = editor.Control<TextBox>("PropRectName");
            box.Focus();
            editor.Layout();
            box.SelectAll();
        }
        else
        {
            editor.ClickRow(walk);
            editor.Press(Key.F2);
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.NodeFor(walk).IsEditing.ShouldBeTrue();
        }
        if (where != "tree-unchanged") editor.Type("Stroll");
        editor.DeletedToastText.ShouldNotBeNull();

        RaiseUndoClick(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        var saved = AnimationEditorHarness.ReadSaved(hero).AnimationChains;
        saved.Select(c => c.Name).ShouldBe(where switch
        {
            "tree" => ["Stroll", "First"],
            "tree-unchanged" => ["Walk", "Run", "First"],
            _ => ["Walk", "First"],
        });
        saved[0].Frames[0].ShapesSave!.AARectSaves.First().Name.ShouldBe(where == "inspector" ? "Stroll" : "Hit");
        editor.DeletedToastText.ShouldBeNull();
    }

    private static void RaiseUndoClick(AnimationEditorHarness editor)
    {
        editor.Notifications.ItemDeletedToastUndoBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        editor.Layout();
    }
}
