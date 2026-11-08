using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dragging a source's header in a <c>.seqj</c> tab's library reorders <see cref="SequenceListSave.Sources"/>: an
/// insertion line between groups, the dragged group dimmed, one undo entry. A plain click on a header still collapses
/// the group, and a drag that starts on a chip is the chip's own drag to the timeline.
/// </summary>
public class SequenceLibraryReorderScenarioTests
{
    private static readonly string[] Four = { "a.achx", "b.achx", "c.achx", "d.achx" };

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static Control Header(AnimationEditorHarness editor, string source) => Library(editor).HeaderFor(source)!;

    private static Control Group(AnimationEditorHarness editor, string source) => Library(editor).GroupFor(source)!;

    /// <summary>
    /// Writes one <c>.achx</c> per source (none for a name starting with "gone") and a <c>.seqj</c> with an empty "Body"
    /// track, then opens it.
    /// </summary>
    private static Task<string> OpenAsync(AnimationEditorHarness editor, params string[] sources) => OpenPlacedAsync(editor, null, sources);

    /// <summary>As <see cref="OpenAsync"/>, with the "Body" track already holding a chain of <paramref name="placedFrom"/>.</summary>
    private static async Task<string> OpenPlacedAsync(AnimationEditorHarness editor, string? placedFrom, string[] sources)
    {
        editor.WritePng("sheet.png", 64, 64);
        foreach (string source in sources)
            if (!source.StartsWith("gone"))
                editor.WriteAchx(source, AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        var sequence = new SequenceSave { Name = "Attack" };
        var body = new SequenceTrackSave { Name = "Body" };
        if (placedFrom is not null)
            body.Chains.Add(new SequenceChainReferenceSave { Source = placedFrom, Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(body);
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        return path;
    }

    private static string[] SavedSources(string path) => SequenceListSave.FromFile(path).Sources.ToArray();

    /// <summary>The order the groups are drawn in, top to bottom.</summary>
    private static string[] VisualOrder(AnimationEditorHarness editor) =>
        Library(editor).Sources.Select(source => source.Source)
            .OrderBy(source => Group(editor, source).TranslatePoint(default, Library(editor))!.Value.Y).ToArray();

    private static string[] ModelOrder(AnimationEditorHarness editor) => Library(editor).Sources.Select(source => source.Source).ToArray();

    // Near the top / bottom edge of a group: the upper / lower half of it, which points at the gap above / below.
    private static Point AboveGroup(AnimationEditorHarness editor, string source) =>
        editor.PointIn(Group(editor, source), Group(editor, source).Bounds.Width / 2, 4);

    private static Point BelowGroup(AnimationEditorHarness editor, string source) =>
        editor.PointIn(Group(editor, source), Group(editor, source).Bounds.Width / 2, Group(editor, source).Bounds.Height - 4);

    // A spot on the header that is neither the chevron nor a button.
    private static Point GrabPoint(AnimationEditorHarness editor, string source) =>
        editor.PointIn(Header(editor, source), Header(editor, source).Bounds.Width * 0.6, Header(editor, source).Bounds.Height / 2);

    private static List<Border> DropLines(AnimationEditorHarness editor) =>
        editor.Control<Canvas>("DragOverlayCanvas").Children.OfType<Border>().Where(border => border.Height == 2).ToList();

    private static double LineY(Border line) => Canvas.GetTop(line) + 1;

    private static double TopInOverlay(AnimationEditorHarness editor, string source) =>
        Group(editor, source).TranslatePoint(default, editor.Control<Canvas>("DragOverlayCanvas"))!.Value.Y;

    private static double BottomInOverlay(AnimationEditorHarness editor, string source) =>
        TopInOverlay(editor, source) + Group(editor, source).Bounds.Height;

    /// <summary>Where the insertion line for gap <paramref name="slot"/> is drawn: the middle of the space between two groups.</summary>
    private static double GapY(AnimationEditorHarness editor, string[] order, int slot)
    {
        editor.Layout();
        if (slot == 0) return TopInOverlay(editor, order[0]);
        if (slot == order.Length) return BottomInOverlay(editor, order[^1]);
        return (BottomInOverlay(editor, order[slot - 1]) + TopInOverlay(editor, order[slot])) / 2;
    }

    private static Button ToggleFor(AnimationEditorHarness editor, string source, bool expanded) =>
        Library(editor).GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == $"{(expanded ? "Collapse" : "Expand")} {source}");

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    private static void Redo(AnimationEditorHarness editor) => editor.Press(Key.Y, RawInputModifiers.Control);

    [AvaloniaFact]
    public async Task DraggingAHeaderBelowAnotherGroup_ReordersTheSources_SavesThem_AndUndoRedoRestoreTheLibrary()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.Drag(GrabPoint(editor, "a.achx"), BelowGroup(editor, "c.achx"));

        ModelOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        SavedSources(boss).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        editor.UndoLabels.ShouldBe(new[] { "Move Source 'a.achx'" });
        DropLines(editor).ShouldBeEmpty();

        Undo(editor);
        ModelOrder(editor).ShouldBe(Four);
        VisualOrder(editor).ShouldBe(Four);
        SavedSources(boss).ShouldBe(Four);

        Redo(editor);
        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        SavedSources(boss).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
    }

    [AvaloniaFact]
    public async Task DraggingAHeaderAboveAnotherGroup_AndToTheTop_Works()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.Drag(GrabPoint(editor, "d.achx"), AboveGroup(editor, "b.achx"));
        VisualOrder(editor).ShouldBe(new[] { "a.achx", "d.achx", "b.achx", "c.achx" });

        editor.Drag(GrabPoint(editor, "c.achx"), AboveGroup(editor, "a.achx"));
        VisualOrder(editor).ShouldBe(new[] { "c.achx", "a.achx", "d.achx", "b.achx" });
        SavedSources(boss).ShouldBe(new[] { "c.achx", "a.achx", "d.achx", "b.achx" });
    }

    [AvaloniaFact]
    public async Task WhileDragging_AnInsertionLineMarksTheGapBetweenGroups_TheDraggedGroupDims_AndBothGoOnRelease()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Four);

        editor.DragHold(GrabPoint(editor, "a.achx"), BelowGroup(editor, "c.achx"));

        LineY(DropLines(editor).ShouldHaveSingleItem()).ShouldBe(GapY(editor, Four, 3), 1.5);
        Group(editor, "a.achx").Opacity.ShouldBeLessThan(1);
        Group(editor, "b.achx").Opacity.ShouldBe(1);

        editor.DragMove(AboveGroup(editor, "b.achx"));
        // The gap next to the dragged group itself moves nothing, so it shows no line.
        DropLines(editor).ShouldBeEmpty();

        editor.DragMove(BelowGroup(editor, "d.achx"));
        LineY(DropLines(editor).ShouldHaveSingleItem()).ShouldBe(GapY(editor, Four, 4), 1.5);

        editor.DragRelease(BelowGroup(editor, "d.achx"));
        DropLines(editor).ShouldBeEmpty();
        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "d.achx", "a.achx" });
        Group(editor, "a.achx").Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task APlainClickOnAHeader_StillCollapsesAndExpandsTheGroup_WithNoUndoEntryAndNoReorder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.ClickAt(GrabPoint(editor, "b.achx"));
        Library(editor).IsCollapsed("b.achx").ShouldBeTrue();
        Library(editor).ChipFor("b.achx", "Walk")!.IsEffectivelyVisible.ShouldBeFalse();

        var title = Header(editor, "b.achx").GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "b.achx");
        editor.Click(title);
        Library(editor).IsCollapsed("b.achx").ShouldBeFalse();
        Library(editor).ChipFor("b.achx", "Walk")!.IsEffectivelyVisible.ShouldBeTrue();

        editor.UndoLabels.ShouldBeEmpty();
        SavedSources(boss).ShouldBe(Four);
    }

    [AvaloniaFact]
    public async Task APressWithATinyWiggleUnderTheThreshold_IsStillAClick()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Four);
        Point grab = GrabPoint(editor, "b.achx");

        editor.Drag(grab, grab + new Point(0, 2));

        Library(editor).IsCollapsed("b.achx").ShouldBeTrue();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PressingAndMovingPastTheThreshold_Reorders_AndDoesNotToggleTheCollapseOnRelease()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Four);

        editor.Drag(GrabPoint(editor, "a.achx"), BelowGroup(editor, "b.achx"));

        VisualOrder(editor).ShouldBe(new[] { "b.achx", "a.achx", "c.achx", "d.achx" });
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();
        Library(editor).ChipFor("a.achx", "Walk")!.IsEffectivelyVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ADragThatDropsOnItsOwnGap_ChangesNothing_RecordsNoUndoEntry_AndDoesNotToggleTheCollapse()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.Drag(GrabPoint(editor, "b.achx"), AboveGroup(editor, "b.achx"));
        editor.Drag(GrabPoint(editor, "b.achx"), BelowGroup(editor, "b.achx"));
        editor.Drag(GrabPoint(editor, "b.achx"), BelowGroup(editor, "a.achx"));
        editor.Drag(GrabPoint(editor, "b.achx"), AboveGroup(editor, "c.achx"));

        VisualOrder(editor).ShouldBe(Four);
        editor.UndoLabels.ShouldBeEmpty();
        SavedSources(boss).ShouldBe(Four);
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();
        DropLines(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheCollapsedState_FollowsTheMovedSource()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Four);
        editor.Click(ToggleFor(editor, "a.achx", expanded: true));
        editor.Click(ToggleFor(editor, "c.achx", expanded: true));

        editor.Drag(GrabPoint(editor, "a.achx"), BelowGroup(editor, "d.achx"));

        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "d.achx", "a.achx" });
        Library(editor).IsCollapsed("a.achx").ShouldBeTrue();
        Library(editor).IsCollapsed("c.achx").ShouldBeTrue();
        Library(editor).IsCollapsed("b.achx").ShouldBeFalse();
        Library(editor).ChipFor("a.achx", "Walk")!.IsEffectivelyVisible.ShouldBeFalse();
        Library(editor).ChipFor("b.achx", "Walk")!.IsEffectivelyVisible.ShouldBeTrue();

        Undo(editor);
        VisualOrder(editor).ShouldBe(Four);
        Library(editor).IsCollapsed("a.achx").ShouldBeTrue();
        Library(editor).ChipFor("a.achx", "Walk")!.IsEffectivelyVisible.ShouldBeFalse();
        Library(editor).ChipFor("d.achx", "Walk")!.IsEffectivelyVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task EscapeDuringTheDrag_CancelsIt_NothingMovesAndNoLineIsLeft()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.DragHold(GrabPoint(editor, "a.achx"), BelowGroup(editor, "c.achx"));
        DropLines(editor).ShouldHaveSingleItem();
        editor.Press(Key.Escape);
        DropLines(editor).ShouldBeEmpty();
        Group(editor, "a.achx").Opacity.ShouldBe(1);
        editor.DragMove(BelowGroup(editor, "d.achx"));
        editor.DragRelease(BelowGroup(editor, "d.achx"));

        VisualOrder(editor).ShouldBe(Four);
        editor.UndoLabels.ShouldBeEmpty();
        SavedSources(boss).ShouldBe(Four);
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();
        DropLines(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingAChip_StillDragsTheChipToTheTimeline_AndNeverReordersAGroup()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);
        var chip = Library(editor).ChipFor("b.achx", "Walk")!;
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        double laneY = timeline.Viewport.LaneTop(0) + TimelineViewport.LaneHeight / 2;
        var moved = new List<string>();
        Library(editor).ChipDragMoved += (dragged, _) => moved.Add(dragged.Chain.Name);

        // Over another group's header first, as a group drag would be aimed, then to the timeline lane.
        editor.DragHold(editor.CenterOf(chip), GrabPoint(editor, "d.achx"));
        DropLines(editor).ShouldBeEmpty();
        Group(editor, "b.achx").Opacity.ShouldBe(1);
        editor.DragMove(editor.PointIn(timeline, timeline.Viewport.XAt(1.0), laneY));
        editor.DragRelease(editor.PointIn(timeline, timeline.Viewport.XAt(1.0), laneY));

        moved.ShouldNotBeEmpty();
        editor.UndoLabels.ShouldBe(new[] { "Add 'Walk' to 'Body'" });
        VisualOrder(editor).ShouldBe(Four);
        SavedSources(boss).ShouldBe(Four);
    }

    [AvaloniaFact]
    public async Task ADragThatEndsOnAnotherGroupsHeader_FromAChip_ChangesNoOrderAndTogglesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);
        var chip = Library(editor).ChipFor("a.achx", "Walk")!;

        editor.Drag(editor.CenterOf(chip), GrabPoint(editor, "c.achx"));

        VisualOrder(editor).ShouldBe(Four);
        SavedSources(boss).ShouldBe(Four);
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task APressOnTheRemoveButton_DoesNotStartAReorder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);
        var remove = Header(editor, "a.achx").GetVisualDescendants().OfType<Button>().Single(button => (string?)button.Tag == "a.achx");

        editor.DragHold(editor.CenterOf(remove), BelowGroup(editor, "c.achx"));
        DropLines(editor).ShouldBeEmpty();
        editor.DragRelease(BelowGroup(editor, "c.achx"));

        VisualOrder(editor).ShouldBe(Four);
        SavedSources(boss).ShouldBe(Four);
    }

    [AvaloniaFact]
    public async Task AMissingSource_CanBeDragged_ButClickingItsHeaderStillDoesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, "a.achx", "gone.achx", "c.achx");

        editor.ClickAt(GrabPoint(editor, "gone.achx"));
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();

        editor.Drag(GrabPoint(editor, "gone.achx"), BelowGroup(editor, "c.achx"));

        VisualOrder(editor).ShouldBe(new[] { "a.achx", "c.achx", "gone.achx" });
        SavedSources(boss).ShouldBe(new[] { "a.achx", "c.achx", "gone.achx" });
        Library(editor).CollapsedSources.Keys.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AMoveChangesNothingOnTheTimeline_AChainStillPointsAtItsSourcePath()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenPlacedAsync(editor, "c.achx", Four);
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        int blocks = timeline.Blocks.Count;
        blocks.ShouldBeGreaterThan(0);

        editor.Drag(GrabPoint(editor, "c.achx"), AboveGroup(editor, "a.achx"));

        VisualOrder(editor).ShouldBe(new[] { "c.achx", "a.achx", "b.achx", "d.achx" });
        timeline.Blocks.Count.ShouldBe(blocks);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains.Single().Source.ShouldBe("c.achx");
        editor.ErrorBannerText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task TheMoveSavesLikeAnyEdit_SoTheLiveReloadDoesNotReactToItsOwnWrite()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenAsync(editor, Four);

        editor.Drag(GrabPoint(editor, "a.achx"), BelowGroup(editor, "d.achx"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1500));

        editor.ToastText.ShouldBeNull();
        editor.ErrorBannerText.ShouldBeNull();
        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "d.achx", "a.achx" });
        SavedSources(boss).ShouldBe(new[] { "b.achx", "c.achx", "d.achx", "a.achx" });
        editor.UndoLabels.ShouldBe(new[] { "Move Source 'a.achx'" });
    }

    [AvaloniaFact]
    public async Task AMove_IsAnEditNotAPlaceToReturnTo_SoBackHasNothingToGoBackTo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Four);
        editor.Control<MenuItem>("MenuNavBack").IsEnabled.ShouldBeFalse();

        editor.Drag(GrabPoint(editor, "a.achx"), BelowGroup(editor, "d.achx"));

        VisualOrder(editor).ShouldBe(new[] { "b.achx", "c.achx", "d.achx", "a.achx" });
        editor.Control<MenuItem>("MenuNavBack").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DraggingNearThePanelEdges_ScrollsTheLibrary_ThenStops_WhenCancelled()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string[] many = Enumerable.Range(0, 14).Select(i => $"s{i:00}.achx").ToArray();
        await OpenAsync(editor, many);
        var scroller = Library(editor).GetVisualDescendants().OfType<ScrollViewer>().First();
        (scroller.Extent.Height > scroller.Viewport.Height).ShouldBeTrue();
        scroller.Offset.Y.ShouldBe(0);

        Point bottomEdge = editor.PointIn(scroller, scroller.Bounds.Width / 2, scroller.Bounds.Height - 3);
        editor.DragHold(GrabPoint(editor, "s00.achx"), bottomEdge);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));
        double scrolled = scroller.Offset.Y;
        scrolled.ShouldBeGreaterThan(0);

        editor.DragMove(editor.PointIn(scroller, scroller.Bounds.Width / 2, 3));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));
        scroller.Offset.Y.ShouldBeLessThan(scrolled);

        editor.Press(Key.Escape);
        double afterCancel = scroller.Offset.Y;
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        scroller.Offset.Y.ShouldBe(afterCancel);
        editor.DragRelease(editor.PointIn(scroller, scroller.Bounds.Width / 2, 3));
        editor.UndoLabels.ShouldBeEmpty();
    }
}
