using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab follows the disk while it stays in front: its own file, the source files it lists (in any folder)
/// and the textures those use. No project folder is open in these scenarios, so nothing but the tab's own watcher can notice.
/// </summary>
public class SequenceLiveReloadScenarioTests
{
    private static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(6);

    private const string Source = "art/hero.achx";

    // boss.seqj lists art/hero.achx (Walk: two 0.1 s frames of art/sheet.png); Attack plays Walk on the Body track.
    private static string WriteFixture(AnimationEditorHarness editor, params string[] extraSequences)
    {
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "art"));
        editor.WritePng(Path.Combine("art", "sheet.png"), 64, 64);
        WriteHero(editor, withRun: false);
        return WriteSeqj(editor, extraSequences);
    }

    private static void WriteHero(AnimationEditorHarness editor, bool withRun)
    {
        var chains = new List<AnimationChainSave>
        {
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
        };
        if (withRun) chains.Add(AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        editor.WriteAchx(Path.Combine("art", "hero.achx"), chains.ToArray());
    }

    private static string WriteSeqj(AnimationEditorHarness editor, string[] extraSequences, string? firstSequence = null)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { Source } };
        var attack = new SequenceSave { Name = firstSequence ?? "Attack" };
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = Source, Chain = "Walk", Start = 0, Loops = 1 });
        attack.Tracks.Add(body);
        save.Sequences.Add(attack);
        foreach (string name in extraSequences) save.Sequences.Add(new SequenceSave { Name = name });
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Point RulerAt(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), TimelineViewport.RulerHeight / 2);
    }

    [AvaloniaFact]
    public async Task SeqjRewrittenOnDisk_ReloadsTheTab_KeepingTheSelectionZoomAndPlayhead()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, "Idle");
        await editor.OpenAsync(path);
        List(editor).SelectedIndex.ShouldBe(0);
        Timeline(editor).Select(new TimelineChainId(0, 0), null);
        Timeline(editor).SetZoomPercent(150);
        editor.WaitUntil(() => !Timeline(editor).IsZoomAnimating, TimeSpan.FromSeconds(2));
        float zoom = Timeline(editor).Zoom;
        editor.ClickAt(RulerAt(editor, 0.1));
        double playhead = Timeline(editor).Playhead;
        playhead.ShouldBeGreaterThan(0);

        // Another sequence is added in front of the open one: it moves down the list.
        var rewritten = SequenceListSave.FromFile(path);
        rewritten.Sequences.Insert(0, new SequenceSave { Name = "Intro" });
        rewritten.Save(path);

        (await editor.WaitUntilAsync(() => editor.SequenceNames.Count == 3, ReloadTimeout))
            .ShouldBeTrue("the sequence list should pick up the rewritten file");
        editor.SequenceNames.ShouldBe(new[] { "Intro", "Attack", "Idle" });
        List(editor).SelectedIndex.ShouldBe(1, "the selected sequence is followed by name");
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 0), "the chain stays selected");
        Timeline(editor).Zoom.ShouldBe(zoom);
        Timeline(editor).Playhead.ShouldBe(playhead, 1e-6);
        editor.UndoLabels.ShouldBeEmpty("history of the old file does not apply to the new one");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task OwnEdit_DoesNotReloadTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);

        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1500));

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Sequence" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        editor.ToastText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task SeqjHalfWritten_KeepsTheTab_ShowsTheBanner_AndReloadsOnceTheWriteFinishes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, "Idle");
        await editor.OpenAsync(path);

        File.WriteAllText(path, "{ \"sequences\": [ { \"name\": \"Hal");

        (await editor.WaitUntilAsync(() => editor.ErrorBannerText is not null, ReloadTimeout))
            .ShouldBeTrue("an unreadable file should raise the error banner");
        editor.ErrorBannerText!.ShouldContain("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });

        var finished = new SequenceListSave { Sources = { Source } };
        finished.Sequences.Add(new SequenceSave { Name = "Attack" });
        finished.Sequences.Add(new SequenceSave { Name = "Idle" });
        finished.Sequences.Add(new SequenceSave { Name = "Die" });
        finished.Save(path);

        (await editor.WaitUntilAsync(() => editor.SequenceNames.Count == 3, ReloadTimeout))
            .ShouldBeTrue("the next change event retries the reload");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Die" });
    }

    [AvaloniaFact]
    public async Task SeqjReplacedByDeleteThenCreate_Reloads()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);

        var rewritten = SequenceListSave.FromFile(path);
        rewritten.Sequences.Add(new SequenceSave { Name = "Die" });
        string json = rewritten.ToJsonString();
        File.Delete(path);
        File.WriteAllText(path, json);

        (await editor.WaitUntilAsync(() => editor.SequenceNames.Count == 2, ReloadTimeout))
            .ShouldBeTrue("an atomic replace is a change, not a deletion");
        (editor.ToastText ?? "").ShouldNotContain("deleted");
    }

    [AvaloniaFact]
    public async Task SeqjDeletedOnDisk_ToastsLikeAnAchx_AndKeepsTheTabAndDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, "Idle");
        await editor.OpenAsync(path);

        File.Delete(path);

        (await editor.WaitUntilAsync(() => editor.ToastText?.Contains("deleted from disk") == true, ReloadTimeout))
            .ShouldBeTrue("a deleted file is announced the way a deleted .achx is");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
    }

    [AvaloniaFact]
    public async Task SourceRewrittenOnDisk_InAnotherFolder_UpdatesTheLibraryAndTimeline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        Library(editor).ChipFor(Source, "Run").ShouldBeNull();
        Timeline(editor).Blocks.Single().Played.ShouldBe(0.2, 1e-6);

        // A third Walk frame and a new Run chain.
        editor.WriteAchx(Path.Combine("art", "hero.achx"),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));

        (await editor.WaitUntilAsync(() => Library(editor).ChipFor(Source, "Run") is not null, ReloadTimeout))
            .ShouldBeTrue("the library should list the chain the source gained");
        Timeline(editor).Blocks.Single().Played.ShouldBe(0.3, 1e-6);
    }

    [AvaloniaFact]
    public async Task SourceDeletedOnDisk_FlagsItsChainsMissing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        Timeline(editor).Blocks.Single().IsMissing.ShouldBeFalse();

        File.Delete(Path.Combine(editor.ProjectFolder, "art", "hero.achx"));

        (await editor.WaitUntilAsync(() => Timeline(editor).Blocks.Single().IsMissing, ReloadTimeout))
            .ShouldBeTrue("the chain's source is gone");
    }

    [AvaloniaFact]
    public async Task SourceAddedAfterOpen_IsWatchedToo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "other"));
        editor.WritePng(Path.Combine("other", "sheet.png"), 32, 32);
        editor.WriteAchx(Path.Combine("other", "foe.achx"), AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        await SequenceLibraryScenarioTests.Drop(editor, Library(editor), Path.Combine(editor.ProjectFolder, "other", "foe.achx"));
        Library(editor).ChipFor("other/foe.achx", "Bite").ShouldNotBeNull();
        Library(editor).ChipFor("other/foe.achx", "Claw").ShouldBeNull();

        editor.WriteAchx(Path.Combine("other", "foe.achx"),
            AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Claw", "sheet.png", (16, 0, 16, 16)));

        (await editor.WaitUntilAsync(() => Library(editor).ChipFor("other/foe.achx", "Claw") is not null, ReloadTimeout))
            .ShouldBeTrue("a source added in this session is watched like the ones the file opened with");
    }

    private static (double Width, double Height) ChipImageSize(AnimationEditorHarness editor)
    {
        var source = Library(editor).ChipFor(Source, "Walk")!.GetVisualDescendants().OfType<Image>().Single().Source!;
        return (source.Size.Width, source.Size.Height);
    }

    private static double SpriteWidthOfFirstCell(AnimationEditorHarness editor)
    {
        var timeline = Timeline(editor);
        return timeline.FilmstripCells(timeline.Blocks.Single())[0].SpriteWidth;
    }

    // How many pixels of the preview (rendered the way the screen draws it) are exactly this color.
    private static int PreviewPixels(AnimationEditorHarness editor, SKColor color)
    {
        using SKBitmap bitmap = editor.Control<PreviewControl>("SequencePreview").RenderToBitmap(200, 200);
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) == color) count++;
        return count;
    }

    [AvaloniaFact]
    public async Task TextureReplacedOnDisk_RefreshesFilmstripPreviewAndChip_WithNoProjectFolderOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        SpriteWidthOfFirstCell(editor).ShouldBe(TimelineLayout.FilmHeight, 1e-6);
        ChipImageSize(editor).Width.ShouldBe(ChipImageSize(editor).Height);
        PreviewPixels(editor, SKColors.CornflowerBlue).ShouldBeGreaterThan(0);
        PreviewPixels(editor, SKColors.Red).ShouldBe(0);

        // Twice as wide and red: the same UV region is now a 2:1 sprite.
        editor.WritePng(Path.Combine("art", "sheet.png"), 128, 64, SKColors.Red);

        (await editor.WaitUntilAsync(() => Math.Abs(SpriteWidthOfFirstCell(editor) - 2 * TimelineLayout.FilmHeight) < 1e-6, ReloadTimeout))
            .ShouldBeTrue("the filmstrip should lay its cells out from the replaced texture");
        (await editor.WaitUntilAsync(() => ChipImageSize(editor).Width == 2 * ChipImageSize(editor).Height, ReloadTimeout))
            .ShouldBeTrue("the chip should show the replaced texture's 2:1 sprite");
        (await editor.WaitUntilAsync(() => PreviewPixels(editor, SKColors.Red) > 0, ReloadTimeout))
            .ShouldBeTrue("the composited preview should draw the replaced pixels");
    }

    [AvaloniaFact]
    public async Task HotReloadOff_LeavesTheSequenceTabAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        editor.ClickMenu("MenuEnableHotReload");

        var rewritten = SequenceListSave.FromFile(path);
        rewritten.Sequences.Add(new SequenceSave { Name = "Die" });
        rewritten.Save(path);
        editor.WriteAchx(Path.Combine("art", "hero.achx"),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1200));

        editor.SequenceNames.ShouldBe(new[] { "Attack" });
    }

    [AvaloniaFact]
    public async Task SourceEditedInItsOwnTab_ThenBackToTheSequenceTab_ShowsTheEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        string hero = Path.Combine(editor.ProjectFolder, "art", "hero.achx");
        await editor.OpenAsync(path);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Services.AppCommands.RenameChain(editor.ChainNamed("Walk"), "Stride");
        editor.Press(Key.S, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.ClickTab("boss.seqj");
        editor.Layout();

        Library(editor).ChipFor(Source, "Stride").ShouldNotBeNull("the tab-switch reload still covers a source edited in its own tab");
        editor.ThrowIfErrorShown();
    }
}
