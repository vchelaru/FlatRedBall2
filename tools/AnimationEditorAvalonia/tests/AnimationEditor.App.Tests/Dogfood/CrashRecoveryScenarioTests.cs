using AnimationEditor.Views.Dialogs;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>What a crash leaves behind: every Untitled document with content that was open, until it is saved or discarded.</summary>
public class CrashRecoveryScenarioTests
{
    private static void NewUntitledWith(AnimationEditorHarness editor, string chain)
    {
        NewUntitled(editor);
        editor.Services.AppCommands.AddAnimationChainWithName(chain);
        editor.Wait(TimeSpan.FromMilliseconds(50));
    }

    private static void NewUntitled(AnimationEditorHarness editor)
    {
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(50));
    }

    private static string Recoverable(AnimationEditorHarness editor) => string.Join("\n", RecoveryFiles(editor).Select(File.ReadAllText));

    /// <summary>The recovery file and any other whose name it starts.</summary>
    private static string[] RecoveryFiles(AnimationEditorHarness editor)
    {
        string path = editor.Services.IoManager.RecoveryFilePath;
        return Directory.Exists(Path.GetDirectoryName(path))
            ? Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "*.achx") : [];
    }

    [AvaloniaFact]
    public void TwoUntitledDocuments_BothAreRecoverable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "First");
        NewUntitledWith(editor, "Second");

        string recoverable = Recoverable(editor);
        editor.Window.CloseWithoutPrompt();

        recoverable.ShouldContain("Second");
        recoverable.ShouldContain("First");
    }

    [AvaloniaFact]
    public void ClosingOneUntitledDocument_KeepsTheOtherRecoverable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "First");
        NewUntitled(editor);

        editor.CloseTab("Untitled (1)");    // empty, closes without asking
        editor.Wait(TimeSpan.FromMilliseconds(50));

        string recoverable = Recoverable(editor);
        editor.TabLabels.ShouldBe(new[] { "Untitled" });
        editor.Window.CloseWithoutPrompt();
        recoverable.ShouldContain("First");
    }

    [AvaloniaFact]
    public async Task SavingAnotherDocumentUnderANewName_KeepsTheUntitledOneRecoverable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        NewUntitledWith(editor, "First");
        await editor.OpenAsync(hero);
        editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, "hero2.achx"));

        await editor.Services.AppCommands.SaveCurrentAnimationChainListAsync();
        editor.Wait(TimeSpan.FromMilliseconds(100));

        string recoverable = Recoverable(editor);
        editor.TabLabels.ShouldBe(new[] { "Untitled", "hero2.achx" });
        editor.Window.CloseWithoutPrompt();
        recoverable.ShouldContain("First");
    }

    [AvaloniaFact]
    public void AfterACrash_EveryUntitledDocumentComesBack_UntilItIsDiscarded()
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using (AnimationEditorHarness crashed = new AnimationEditorHarness(recoveryFilePath: Path.Combine(folder, "a", "recovery.achx")))
            {
                NewUntitledWith(crashed, "First");
                NewUntitledWith(crashed, "Second");
                // A crash leaves the files where they are; copied aside so disposing this editor cannot tidy them.
                Directory.CreateDirectory(Path.Combine(folder, "b"));
                foreach (string file in RecoveryFiles(crashed)) File.Copy(file, Path.Combine(folder, "b", Path.GetFileName(file)));
                crashed.Window.CloseWithoutPrompt();
            }

            using AnimationEditorHarness editor = new AnimationEditorHarness(recoveryFilePath: Path.Combine(folder, "b", "recovery.achx"));
            editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();
            editor.Project.AnimationChains.Single().Name.ShouldBe("Second", "the one edited last is in front");
            editor.ClickTab("Untitled");
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Project.AnimationChains.Single().Name.ShouldBe("First");

            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            editor.CloseTab("Untitled");
            editor.Wait(TimeSpan.FromMilliseconds(50));
            Recoverable(editor).ShouldNotContain("First");
            Recoverable(editor).ShouldContain("Second");

            editor.Window.CloseWithoutPrompt();
            RecoveryFiles(editor).ShouldBeEmpty("a normal close is not a crash");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ARecoveredDocument_IsStillRecoverableAfterASecondCrash()
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string recovery = Path.Combine(folder, "recovery.achx");
            AnimationChainListSave crashed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
            crashed.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
            crashed.Save(recovery);

            using AnimationEditorHarness editor = new AnimationEditorHarness(recoveryFilePath: recovery);
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5)).ShouldBeTrue();

            string recoverable = Recoverable(editor);
            editor.Window.CloseWithoutPrompt();
            recoverable.ShouldContain("Recovered");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
