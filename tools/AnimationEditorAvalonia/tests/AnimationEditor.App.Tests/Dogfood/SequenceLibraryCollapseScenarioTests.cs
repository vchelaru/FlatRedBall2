using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Collapsing a source in a <c>.seqj</c> tab's library hides its chips but keeps its header. It is view state: it
/// survives the library rebuilding, is remembered per file, and never lands in the undo history.
/// </summary>
public class SequenceLibraryCollapseScenarioTests
{
    private static string WriteSeqj(AnimationEditorHarness editor, params string[] sources)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        SequenceListSave save = new SequenceListSave();
        save.Sources.AddRange(sources);
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        save.Save(path);
        return path;
    }

    private static void WriteSheetAndAchx(AnimationEditorHarness editor, string name, params string[] chains)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx(name, chains.Select(chain => AnimationEditorHarness.Chain(chain, "sheet.png", (0, 0, 16, 16))).ToArray());
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static Button ToggleFor(AnimationEditorHarness editor, string source, bool expanded) =>
        Library(editor).GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == $"{(expanded ? "Collapse" : "Expand")} {source}");

    private static bool ChipShown(AnimationEditorHarness editor, string source, string chain) =>
        Library(editor).ChipFor(source, chain)!.IsEffectivelyVisible;

    // OS drag-drop can't be driven headlessly, so this raises the drag-over and drop a real drag would end with.
    private static async Task Drop(AnimationEditorHarness editor, string path)
    {
        DataTransfer data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile((await editor.Window.StorageProvider.TryGetFileFromPathAsync(path))!));
        Control target = Library(editor);
        Point point = editor.CenterOf(target);
        target.RaiseEvent(new DragEventArgs(DragDrop.DragOverEvent, data, target, point, KeyModifiers.None));
        target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, target, point, KeyModifiers.None));
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task ChevronAndTitle_CollapseAndExpandASource_KeepingItsHeader()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteSheetAndAchx(editor, "hero.achx", "Walk", "Run");
        await editor.OpenAsync(WriteSeqj(editor, "hero.achx", "gone.achx"));
        ChipShown(editor, "hero.achx", "Walk").ShouldBeTrue();

        editor.Click(ToggleFor(editor, "hero.achx", expanded: true));

        ChipShown(editor, "hero.achx", "Walk").ShouldBeFalse();
        ChipShown(editor, "hero.achx", "Run").ShouldBeFalse();
        Library(editor).IsCollapsed("hero.achx").ShouldBeTrue();
        var texts = Library(editor).GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        texts.ShouldContain("hero.achx");
        texts.ShouldContain("2 chains");
        Library(editor).GetVisualDescendants().OfType<Button>().ShouldContain(button => (string?)button.Tag == "hero.achx");

        TextBlock title = Library(editor).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "hero.achx");
        editor.Click(title);

        ChipShown(editor, "hero.achx", "Walk").ShouldBeTrue();
        Library(editor).IsCollapsed("hero.achx").ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task SourceHeader_ShowsTheFileName_AndTheFullRelativePathInItsTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "chars"));
        editor.WritePng(Path.Combine("chars", "sheet.png"), 64, 64);
        editor.WriteAchx(Path.Combine("chars", "hero.achx"), AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = WriteSeqj(editor, "chars/hero.achx", "../OldCoil/Gone.achj");

        await editor.OpenAsync(boss);

        var texts = Library(editor).GetVisualDescendants().OfType<TextBlock>().ToList();
        texts.Select(text => text.Text).ShouldContain("hero.achx");
        texts.Select(text => text.Text).ShouldContain("⚠ Gone.achj");
        ToolTip.GetTip(texts.Single(text => text.Text == "hero.achx")).ShouldBe("chars/hero.achx");
        ToolTip.GetTip(texts.Single(text => text.Text == "⚠ Gone.achj")).ShouldBe("../OldCoil/Gone.achj");
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "chars/hero.achx", "../OldCoil/Gone.achj" });
    }

    [AvaloniaFact]
    public async Task AMissingSource_HasNoToggle_AndClickingItsHeaderDoesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteSeqj(editor, "gone.achx"));

        TextBlock title = Library(editor).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "⚠ gone.achx");
        editor.Click(title);

        Library(editor).GetVisualDescendants().OfType<Button>()
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button))
            .ShouldNotContain(name => (name ?? "").StartsWith("Collapse") || (name ?? "").StartsWith("Expand"));
        Library(editor).IsCollapsed("gone.achx").ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Collapsed_SurvivesAddingASecondSource_AndUndoOfTheAdd_AndMakesNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteSheetAndAchx(editor, "hero.achx", "Walk");
        editor.WriteAchx("foe.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(WriteSeqj(editor, "hero.achx"));

        editor.Click(ToggleFor(editor, "hero.achx", expanded: true));
        editor.UndoLabels.ShouldBeEmpty();

        await Drop(editor, Path.Combine(editor.ProjectFolder, "foe.achx"));

        editor.UndoLabels.ShouldBe(new[] { "Add Source 'foe.achx'" });
        ChipShown(editor, "hero.achx", "Walk").ShouldBeFalse();
        ChipShown(editor, "foe.achx", "Bite").ShouldBeTrue();

        editor.Press(Key.Z, RawInputModifiers.Control);

        Library(editor).Sources.Select(source => source.Source).ShouldBe(new[] { "hero.achx" });
        ChipShown(editor, "hero.achx", "Walk").ShouldBeFalse();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Collapsed_IsWrittenToSettingsPerFile_AndForgottenOnceTheSourceIsRemoved()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteSheetAndAchx(editor, "hero.achx", "Walk");
        string boss = WriteSeqj(editor, "hero.achx");
        await editor.OpenAsync(boss);
        string settingsPath = AppSettingsLocation.ForApplicationDataRoot(editor.Services.SettingsRoot).FullPath;

        editor.Click(ToggleFor(editor, "hero.achx", expanded: true));

        var stored = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsPath))!;
        stored.CollapsedLibrarySources[new FilePath(boss).Standardized].ShouldBe(new[] { "hero.achx" });

        Button remove = Library(editor).GetVisualDescendants().OfType<Button>().Single(button => (string?)button.Tag == "hero.achx");
        editor.Click(remove);

        stored = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsPath))!;
        stored.CollapsedLibrarySources.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void RestoringASession_ShowsTheSourceCollapsedThatWasLeftCollapsed()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        var chains = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
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
            CollapsedLibrarySources = new Dictionary<string, List<string>>
            {
                [new FilePath(boss).Standardized] = new List<string> { "hero.achx" },
            },
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            editor.Layout();

            Library(editor).IsCollapsed("hero.achx").ShouldBeTrue();
            ChipShown(editor, "hero.achx", "Walk").ShouldBeFalse();
            ToggleFor(editor, "hero.achx", expanded: false).ShouldNotBeNull();
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }
}
