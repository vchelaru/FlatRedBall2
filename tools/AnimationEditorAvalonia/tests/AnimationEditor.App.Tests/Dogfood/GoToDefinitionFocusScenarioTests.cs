using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Go to Definition from a .seqj tab lands on the chain the way a double-click on its tree row does:
/// selected, expanded, scrolled into view, and its frames fitted in the wireframe.
/// </summary>
public class GoToDefinitionFocusScenarioTests
{
    private const int FillerChains = 40;

    // Run sits in the far corner of a large sheet, below FillerChains rows, so only a real focus puts it in view.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 512, 512);
        var chains = new List<AnimationChainSave>();
        for (int i = 0; i < FillerChains; i++)
            chains.Add(AnimationEditorHarness.Chain($"Filler{i}", "sheet.png", (0, 0, 16, 16)));
        chains.Add(AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        chains.Add(AnimationEditorHarness.Chain("Run", "sheet.png", (400, 400, 32, 32), (432, 400, 32, 32)));
        editor.WriteAchx("hero.achx", chains.ToArray());

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
        editor.ClickAt(OnChain(editor, 1.05));
        editor.Click(editor.Control<SequenceInspectorControl>("SequenceInspector").GoToDefinitionButton);
        await editor.WaitUntilAsync(() => editor.Services.SelectedState.SelectedChain?.Name == "Run", TimeSpan.FromSeconds(5));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>What a double-click on the chain's tree row leaves behind, read after moving the camera away.</summary>
    private static (float PanX, float PanY, float Zoom) CameraAfterDoubleClick(AnimationEditorHarness editor, AnimationChainSave chain)
    {
        editor.Wireframe.SetCamera(7, 7, 1);
        editor.DoubleClickRow(chain);
        return editor.Wireframe.CameraState;
    }

    [AvaloniaFact]
    public async Task GoToDefinition_WhenTheAchxIsNotOpen_FocusesTheChainLikeADoubleClick()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        await GoToDefinitionOfRunAsync(editor);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        var run = editor.ChainNamed("Run");
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        editor.RowFor(run).IsExpanded.ShouldBeTrue();
        editor.RowHeaderPoint(run); // throws when the row is outside the tree's viewport
        editor.Wireframe.FrameRectCount.ShouldBe(2);
        var focused = editor.Wireframe.CameraState;
        focused.ShouldNotBe((0f, 0f, 1f));
        // The same state a double-click on that row produces.
        CameraAfterDoubleClick(editor, run).ShouldBe(focused);
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task GoToDefinition_WhenTheAchxIsAlreadyATab_FocusesTheChainLikeADoubleClick()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.ClickRow(editor.ChainNamed("Walk"));
        var before = editor.Wireframe.CameraState;
        await editor.OpenAsync(boss);

        await GoToDefinitionOfRunAsync(editor);

        editor.TabLabels.ShouldBe(new[] { "hero.achx", "boss.seqj" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        var run = editor.ChainNamed("Run");
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        editor.RowFor(run).IsExpanded.ShouldBeTrue();
        editor.RowHeaderPoint(run);
        var focused = editor.Wireframe.CameraState;
        focused.ShouldNotBe(before);
        CameraAfterDoubleClick(editor, run).ShouldBe(focused);
    }

    [AvaloniaFact]
    public async Task GoToDefinition_BackAndForward_KeepTheChainAndItsFramesShown()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        await GoToDefinitionOfRunAsync(editor);

        editor.Press(Key.Left, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");

        editor.Press(Key.Right, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
        editor.Wireframe.FrameRectCount.ShouldBe(2);
    }
}
