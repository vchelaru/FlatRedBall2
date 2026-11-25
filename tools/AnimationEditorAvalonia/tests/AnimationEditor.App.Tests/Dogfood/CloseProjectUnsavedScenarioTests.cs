using AnimationEditor.Views.Controls;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// File &gt; Close Project Folder with a tab whose edits its file lacks asks the question closing that tab asks, before it
/// closes anything: Save, Don't Save, or Cancel, which keeps the project and every tab open.
/// </summary>
public class CloseProjectUnsavedScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="sheet.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="100"/>
           <frame tileid="1" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static string Prompt(string file) =>
        $"save-discard-cancel: Unsaved Changes / \"{file}\" has unsaved changes. Save before closing?";

    /// <summary>Opens a project folder whose file "hero.(kind)" is open, unwritable and edited. The edit is an added chain or
    /// sequence, or a frame length. "outside.achx" of another folder is open too.</summary>
    private static async Task<string> OpenAndFailASaveAsync(AnimationEditorHarness editor, string kind, string name = "hero")
    {
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "game"));
        editor.WritePng("game/sheet.png", 64, 64);
        editor.WritePng("sheet.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, "game", $"{name}.{kind}");
        switch (kind)
        {
            case "achx": editor.WriteAchx($"game/{name}.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))); break;
            case "tsx": File.WriteAllText(path, TsxXml); break;
            default: editor.WriteSeqj($"game/{name}.seqj", "One"); break;
        }
        if (editor.Tabs.Tabs.Count == 0)
        {
            await editor.OpenProjectFolderAsync(Path.Combine(editor.ProjectFolder, "game"));
            await editor.OpenAsync(editor.WriteAchx("outside.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16))));
        }
        await editor.OpenAsync(path);
        UnusualProjectScenarioTests.MakeUnwritable(path);
        if (kind == "seqj") editor.Click(editor.Control<Button>("AddSequenceButton"));
        else editor.Services.AppCommands.SetFrameLength(editor.Project.AnimationChains[0].Frames, kind == "tsx" ? 300m : 0.5m);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
        return path;
    }

    private static async Task CloseProject(AnimationEditorHarness editor)
    {
        editor.Dialogs.Shown.Clear();
        editor.Window.CloseProjectFolder();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
    }

    private static bool ProjectIsOpen(AnimationEditorHarness editor) => editor.Services.ProjectManager.ProjectFolderPath is not null;

    [AvaloniaTheory]
    [InlineData("achx", true)]
    [InlineData("achx", false)]
    [InlineData("tsx", true)]
    [InlineData("tsx", false)]
    [InlineData("seqj", true)]
    [InlineData("seqj", false)]
    public async Task Cancel_KeepsTheProjectTheTabsAndTheEdit(string kind, bool inFront)
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenAndFailASaveAsync(editor, kind);
        try
        {
            if (!inFront) editor.ClickTab("outside.achx");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            await CloseProject(editor);

            editor.Dialogs.Shown.ShouldBe([Prompt($"hero.{kind}")]);
            editor.TabLabels.ShouldBe(["outside.achx", $"hero.{kind}"]);
            ProjectIsOpen(editor).ShouldBeTrue();
            editor.Control<ProjectPanelControl>("ProjectPanel").TreeRoots.ShouldNotBeEmpty();
            if (!inFront) editor.ClickTab($"hero.{kind}");
            if (kind == "seqj") editor.SequenceNames.Count.ShouldBe(2);
            else editor.Project.AnimationChains[0].Frames[0].FrameLength.ShouldBe(kind == "tsx" ? 300f : 0.5f);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaTheory]
    [InlineData("achx")]
    [InlineData("tsx")]
    [InlineData("seqj")]
    public async Task Save_WritesTheEdit_AndDontSaveDropsIt(string kind)
    {
        foreach (var choice in new[] { SaveDiscardCancelChoice.Save, SaveDiscardCancelChoice.Discard })
        {
            using var editor = new AnimationEditorHarness();
            string path = await OpenAndFailASaveAsync(editor, kind);
            UnusualProjectScenarioTests.MakeWritable(path);
            string before = File.ReadAllText(path);
            editor.ClickTab("outside.achx");
            editor.Dialogs.AnswerNextSaveDiscardCancel(choice);

            await CloseProject(editor);

            editor.TabLabels.ShouldBe(["outside.achx"]);
            ProjectIsOpen(editor).ShouldBeFalse();
            (File.ReadAllText(path) != before).ShouldBe(choice == SaveDiscardCancelChoice.Save);
            // Opened again it is the file, not the document that was closed.
            await editor.OpenAsync(path);
            if (kind == "seqj") editor.SequenceNames.Count.ShouldBe(choice == SaveDiscardCancelChoice.Save ? 2 : 1);
        }
    }

    [AvaloniaFact]
    public async Task ASaveThatFailsAgain_KeepsEverythingOpen()
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenAndFailASaveAsync(editor, "achx");
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

            await CloseProject(editor);

            editor.TabLabels.ShouldBe(["outside.achx", "hero.achx"]);
            ProjectIsOpen(editor).ShouldBeTrue();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task SeveralUnsaved_AskOncePerTab_AndACancelClosesNone()
    {
        if (!OperatingSystem.IsWindows()) return; // elsewhere an unwritable file is an unwritable folder, so the second cannot be written
        using var editor = new AnimationEditorHarness();
        string hero = await OpenAndFailASaveAsync(editor, "achx");
        string fight = await OpenAndFailASaveAsync(editor, "seqj", "fight");
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);
            await CloseProject(editor);
            editor.Dialogs.Shown.ShouldBe([Prompt("hero.achx"), Prompt("fight.seqj")]);
            editor.TabLabels.ShouldBe(["outside.achx", "hero.achx", "fight.seqj"]);
            editor.SequenceNames.Count.ShouldBe(2);

            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            await CloseProject(editor);
            editor.TabLabels.ShouldBe(["outside.achx"]);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
            UnusualProjectScenarioTests.MakeWritable(fight);
        }
    }

    [AvaloniaTheory]
    [InlineData("project")]
    [InlineData("window")]
    public async Task ClosingAgainWhileTheQuestionIsUp_AsksOnce_AndClosesNothing(string second)
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenAndFailASaveAsync(editor, "achx");
        try
        {
            var answer = new TaskCompletionSource<SaveDiscardCancelChoice>();
            int asked = 0;
            editor.Window.ShowSaveDiscardCancelDialogAsync = (_, _) => { asked++; return answer.Task; };
            await CloseProject(editor);

            if (second == "project") await CloseProject(editor);
            else editor.Window.Close();
            await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

            asked.ShouldBe(1);
            editor.Window.IsVisible.ShouldBeTrue();
            answer.SetResult(SaveDiscardCancelChoice.Cancel);
            await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
            ProjectIsOpen(editor).ShouldBeTrue();
            editor.TabLabels.ShouldBe(["outside.achx", "hero.achx"]);

            // The answer ends it: the next close asks again.
            editor.Window.ShowSaveDiscardCancelDialogAsync = editor.Dialogs.SaveDiscardCancelAsync;
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            await CloseProject(editor);
            editor.TabLabels.ShouldBe(["outside.achx"]);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ATypedLengthThatCannotBeSaved_IsAskedAbout()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.Window.SidebarTabs.SelectedItem = editor.Control<TabItem>("InspectorTab");
        editor.Expand(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Walk").Frames[0]);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            TextBox box = editor.Control<FlankerNumericField>("PropFrameLen").GetVisualDescendants().OfType<TextBox>().First();
            box.Focus();
            box.SelectAll();
            editor.Type("0.5");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            await CloseProject(editor);

            editor.Dialogs.Shown.ShouldBe([Prompt("hero.achx")]);
            editor.TabLabels.ShouldBe(["hero.achx"]);
            editor.ChainNamed("Walk").Frames[0].FrameLength.ShouldBe(0.5f);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UntitledContent_StaysOpen_WithItsRecoveryFile(bool hasATab)
    {
        using var editor = new AnimationEditorHarness();
        await editor.OpenProjectFolderAsync();
        if (hasATab)
        {
            editor.Dialogs.AnswerNextSaveFile(null);
            editor.ClickMenu("MenuNew");
            editor.Wait(TimeSpan.FromMilliseconds(100));
        }
        editor.Services.AppCommands.AddAnimationChainWithName("Pasted"); // a paste into an empty editor opens no tab
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Services.IoManager.FindRecoveryFiles().ShouldHaveSingleItem();

        await CloseProject(editor);

        editor.Dialogs.Shown.ShouldBeEmpty();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(["Pasted"]);
        editor.TabLabels.Count.ShouldBe(1);
        editor.Services.IoManager.FindRecoveryFiles().ShouldHaveSingleItem();
        ProjectIsOpen(editor).ShouldBeFalse();
    }
}
