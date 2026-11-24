using AnimationEditor.Views.Dialogs;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Saving under another type's extension through the real window: the problem banner says what to type, the other
/// file stays as it is, and the document keeps its tab and file.
/// </summary>
public class SaveAsExtensionScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="sheet.png" width="64" height="64"/>
        </tileset>
        """;

    private static string WriteOther(AnimationEditorHarness editor, string name)
    {
        string path = Path.Combine(editor.ProjectFolder, name);
        File.WriteAllText(path, "another file");
        return path;
    }

    [AvaloniaTheory]
    [InlineData("hero.achx", "boss.tsx", "boss.tsx is not an .achj or .achx file. Give it an .achj or .achx extension.")]
    [InlineData("hero.tsx", "boss.achx", "boss.achx is not a .tsx file. Give it a .tsx extension.")]
    public async Task SaveAs_AnotherTypesExtension_ShowsTheProblemBanner_AndKeepsTheDocument(string heroName, string picked, string message)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = Path.Combine(editor.ProjectFolder, heroName);
        if (heroName.EndsWith(".tsx"))
            File.WriteAllText(hero, TsxXml);
        else
            editor.WriteAchx(heroName, AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = WriteOther(editor, picked);
        await editor.OpenAsync(hero);

        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe(message);
        File.ReadAllText(boss).ShouldBe("another file");
        editor.TabLabels.ShouldBe(new[] { heroName });
        editor.Services.ProjectManager.FileName.ShouldBe(new AnimationEditor.Core.Paths.FilePath(hero).FullPath);
    }

    // File > New asks where to save at once; so do closing the Untitled tab and the window with Save.
    [AvaloniaFact]
    public void FileNew_ThenCloseTabAndWindow_AnotherTypesExtension_StaysOpenAndUntitled()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteOther(editor, "boss.seqj");
        const string message = "boss.seqj is not an .achj or .achx file. Give it an .achj or .achx extension.";
        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ProblemText.ShouldBe(message);
        editor.Services.AppCommands.AddAnimationChainWithName("Walk");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        string untitled = editor.Tabs.ActiveTab!.DisplayName;

        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.CloseTab(untitled);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.Window.Close();
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Window.IsVisible.ShouldBeTrue();
        editor.ProblemText.ShouldBe(message);
        File.ReadAllText(boss).ShouldBe("another file");
        editor.TabLabels.ShouldBe(new[] { untitled });
        editor.Services.ProjectManager.FileName.ShouldBeNull();
        editor.Project.AnimationChains.Single().Name.ShouldBe("Walk");
    }

    [AvaloniaFact]
    public void NewSequenceFile_AnotherTypesExtension_IsRefused_AndOpensNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteOther(editor, "boss.achx");

        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.ClickMenu("MenuNewSequence");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe("Could not create boss.achx: it is not a .seqj file. Give it a .seqj extension.");
        File.ReadAllText(boss).ShouldBe("another file");
        editor.TabLabels.ShouldNotContain("boss.achx");
    }
}
