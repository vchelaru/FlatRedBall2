using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// "Add Source…" in the library: a multi-select file dialog for .achx/.achj, reporting refused files like a drop does,
/// and an empty state that never leaves a new .seqj without a way to add one.
/// </summary>
public class SequenceAddSourceScenarioTests
{
    private static string WriteSeqj(AnimationEditorHarness editor, params string[] sources)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        save.Save(path);
        return path;
    }

    private static void WriteHero(AnimationEditorHarness editor, string name = "hero.achx", string chain = "Walk")
    {
        if (!File.Exists(Path.Combine(editor.ProjectFolder, "sheet.png"))) editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx(name, AnimationEditorHarness.Chain(chain, "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static string Path_(AnimationEditorHarness editor, string name) => Path.Combine(editor.ProjectFolder, name);

    [AvaloniaFact]
    public async Task AddSourceButton_PicksSeveralFiles_AddsTheSources_AndReportsTheRefusedOnes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHero(editor);
        WriteHero(editor, "foe.achx", "Bite");
        string other = editor.WriteSeqj("other.seqj", "Idle");
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);
        editor.Dialogs.AnswerNextOpenFiles(Path_(editor, "hero.achx"), Path_(editor, "foe.achx"), other);

        editor.Click(Library(editor).AddSourceButton);

        Library(editor).ChipFor("hero.achx", "Walk").ShouldNotBeNull();
        Library(editor).ChipFor("foe.achx", "Bite").ShouldNotBeNull();
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "hero.achx", "foe.achx" });
        editor.ErrorBannerText.ShouldBe("other.seqj can't be a source. Only .achx and .achj files can.");
        editor.UndoLabels.ShouldBe(new[] { "Add 2 Sources" });
        editor.Dialogs.OpenFilesFilters.ShouldBe(new[] { "Animation Chain: *.achx *.achj" });
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        Library(editor).IsCollapsed("hero.achx").ShouldBeFalse();
        editor.Press(Key.Z, RawInputModifiers.Control);
        Library(editor).Sources.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AddSource_OfASourceAlreadyInTheLibrary_ShowsTheBanner_AndAddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHero(editor);
        string boss = WriteSeqj(editor, "hero.achx");
        await editor.OpenAsync(boss);
        editor.Dialogs.AnswerNextOpenFiles(Path_(editor, "hero.achx"));

        editor.Click(Library(editor).AddSourceButton);

        editor.ErrorBannerText.ShouldBe("hero.achx is already in the library.");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CancellingTheAddSourceDialog_ChangesNothing_AndSaysNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);
        editor.Dialogs.AnswerNextOpenFiles();

        editor.Click(Library(editor).AddSourceButton);

        editor.ErrorBannerText.ShouldBeNull();
        editor.UndoLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ASourceAddedWithThePicker_IsWatchedLikeADroppedOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHero(editor);
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);
        editor.Dialogs.AnswerNextOpenFiles(Path_(editor, "hero.achx"));
        editor.Click(Library(editor).AddSourceButton);
        Library(editor).ChipFor("hero.achx", "Run").ShouldBeNull();

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));

        (await editor.WaitUntilAsync(() => Library(editor).ChipFor("hero.achx", "Run") is not null, TimeSpan.FromSeconds(6)))
            .ShouldBeTrue("a source added with the picker is watched like the ones the file opened with");
    }

    [AvaloniaFact]
    public async Task ALibraryWithNoSources_ShowsADragHintAndAnAddSourceButton_ThatWorks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHero(editor);
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);
        var library = Library(editor);

        library.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text)
            .ShouldContain("Drag .achx or .achj files here");
        library.AddSourceButton.IsEffectivelyVisible.ShouldBeTrue();
        library.AddSourceButton.Content.ShouldBe("+ Add Source…");
        editor.Dialogs.AnswerNextOpenFiles(Path_(editor, "hero.achx"));
        editor.Click(library.AddSourceButton);

        library.Sources.Count.ShouldBe(1);
        library.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text)
            .ShouldNotContain("Drag .achx or .achj files here");
        library.AddSourceButton.IsEffectivelyVisible.ShouldBeTrue("the button stays once there are sources");
    }
}
