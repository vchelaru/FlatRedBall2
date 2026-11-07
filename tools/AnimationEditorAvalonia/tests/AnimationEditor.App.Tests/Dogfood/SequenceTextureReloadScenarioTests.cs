using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A source texture replaced on disk refreshes the sequence tab's filmstrip and library chips without reopening it (#1624).</summary>
public class SequenceTextureReloadScenarioTests
{
    private static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(5);

    // Walk: 16x16 frames on a 64x64 sheet, so every sprite is square until the sheet changes shape.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static double SpriteWidthOfFirstCell(AnimationEditorHarness editor)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        return timeline.FilmstripCells(timeline.Blocks.Single())[0].SpriteWidth;
    }

    private static (double Width, double Height) ChipImageSize(AnimationEditorHarness editor)
    {
        var library = editor.Control<SequenceLibraryControl>("SequenceLibrary");
        var source = library.ChipFor("hero.achx", "Walk")!.GetVisualDescendants().OfType<Image>().Single().Source!;
        return (source.Size.Width, source.Size.Height);
    }

    [AvaloniaFact]
    public async Task ReplacingTheSheetOnDisk_ReSizesTheFilmstripCells_WithoutReopeningTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        // The sources come from the Animations browser, so a project folder is open and its watcher sees the sheet change.
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await editor.OpenAsync(seqj);
        SpriteWidthOfFirstCell(editor).ShouldBe(TimelineLayout.FilmHeight, 1e-6);

        // Twice as wide: the same UV region is now a 2:1 sprite.
        editor.WritePng("sheet.png", 128, 64, SKColors.Red);

        (await editor.WaitUntilAsync(() => Math.Abs(SpriteWidthOfFirstCell(editor) - 2 * TimelineLayout.FilmHeight) < 1e-6, ReloadTimeout))
            .ShouldBeTrue("the filmstrip should lay its cells out from the replaced texture");
    }

    [AvaloniaFact]
    public async Task ReplacingTheSheetOnDisk_ReRendersTheLibraryChip_WithoutReopeningTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        // The sources come from the Animations browser, so a project folder is open and its watcher sees the sheet change.
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await editor.OpenAsync(seqj);
        var before = ChipImageSize(editor);
        before.Width.ShouldBe(before.Height);

        editor.WritePng("sheet.png", 128, 64, SKColors.Red);

        (await editor.WaitUntilAsync(() => ChipImageSize(editor).Width == 2 * ChipImageSize(editor).Height, ReloadTimeout))
            .ShouldBeTrue("the chip should show the replaced texture's 2:1 sprite");
    }
}
