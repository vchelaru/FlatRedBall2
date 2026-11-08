using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Inspector tab on a <c>.seqj</c> tab: it follows the timeline's selection (sequence, track, chain), every commit
/// is one undo step, refused values stay out of the file, and Go to Definition opens the chain's source.
/// </summary>
public class SequenceInspectorScenarioTests
{
    // Walk: five 0.1 s frames, a 0.5 s cycle. Run: two frames, 0.2 s.
    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave Track(string name, params (string Chain, double Start, float Loops)[] chains) =>
        Track(name, "hero.achx", chains);

    private static SequenceTrackSave Track(string name, string source, params (string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static SequenceTrackSave SavedTrack(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track];

    private static Point OnHeader(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(lane) + 10);
    }

    private static Point OnChain(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static TextBox BoxOf(FlankerNumericField field) => field.GetVisualDescendants().OfType<TextBox>().First();

    private static void TypeInto(AnimationEditorHarness editor, FlankerNumericField field, string text) =>
        editor.TypeAndEnter(BoxOf(field), text);

    [AvaloniaFact]
    public async Task Selecting_ShowsTheSequenceTrackOrChainFields_OneAtATime()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.5, 2)));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);

        // Nothing selected: the sequence.
        inspector.SequencePanel.IsVisible.ShouldBeTrue();
        (inspector.TrackPanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((false, false));
        inspector.SequenceNameBox.Text.ShouldBe("Attack");
        inspector.SequenceLengthText.Text.ShouldBe("1.5 s");

        editor.ClickAt(OnHeader(editor, 0));
        (inspector.SequencePanel.IsVisible, inspector.TrackPanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((false, true, false));
        Timeline(editor).SelectedTrack.ShouldBe(0);
        Timeline(editor).Selection.ShouldBeNull();
        inspector.TrackNameBox.Text.ShouldBe("Body");

        editor.ClickAt(OnChain(editor, 0, 1.0));
        (inspector.SequencePanel.IsVisible, inspector.TrackPanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((false, false, true));
        Timeline(editor).SelectedTrack.ShouldBeNull();
        inspector.ChainSourceText.Text.ShouldBe("hero.achx");
        inspector.ChainNameText.Text.ShouldBe("Walk");
        inspector.ChainStartField.Value.ShouldBe(0.5m);
        inspector.ChainLoopsField.Value.ShouldBe(2m);
        inspector.ChainLengthText.Text.ShouldBe("1 s (0.5 s × 2)");

        // Empty timeline space goes back to the sequence.
        editor.ClickAt(editor.PointIn(Timeline(editor), 400, Timeline(editor).Viewport.LaneTop(3)));
        inspector.SequencePanel.IsVisible.ShouldBeTrue();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Selecting_OnTheImagesTab_BringsTheInspectorForward_ButNotOffHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var tabs = editor.Control<TabControl>("SidebarTabs");

        tabs.SelectedItem = editor.Control<TabItem>("FilesTab");
        editor.ClickAt(OnHeader(editor, 0));
        tabs.SelectedItem.ShouldBe(editor.Control<TabItem>("InspectorTab"));

        tabs.SelectedItem = editor.Control<TabItem>("HistoryTab");
        editor.ClickAt(OnChain(editor, 0, 0.1));
        tabs.SelectedItem.ShouldBe(editor.Control<TabItem>("HistoryTab"));
    }

    // Two sequences, each with a track, so clicking the list changes what is selected.
    private static string WriteTwoSequences(AnimationEditorHarness editor)
    {
        string path = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        var save = SequenceListSave.FromFile(path);
        var second = new SequenceSave { Name = "Idle" };
        second.Tracks.Add(Track("Legs", ("Run", 0, 2)));
        save.Sequences.Add(second);
        save.Save(path);
        return path;
    }

    private static ListBox SequenceListBox(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    [AvaloniaFact]
    public async Task ClickingTheSequence_AfterATrackIsSelected_ShowsTheSequenceSection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteTwoSequences(editor);
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        editor.ClickAt(OnHeader(editor, 0));
        inspector.TrackPanel.IsVisible.ShouldBeTrue();

        // The row is already the selected one, so the list sees no selection change.
        editor.Click((Control)SequenceListBox(editor).ContainerFromIndex(0)!);

        (inspector.SequencePanel.IsVisible, inspector.TrackPanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((true, false, false));
        Timeline(editor).SelectedTrack.ShouldBeNull();
        inspector.SequenceNameBox.Text.ShouldBe("Attack");
        editor.Control<TabControl>("SidebarTabs").SelectedItem.ShouldBe(editor.Control<TabItem>("InspectorTab"));
    }

    [AvaloniaFact]
    public async Task ClickingAnotherSequence_ShowsItsFields_AndBringsTheInspectorForward_ButNotOffHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteTwoSequences(editor);
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        var tabs = editor.Control<TabControl>("SidebarTabs");
        tabs.SelectedItem = editor.Control<TabItem>("FilesTab");

        editor.Click((Control)SequenceListBox(editor).ContainerFromIndex(1)!);

        inspector.SequencePanel.IsVisible.ShouldBeTrue();
        inspector.SequenceNameBox.Text.ShouldBe("Idle");
        tabs.SelectedItem.ShouldBe(editor.Control<TabItem>("InspectorTab"));

        tabs.SelectedItem = editor.Control<TabItem>("HistoryTab");
        editor.Click((Control)SequenceListBox(editor).ContainerFromIndex(0)!);
        inspector.SequenceNameBox.Text.ShouldBe("Attack");
        tabs.SelectedItem.ShouldBe(editor.Control<TabItem>("HistoryTab"));
    }

    [AvaloniaFact]
    public async Task RenamingTheClickedSequence_InTheInspector_IsOneUndoStep_AndTheListFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteTwoSequences(editor);
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        editor.ClickAt(OnChain(editor, 0, 0.1));
        editor.Click((Control)SequenceListBox(editor).ContainerFromIndex(1)!);

        editor.TypeAndEnter(inspector.SequenceNameBox, "Slam");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Slam" });
        SequenceListSave.FromFile(boss).Sequences.Select(sequence => sequence.Name).ShouldBe(new[] { "Attack", "Slam" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Slam'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        inspector.SequenceNameBox.Text.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task SelectingAChain_ThenASequence_SwitchesSections()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteTwoSequences(editor);
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);

        editor.ClickAt(OnChain(editor, 0, 0.1));
        inspector.ChainPanel.IsVisible.ShouldBeTrue();
        editor.Click((Control)SequenceListBox(editor).ContainerFromIndex(1)!);
        (inspector.SequencePanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((true, false));
        editor.ClickAt(OnChain(editor, 0, 0.1));
        (inspector.SequencePanel.IsVisible, inspector.ChainPanel.IsVisible).ShouldBe((false, true));
        inspector.ChainNameText.Text.ShouldBe("Run");
    }

    [AvaloniaFact]
    public async Task EditingATrack_EachFieldIsOneUndoStep_AndCtrlZRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        editor.ClickAt(OnHeader(editor, 0));

        editor.TypeAndEnter(inspector.TrackNameBox, "Torso");
        TypeInto(editor, inspector.TrackXField, "12.5");
        TypeInto(editor, inspector.TrackYField, "-3");
        editor.Click(inspector.TrackFlipBox);

        var saved = SavedTrack(boss, 0);
        (saved.Name, saved.X, saved.Y, saved.FlipHorizontal).ShouldBe(("Torso", 12.5f, -3f, true));
        editor.UndoLabels.ShouldBe(new[]
        {
            "Rename Track 'Body' to 'Torso'", "Set Offset of 'Torso' to (12.5, 0)", "Set Offset of 'Torso' to (12.5, -3)",
            "Turn Flip On for 'Torso'",
        });

        for (int i = 0; i < 4; i++)
            editor.Press(Key.Z, RawInputModifiers.Control);
        saved = SavedTrack(boss, 0);
        (saved.Name, saved.X, saved.Y, saved.FlipHorizontal).ShouldBe(("Body", 0f, 0f, false));
        // The undone values are back in the fields, not just in the file.
        inspector.TrackNameBox.Text.ShouldBe("Body");
        inspector.TrackXField.Value.ShouldBe(0m);
        inspector.TrackFlipBox.IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task EditingAChain_StartAndLoops_AreUndoable_AndTheTimelineFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        editor.ClickAt(OnChain(editor, 0, 0.1));

        TypeInto(editor, inspector.ChainStartField, "1.5");
        TypeInto(editor, inspector.ChainLoopsField, "2.5");

        var chain = SavedTrack(boss, 0).Chains.Single();
        (chain.Start, chain.Loops).ShouldBe((1.5, 2.5f));
        Timeline(editor).Blocks.Single().Start.ShouldBe(1.5);
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 0));
        editor.UndoLabels.ShouldBe(new[] { "Set Start of 'Walk' to 1.5", "Set Cycles of 'Walk' to 2.5" });

        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.Single().Loops.ShouldBe(1f);
        inspector.ChainLoopsField.Value.ShouldBe(1m);
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task EditingTheSequence_NameAndLoop_AreUndoable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body"));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);

        editor.TypeAndEnter(inspector.SequenceNameBox, "Slam");
        editor.Click(inspector.SequenceLoopBox);

        var saved = SequenceListSave.FromFile(boss).Sequences.Single();
        (saved.Name, saved.Loop).ShouldBe(("Slam", true));
        editor.SequenceNames.ShouldBe(new[] { "Slam" });
        editor.Control<ToggleButton>("SequenceLoopToggle").IsChecked.ShouldBe(true);
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Attack' to 'Slam'", "Turn Loop On for 'Slam'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences.Single().Name.ShouldBe("Attack");
        inspector.SequenceNameBox.Text.ShouldBe("Attack");
    }

    [AvaloniaFact]
    public async Task InvalidValues_AreRefusedWithABanner_AndTheFieldGoesBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.5, 1)), Track("Arms"));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);

        editor.ClickAt(OnChain(editor, 0, 0.6));
        TypeInto(editor, inspector.ChainLoopsField, "0");
        editor.ErrorBannerText.ShouldBe("Cycles must be above 0.");
        inspector.ChainLoopsField.Value.ShouldBe(1m);
        BoxOf(inspector.ChainLoopsField).Text.ShouldBe("1");
        TypeInto(editor, inspector.ChainStartField, "-2");
        editor.ErrorBannerText.ShouldBe("Start must be 0 or more.");
        inspector.ChainStartField.Value.ShouldBe(0.5m);

        editor.ClickAt(OnHeader(editor, 1));
        editor.TypeAndEnter(inspector.TrackNameBox, "Body");
        editor.ErrorBannerText.ShouldBe("A track named \"Body\" already exists.");
        inspector.TrackNameBox.Text.ShouldBe("Arms");
        editor.TypeAndEnter(inspector.TrackNameBox, "   ");
        editor.ErrorBannerText.ShouldBe("A track needs a name.");
        inspector.TrackNameBox.Text.ShouldBe("Arms");

        editor.ClickAt(editor.PointIn(Timeline(editor), 400, Timeline(editor).Viewport.LaneTop(4)));
        inspector.SequenceNameBox.Focus();
        editor.Layout();
        inspector.SequenceNameBox.SelectAll();
        editor.Press(Key.Back);
        editor.Press(Key.Enter);
        editor.ErrorBannerText.ShouldBe("A sequence needs a name.");
        inspector.SequenceNameBox.Text.ShouldBe("Attack");

        editor.UndoLabels.ShouldBeEmpty();
        var saved = SequenceListSave.FromFile(boss).Sequences.Single();
        saved.Name.ShouldBe("Attack");
        saved.Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Arms" });
        saved.Tracks[0].Chains.Single().Loops.ShouldBe(1f);
    }

    [AvaloniaFact]
    public async Task DraggingAChain_UpdatesTheInspectorLive()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Arms"));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        var timeline = Timeline(editor);
        Point from = OnChain(editor, 0, 0.25);
        Point to = OnChain(editor, 0, 1.25);

        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        editor.Layout();

        // Mouse still down: nothing is committed, yet the field already shows the dragged start.
        editor.UndoLabels.ShouldBeEmpty();
        inspector.ChainStartField.Value.ShouldBe(1m);

        editor.Window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();
        inspector.ChainStartField.Value.ShouldBe(1m);
        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
    }

    [AvaloniaFact]
    public async Task AnEditElsewhere_DoesNotRewriteTheFieldBeingTypedIn()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);
        editor.ClickAt(OnChain(editor, 0, 0.1));

        var box = BoxOf(inspector.ChainStartField);
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Window.KeyTextInput("7");
        editor.Layout();
        // The window refreshes the inspector on every document change; here, another change lands mid-typing.
        inspector.Show(SequenceDocument.Load(boss), 0, new UndoManager(), new TimelineChainId(0, 0), null);

        box.Text.ShouldBe("7");
        inspector.ChainStartField.Value.ShouldBe(0m);
    }

    [AvaloniaFact]
    public async Task GoToDefinition_FromTheInspectorButton_OpensTheSourceAndSelectsTheChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Run", 0, 1)));
        string hero = Path.Combine(editor.ProjectFolder, "hero.achx");
        await editor.OpenAsync(boss);
        editor.ClickAt(OnChain(editor, 0, 0.05));

        editor.Click(Inspector(editor).GoToDefinitionButton);
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
        editor.Control<Grid>("AchxEditorPane").IsVisible.ShouldBeTrue();
        editor.ErrorBannerText.ShouldBeNull();
        File.Exists(hero).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task GoToDefinition_DoubleClickAndMenu_ReachTheSourceToo_AndTheSequenceTabKeepsItsHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1), ("Run", 1, 1)));
        await editor.OpenAsync(boss);
        editor.ClickAt(OnHeader(editor, 0));
        editor.TypeAndEnter(Inspector(editor).TrackNameBox, "Torso");

        editor.DoubleClickAt(OnChain(editor, 0, 1.05));
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");

        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Body' to 'Torso'" });
        editor.RightClickAt(OnChain(editor, 0, 0.1));
        editor.PickMenuItem(Timeline(editor).Menu, "Go to Definition");
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Walk", TimeSpan.FromSeconds(5));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Walk");
        // Back on the sequence tab its own undo entry is still there.
        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Body' to 'Torso'" });
    }

    [AvaloniaFact]
    public async Task GoToDefinition_MissingSourceOrChain_ShowsTheBanner_AndOpensNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Gone", 0, 1)), Track("Ghost", "nowhere.achx", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var inspector = Inspector(editor);

        editor.ClickAt(OnChain(editor, 0, 0.1));
        inspector.ChainMissingText.IsVisible.ShouldBeTrue();
        editor.Click(inspector.GoToDefinitionButton);
        editor.ErrorBannerText.ShouldBe("hero.achx has no chain named \"Gone\".");

        editor.ClickAt(OnChain(editor, 1, 0.1));
        editor.Click(inspector.GoToDefinitionButton);
        editor.ErrorBannerText.ShouldBe("nowhere.achx is missing or could not be read.");

        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
    }

    [AvaloniaFact]
    public async Task SwitchingBetweenAchxAndSeqjTabs_ShowsEachKindsInspector()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        string hero = Path.Combine(editor.ProjectFolder, "hero.achx");
        var inspectorTab = editor.Control<TabItem>("InspectorTab");
        await editor.OpenAsync(hero);
        editor.Control<ScrollViewer>("AchxInspector").IsVisible.ShouldBeTrue();
        Inspector(editor).IsVisible.ShouldBeFalse();

        await editor.OpenAsync(boss);
        inspectorTab.IsVisible.ShouldBeTrue();
        editor.Control<ScrollViewer>("AchxInspector").IsVisible.ShouldBeFalse();
        Inspector(editor).IsVisible.ShouldBeTrue();

        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.Control<ScrollViewer>("AchxInspector").IsVisible.ShouldBeTrue();
        Inspector(editor).IsVisible.ShouldBeFalse();
        inspectorTab.IsVisible.ShouldBeTrue();

        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        Inspector(editor).IsVisible.ShouldBeTrue();
        editor.Control<ScrollViewer>("AchxInspector").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DeleteKey_WithATrackSelected_DeletesTheTrack_AndCtrlZBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Arms"));
        await editor.OpenAsync(boss);
        editor.ClickAt(OnHeader(editor, 1));

        editor.Press(Key.Delete);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body" });
        Timeline(editor).SelectedTrack.ShouldBeNull();
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Arms" });
    }
}
