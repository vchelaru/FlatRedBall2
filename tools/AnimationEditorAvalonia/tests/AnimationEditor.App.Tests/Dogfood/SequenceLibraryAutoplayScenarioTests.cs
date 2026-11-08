using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The library header's "play all" toggle (every chip animates, not just the hovered one) and each source header's
/// "Open in tab" button (#1622).
/// </summary>
public class SequenceLibraryAutoplayScenarioTests
{
    private const string PlayAll = "Play all chips";

    private static string WriteTwoSources(AnimationEditorHarness editor, params string[] extraSources)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16), (16, 16, 16, 16)));
        editor.WriteAchx("foe.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        SequenceListSave save = new SequenceListSave();
        save.Sources.AddRange(new[] { "hero.achx", "foe.achx" }.Concat(extraSources));
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        save.Save(path);
        return path;
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static ToggleButton PlayAllToggle(AnimationEditorHarness editor) =>
        Library(editor).GetVisualDescendants().OfType<ToggleButton>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == PlayAll);

    private static IEnumerable<Button> OpenButtons(AnimationEditorHarness editor) =>
        Library(editor).GetVisualDescendants().OfType<Button>().Where(button =>
            (Avalonia.Automation.AutomationProperties.GetName(button) ?? "").StartsWith("Open "));

    private static Button OpenButtonFor(AnimationEditorHarness editor, string source) =>
        OpenButtons(editor).Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == $"Open {source} in tab");

    private static Button CollapseToggleFor(AnimationEditorHarness editor, string source) =>
        Library(editor).GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == $"Collapse {source}");

    private static LibraryChipView Chip(AnimationEditorHarness editor, string source, string chain) =>
        Library(editor).ChipFor(source, chain)!;

    [AvaloniaFact]
    public async Task PlayAllToggle_MakesEveryVisibleChipPlay_AndOffStopsThemOnTheFirstFrame_WithNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));
        var chips = new[] { Chip(editor, "hero.achx", "Walk"), Chip(editor, "hero.achx", "Run"), Chip(editor, "foe.achx", "Bite") };
        chips.ShouldAllBe(chip => !chip.IsPlaying);
        Library(editor).IsPlaybackTimerRunning.ShouldBeFalse();

        editor.Click(PlayAllToggle(editor));

        chips.ShouldAllBe(chip => chip.IsPlaying);
        Library(editor).IsPlaybackTimerRunning.ShouldBeTrue();
        await editor.WaitUntilAsync(() => chips.All(chip => chip.FrameIndex > 0), TimeSpan.FromSeconds(2));

        editor.Click(PlayAllToggle(editor));

        chips.ShouldAllBe(chip => !chip.IsPlaying);
        chips.ShouldAllBe(chip => chip.FrameIndex == 0);
        Library(editor).IsPlaybackTimerRunning.ShouldBeFalse();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task WithPlayAllOn_AChipKeepsPlayingAfterThePointerLeaves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));
        editor.Click(PlayAllToggle(editor));
        LibraryChipView walk = Chip(editor, "hero.achx", "Walk");

        editor.Hover(editor.CenterOf(walk));
        editor.Hover(editor.CenterOf(editor.Control<Button>("AddSequenceButton")));

        walk.IsPlaying.ShouldBeTrue();
        Library(editor).IsPlaybackTimerRunning.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task CollapsedSources_DoNotTickTheirChips_UntilExpanded()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));
        editor.Click(PlayAllToggle(editor));

        editor.Click(CollapseToggleFor(editor, "hero.achx"));

        Chip(editor, "hero.achx", "Walk").IsPlaying.ShouldBeFalse();
        Chip(editor, "hero.achx", "Run").IsPlaying.ShouldBeFalse();
        Chip(editor, "foe.achx", "Bite").IsPlaying.ShouldBeTrue();
        Library(editor).IsPlaybackTimerRunning.ShouldBeTrue();

        editor.Click(CollapseToggleFor(editor, "foe.achx"));

        Library(editor).IsPlaybackTimerRunning.ShouldBeFalse();
        editor.Click(Library(editor).GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == "Expand hero.achx"));
        Chip(editor, "hero.achx", "Walk").IsPlaying.ShouldBeTrue();
        Library(editor).IsPlaybackTimerRunning.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task LeavingTheSequenceTab_StopsTheTimer_AndComingBackResumesPlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));
        editor.Click(PlayAllToggle(editor));
        editor.Click(OpenButtonFor(editor, "hero.achx"));
        await editor.WaitUntilAsync(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));

        Library(editor).IsPlaybackTimerRunning.ShouldBeFalse();

        editor.ClickTab("boss.seqj");

        Library(editor).IsPlaybackTimerRunning.ShouldBeTrue();
        Chip(editor, "foe.achx", "Bite").IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task PlayAllToggle_IsWrittenToSettings_AndClearedWhenToggledOff()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));
        string settingsPath = AppSettingsLocation.ForApplicationDataRoot(editor.Services.SettingsRoot).FullPath;

        editor.Click(PlayAllToggle(editor));

        JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsPath))!.LibraryAutoplayAll.ShouldBeTrue();
        editor.Click(PlayAllToggle(editor));
        JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsPath))!.LibraryAutoplayAll.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void RestoringASession_ShowsPlayAllOn_AndPlaysTheChips()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        var chains = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        walk.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        chains.AnimationChains.Add(walk);
        chains.Save(Path.Combine(folder, "hero.achx"));
        string boss = Path.Combine(folder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(boss);
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { boss },
            ActiveTabPath = boss,
            LibraryAutoplayAll = true,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            editor.Layout();

            PlayAllToggle(editor).IsChecked.ShouldBe(true);
            Chip(editor, "hero.achx", "Walk").IsPlaying.ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task OpenInTab_OpensTheSource_SelectsNothing_AndMakesNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor));

        editor.Click(OpenButtonFor(editor, "hero.achx"));
        await editor.WaitUntilAsync(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        // No chain is targeted: the file opens as a plain open does, with its first chain selected, not "Run".
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Walk");
        editor.ProblemText.ShouldBeNull();
        editor.ClickTab("boss.seqj");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AMissingSource_HasNoOpenInTabButton_ButPresentOnesDo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteTwoSources(editor, "gone.achx"));

        OpenButtons(editor).Select(button => Avalonia.Automation.AutomationProperties.GetName(button))
            .ShouldBe(new[] { "Open hero.achx in tab", "Open foe.achx in tab" });
    }
}
