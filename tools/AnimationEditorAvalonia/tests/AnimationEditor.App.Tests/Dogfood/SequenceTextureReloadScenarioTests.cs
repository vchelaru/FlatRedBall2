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

    // Walk: 16x16 frames on a 64x64 sheet, so every sprite is square.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0 });
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
    public async Task ReplacingTheSheetOnDisk_LaysTheFilmstripCellsOutAgain_WithoutReopeningTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        // The sources come from the Animations browser, so a project folder is open and its watcher sees the sheet change.
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(seqj);
        SpriteWidthOfFirstCell(editor).ShouldBe(TimelineLayout.FilmHeight, 1e-6);

        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        var cells = timeline.FilmstripCells(timeline.Blocks.Single());

        editor.WritePng("sheet.png", 128, 64, SKColors.Red);

        (await editor.WaitUntilAsync(() => !ReferenceEquals(cells, timeline.FilmstripCells(timeline.Blocks.Single())), ReloadTimeout))
            .ShouldBeTrue("the filmstrip should lay its cells out again for the replaced texture");
        SpriteWidthOfFirstCell(editor).ShouldBe(TimelineLayout.FilmHeight, 1e-6, "the frame keeps its 16x16 pixels on a wider sheet (#1952)");
    }

    /// <summary>#2112: the pixels of a UV frame come from the size of its texture.</summary>
    [AvaloniaFact]
    public async Task TheSheetOfAUvSourceAppearingOnDisk_ShowsTheWholeFrame_WithoutReopeningTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        File.Delete(Path.Combine(editor.ProjectFolder, "sheet.png"));
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(editor.ProjectFolder, "hero.achx"));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(seqj);

        editor.WritePng("sheet.png", 64, 32);

        (await editor.WaitUntilAsync(() => Math.Abs(SpriteWidthOfFirstCell(editor) - 2 * TimelineLayout.FilmHeight) < 1e-6, ReloadTimeout))
            .ShouldBeTrue("the frame is the whole 64x32 texture");
    }

    [AvaloniaFact]
    public async Task ReplacingTheSheetOnDisk_ReRendersTheLibraryChip_WithoutReopeningTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        // The sources come from the Animations browser, so a project folder is open and its watcher sees the sheet change.
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(seqj);
        var before = ChipImageSize(editor);
        before.Width.ShouldBe(before.Height);

        // Half a frame wide: the chip crops what is left of the 16x16 region, 8x16.
        editor.WritePng("sheet.png", 8, 64, SKColors.Red);

        (await editor.WaitUntilAsync(() => ChipImageSize(editor).Height == 2 * ChipImageSize(editor).Width, ReloadTimeout))
            .ShouldBeTrue("the chip should show the replaced texture's 1:2 sprite");
    }
}
