using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Back and forward (mouse buttons 4 and 5, Alt+Left/Right, View menu) over the places the user jumped between:
/// Go to Definition and tab-strip clicks. Plain selection clicks record nothing.
/// </summary>
public class NavigationHistoryScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor)
    {
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
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    // Walk spans 0 to 0.2 s of lane 0, Run starts at 1 s.
    private static Point OnChain(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static async Task GoToDefinitionOfRunAsync(AnimationEditorHarness editor)
    {
        editor.DoubleClickAt(OnChain(editor, 1.05));
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
    }

    private static void MouseButtonClick(AnimationEditorHarness editor, MouseButton button)
    {
        var point = new Point(300, 300);
        editor.Window.MouseDown(point, button);
        editor.Window.MouseUp(point, button);
        editor.Layout();
    }

    private static bool BackEnabled(AnimationEditorHarness editor) => editor.Control<MenuItem>("MenuNavBack").IsEnabled;

    private static bool ForwardEnabled(AnimationEditorHarness editor) => editor.Control<MenuItem>("MenuNavForward").IsEnabled;

    private static async Task SettleAsync(AnimationEditorHarness editor) => await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

    [AvaloniaFact]
    public async Task MouseBackAndForward_AfterGoToDefinition_ReturnToTheChainOnEachTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        editor.ClickAt(OnChain(editor, 1.05));
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 1));
        await GoToDefinitionOfRunAsync(editor);

        MouseButtonClick(editor, MouseButton.XButton1);
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 1));

        MouseButtonClick(editor, MouseButton.XButton2);
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
    }

    [AvaloniaFact]
    public async Task AltLeftAndAltRight_DoWhatTheMouseButtonsDo_AndTheMenuShowsThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        await GoToDefinitionOfRunAsync(editor);
        editor.Control<MenuItem>("MenuNavBack").InputGesture!.ToString().ShouldBe("Alt+Left");
        editor.Control<MenuItem>("MenuNavForward").InputGesture!.ToString().ShouldBe("Alt+Right");

        editor.Press(Key.Left, RawInputModifiers.Alt);
        await SettleAsync(editor);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");

        editor.Press(Key.Right, RawInputModifiers.Alt);
        await SettleAsync(editor);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
    }

    [AvaloniaFact]
    public async Task TabStripClicks_AreNavigable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string first = editor.WriteAchx("a.achx", AnimationEditorHarness.Chain("One", "sheet.png", (0, 0, 16, 16)));
        string second = editor.WriteAchx("b.achx", AnimationEditorHarness.Chain("Two", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(first);
        await editor.OpenAsync(second);
        BackEnabled(editor).ShouldBeFalse();

        editor.ClickTab("a.achx");
        await SettleAsync(editor);
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("One");
        BackEnabled(editor).ShouldBeTrue();

        MouseButtonClick(editor, MouseButton.XButton1);
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("b.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Two");
        ForwardEnabled(editor).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ANewNavigationAfterGoingBack_ClearsForward()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        await GoToDefinitionOfRunAsync(editor);
        MouseButtonClick(editor, MouseButton.XButton1);
        await SettleAsync(editor);
        ForwardEnabled(editor).ShouldBeTrue();

        editor.ClickTab("hero.achx");
        await SettleAsync(editor);

        ForwardEnabled(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task PlainSelectionClicks_RecordNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        editor.ClickAt(OnChain(editor, 0.05));
        editor.ClickAt(OnChain(editor, 1.05));
        editor.Press(Key.Left, RawInputModifiers.Alt);
        await SettleAsync(editor);

        BackEnabled(editor).ShouldBeFalse();
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 1));
    }

    [AvaloniaFact]
    public async Task Back_ToAClosedTab_ReopensItsFileWithTheChainSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        await GoToDefinitionOfRunAsync(editor);
        editor.ClickTab("boss.seqj");
        await SettleAsync(editor);
        editor.CloseTab("hero.achx");
        await SettleAsync(editor);
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });

        MouseButtonClick(editor, MouseButton.XButton1);
        await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.DisplayName == "hero.achx", TimeSpan.FromSeconds(5));
        await SettleAsync(editor);

        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
    }

    [AvaloniaFact]
    public async Task Back_ToAClosedTabWhoseFileIsGone_IsSkipped()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        await GoToDefinitionOfRunAsync(editor);
        editor.ClickTab("boss.seqj");
        await SettleAsync(editor);
        editor.CloseTab("hero.achx");
        await SettleAsync(editor);
        File.Delete(Path.Combine(editor.ProjectFolder, "hero.achx"));

        MouseButtonClick(editor, MouseButton.XButton1);
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
    }

    [AvaloniaFact]
    public async Task BackWithNothingRecorded_DoesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        MouseButtonClick(editor, MouseButton.XButton1);
        MouseButtonClick(editor, MouseButton.XButton2);
        await SettleAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");
    }

    [Theory]
    [InlineData(PointerUpdateKind.XButton1Pressed, -1)]
    [InlineData(PointerUpdateKind.XButton2Pressed, 1)]
    [InlineData(PointerUpdateKind.LeftButtonPressed, null)]
    [InlineData(PointerUpdateKind.XButton1Released, null)]
    public void MouseNavigationDirection_MapsOnlyTheSideButtonPresses(PointerUpdateKind kind, int? expected)
    {
        MainWindow.MouseNavigationDirection(kind).ShouldBe(expected);
    }
}
