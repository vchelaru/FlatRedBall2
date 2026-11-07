using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The history is global: any click or key that changes the active tab or the selection records the place it left,
/// frame moves within a chain collapse into one entry, and undo, Back and Forward, drags and in-place edits record
/// nothing.
/// </summary>
public class NavigationGlobalHistoryScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenHeroAsync(params string[] chains)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", chains.Select((name, index) =>
            AnimationEditorHarness.Chain(name, "sheet.png", (0, index * 16, 16, 16), (16, index * 16, 16, 16), (32, index * 16, 16, 16))).ToArray());
        await editor.OpenAsync(path);
        return editor;
    }

    private static void Back(AnimationEditorHarness editor) => editor.Press(Key.Left, RawInputModifiers.Alt);

    private static void Forward(AnimationEditorHarness editor) => editor.Press(Key.Right, RawInputModifiers.Alt);

    private static async Task SettleAsync(AnimationEditorHarness editor) => await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

    private static bool BackEnabled(AnimationEditorHarness editor) => editor.Control<MenuItem>("MenuNavBack").IsEnabled;

    private static bool ForwardEnabled(AnimationEditorHarness editor) => editor.Control<MenuItem>("MenuNavForward").IsEnabled;

    private static string? SelectedChainName(AnimationEditorHarness editor) => editor.Services.SelectedState.SelectedChain?.Name;

    [AvaloniaFact]
    public async Task ChainRowClicks_RecordEachChain_AndBackWalksThemInReverse()
    {
        using var editor = await OpenHeroAsync("Walk", "Run", "Idle");
        BackEnabled(editor).ShouldBeFalse();

        editor.ClickRow(editor.ChainNamed("Run"));
        editor.ClickRow(editor.ChainNamed("Idle"));
        SelectedChainName(editor).ShouldBe("Idle");

        Back(editor);
        await SettleAsync(editor);
        SelectedChainName(editor).ShouldBe("Run");
        Back(editor);
        await SettleAsync(editor);
        SelectedChainName(editor).ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
        ForwardEnabled(editor).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task BackAndForward_WalkTheWholeHistory_WithoutAddingEntries()
    {
        using var editor = await OpenHeroAsync("Walk", "Run", "Idle");
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.ClickRow(editor.ChainNamed("Idle"));

        Back(editor);
        await SettleAsync(editor);
        Back(editor);
        await SettleAsync(editor);
        Forward(editor);
        await SettleAsync(editor);
        Forward(editor);
        await SettleAsync(editor);

        SelectedChainName(editor).ShouldBe("Idle");
        ForwardEnabled(editor).ShouldBeFalse();
        Back(editor);
        await SettleAsync(editor);
        Back(editor);
        await SettleAsync(editor);
        SelectedChainName(editor).ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task FrameClicksInsideOneChain_CollapseIntoOneEntry()
    {
        using var editor = await OpenHeroAsync("Walk", "Run");
        var run = editor.ChainNamed("Run");
        editor.ClickRow(run);
        editor.Expand(run);
        editor.ClickRow(run.Frames[0]);
        editor.ClickRow(run.Frames[1]);
        editor.ClickRow(run.Frames[2]);
        editor.Services.SelectedState.SelectedFrame.ShouldBe(run.Frames[2]);

        Back(editor);
        await SettleAsync(editor);

        SelectedChainName(editor).ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ArrowingThroughFrames_IsOneEntry_ThenTheNextChainIsAnother()
    {
        using var editor = await OpenHeroAsync("Walk", "Run");
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk);
        editor.Press(Key.Down);
        editor.Press(Key.Down);
        editor.Press(Key.Down);
        editor.Services.SelectedState.SelectedFrame.ShouldBe(walk.Frames[2]);

        editor.Press(Key.Down);
        SelectedChainName(editor).ShouldBe("Run");
        Back(editor);
        await SettleAsync(editor);

        // The last frame the user was on, not each frame passed on the way.
        editor.Services.SelectedState.SelectedFrame.ShouldBe(walk.Frames[2]);
        Back(editor);
        await SettleAsync(editor);
        SelectedChainName(editor).ShouldBe("Walk");
        editor.Services.SelectedState.SelectedFrame.ShouldBeNull();
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Undo_RestoringASelection_RecordsNothing()
    {
        using var editor = await OpenHeroAsync("Walk", "Run");
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        SelectedChainName(editor).ShouldNotBe("Run");

        editor.ClickMenu("MenuUndo");
        await SettleAsync(editor);

        SelectedChainName(editor).ShouldBe("Run");
        Back(editor);
        await SettleAsync(editor);
        SelectedChainName(editor).ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task RenamingTheSelectedChain_RecordsNothing()
    {
        using var editor = await OpenHeroAsync("Walk", "Run");
        var run = editor.ChainNamed("Run");
        editor.ClickRow(run);
        editor.RightClickRow(run);
        editor.PickTreeMenuItem("Rename…");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Sprint");
        editor.Press(Key.Enter);
        run.Name.ShouldBe("Sprint");

        Back(editor);
        await SettleAsync(editor);

        SelectedChainName(editor).ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DraggingAFrameInTheWireframe_RecordsNothingPerStep()
    {
        using var editor = await OpenHeroAsync("Walk");
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Services.SelectedState.SelectedFrame.ShouldBe(walk.Frames[0]);
        var center = editor.WireframeRectOf(walk.Frames[0]).Center;

        editor.Drag(center, center + new Vector(20, 10));

        editor.Services.SelectedState.SelectedFrame.ShouldBe(walk.Frames[0]);
        // Only the click that picked the frame was recorded, not the drag's steps.
        Back(editor);
        await SettleAsync(editor);
        editor.Services.SelectedState.SelectedFrame.ShouldBeNull();
        BackEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ProjectPanelClicks_RecordTheFileTheyLeave()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        var panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        BackEnabled(editor).ShouldBeFalse();
        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "enemy.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        BackEnabled(editor).ShouldBeTrue();

        Back(editor);
        await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.DisplayName == "hero.achx", TimeSpan.FromSeconds(5));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
    }

    [AvaloniaFact]
    public async Task OpeningAFile_RecordsTheTabItLeft()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string first = editor.WriteAchx("a.achx", AnimationEditorHarness.Chain("One", "sheet.png", (0, 0, 16, 16)));
        string second = editor.WriteAchx("b.achx", AnimationEditorHarness.Chain("Two", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(first);
        BackEnabled(editor).ShouldBeFalse();

        await editor.OpenAsync(second);

        BackEnabled(editor).ShouldBeTrue();
        Back(editor);
        await SettleAsync(editor);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("a.achx");
        SelectedChainName(editor).ShouldBe("One");
    }

    [AvaloniaFact]
    public async Task ClosingTheActiveTab_RecordsIt_AndBackReopensIt()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string first = editor.WriteAchx("a.achx", AnimationEditorHarness.Chain("One", "sheet.png", (0, 0, 16, 16)));
        string second = editor.WriteAchx("b.achx", AnimationEditorHarness.Chain("Two", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(first);
        await editor.OpenAsync(second);
        Back(editor);
        await SettleAsync(editor);
        Forward(editor);
        await SettleAsync(editor);
        // Back and Forward alone left one entry; closing the active tab adds the second.

        editor.CloseTab("b.achx");
        await SettleAsync(editor);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("a.achx");

        Back(editor);
        await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.DisplayName == "b.achx", TimeSpan.FromSeconds(5));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("b.achx");
        SelectedChainName(editor).ShouldBe("Two");
    }

    [AvaloniaFact]
    public async Task ClosingAnInactiveTab_RecordsNothing()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string first = editor.WriteAchx("a.achx", AnimationEditorHarness.Chain("One", "sheet.png", (0, 0, 16, 16)));
        string second = editor.WriteAchx("b.achx", AnimationEditorHarness.Chain("Two", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(first);
        await editor.OpenAsync(second);
        Back(editor);
        await SettleAsync(editor);
        Forward(editor);
        await SettleAsync(editor);
        editor.ClickTab("b.achx");
        await SettleAsync(editor);
        var backBefore = BackEnabled(editor);

        editor.CloseTab("a.achx");
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("b.achx");
        BackEnabled(editor).ShouldBe(backBefore);
    }

    [AvaloniaFact]
    public async Task SequenceListRows_AreEntries_AndBackReturnsToTheSequence()
    {
        using var editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "Idle", "Attack", "Die");
        await editor.OpenAsync(path);
        var list = editor.Control<ListBox>("SequenceList");
        list.SelectedIndex.ShouldBe(0);

        editor.Click((Control)list.ContainerFromIndex(1)!);
        editor.Click((Control)list.ContainerFromIndex(2)!);
        list.SelectedIndex.ShouldBe(2);

        Back(editor);
        await SettleAsync(editor);
        list.SelectedIndex.ShouldBe(1);
        Back(editor);
        await SettleAsync(editor);
        list.SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task SequenceTimelineClicks_RecordTrackAndChainSelections()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Run", Start = 1, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        Point OnChain(double time) => editor.PointIn(timeline, timeline.Viewport.XAt(time),
            timeline.Viewport.LaneTop(0) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2);

        editor.ClickAt(OnChain(0.05));
        editor.ClickAt(OnChain(1.05));
        timeline.Selection.ShouldBe(new TimelineChainId(0, 1));

        Back(editor);
        await SettleAsync(editor);
        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
        ForwardEnabled(editor).ShouldBeTrue();
    }
}
