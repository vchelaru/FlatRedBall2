using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> whose references form a cycle, written outside the editor (#1793): it opens, the closing reference is
/// drawn like a missing one and its tooltip names the cycle, and the problem banner says so on open and on reload.
/// </summary>
public class SequenceCycleScenarioTests
{
    // A: Body (Walk) and b, a reference to B. B: Legs (Walk) and, when cyclic, a, a reference back to A.
    private static string WriteFixture(AnimationEditorHarness editor, bool cyclic)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var a = new SequenceSave { Name = "A", Tracks = { ChainTrack("Body"), RefTrack("b", "B") } };
        var b = new SequenceSave { Name = "B", Tracks = { ChainTrack("Legs") } };
        if (cyclic) b.Tracks.Add(RefTrack("a", "A"));
        new SequenceListSave { Sources = { "hero.achx" }, Sequences = { a, b } }.Save(path);
        return path;
    }

    private static SequenceTrackSave ChainTrack(string name) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" } } };

    private static SequenceTrackSave RefTrack(string name, string target) =>
        new() { Name = name, References = [new SequenceReferenceSave { Sequence = target }] };

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    [AvaloniaFact]
    public async Task CyclicFile_Opens_TheClosingReferenceIsBroken_ItsTooltipNamesTheCycle_AndTheBannerSaysSo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, cyclic: true));
        var timeline = Timeline(editor);

        editor.TabLabels.ShouldContain("boss.seqj");
        editor.ProblemText.ShouldNotBeNull().ShouldContain("A > B > A: these sequences play each other");
        timeline.References.Single().IsBroken.ShouldBeTrue();

        editor.Hover(editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(1) + TimelineLayout.BodyTop + 4));
        (await editor.WaitUntilAsync(() => timeline.BlockTooltip is not null, TimelineTooltips.ShowDelay + TimeSpan.FromSeconds(2))).ShouldBeTrue();
        timeline.BlockTooltip.ShouldBe("⚠ A > B > A: these sequences play each other");
    }

    [AvaloniaFact]
    public async Task ExternalEditAddingACycle_WhileOpen_Reloads_AndTheBannerNamesTheCycle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, cyclic: false);
        await editor.OpenAsync(path);
        editor.ProblemText.ShouldBeNull();

        WriteFixture(editor, cyclic: true);
        editor.ClickMenu("MenuReloadFromDisk");

        editor.ProblemText.ShouldNotBeNull().ShouldContain("A > B > A");
        Timeline(editor).References.Single().IsBroken.ShouldBeTrue();
    }
}
