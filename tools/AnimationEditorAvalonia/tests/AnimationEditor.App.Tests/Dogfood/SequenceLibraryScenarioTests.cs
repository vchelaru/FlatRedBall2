using AnimationEditor.App.Controls;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab's library: sources dropped in from the Animations browser, shown as chips per chain.
/// </summary>
public class SequenceLibraryScenarioTests
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

    // The sheet and the .achx sit in a subfolder, so a texture resolved against the .seqj's folder would not be found.
    private static void WriteHeroInSubfolder(AnimationEditorHarness editor)
    {
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "chars"));
        editor.WritePng(Path.Combine("chars", "sheet.png"), 64, 64);
        editor.WriteAchx(Path.Combine("chars", "hero.achx"),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    // OS drag-drop can't be driven headlessly, so this raises the drag-over and drop a real drag would end with.
    internal static async Task Drop(AnimationEditorHarness editor, Interactive target, params string[] paths)
    {
        DataTransfer data = new DataTransfer();
        foreach (string path in paths)
            data.Add(DataTransferItem.CreateFile((await editor.Window.StorageProvider.TryGetFileFromPathAsync(path))!));
        Point point = editor.CenterOf((Control)target);
        target.RaiseEvent(new DragEventArgs(DragDrop.DragOverEvent, data, target, point, KeyModifiers.None));
        target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, target, point, KeyModifiers.None));
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task Library_ShowsAChipPerChainWithItsFirstFrame_AndFlagsAMissingSource()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx", "gone.achx");

        await editor.OpenAsync(boss);

        SequenceLibraryControl library = Library(editor);
        library.Sources.Select(source => (source.Source, source.IsMissing))
            .ShouldBe(new[] { ("chars/hero.achx", false), ("gone.achx", true) });
        LibraryChipView walk = library.ChipFor("chars/hero.achx", "Walk")!;
        walk.FrameIndex.ShouldBe(0);
        walk.GetVisualDescendants().OfType<Image>().Single().Source.ShouldNotBeNull();
        library.ChipFor("chars/hero.achx", "Run").ShouldNotBeNull();
        library.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ShouldContain("file not found");
    }

    [AvaloniaFact]
    public async Task HoveringAChip_PlaysItsChain_AndLeavingShowsTheFirstFrameAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        await editor.OpenAsync(WriteSeqj(editor, "chars/hero.achx"));
        LibraryChipView walk = Library(editor).ChipFor("chars/hero.achx", "Walk")!;

        editor.Hover(editor.CenterOf(walk));
        await editor.WaitUntilAsync(() => walk.FrameIndex > 0, TimeSpan.FromSeconds(2));

        walk.FrameIndex.ShouldBeGreaterThan(0);
        editor.Hover(editor.CenterOf(editor.Control<Button>("AddSequenceButton")));
        walk.IsPlaying.ShouldBeFalse();
        walk.FrameIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task DroppingFiles_AddsTheAchxAsOneUndoStep_RefusesTheSeqj_AndOpensNoTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string other = editor.WriteSeqj("other.seqj", "Idle");
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);

        await Drop(editor, Library(editor), Path.Combine(editor.ProjectFolder, "chars", "hero.achx"), other);

        Library(editor).ChipFor("chars/hero.achx", "Walk").ShouldNotBeNull();
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "chars/hero.achx" });
        editor.ProblemText.ShouldBe("other.seqj can't be a source. Only .achx and .achj files can.");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.UndoLabels.ShouldBe(new[] { "Add Source 'hero.achx'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Library(editor).Sources.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task RemoveButton_RemovesTheSource_AndCtrlZBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx");
        await editor.OpenAsync(boss);
        Button remove = Library(editor).GetVisualDescendants().OfType<Button>()
            .Single(button => (string?)button.Tag == "chars/hero.achx");

        editor.Click(remove);

        Library(editor).Sources.ShouldBeEmpty();
        SequenceListSave.FromFile(boss).Sources.ShouldBeEmpty();
        editor.UndoLabels.ShouldBe(new[] { "Remove Source 'hero.achx'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Library(editor).ChipFor("chars/hero.achx", "Walk").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task OnASequenceTab_ClickingAFileInTheAnimationsBrowserStaysPut_DoubleClickOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = WriteSeqj(editor);
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        await editor.OpenAsync(boss);
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");

        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.Control<Grid>("SequenceEditorPane").IsVisible.ShouldBeTrue();

        // Past the double-click window, so this is a fresh double-click, not a third click.
        Thread.Sleep(600);
        editor.DoubleClickAt(editor.CenterOf(PanelsScenarioTests.RowFor(editor, panel, "hero.achx")));
        editor.WaitUntil(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));

        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeFalse();
        editor.TabLabels.ShouldContain("boss.seqj");
    }

    // The title side of a header row, away from the remove button at its right edge.
    private static Point OnHeaderTitle(AnimationEditorHarness editor, Control header)
    {
        Point center = editor.CenterOf(header);
        return new Point(center.X - header.Bounds.Width / 4, center.Y);
    }

    private static MenuItem Item(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);

    [AvaloniaFact]
    public async Task RightClickingAChip_OffersGoToDefinition_WhichOpensTheSourceAndSelectsThatChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx");
        await editor.OpenAsync(boss);
        LibraryChipView run = Library(editor).ChipFor("chars/hero.achx", "Run")!;

        editor.RightClick(run);

        run.ContextMenu!.IsOpen.ShouldBeTrue();
        run.ContextMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header)
            .ShouldBe(new[] { "Add to Timeline", "Add to Selected Track", "Go to Definition", "Open Source" });
        editor.PickMenuItem(run.ContextMenu, "Go to Definition");
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task RightClickingASourceHeader_OffersGoToDefinitionAndRemove_AndGoToDefinitionOpensTheSource()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx");
        await editor.OpenAsync(boss);
        Control header = Library(editor).HeaderFor("chars/hero.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));

        header.ContextMenu!.IsOpen.ShouldBeTrue();
        header.ContextMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header)
            .ShouldBe(new[] { "Go to Definition", "Replace Source…", "Remove Source" });
        editor.PickMenuItem(header.ContextMenu, "Go to Definition");
        await editor.WaitUntilAsync(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task HeaderRemoveSource_DoesWhatTheXButtonDoes_AndCtrlZBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx");
        await editor.OpenAsync(boss);
        Control header = Library(editor).HeaderFor("chars/hero.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));
        editor.PickMenuItem(header.ContextMenu!, "Remove Source");

        Library(editor).Sources.ShouldBeEmpty();
        editor.UndoLabels.ShouldBe(new[] { "Remove Source 'hero.achx'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Library(editor).ChipFor("chars/hero.achx", "Walk").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task AMissingSourcesHeader_DisablesGoToDefinition_ButKeepsRemove()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteSeqj(editor, "gone.achx");
        await editor.OpenAsync(boss);
        Control header = Library(editor).HeaderFor("gone.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));

        header.ContextMenu!.IsOpen.ShouldBeTrue();
        Item(header.ContextMenu, "Go to Definition").IsEnabled.ShouldBeFalse();
        Item(header.ContextMenu, "Remove Source").IsEnabled.ShouldBeTrue();
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
    }

    [AvaloniaFact]
    public async Task RightClickingAChipMidDrag_OpensNoMenu_AndDoesNotBreakTheDrag()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor, "chars/hero.achx");
        await editor.OpenAsync(boss);
        LibraryChipView walk = Library(editor).ChipFor("chars/hero.achx", "Walk")!;
        Point start = editor.CenterOf(walk);
        Point away = new Point(start.X + 30, start.Y + 30);

        editor.Window.MouseMove(start, RawInputModifiers.None);
        editor.Window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(away, RawInputModifiers.LeftMouseButton);
        editor.Window.MouseDown(away, MouseButton.Right, RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(away, MouseButton.Right, RawInputModifiers.LeftMouseButton);
        editor.Layout();

        walk.ContextMenu!.IsOpen.ShouldBeFalse();
        editor.Window.MouseUp(away, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task GoToDefinitionFromTheLibrary_LeavesTheSequenceTabsUndoHistoryAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        string boss = WriteSeqj(editor);
        await editor.OpenAsync(boss);
        await Drop(editor, Library(editor), Path.Combine(editor.ProjectFolder, "chars", "hero.achx"));
        editor.UndoLabels.ShouldBe(new[] { "Add Source 'hero.achx'" });
        LibraryChipView walk = Library(editor).ChipFor("chars/hero.achx", "Walk")!;

        editor.RightClick(walk);
        editor.PickMenuItem(walk.ContextMenu!, "Go to Definition");
        await editor.WaitUntilAsync(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));
        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        editor.UndoLabels.ShouldBe(new[] { "Add Source 'hero.achx'" });
    }

    [AvaloniaFact]
    public async Task GoToDefinitionFromTheLibrary_PushesAHistoryEntry_SoMouseBackReturnsToTheSequenceTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        WriteHeroInSubfolder(editor);
        await editor.OpenAsync(WriteSeqj(editor, "chars/hero.achx"));
        LibraryChipView run = Library(editor).ChipFor("chars/hero.achx", "Run")!;

        editor.RightClick(run);
        editor.PickMenuItem(run.ContextMenu!, "Go to Definition");
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Control<MenuItem>("MenuNavBack").IsEnabled.ShouldBeTrue();

        Point point = new Point(300, 300);
        editor.Window.MouseDown(point, MouseButton.XButton1);
        editor.Window.MouseUp(point, MouseButton.XButton1);
        editor.Layout();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");
    }
}
