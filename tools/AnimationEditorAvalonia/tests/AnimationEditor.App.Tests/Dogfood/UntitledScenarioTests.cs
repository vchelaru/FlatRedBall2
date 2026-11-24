using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A fresh editor with nothing open: the untitled document takes edits, Ctrl+S asks where to
/// save, and a restart with the same settings reopens what was open.
/// </summary>
public class UntitledScenarioTests
{
    [AvaloniaFact]
    public void FreshEditor_AddChainThenCtrlS_AsksForAPath_AndWritesTheFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string target = Path.Combine(editor.ProjectFolder, "new.achx");
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(target).ShouldBeTrue();
        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Idle");
        editor.Services.ProjectManager.FileName.ShouldBe(target);
        editor.TitleFileName.ShouldContain("new");
        editor.TabLabels.ShouldContain(label => label.Contains("new"));
    }

    [AvaloniaFact]
    public async Task FileNew_AddChainAndFrames_SaveThenCloseAndReopen_ShowsTheSameDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        AnimationChainSave idle = editor.ChainNamed("Idle");
        editor.ClickRow(idle);
        editor.Click(editor.RowButton(idle, "Add Frame"));
        editor.Click(editor.RowButton(idle, "Add Frame"));
        editor.Click(editor.RowButton(idle, "Add Frame"));
        editor.ClickRow(idle.Frames[2]);
        editor.TypeFlanker("PropFrameLen", "0.5");
        string target = Path.Combine(editor.ProjectFolder, "idle.achx");
        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.ThrowIfErrorShown();
        int dialogsBeforeClose = editor.Dialogs.Shown.Count;

        editor.CloseTab("idle.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Dialogs.Shown.Count.ShouldBe(dialogsBeforeClose, "a saved document closes without asking");
        await editor.OpenAsync(target);

        editor.TabLabels.ShouldContain("idle.achx");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Idle" });
        AnimationChainSave reopened = editor.ChainNamed("Idle");
        reopened.Frames.Count.ShouldBe(3);
        reopened.Frames.Select(frame => frame.FrameLength).ShouldBe(new[] { idle.Frames[0].FrameLength, idle.Frames[1].FrameLength, 0.5f });
        editor.Expand(reopened);
        editor.Nodes.Select(node => node.Header).ShouldContain("Frame 3");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public void FreshEditor_CtrlS_ThenCancel_StaysUntitledWithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(Key.Enter);

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Services.ProjectManager.FileName.ShouldBeNullOrEmpty();
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task Restart_WithTheSameSettings_ReopensTheLastFile()
    {
        string settingsRoot = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", "settings-" + Guid.NewGuid().ToString("N"));
        string kept = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", "keep-" + Guid.NewGuid().ToString("N") + ".achx");
        string path;
        using (AnimationEditorHarness first = new AnimationEditorHarness(settingsRoot))
        {
            first.WritePng("sheet.png", 64, 64);
            path = first.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
            await first.OpenAsync(path);
            first.Tabs.Tabs.Count.ShouldBe(1);
            // The first harness deletes its project folder on dispose; keep a copy to put back.
            File.Copy(path, kept);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(kept, path, overwrite: true);
        File.Delete(kept);

        using AnimationEditorHarness second = new AnimationEditorHarness(settingsRoot);
        second.WaitUntil(() => second.Tabs.Tabs.Count == 1, TimeSpan.FromSeconds(3))
            .ShouldBeTrue("the restarted editor reopens the tab that was open");

        second.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        second.Nodes.Select(node => node.Header).ShouldContain("Walk");
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    private static void ShouldShowTheAnimationEditor(AnimationEditorHarness editor)
    {
        editor.Control<Control>("AnimationsBlock").IsVisible.ShouldBeTrue();
        editor.Control<Control>("AchxEditorPane").IsVisible.ShouldBeTrue();
        editor.Control<Control>("SequenceEditorPane").IsVisible.ShouldBeFalse();
        editor.Control<Control>("PngPaneGrid").IsVisible.ShouldBeFalse();
        editor.Control<TextBlock>("TitleFileName").IsVisible.ShouldBeFalse("the title describes the Untitled document");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Not saved", "the status bar describes the Untitled document");
        editor.Control<TextBlock>("StatusFilename").Text.ShouldBeNullOrEmpty();
        editor.Control<TextBlock>("StatusCounts").Text.ShouldBe(editor.Project.AnimationChains.Count == 0 ? "" : "1 chain · 0 frames · 0.00s");
    }

    [AvaloniaTheory]
    [InlineData("boss.seqj")]
    [InlineData("sheet.png")]
    [InlineData("hero.achx")]
    [InlineData("Tiles.tsx")]
    public async Task FileNew_FromAnyTab_ShowsTheAnimationEditor(string front)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        if (front == "sheet.png") await editor.OpenPngTabAsync(png);
        else if (front == "hero.achx") await editor.OpenAsync(editor.WriteAchx(front, AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        else if (front == "Tiles.tsx")
        {
            string path = Path.Combine(editor.ProjectFolder, front);
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.12.2" name="Tiles" tilewidth="16" tileheight="16" tilecount="16" columns="4">
                 <image source="sheet.png" width="64" height="64"/>
                 <tile id="0"><animation><frame tileid="0" duration="100"/><frame tileid="1" duration="100"/></animation></tile>
                </tileset>
                """);
            await editor.OpenAsync(path);
        }
        else await editor.OpenAsync(editor.WriteSeqj(front, "Attack"));
        if (front is "hero.achx" or "Tiles.tsx") editor.Control<TextBlock>("StatusCounts").Text.ShouldStartWith("1 chain");
        editor.Dialogs.AnswerNextSaveFile(null);

        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        ShouldShowTheAnimationEditor(editor);
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Press(Key.Enter);
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.ClickTab(front);
        editor.Control<Control>("AnimationsBlock").IsVisible.ShouldBe(front is "hero.achx" or "Tiles.tsx", "the tab left for the new one still shows its own pane");
    }

    [AvaloniaFact]
    public void CrashRecovery_WhenTheRestoredTabIsASeqj_ShowsTheAnimationEditor()
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string seqj = Path.Combine(folder, "boss.seqj");
            SequenceListSave sequences = new SequenceListSave();
            sequences.Sequences.Add(new SequenceSave { Name = "Attack" });
            sequences.Save(seqj);
            string recovery = Path.Combine(folder, "recovery.achx");
            AnimationChainListSave recovered = new AnimationChainListSave();
            recovered.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
            recovered.Save(recovery);
            string settings = Path.Combine(folder, "settings");
            var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settings);
            Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
            File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(
                new AppSettingsModel { OpenTabPaths = new List<string> { seqj }, ActiveTabPath = seqj }));

            using AnimationEditorHarness editor = new AnimationEditorHarness(settings, recovery);
            editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();

            ShouldShowTheAnimationEditor(editor);
            editor.VisibleChainHeaders.ShouldBe(new[] { "Recovered" });
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
