using AnimationEditor.Core.Models;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #2036: a Save As whose write fails saved nothing, so the tab keeps its name, its file and its content, and nothing that
/// asked for the save (the tab close prompt, the window close prompt) goes on as if it had.
/// </summary>
public class SaveAsFailedScenarioTests
{
    /// <summary>A file that cannot be written, in a folder of its own: on Linux that is an unwritable folder.</summary>
    private static string UnwritableFile(AnimationEditorHarness editor, string name)
    {
        string path = Path.Combine(Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "readonly")).FullName, name);
        File.WriteAllText(path, "");
        UnusualProjectScenarioTests.MakeUnwritable(path);
        return path;
    }

    /// <summary>File &gt; New with the save dialog cancelled, then a chain in the new Untitled tab.</summary>
    private static string NewUntitledTabWithAChain(AnimationEditorHarness editor)
    {
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        return editor.Tabs.ActiveTab!.DisplayName;
    }

    private static List<string> RecentFiles(AnimationEditorHarness editor) =>
        ((AppSettingsModel)typeof(MainWindow).GetField("_appSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(editor.Window)!).RecentFiles.ToList();

    [AvaloniaTheory]
    [InlineData("save")]
    [InlineData("save as")]
    [InlineData("close tab")]
    [InlineData("close window")]
    public async Task SavingAnUntitledTab_WhereTheFileCannotBeWritten_KeepsItOpenAndUntitled(string how)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string untitled = NewUntitledTabWithAChain(editor);
        string taken = UnwritableFile(editor, "taken.achj");
        try
        {
            editor.Dialogs.AnswerNextSaveFile(taken);
            if (how.StartsWith("close")) editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
            switch (how)
            {
                case "save": editor.ClickMenu("MenuSave"); break;
                case "save as": editor.ClickMenu("MenuSaveAs"); break;
                case "close tab": editor.CloseTab(untitled); break;
                default: editor.Window.Close(); break;
            }
            await editor.WaitUntilAsync(() => editor.ProblemText != null, TimeSpan.FromSeconds(2));
            await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromMilliseconds(300));

            editor.Window.IsVisible.ShouldBeTrue();
            editor.TabLabels.ShouldBe(new[] { untitled });
            editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");
            editor.Services.ProjectManager.FileName.ShouldBeNullOrEmpty();
            editor.Services.IoManager.RecoveryFileExists().ShouldBeTrue("a crash now must not lose the document");
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Not saved");
            RecentFiles(editor).ShouldBeEmpty();
            editor.ProblemText!.ShouldStartWith("Could not save taken.achj: ");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(taken);
        }
    }

    [AvaloniaTheory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    [InlineData("hero.tsx")]
    [InlineData("hero.seqj")]
    public async Task SaveAs_WhereTheFileCannotBeWritten_LeavesTheTabOnItsOwnFile(string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, name);
        string extension = Path.GetExtension(name);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        if (extension == ".achj")
            new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, AnimationChains = { walk } }.SaveJson(path);
        else if (extension == ".tsx")
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
                 <image source="sheet.png" width="64" height="64"/>
                </tileset>
                """);
        else if (extension == ".seqj")
            editor.WriteSeqj(name, "Attack");
        else
            editor.WriteAchx(name, walk);
        await editor.OpenAsync(path);
        string title = editor.TitleFileName;
        string? status = editor.Control<TextBlock>("StatusSaveLabel").Text;
        string taken = UnwritableFile(editor, "taken" + extension);
        try
        {
            editor.Dialogs.AnswerNextSaveFile(taken);
            editor.ClickMenu("MenuSaveAs");
            editor.Wait(TimeSpan.FromMilliseconds(100));

            editor.TabLabels.ShouldBe(new[] { name });
            editor.TitleFileName.ShouldBe(title);
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe(status);
            RecentFiles(editor).ShouldAllBe(recent => !recent.Contains("taken"));
            editor.ProblemText!.ShouldStartWith($"Could not save taken{extension}: ");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(taken);
        }

        // Its own file holds everything, so closing asks nothing.
        editor.CloseTab(name);
        editor.TabLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.ShouldNotContain(shown => shown.StartsWith("save-discard-cancel"));
    }
}
