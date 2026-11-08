using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The sequence list row, track header, chain block and blank-space menus offer the same Copy, Cut, Paste, Duplicate and
/// Delete as the hotkeys, in the order, wording and icons of the ANIMATIONS tree's menu (Copy, Cut, Paste, Duplicate; then
/// Rename; then Delete). Blank lane and header space offer Add Track and Paste. Fixture: Attack (tracks Body: Walk, Walk,
/// Run; Head: Run, Walk; Tail: empty), then the empty sequences Idle and Walk.
/// </summary>
public class SequenceClipboardMenuScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static string WriteThreeSequences(AnimationEditorHarness editor)
    {
        string path = WriteFixture(editor);
        var save = SequenceListSave.FromFile(path);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Walk" });
        save.Save(path);
        return path;
    }

    private static Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static List<string> Headers(ContextMenu menu) =>
        menu.Items.Select(item => item is MenuItem menuItem ? (string)menuItem.Header! : "-").ToList();

    private static MenuItem ItemOf(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);

    private static List<string> SavedSequences(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static List<string> SavedTrackNames(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    private static int SavedChainCount(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Sum(track => track.Chains.Count);

    private static void ClickRow(AnimationEditorHarness editor, int row, RawInputModifiers modifiers = RawInputModifiers.None) =>
        editor.ClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!), modifiers);

    private static void RightClickBlankList(AnimationEditorHarness editor)
    {
        ListBox list = List(editor);
        editor.RightClickAt(editor.PointIn(list, 20, list.Bounds.Height - 10));
    }


    private static void ItemsShowTheTreesIcons(ContextMenu menu)
    {
        // Every item has an icon, as every tree menu item does (Go to Definition and Hide/Show Track reuse the nearest art).
        foreach (var item in menu.Items.OfType<MenuItem>())
            item.Icon.ShouldNotBeNull($"\"{item.Header}\" has no icon");
    }

    // ── Sequence list ───────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task ARowMenu_OffersCopyCutPasteDuplicate_ThenRename_ThenDelete_ThenAddSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);

        Headers(List(editor).ContextMenu!).ShouldBe(new[]
        {
            "Copy", "Cut", "Paste", "Duplicate Sequence", "-", "Rename…", "Copy Qualified Name", "-", "Delete", "-", "Add Sequence",
        });
        ItemsShowTheTreesIcons(List(editor).ContextMenu!);
    }

    [AvaloniaFact]
    public async Task ARowMenu_WithSeveralSelected_CountsThemAndDisablesRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);

        var menu = List(editor).ContextMenu!;
        Headers(menu).ShouldBe(new[]
        {
            "Copy 2 Sequences", "Cut 2 Sequences", "Paste", "Duplicate 2 Sequences", "-", "Rename…", "Copy Qualified Name", "-", "Delete 2 Sequences", "-", "Add Sequence",
        });
        ItemOf(menu, "Rename…").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task BlankListSpace_OffersAddSequenceAndPaste()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        RightClickBlankList(editor);

        Headers(List(editor).ContextMenu!).ShouldBe(new[] { "Add Sequence", "-", "Paste" });
    }

    [AvaloniaFact]
    public async Task ARowMenu_CopyThenPaste_PastesACopyAfterTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Copy");
        (await editor.Window.Clipboard!.TryGetTextAsync()).ShouldNotBeNull().ShouldContain("Idle");

        editor.RightClick((Control)List(editor).ContainerFromIndex(2)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedSequences(path).Count.ShouldBe(4);
        SavedSequences(path).Count(name => name.StartsWith("Idle")).ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task ARowMenu_Cut_RemovesTheSequenceAndPutsItOnTheClipboard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);

        editor.PickMenuItem(List(editor).ContextMenu!, "Cut");

        SavedSequences(path).ShouldBe(new[] { "Attack", "Walk" });
        (await editor.Window.Clipboard!.TryGetTextAsync()).ShouldNotBeNull().ShouldContain("Idle");
    }

    [AvaloniaFact]
    public async Task BlankSpaceMenu_Paste_PastesCopiedSequences()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        ClickRow(editor, 0);
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        RightClickBlankList(editor);

        editor.PickMenuItem(List(editor).ContextMenu!, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedSequences(path).Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public async Task APasteFromAMenu_WithNothingCopied_SaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        await editor.Window.Clipboard!.ClearAsync();
        RightClickBlankList(editor);

        editor.PickMenuItem(List(editor).ContextMenu!, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.ProblemText.ShouldNotBeNull().ShouldContain("Nothing to paste");
    }

    // ── Timeline ────────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_OffersCopyCutPasteDuplicate_ThenRenameAndVisibility_ThenDelete()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        editor.RightClickAt(Header(editor, 1));

        Headers(Timeline(editor).Menu).ShouldBe(new[]
        {
            "Copy", "Cut", "Paste", "Duplicate Track", "-", "Rename Track…", "Hide Track", "-", "Delete Track",
        });
        ItemsShowTheTreesIcons(Timeline(editor).Menu);
    }

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_WithSeveralSelected_CountsThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);

        editor.RightClickAt(Header(editor, 1));

        Headers(Timeline(editor).Menu).ShouldBe(new[]
        {
            "Copy 2 Tracks", "Cut 2 Tracks", "Paste", "Duplicate 2 Tracks", "-", "Rename Track…", "Hide Track", "-", "Delete 2 Tracks",
        });
    }

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_CopyThenPaste_AddsTheTrackAtTheEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClickAt(Header(editor, 0));
        editor.PickMenuItem(Timeline(editor).Menu, "Copy");
        editor.RightClickAt(Header(editor, 2));
        editor.PickMenuItem(Timeline(editor).Menu, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedTrackNames(path).ShouldBe(new[] { "Body", "Head", "Tail", "Body 2" });
    }

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_Cut_RemovesTheTrackAndPutsItOnTheClipboard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClickAt(Header(editor, 0));

        editor.PickMenuItem(Timeline(editor).Menu, "Cut");

        SavedTrackNames(path).ShouldBe(new[] { "Head", "Tail" });
        (await editor.Window.Clipboard!.TryGetTextAsync()).ShouldNotBeNull().ShouldContain("Body");
    }

    [AvaloniaFact]
    public async Task AChainMenu_OffersCopyCutPasteDuplicate_ThenGoToDefinition_ThenRemove()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        editor.RightClickAt(At(editor, 0, 0.25));

        Headers(Timeline(editor).Menu).ShouldBe(new[]
        {
            "Copy", "Cut", "Paste", "Duplicate", "-", "Go to Definition", "-", "Remove from Track",
        });
        ItemsShowTheTreesIcons(Timeline(editor).Menu);
    }

    [AvaloniaFact]
    public async Task AChainMenu_WithSeveralSelected_CountsThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);

        editor.RightClickAt(At(editor, 1, 0.6));

        Headers(Timeline(editor).Menu).ShouldBe(new[]
        {
            "Copy 2 Chains", "Cut 2 Chains", "Paste", "Duplicate 2 Chains", "-", "Go to Definition", "-", "Remove 2 Chains",
        });
    }

    [AvaloniaFact]
    public async Task AChainMenu_CopyThenPaste_AddsTheChain_AndCutRemovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClickAt(At(editor, 0, 0.25));
        editor.PickMenuItem(Timeline(editor).Menu, "Copy");
        editor.RightClickAt(At(editor, 0, 0.25));
        editor.PickMenuItem(Timeline(editor).Menu, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        SavedChainCount(path).ShouldBe(6);

        editor.RightClickAt(At(editor, 0, 0.25));
        editor.PickMenuItem(Timeline(editor).Menu, "Cut");
        SavedChainCount(path).ShouldBe(5);
    }

    [AvaloniaFact]
    public async Task BlankLaneSpace_OffersAddTrackAndPaste()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        editor.RightClickAt(At(editor, 2, 5.0));

        Headers(Timeline(editor).Menu).ShouldBe(new[] { "Add Track", "-", "Paste" });
    }

    [AvaloniaFact]
    public async Task TheNewTrackRow_AndTheSpaceBelowIt_OfferAddTrackAndPaste()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        var timeline = Timeline(editor);

        editor.RightClickAt(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(3) + 10));
        Headers(timeline.Menu).ShouldBe(new[] { "Add Track", "-", "Paste" });
        timeline.Menu.Close();

        editor.RightClickAt(editor.PointIn(timeline, 400, timeline.Viewport.LaneTop(3) + 10));
        Headers(timeline.Menu).ShouldBe(new[] { "Add Track", "-", "Paste" });
    }

    [AvaloniaFact]
    public async Task TheRuler_HasNoMenu()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        var timeline = Timeline(editor);

        editor.RightClickAt(editor.PointIn(timeline, 400, 8));

        timeline.Menu.Items.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task BlankLaneMenu_AddTrack_AddsAndNamesInFront_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClickAt(At(editor, 2, 5.0));

        editor.PickMenuItem(Timeline(editor).Menu, "Add Track");

        SavedTrackNames(path).ShouldBe(new[] { "Track", "Body", "Head", "Tail" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Track'" });
        Timeline(editor).RenamingTrack.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task BlankLaneMenu_Paste_PastesCopiedChainsOntoTheirTracks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.RightClickAt(At(editor, 2, 5.0));

        editor.PickMenuItem(Timeline(editor).Menu, "Paste");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedChainCount(path).ShouldBe(6);
    }
}
