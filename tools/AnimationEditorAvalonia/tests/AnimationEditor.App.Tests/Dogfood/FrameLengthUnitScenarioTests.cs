using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #1829: a Millisecond file's frame lengths are shown, edited and created in milliseconds, and the
/// file's unit is never rewritten.
/// </summary>
public class FrameLengthUnitScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path, AnimationChainSave Walk)> OpenAsync(TimeMeasurementUnit unit)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", unit,
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        return (editor, path, walk);
    }

    [AvaloniaFact]
    public async Task MillisecondFile_InspectorShowsAndEditsLengthInMilliseconds()
    {
        var (editor, path, walk) = await OpenAsync(TimeMeasurementUnit.Millisecond);
        using (editor)
        {
            editor.ClickRow(walk.Frames[0]);

            editor.Control<FlankerNumericField>("PropFrameLen").Value.ShouldBe(100m);
            editor.Control<TextBlock>("PropFrameLenUnit").Text.ShouldBe("ms");

            editor.TypeFlanker("PropFrameLen", "250");

            walk.Frames[0].FrameLength.ShouldBe(250f);
            AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
            saved.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
            saved.AnimationChains.Single().Frames.Select(f => f.FrameLength).ShouldBe(new[] { 250f, 100f });
            editor.UndoLabels.Last().ShouldBe("Set Length: 250ms");
        }
    }

    [AvaloniaFact]
    public async Task SecondFile_InspectorStillShowsSeconds()
    {
        var (editor, _, walk) = await OpenAsync(TimeMeasurementUnit.Second);
        using (editor)
        {
            editor.ClickRow(walk.Frames[0]);

            editor.Control<FlankerNumericField>("PropFrameLen").Value.ShouldBe(0.1m);
            editor.Control<TextBlock>("PropFrameLenUnit").Text.ShouldBe("s");
        }
    }

    [AvaloniaFact]
    public async Task MillisecondFile_TreeAndTimelineLabelsUseMilliseconds()
    {
        var (editor, _, walk) = await OpenAsync(TimeMeasurementUnit.Millisecond);
        using (editor)
        {
            editor.ClickRow(walk);

            editor.Nodes.First(n => ReferenceEquals(n.Data, walk)).Meta.ShouldBe("2 fr · 200ms");
            editor.Nodes.First(n => ReferenceEquals(n.Data, walk.Frames[0])).Meta.ShouldBe("100ms");
            editor.Control<TextBlock>("TimelineHeaderLabel").Text.ShouldBe("TIMELINE · 200ms");
        }
    }

    [AvaloniaFact]
    public async Task MillisecondFile_AddFrame_SavesAHundredMilliseconds_AndKeepsTheUnit()
    {
        var (editor, path, walk) = await OpenAsync(TimeMeasurementUnit.Millisecond);
        using (editor)
        {
            editor.RightClickRow(walk);
            editor.PickTreeMenuItem("Add Frame");

            walk.Frames.Count.ShouldBe(3);
            AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
            saved.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
            saved.AnimationChains.Single().Frames.Select(f => f.FrameLength).ShouldBe(new[] { 100f, 100f, 100f });
            editor.Nodes.First(n => ReferenceEquals(n.Data, walk.Frames[2])).Meta.ShouldBe("100ms");
        }
    }

    [AvaloniaFact]
    public async Task MillisecondFile_AdjustFrameTime_TakesAndScalesInMilliseconds()
    {
        var (editor, _, walk) = await OpenAsync(TimeMeasurementUnit.Millisecond);
        using (editor)
        {
            string? prompt = null;
            decimal shownTotal = 0;
            editor.Dialogs.AnswerNextEditorDialog(content =>
            {
                FlankerNumericField total = content.GetVisualDescendants().OfType<FlankerNumericField>().Single();
                shownTotal = total.Value ?? 0m;
                prompt = string.Join("|", content.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                total.Value = 400m;
                return true;
            });
            editor.RightClickRow(walk);
            editor.PickTreeMenuItem("Adjust Frame Time…");
            editor.Wait(TimeSpan.FromMilliseconds(100));

            shownTotal.ShouldBe(200m);
            prompt.ShouldNotBeNull();
            prompt.ShouldContain("milliseconds");
            walk.Frames.Select(f => f.FrameLength).ShouldBe(new[] { 200f, 200f });
        }
    }
}
