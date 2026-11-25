using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)), AnimationEditorHarness.Chain("Run", "sheet.png", (0, 0, 16, 16)));
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
            case "undo": editor.Press(Key.Z, RawInputModifiers.Control); break;
        }
        editor.Layout();

        editor.DeletedToastText.ShouldBeNull();
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
}
