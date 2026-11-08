using AnimationEditor.App.Controls;
using AnimationEditor.Core.Paths;
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
/// Save As on a <c>.seqj</c> tab (the file moves, sources are rewritten to still resolve, the tab, recents, history and
/// watcher follow) and the repairs for a reference whose file or chain is gone: Replace Source in the library and the
/// Inspector's Chain dropdown.
/// </summary>
public class SequenceSaveAsAndRelinkScenarioTests
{
    // chars/hero.achx has Walk and Run; boss.seqj uses Walk from it on one track.
    private static string WriteBoss(AnimationEditorHarness editor, params string[] extraSources)
    {
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "chars"));
        editor.WritePng(Path.Combine("chars", "sheet.png"), 64, 64);
        editor.WriteAchx(Path.Combine("chars", "hero.achx"),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sources.Add("chars/hero.achx");
        save.Sources.AddRange(extraSources);
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "chars/hero.achx", Chain = "Walk", Loops = 1 });
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static List<string> RecentTips(AnimationEditorHarness editor) =>
        editor.Control<MenuItem>("MenuLoadRecent").Items.OfType<MenuItem>()
            .Select(item => (string?)ToolTip.GetTip(item) ?? "").ToList();

    private static Point OnHeaderTitle(AnimationEditorHarness editor, Control header)
    {
        Point center = editor.CenterOf(header);
        return new Point(center.X - header.Bounds.Width / 4, center.Y);
    }

    private static Button ReplaceButton(AnimationEditorHarness editor, string source) =>
        Library(editor).HeaderFor(source)!.GetVisualDescendants().OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == $"Replace {source}");

    private static Point OnChain(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static string[] ChainSources(SequenceListSave save) =>
        save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source).ToArray();

    // ---- Save As ----

    [AvaloniaFact]
    public async Task CtrlShiftS_OnASequenceTab_SavesToTheNewFolder_RewritesSources_AndTheTabFollowsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        byte[] oldBytes = File.ReadAllBytes(boss);
        await editor.OpenAsync(boss);
        string target = Path.Combine(editor.ProjectFolder, "levels", "boss2.seqj");

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        await editor.WaitUntilAsync(() => editor.TabLabels.SequenceEqual(new[] { "boss2.seqj" }), TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss2.seqj" });
        editor.TitleFileName.ShouldBe("boss2.seqj");
        SequenceListSave saved = SequenceListSave.FromFile(target);
        saved.Sources.ShouldBe(new[] { "../chars/hero.achx" });
        ChainSources(saved).ShouldBe(new[] { "../chars/hero.achx" });
        File.ReadAllBytes(boss).ShouldBe(oldBytes);
        Library(editor).Sources.Select(source => (source.Source, source.IsMissing)).ShouldBe(new[] { ("../chars/hero.achx", false) });
        Library(editor).ChipFor("../chars/hero.achx", "Walk").ShouldNotBeNull();
        RecentTips(editor)[0].ShouldBe(new FilePath(target).FullPath);
        editor.ErrorBannerText.ShouldBeNull();
        editor.Dialogs.ThrowIfUnanswered();
    }

    [AvaloniaFact]
    public async Task FileMenuSaveAs_IsEnabledOnASequenceTab_AndDisabledOnAPngTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        string png = editor.WritePng("loose.png", 8, 8);

        editor.Control<MenuItem>("MenuSaveAs").IsEnabled.ShouldBeTrue();

        await editor.OpenPngTabAsync(png);

        editor.Control<MenuItem>("MenuSaveAs").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task SaveAsFromTheMenu_AfterEdits_KeepsTheUndoHistory_AndLaterEditsGoToTheNewFileOnly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.Press(Key.Escape);   // keep the default name
        List<string> labelsBefore = editor.UndoLabels;
        labelsBefore.Count.ShouldBeGreaterThan(0);
        string target = Path.Combine(editor.ProjectFolder, "levels", "boss2.seqj");
        byte[] oldBytes = File.ReadAllBytes(boss);

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.ClickMenu("MenuSaveAs");
        await editor.WaitUntilAsync(() => editor.TabLabels.SequenceEqual(new[] { "boss2.seqj" }), TimeSpan.FromSeconds(5));
        // Past the watcher's coalescing: an own save recognised as ours must not reload the document and clear the history.
        await editor.WaitAsync(TimeSpan.FromSeconds(1.5));

        editor.UndoLabels.ShouldBe(labelsBefore);
        editor.ToastText.ShouldBeNull();
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Attack" });
        Library(editor).Sources[0].IsMissing.ShouldBeFalse();
        SequenceListSave.FromFile(target).Sequences.Count.ShouldBe(1);
        SequenceListSave.FromFile(target).Sources.ShouldBe(new[] { "../chars/hero.achx" });
        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.SequenceNames.Count.ShouldBe(2);
        SequenceListSave.FromFile(target).Sequences.Count.ShouldBe(2);
        File.ReadAllBytes(boss).ShouldBe(oldBytes);
    }

    [AvaloniaFact]
    public async Task SaveAs_AfterwardsTheWatcherFollowsTheNewFile_AnExternalEditReloadsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        string target = Path.Combine(editor.ProjectFolder, "levels", "boss2.seqj");
        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        await editor.WaitUntilAsync(() => editor.TabLabels.SequenceEqual(new[] { "boss2.seqj" }), TimeSpan.FromSeconds(5));

        SequenceListSave external = SequenceListSave.FromFile(target);
        external.Sequences.Add(new SequenceSave { Name = "Elsewhere" });
        external.Save(target);
        bool reloaded = await editor.WaitUntilAsync(() => editor.SequenceNames.Contains("Elsewhere"), TimeSpan.FromSeconds(10));

        reloaded.ShouldBeTrue("a change to the new file reloads the open document");
    }

    [AvaloniaFact]
    public async Task SaveAs_CancelledDialog_ChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.ErrorBannerText.ShouldBeNull();
        editor.Dialogs.ThrowIfUnanswered();
    }

    [AvaloniaFact]
    public async Task SaveAs_OverAFileAnotherTabHasOpen_IsRefusedWithAMessage()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        string other = editor.WriteSeqj("other.seqj", "Idle");
        await editor.OpenAsync(other);
        await editor.OpenAsync(boss);

        editor.Dialogs.AnswerNextSaveFile(other);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("other.seqj");
        editor.TabLabels.ShouldBe(new[] { "other.seqj", "boss.seqj" });
        SequenceListSave.FromFile(other).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task SaveAs_ToAName_ThatIsNotSeqj_IsRefused_NothingIsWritten()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        string target = Path.Combine(editor.ProjectFolder, "boss.txt");

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain(".seqj");
        File.Exists(target).ShouldBeFalse();
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
    }

    // ---- Replace Source ----

    [AvaloniaFact]
    public async Task ReplaceButtonOnAMissingSource_SwapsItForTheChosenFile_KeepsTheChain_AndUndoRedoWork()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        // hero.achx moved: the source is now where the file is not, and a copy sits at moved/.
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "moved"));
        editor.WritePng(Path.Combine("moved", "sheet.png"), 64, 64);
        string moved = editor.WriteAchx(Path.Combine("moved", "hero.achx"),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        File.Delete(Path.Combine(editor.ProjectFolder, "chars", "hero.achx"));
        await editor.OpenAsync(boss);
        Library(editor).Sources[0].IsMissing.ShouldBeTrue();

        editor.Dialogs.AnswerNextOpenFile(moved);
        editor.Click(ReplaceButton(editor, "chars/hero.achx"));
        bool replaced = await editor.WaitUntilAsync(() => !Library(editor).Sources[0].IsMissing, TimeSpan.FromSeconds(5));

        replaced.ShouldBeTrue($"dialogs [{string.Join("; ", editor.Dialogs.Shown)}] banner [{editor.ErrorBannerText}]");
        Library(editor).Sources.Select(source => (source.Source, source.IsMissing)).ShouldBe(new[] { ("moved/hero.achx", false) });
        Library(editor).ChipFor("moved/hero.achx", "Walk").ShouldNotBeNull();
        editor.UndoLabels.ShouldBe(new[] { "Replace Source 'hero.achx' with 'hero.achx'" });
        SequenceListSave saved = SequenceListSave.FromFile(boss);
        saved.Sources.ShouldBe(new[] { "moved/hero.achx" });
        ChainSources(saved).ShouldBe(new[] { "moved/hero.achx" });
        saved.Sequences[0].Tracks[0].Chains[0].Chain.ShouldBe("Walk");
        editor.Dialogs.OpenFileFiltersOffered.ShouldBe(new[] { "Animation Chain: *.achx *.achj" });

        editor.Press(Key.Z, RawInputModifiers.Control);
        Library(editor).Sources.Select(source => (source.Source, source.IsMissing)).ShouldBe(new[] { ("chars/hero.achx", true) });
        ChainSources(SequenceListSave.FromFile(boss)).ShouldBe(new[] { "chars/hero.achx" });
        editor.Press(Key.Y, RawInputModifiers.Control);
        Library(editor).Sources.Select(source => (source.Source, source.IsMissing)).ShouldBe(new[] { ("moved/hero.achx", false) });
    }

    [AvaloniaFact]
    public async Task ContextMenuReplaceSource_OnAnyHeader_KeepsTheSourceOrder_TheCollapsedState_AndEveryReference()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor, "tail.achx");
        editor.WriteAchx("tail.achx", AnimationEditorHarness.Chain("Wag", "chars/sheet.png", (0, 0, 8, 8)));
        string heroes = editor.WriteAchx("heroes.achx",
            AnimationEditorHarness.Chain("Walk", "chars/sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        Library(editor).ToggleSource("chars/hero.achx");
        Control header = Library(editor).HeaderFor("chars/hero.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));
        editor.Dialogs.AnswerNextOpenFile(heroes);
        editor.PickMenuItem(header.ContextMenu!, "Replace Source…");
        await editor.WaitUntilAsync(() => Library(editor).Sources[0].Source == "heroes.achx", TimeSpan.FromSeconds(5));

        Library(editor).Sources.Select(source => source.Source).ShouldBe(new[] { "heroes.achx", "tail.achx" });
        Library(editor).IsCollapsed("heroes.achx").ShouldBeTrue();
        ChainSources(SequenceListSave.FromFile(boss)).ShouldBe(new[] { "heroes.achx" });
        editor.UndoLabels.ShouldBe(new[] { "Replace Source 'hero.achx' with 'heroes.achx'" });
    }

    [AvaloniaFact]
    public async Task ReplaceSource_WithAFileThatIsAlreadyASource_SaysSo_AndChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor, "tail.achx");
        string tail = editor.WriteAchx("tail.achx", AnimationEditorHarness.Chain("Wag", "chars/sheet.png", (0, 0, 8, 8)));
        await editor.OpenAsync(boss);
        Control header = Library(editor).HeaderFor("chars/hero.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));
        editor.Dialogs.AnswerNextOpenFile(tail);
        editor.PickMenuItem(header.ContextMenu!, "Replace Source…");
        await editor.WaitUntilAsync(() => editor.ErrorBannerText is not null, TimeSpan.FromSeconds(5));

        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("already in the library");
        Library(editor).Sources.Select(source => source.Source).ShouldBe(new[] { "chars/hero.achx", "tail.achx" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ReplaceSource_CancelledDialog_ChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        Control header = Library(editor).HeaderFor("chars/hero.achx")!;

        editor.RightClickAt(OnHeaderTitle(editor, header));
        editor.Dialogs.AnswerNextOpenFile(null);
        editor.PickMenuItem(header.ContextMenu!, "Replace Source…");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.UndoLabels.ShouldBeEmpty();
        editor.ErrorBannerText.ShouldBeNull();
    }

    // ---- Relink a chain ----

    [AvaloniaFact]
    public async Task InspectorChainDropdown_ShowsAMissingChainAsMissing_AndPickingAnotherRelinksItInOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        SequenceListSave edit = SequenceListSave.FromFile(boss);
        edit.Sequences[0].Tracks[0].Chains[0].Chain = "Sprint";   // renamed away in the source
        edit.Save(boss);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnChain(editor, 0, 0.1));
        ComboBox chain = SequenceInspectorScenarioTests.ChainCombo(Inspector(editor));

        chain.Items.OfType<string>().ShouldBe(new[] { "Sprint (missing)", "Walk", "Run" });
        chain.SelectedItem.ShouldBe("Sprint (missing)");
        Inspector(editor).ChainMissingText.IsVisible.ShouldBeTrue();

        editor.PickComboItem(chain, "Run");

        SequenceInspectorScenarioTests.ChainCombo(Inspector(editor)).SelectedItem.ShouldBe("Run");
        Inspector(editor).ChainMissingText.IsVisible.ShouldBeFalse();
        editor.UndoLabels.ShouldBe(new[] { "Relink Chain 'Sprint' to 'Run'" });
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains[0].Chain.ShouldBe("Run");

        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains[0].Chain.ShouldBe("Sprint");
        editor.Press(Key.Y, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains[0].Chain.ShouldBe("Run");
    }

    [AvaloniaFact]
    public async Task InspectorChainDropdown_OnAFoundChain_ListsTheSourcesChains_WithNoMissingEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteBoss(editor);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnChain(editor, 0, 0.1));

        ComboBox chain = SequenceInspectorScenarioTests.ChainCombo(Inspector(editor));

        chain.Items.OfType<string>().ShouldBe(new[] { "Walk", "Run" });
        chain.SelectedItem.ShouldBe("Walk");
    }
}
