using AnimationEditor.App.Controls;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Controls;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab behaves like an <c>.achx</c> tab where users meet it first: a failed save says so and does not lose
/// edits on close, files dropped on the window open, the Settings canvas colours reach the preview, and opened files
/// are remembered in Open Recent.
/// </summary>
public class SequenceParityScenarioTests
{
    private const string AutoSaveFailedPrefix = "Auto save failed — ";

    private static List<string> SavedSequenceNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static void AddSequence(AnimationEditorHarness editor) =>
        editor.Click(editor.Control<Button>("AddSequenceButton"));

    // ---- failed save ----

    [AvaloniaFact]
    public async Task FailedSave_ShowsTheAutoSaveFailedToast_AsAnAchxDoes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);

            editor.InfoText.ShouldNotBeNull();
            editor.InfoText!.ShouldStartWith(AutoSaveFailedPrefix);
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task CtrlS_OnASequenceTab_RetriesAFailedSave_AndSavesOnceWritable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Press(Key.S, RawInputModifiers.Control);

            editor.InfoText!.ShouldStartWith(AutoSaveFailedPrefix);
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }

        editor.Press(Key.S, RawInputModifiers.Control);

        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence", "Sequence 2" });
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Asks_AndCancelKeepsTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            editor.CloseTab("boss.seqj");

            editor.Dialogs.Shown.ShouldBe(new[] { "save-discard-cancel: Unsaved Changes / \"boss.seqj\" has unsaved changes. Save before closing?" });
            editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
            editor.SequenceNames.ShouldBe(new[] { "Sequence", "Sequence 2" });
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Discard_ClosesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            editor.CloseTab("boss.seqj");

            editor.TabLabels.ShouldBeEmpty();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence" });
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Save_StillUnwritable_KeepsTheTabAndSaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

            editor.CloseTab("boss.seqj");

            editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
            editor.InfoText!.ShouldStartWith(AutoSaveFailedPrefix);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Save_OnceWritable_SavesAndCloses()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        AddSequence(editor);
        UnusualProjectScenarioTests.MakeWritable(boss);
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

        editor.CloseTab("boss.seqj");

        editor.TabLabels.ShouldBeEmpty();
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence", "Sequence 2" });
    }

    [AvaloniaFact]
    public async Task ClosingATabThatSavedFine_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        AddSequence(editor);

        editor.CloseTab("boss.seqj");

        editor.TabLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Asks_AndCancelKeepsTheWindowOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => editor.Dialogs.Shown.Count > 0, TimeSpan.FromSeconds(5));
            editor.Layout();

            editor.Dialogs.Shown.ShouldBe(new[] { "save-discard-cancel: Unsaved Changes / \"boss.seqj\" has unsaved changes. Save before closing?" });
            editor.Window.IsVisible.ShouldBeTrue("Cancel keeps the window open");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Discard_ClosesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            AddSequence(editor);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromSeconds(5));

            editor.Window.IsVisible.ShouldBeFalse();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowAfterAFailedSaveHasBeenRetried_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        AddSequence(editor);
        UnusualProjectScenarioTests.MakeWritable(boss);
        editor.Press(Key.S, RawInputModifiers.Control);

        editor.Window.Close();
        editor.Layout();

        editor.Window.IsVisible.ShouldBeFalse();
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    // ---- drop on the window ----

    [AvaloniaFact]
    public async Task DroppingASeqjOnTheWindow_OpensItAsASequenceTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");

        await SequenceLibraryScenarioTests.Drop(editor, editor.Window, boss);
        await editor.WaitUntilAsync(() => editor.TabLabels.Count > 0, TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
    }

    [AvaloniaFact]
    public async Task DroppingASeqjOnTheLibrary_StillRefusesItAsASource_AndOpensNoTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string other = editor.WriteSeqj("other.seqj", "Idle");
        string boss = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(boss);

        await SequenceLibraryScenarioTests.Drop(editor, editor.Control<SequenceLibraryControl>("SequenceLibrary"), other);

        editor.ProblemText.ShouldBe("other.seqj can't be a source. Only .achx and .achj files can.");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
    }

    // ---- canvas colours ----

    private static ComboBox BackgroundCombo(Window settings) =>
        settings.GetVisualDescendants().OfType<ComboBox>().First(combo => combo.Items.Cast<object?>().Contains("Mid Gray"));

    [AvaloniaFact]
    public async Task SettingsCanvasBackground_ReachesTheSequencePreview_AndFollowsLaterChanges()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        PreviewControl sequencePreview = editor.Control<PreviewControl>("SequencePreview");
        uint themeDefault = sequencePreview.CanvasBackgroundArgb;

        editor.ClickMenu("MenuSettings");
        Window settings = editor.Window.OwnedWindows.Single();
        BackgroundCombo(settings).SelectedItem = "White";
        editor.Layout();

        sequencePreview.CanvasBackgroundArgb.ShouldBe(0xFFFFFFFFu);
        editor.Preview.CanvasBackgroundArgb.ShouldBe(0xFFFFFFFFu, "the achx preview gets the same colour");

        BackgroundCombo(settings).SelectedItem = "Theme Default";
        editor.Layout();
        sequencePreview.CanvasBackgroundArgb.ShouldBe(themeDefault);
        settings.Close();
        editor.Layout();
    }

    // The guide row only offers Theme Default and a colour picker, which a script cannot answer, so this calls the setter
    // the Settings window's callback is wired to; the wiring itself is the same as for the background above.
    [AvaloniaFact]
    public async Task SettingsGuideLineColour_ReachesTheSequencePreview()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));
        PreviewControl sequencePreview = editor.Control<PreviewControl>("SequencePreview");

        typeof(MainWindow).GetMethod("SetGuideLineColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(editor.Window, new object?[] { 0xFF336699u });

        sequencePreview.GuideLineOverride.ShouldBe(0xFF336699u);
        editor.Preview.GuideLineOverride.ShouldBe(0xFF336699u);
    }

    // ---- Open Recent ----

    private static List<string> RecentTips(AnimationEditorHarness editor) =>
        editor.Control<MenuItem>("MenuLoadRecent").Items.OfType<MenuItem>()
            .Select(item => (string?)ToolTip.GetTip(item) ?? "").ToList();

    [AvaloniaFact]
    public async Task OpeningASeqj_AddsItToOpenRecent_AndReopeningFromThereOpensASequenceTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");
        await editor.OpenAsync(boss);

        RecentTips(editor).ShouldBe(new[] { new FilePath(boss).FullPath });
        editor.CloseTab("boss.seqj");
        editor.TabLabels.ShouldBeEmpty();

        editor.ClickMenu(editor.Control<MenuItem>("MenuLoadRecent").Items.OfType<MenuItem>().Single());
        await editor.WaitUntilAsync(() => editor.TabLabels.Count > 0, TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
    }

    [AvaloniaFact]
    public async Task OpenRecent_ListsSeqjAndAchxTogether_MostRecentFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(boss);

        RecentTips(editor).ShouldBe(new[] { new FilePath(boss).FullPath, new FilePath(hero).FullPath });
    }

    // ---- duplicate names ----

    [AvaloniaFact]
    public async Task OpeningAFileWithDuplicateSequenceNames_ShowsABanner_AndEverySequenceIsReachable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        SequenceListSave save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        SequenceSave second = new SequenceSave { Name = "Idle" };
        second.Tracks.Add(new SequenceTrackSave { Name = "OnlyInSecond" });
        save.Sequences.Add(second);
        save.Save(path);
        string before = File.ReadAllText(path);

        await editor.OpenAsync(path);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Idle 2" });
        editor.InfoText.ShouldNotBeNull();
        editor.InfoText!.ShouldContain("'Idle' as 'Idle 2'");
        File.ReadAllText(path).ShouldBe(before, "opening never rewrites the file");
        ListBox list = editor.Control<ListBox>("SequenceList");
        editor.ClickAt(editor.CenterOf((Control)list.ContainerFromIndex(1)!));
        editor.Control<AnimationEditor.App.Controls.SequenceTimelineControl>("SequenceTimeline").SelectTracks(new[] { 0 });
        editor.Layout();
        editor.Control<AnimationEditor.App.Controls.SequenceInspectorControl>("SequenceInspector").TrackNameBox.Text.ShouldBe("OnlyInSecond");
    }

    // ---- Detach to New Window ----

    [AvaloniaFact]
    public async Task DetachToNewWindow_OnASequenceTab_MovesTheFileToAFreshWindow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");
        MainWindow? detached = null;
        string temp = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        editor.Window.DetachedWindowFactory = () =>
            detached = App.CreateDetachedWindow(temp, Path.Combine(temp, "recovery.achx"));
        await editor.OpenAsync(boss);
        MenuItem detach = editor.TabFor("boss.seqj").ContextMenu!.Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "Detach to New Window");
        try
        {
            detach.Command!.Execute(null);
            await editor.WaitUntilAsync(() => detached?.FindControl<ListBox>("SequenceList")!.ItemCount == 2, TimeSpan.FromSeconds(10));

            editor.TabLabels.ShouldBeEmpty();
            detached.ShouldNotBeNull();
            detached!.FindControl<ListBox>("SequenceList")!.Items.OfType<AnimationEditor.App.Models.SequenceListItem>()
                .Select(item => item.Name).ShouldBe(new[] { "Idle", "Attack" });
        }
        finally
        {
            detached?.CloseWithoutPrompt();
        }
    }

    // ---- guides ----

    [AvaloniaFact]
    public async Task Guides_BelongToTheirOwnSequenceTab_AndCanBeHidden()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string a = editor.WriteSeqj("a.seqj", "Idle");
        string b = editor.WriteSeqj("b.seqj", "Idle");
        await editor.OpenAsync(a);
        PreviewControl preview = editor.Control<PreviewControl>("SequencePreview");
        ToggleButton toggle = editor.Control<ToggleButton>("SequenceShowUserGuides");
        toggle.IsVisible.ShouldBeFalse("the toggle shows once a guide exists, as on the achx preview");

        preview.AddHGuide(10);
        editor.Layout();
        toggle.IsVisible.ShouldBeTrue();
        toggle.IsChecked.ShouldBe(true);
        editor.Click(toggle);
        preview.ShowUserGuides.ShouldBeFalse();

        await editor.OpenAsync(b);
        preview.HGuideCount.ShouldBe(0, "a guide placed on a.seqj does not appear on b.seqj");
        toggle.IsVisible.ShouldBeFalse();
        editor.ClickTab("a.seqj");

        preview.HGuideCount.ShouldBe(1, "and it is back when a.seqj is");
        toggle.IsVisible.ShouldBeTrue();
        preview.ShowUserGuides.ShouldBeFalse("the hidden state is a.seqj's too");
    }
}
