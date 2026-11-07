using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> with a field of the wrong JSON kind opens like a corrupt one: the error banner names the file and the
/// property, no tab is left behind, and the next file still opens. Covers each way a file is opened.
/// </summary>
public class SequenceMalformedOpenScenarioTests
{
    private const string WrongKindJson = """{ "sequences": [ { "name": 5 } ] }""";
    private const string Problem = "sequences[0].name must be a string";

    private static string WriteMalformed(AnimationEditorHarness editor, string name = "broken.seqj")
    {
        string path = Path.Combine(editor.ProjectFolder, name);
        File.WriteAllText(path, WrongKindJson);
        return path;
    }

    private static void ShouldShowTheBannerFor(AnimationEditorHarness editor, string file)
    {
        editor.ErrorBannerText.ShouldNotBeNull();
        editor.ErrorBannerText!.ShouldContain($"Could not open {file}");
        editor.ErrorBannerText.ShouldContain(Problem);
    }

    private static bool SequencePaneShown(AnimationEditorHarness editor) =>
        editor.Control<Grid>("SequenceEditorPane").IsVisible && editor.Control<Control>("SequenceListPane").IsVisible;

    [AvaloniaFact]
    public async Task Opening_AWrongKindFieldSeqj_ShowsTheBanner_LeavesNoTab_AndTheNextFileStillOpens()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string broken = WriteMalformed(editor);
        string good = editor.WriteSeqj("good.seqj", "Idle");

        await editor.OpenAsync(broken);

        ShouldShowTheBannerFor(editor, "broken.seqj");
        editor.TabLabels.ShouldBeEmpty();
        await editor.OpenAsync(good);
        editor.TabLabels.ShouldBe(new[] { "good.seqj" });
        SequencePaneShown(editor).ShouldBeTrue();
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task Opening_AWrongKindFieldSeqj_WithAnotherTabOpen_KeepsThatTabShowing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string good = editor.WriteSeqj("good.seqj", "Idle");
        await editor.OpenAsync(good);
        string broken = WriteMalformed(editor);

        await editor.OpenAsync(broken);

        ShouldShowTheBannerFor(editor, "broken.seqj");
        editor.TabLabels.ShouldBe(new[] { "good.seqj" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("good.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task ProjectPanel_DoubleClickingAWrongKindFieldSeqj_ShowsTheBanner_AndOpensNoTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteMalformed(editor);
        editor.WriteSeqj("good.seqj", "Idle");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        var panel = editor.Control<ProjectPanelControl>("ProjectPanel");

        editor.DoubleClickAt(editor.CenterOf(PanelsScenarioTests.RowFor(editor, panel, "broken.seqj")));
        editor.Wait(TimeSpan.FromMilliseconds(200));

        ShouldShowTheBannerFor(editor, "broken.seqj");
        editor.TabLabels.ShouldBeEmpty();
        editor.DoubleClickAt(editor.CenterOf(PanelsScenarioTests.RowFor(editor, panel, "good.seqj")));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.TabLabels.ShouldBe(new[] { "good.seqj" });
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task ProjectPanel_SingleClickingAWrongKindFieldSeqj_ShowsTheBanner_AndLeavesNoPreviewTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteMalformed(editor);
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        var panel = editor.Control<ProjectPanelControl>("ProjectPanel");

        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "broken.seqj"));
        editor.Wait(TimeSpan.FromMilliseconds(200));

        ShouldShowTheBannerFor(editor, "broken.seqj");
        editor.TabLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Back_ToAClosedSeqjThatIsNowWrongKind_ShowsTheBanner_AndOpensNoTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.CloseTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        File.WriteAllText(boss, WrongKindJson);

        var point = new Point(300, 300);
        editor.Window.MouseDown(point, MouseButton.XButton1);
        editor.Window.MouseUp(point, MouseButton.XButton1);
        editor.Layout();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        ShouldShowTheBannerFor(editor, "boss.seqj");
        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
    }

    [AvaloniaFact]
    public void Restoring_ASessionWhoseActiveTabIsWrongKind_ShowsTheBanner_AndShowsTheOtherTab()
    {
        RunRestore(
            folder => (new[] { Path.Combine(folder, "broken.seqj"), Path.Combine(folder, "good.seqj") }, Path.Combine(folder, "broken.seqj")),
            editor =>
            {
                editor.WaitUntil(() => editor.ErrorBannerText != null, TimeSpan.FromSeconds(5));
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.seqj");
                editor.TabLabels.ShouldBe(new[] { "good.seqj" });
                editor.Tabs.ActiveTab!.DisplayName.ShouldBe("good.seqj");
                editor.SequenceNames.ShouldBe(new[] { "Idle" });
            });
    }

    [AvaloniaFact]
    public void Restoring_ASessionWhoseOnlyTabIsWrongKind_ShowsTheBanner_AndOpensNoTab()
    {
        RunRestore(
            folder => (new[] { Path.Combine(folder, "broken.seqj") }, Path.Combine(folder, "broken.seqj")),
            editor =>
            {
                editor.WaitUntil(() => editor.ErrorBannerText != null, TimeSpan.FromSeconds(5));
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.seqj");
                editor.TabLabels.ShouldBeEmpty();
            });
    }

    [AvaloniaFact]
    public void Restoring_AWrongKindTabBehindTheActiveOne_ShowsTheBannerWhenItIsSelected_AndDropsIt()
    {
        RunRestore(
            folder => (new[] { Path.Combine(folder, "good.seqj"), Path.Combine(folder, "broken.seqj") }, Path.Combine(folder, "good.seqj")),
            editor =>
            {
                editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(5));
                editor.Layout();
                editor.ErrorBannerText.ShouldBeNull();

                editor.ClickTab("broken.seqj");
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.seqj");
                editor.TabLabels.ShouldBe(new[] { "good.seqj" });
                editor.Tabs.ActiveTab!.DisplayName.ShouldBe("good.seqj");
                editor.SequenceNames.ShouldBe(new[] { "Idle" });
            });
    }

    // Writes broken.seqj and good.seqj into a fresh folder, saves a session over the tabs `session` picks, and starts the editor on it.
    // `moreFiles` writes extra fixtures into that folder before the editor starts.
    internal static void RunRestore(Func<string, (string[] Tabs, string Active)> session, Action<AnimationEditorHarness> check,
        Action<string>? moreFiles = null)
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "broken.seqj"), WrongKindJson);
        File.WriteAllText(Path.Combine(folder, "good.seqj"),
            """{ "version": 1, "sources": [], "sequences": [ { "name": "Idle", "loop": false, "tracks": [] } ] }""");
        moreFiles?.Invoke(folder);
        var (tabs, active) = session(folder);
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = tabs.ToList(),
            ActiveTabPath = active,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            check(editor);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }
}
