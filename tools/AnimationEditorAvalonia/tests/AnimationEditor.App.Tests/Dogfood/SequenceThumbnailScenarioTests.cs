using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> row of the Open Project Folder tree and every row of the sequence list inside a <c>.seqj</c> tab show a
/// thumbnail: the sequence's composite at the first instant where something is visible, loaded in the background, kept
/// current as the sequence, its sources and its textures change, and without disturbing selection, drag or the row menu.
/// </summary>
public class SequenceThumbnailScenarioTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    // hero.achx: Walk, one 16x16 frame on the 64x64 sheet. boss.seqj: A, B and C each play Walk on a track; Empty has no track.
    private static string WriteFixture(AnimationEditorHarness editor, params string[] sequences)
    {
        editor.WritePng("sheet.png", 64, 64, SKColors.Red);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("plain.achx", AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        foreach (string name in sequences.Length > 0 ? sequences : new[] { "A", "B", "C", "Empty" })
        {
            var sequence = new SequenceSave { Name = name };
            if (name != "Empty")
            {
                var track = new SequenceTrackSave { Name = "Body" };
                track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
                sequence.Tracks.Add(track);
            }
            save.Sequences.Add(sequence);
        }
        save.Save(path);
        return path;
    }

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static List<SequenceListItem> Items(AnimationEditorHarness editor) =>
        List(editor).Items.OfType<SequenceListItem>().ToList();

    private static Bitmap? ThumbnailOf(AnimationEditorHarness editor, string name) =>
        Items(editor).Single(item => item.Name == name).Thumbnail;

    private static Control Row(AnimationEditorHarness editor, int row) => (Control)List(editor).ContainerFromIndex(row)!;

    private static async Task<bool> AllLoadedAsync(AnimationEditorHarness editor, params string[] names) =>
        await editor.WaitUntilAsync(() => names.All(name => ThumbnailOf(editor, name) is not null), Timeout);

    private static ProjectPanelControl Panel(AnimationEditorHarness editor) => editor.Control<ProjectPanelControl>("ProjectPanel");

    private static AchxTreeNodeVm Node(ProjectPanelControl panel, string fileName)
    {
        AchxTreeNodeVm? Find(IEnumerable<AchxTreeNodeVm> nodes) =>
            nodes.Select(node => node.Entry?.FileName == fileName ? node : Find(node.Children)).FirstOrDefault(found => found is not null);
        return Find(panel.TreeRoots)!;
    }

    [AvaloniaFact]
    public async Task ProjectTree_SeqjRowGetsAThumbnailLikeAnAchxRow_AndTheIconStepsAside()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteFixture(editor);
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        var panel = Panel(editor);
        await panel.ThumbnailLoadTask;

        var seqj = Node(panel, "boss.seqj");
        var achx = Node(panel, "hero.achx");
        achx.Thumbnail.ShouldNotBeNull();
        seqj.Thumbnail.ShouldNotBeNull();
        // Same fit as the .achx row: the 16x16 sprite fills the 28 px box.
        seqj.Thumbnail!.PixelSize.ShouldBe(achx.Thumbnail!.PixelSize);
        seqj.FileIconContent.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ProjectTree_ASequenceWithNothingToShow_KeepsTheSequenceIcon()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WriteSeqj("empty.seqj", "A");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await Panel(editor).ThumbnailLoadTask;

        var node = Node(Panel(editor), "empty.seqj");
        node.Thumbnail.ShouldBeNull();
        node.FileIconContent.ShouldBe(ProjectRowSvgIcon.Sequence);
    }

    [AvaloniaFact]
    public async Task ProjectTree_ChangingASourceOnDisk_RefreshesTheSeqjRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteFixture(editor);
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await Panel(editor).ThumbnailLoadTask;
        var seqj = Node(Panel(editor), "boss.seqj");
        seqj.Thumbnail!.PixelSize.Height.ShouldBe(28);

        // The Walk frame becomes 32x16: a 2:1 picture, 28 wide and 14 tall.
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 16)));

        (await editor.WaitUntilAsync(() => seqj.Thumbnail?.PixelSize.Height == 14, Timeout))
            .ShouldBeTrue("the row should redraw from the changed source");
    }

    [AvaloniaFact]
    public async Task ProjectTree_EditingTheOpenSeqj_UpdatesItsRow_AndUndoBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "A");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await Panel(editor).ThumbnailLoadTask;
        await editor.OpenAsync(boss);
        var seqj = Node(Panel(editor), "boss.seqj");
        seqj.Thumbnail.ShouldNotBeNull();

        // Hiding the only track leaves nothing to show.
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        editor.ClickAt(editor.PointIn(timeline, TimelineLayout.EyeCenterX, timeline.HeaderCenters(0).Eye));
        (await editor.WaitUntilAsync(() => seqj.Thumbnail is null, Timeout)).ShouldBeTrue("a hidden track leaves the row without a picture");

        editor.Press(Key.Z, RawInputModifiers.Control);
        (await editor.WaitUntilAsync(() => seqj.Thumbnail is not null, Timeout)).ShouldBeTrue("undo restores it");
    }

    [AvaloniaFact]
    public async Task List_EveryRowReservesTheSlot_AndRowsWithContentFillIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();
        ThumbnailOf(editor, "Empty").ShouldBeNull();
        foreach (int row in Enumerable.Range(0, 4))
        {
            // The same box as the ANIMATIONS chain rows: 28 px square, Uniform, 4 px gap.
            var image = Row(editor, row).GetVisualDescendants().OfType<Image>().Single();
            (image.Width, image.Height).ShouldBe((28d, 28d));
            image.Margin.ShouldBe(new Thickness(0, 0, 4, 0));
            image.Stretch.ShouldBe(Avalonia.Media.Stretch.Uniform);
        }
        // The summary and the name still sit beside it, and the name is the column that gives way.
        Row(editor, 0).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("1 track · 0.10s");
    }

    [AvaloniaFact]
    public async Task List_ARowFollowsItsSequence_ThroughAnEditAndUndo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, "A"));
        (await AllLoadedAsync(editor, "A")).ShouldBeTrue();

        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        editor.ClickAt(editor.PointIn(timeline, TimelineLayout.EyeCenterX, timeline.HeaderCenters(0).Eye));
        (await editor.WaitUntilAsync(() => ThumbnailOf(editor, "A") is null, Timeout)).ShouldBeTrue();

        editor.Press(Key.Z, RawInputModifiers.Control);
        (await AllLoadedAsync(editor, "A")).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task List_AReplacedTexture_RedrawsTheRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "A");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await editor.OpenAsync(boss);
        (await AllLoadedAsync(editor, "A")).ShouldBeTrue();
        ThumbnailOf(editor, "A")!.PixelSize.Height.ShouldBe(56);

        // Twice as wide: the same UV region is now a 2:1 sprite.
        editor.WritePng("sheet.png", 128, 64, SKColors.Lime);

        (await editor.WaitUntilAsync(() => ThumbnailOf(editor, "A")?.PixelSize.Height == 28, Timeout))
            .ShouldBeTrue("the row should draw the replaced sheet");
    }

    [AvaloniaFact]
    public async Task List_MultiSelect_StillSelectsSeveralRows_WithThumbnailsPresent()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();

        editor.ClickAt(editor.CenterOf(Row(editor, 0)));
        editor.ClickAt(editor.CenterOf(Row(editor, 2)), RawInputModifiers.Control);

        List(editor).SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ShouldBe(new[] { "A", "C" });
    }

    [AvaloniaFact]
    public async Task List_DragReorder_StillMovesTheRow_AndEachRowKeepsItsPicture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();

        var below = editor.PointIn(Row(editor, 2), Row(editor, 2).Bounds.Width / 2, Row(editor, 2).Bounds.Height * 0.75);
        editor.Drag(editor.CenterOf(Row(editor, 0)), below);

        editor.SequenceNames.ShouldBe(new[] { "B", "C", "A", "Empty" });
        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();
        ThumbnailOf(editor, "Empty").ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task List_RowMenu_StillOpensOnARightClick_WithThumbnailsPresent()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();

        editor.RightClickAt(editor.CenterOf(Row(editor, 1)));

        var menu = editor.Control<ContextMenu>("SequenceListMenu");
        menu.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ShouldContain("Delete Sequence");
        menu.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ShouldContain("Rename…");
    }

    [AvaloniaFact]
    public async Task List_RenamingARow_StillOpensTheBox_AndKeepsThePicture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        (await AllLoadedAsync(editor, "A", "B", "C")).ShouldBeTrue();

        editor.DoubleClickAt(editor.CenterOf(Row(editor, 0)));
        editor.TypeIntoOpenSequenceRename("Renamed");
        editor.Press(Key.Enter);

        editor.SequenceNames[0].ShouldBe("Renamed");
        (await AllLoadedAsync(editor, "Renamed")).ShouldBeTrue();
    }
}
