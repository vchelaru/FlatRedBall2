using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The spinner rule: a numeric field shows no up/down chevrons, anywhere. Arrow keys and the mouse wheel still step it.
/// The zoom and playback Speed fields (and the Grid size beside the zoom) keep their own [-][+] buttons. This walks every
/// numeric control in the window across the Inspector states, so a new field that breaks the rule fails here.
/// </summary>
public class NumericSpinnerRuleScenarioTests
{
    // Flanker ([-][value][+]) fields whose buttons stay: the transport and zoom toolbars, plus the three Inspector
    // scalars that have always had them. Anything else with visible buttons is a new field that needs a decision.
    private static readonly HashSet<string> FlankerWithButtons = new()
    {
        "SpeedInput", "SequenceSpeedInput", "GridSizeInput", "PropFrameLen", "ChainStartField", "ChainLoopsField",
    };

    // NumericUpDown fields allowed to show the chevron spinner. Empty on purpose.
    private static readonly HashSet<string> ChevronAllowed = new();

    private static async Task<string> OpenFixtureAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Orb", Radius = 4 });
        PolygonSave polygon = new PolygonSave { Name = "Poly" };
        polygon.Points.Add(new Vector2Save { X = 0, Y = 0 });
        polygon.Points.Add(new Vector2Save { X = 8, Y = 0 });
        polygon.Points.Add(new Vector2Save { X = 8, Y = 8 });
        walk.Frames[0].ShapesSave!.Add(polygon);
        string hero = editor.WriteAchx("hero.achx", walk);

        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(Path.Combine(editor.ProjectFolder, "boss.seqj"));
        await editor.OpenAsync(hero);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "boss.seqj"));
        return hero;
    }

    private static List<string> Violations(AnimationEditorHarness editor, string state)
    {
        editor.Layout();
        var problems = new List<string>();
        Window window = editor.Window;
        var numerics = window.GetVisualDescendants().OfType<NumericUpDown>().ToList();
        numerics.ShouldNotBeEmpty();
        foreach (NumericUpDown n in numerics)
        {
            bool allowed = ChevronAllowed.Contains(n.Name ?? "");
            var panel = n.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "PART_SpinnerPanel");
            if (!allowed && (n.ShowButtonSpinner || panel is { IsVisible: true }))
                problems.Add($"{state}: NumericUpDown '{n.Name}' shows the chevron spinner");
        }
        foreach (FlankerNumericField f in window.GetVisualDescendants().OfType<FlankerNumericField>())
        {
            bool shown = f.MinusBtn.IsVisible || f.PlusBtn.IsVisible;
            if (shown && !FlankerWithButtons.Contains(f.Name ?? ""))
                problems.Add($"{state}: FlankerNumericField '{f.Name}' shows -/+ buttons");
        }
        return problems;
    }

    [AvaloniaFact]
    public async Task EveryNumericField_InEveryInspectorState_ShowsNoChevronSpinner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = await OpenFixtureAsync(editor);
        var problems = new List<string>();

        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);

        problems.AddRange(Violations(editor, "nothing selected"));
        editor.ClickRow(walk);
        problems.AddRange(Violations(editor, "chain"));
        editor.ClickRow(walk.Frames[0]);
        problems.AddRange(Violations(editor, "frame 0"));
        editor.ClickRow(walk.Frames[1]);
        problems.AddRange(Violations(editor, "frame 1"));
        editor.ClickRow(walk.Frames[1], Avalonia.Input.RawInputModifiers.Control);
        problems.AddRange(Violations(editor, "frame 1 plus frame 0"));
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[0].ShapesSave!.AARectSaves.Single());
        problems.AddRange(Violations(editor, "rectangle"));
        editor.ClickRow(walk.Frames[0].ShapesSave!.CircleSaves.Single());
        problems.AddRange(Violations(editor, "circle"));
        editor.ClickRow(walk.Frames[0].ShapesSave!.PolygonSaves.Single());
        problems.AddRange(Violations(editor, "polygon"));

        editor.ClickTab("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        problems.AddRange(Violations(editor, "sequence"));
        editor.ClickAt(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(0) + 10));
        problems.AddRange(Violations(editor, "sequence track"));

        problems.Distinct().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ANewNumericUpDown_WithNoPerControlFlags_ShowsNoSpinner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        var fresh = new NumericUpDown { Name = "FreshField", Minimum = 0, Maximum = 10, Value = 3 };
        editor.Control<Grid>("TopPane").Children.Add(fresh);
        editor.Layout();

        fresh.ShowButtonSpinner.ShouldBeFalse();
        fresh.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_SpinnerPanel").IsVisible.ShouldBeFalse();
    }
}
