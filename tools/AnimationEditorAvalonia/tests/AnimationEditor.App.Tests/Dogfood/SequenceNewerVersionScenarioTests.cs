using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> written by a newer version of the format opens as far as it can, and the editor says so once per
/// open. Saving keeps the file's version number and the fields this editor does not know.
/// </summary>
public class SequenceNewerVersionScenarioTests
{
    private const string NewerJson = """
        {
          "version": 2,
          "futureSetting": { "mode": "wide" },
          "sources": [],
          "sequences": [ { "name": "Idle", "loop": false, "tracks": [], "futureTag": 7 } ]
        }
        """;

    private const string Notice = "Newer file format (v2). Unknown data is kept when you save.";

    private static void DismissNotice(AnimationEditorHarness editor) =>
        editor.Click(editor.Notifications.ErrorBannerDismissBtn);

    [AvaloniaFact]
    public async Task OpeningANewerSeqj_ShowsTheNotice_AndSavingKeepsTheVersionAndUnknownFields()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        File.WriteAllText(boss, NewerJson);

        await editor.OpenAsync(boss);

        editor.ProblemText.ShouldBe(Notice);
        editor.InfoText.ShouldBeNull();
        editor.SequenceNames.ShouldBe(new[] { "Idle" });

        editor.Click(editor.Control<Button>("AddSequenceButton"));

        string onDisk = File.ReadAllText(boss);
        onDisk.ShouldContain("\"version\": 2");
        onDisk.ShouldContain("futureSetting");
        onDisk.ShouldContain("futureTag");
    }

    [AvaloniaFact]
    public async Task TheNotice_IsAProblemBanner_ThatStaysUntilDismissed_AndDoesNotReturnOnTabSwitchOrEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        File.WriteAllText(boss, NewerJson);
        string other = editor.WriteSeqj("other.seqj", "Walk");
        await editor.OpenAsync(boss);
        // A problem banner normally hides itself after about 8 seconds; this one stays because editing the file may lose data.
        editor.ProblemText.ShouldBe(Notice);
        (await editor.WaitUntilAsync(() => editor.ProblemText == null, TimeSpan.FromSeconds(10))).ShouldBeFalse();
        editor.ProblemText.ShouldBe(Notice);

        DismissNotice(editor);
        editor.ProblemText.ShouldBeNull();
        await editor.OpenAsync(other);
        editor.ClickTab("boss.seqj");
        editor.ClickTab("other.seqj");
        editor.ClickTab("boss.seqj");
        editor.Click(editor.Control<Button>("AddSequenceButton"));

        editor.ProblemText.ShouldBeNull();
        editor.InfoText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task OpeningACurrentSeqj_ShowsNoNotice()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");

        await editor.OpenAsync(boss);

        editor.InfoText.ShouldBeNull();
        editor.ProblemText.ShouldBeNull();
        editor.InfoText.ShouldBeNull();
    }

    [AvaloniaFact]
    public void RestoringASessionWithANewerSeqj_ShowsTheNoticeOnce()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        string boss = Path.Combine(folder, "boss.seqj");
        string other = Path.Combine(folder, "other.seqj");
        File.WriteAllText(boss, NewerJson);
        File.WriteAllText(other, """{ "version": 1, "sources": [], "sequences": [ { "name": "Walk", "loop": false, "tracks": [] } ] }""");
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { boss, other },
            ActiveTabPath = boss,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 2 && editor.ProblemText != null, TimeSpan.FromSeconds(5));
            editor.Layout();

            editor.ProblemText.ShouldBe(Notice);
            DismissNotice(editor);
            editor.ClickTab("other.seqj");
            editor.ClickTab("boss.seqj");
            editor.ProblemText.ShouldBeNull();
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }
}
