using AnimationEditor.App.Controls;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The dialogs that open straight through <c>EditorDialogs</c>, driven through the scripted
/// host: Adjust Frame Time in both modes and cancelled, Add Multiple Frames, Adjust Offsets in
/// its three modes, and the Files panel beside them.
/// </summary>
public class DialogScenarioTests
{
    [AvaloniaFact]
    public async Task AddMultipleFrames_Three_WithIncrementUv_AppendsThreeAdvancingFrames()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 32);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<NumericUpDown>().Single().Value = 3;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Multiple Frames…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.Count.ShouldBe(4);
        walk.Frames.Select(frame => editor.PixelRectOf(frame).X).ShouldBe(new[] { 0, 16, 32, 48 });
        editor.Nodes.Count(node => node.IsFrameNode).ShouldBe(4);
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(1, "one undo removes the whole batch");
    }

    [AvaloniaFact]
    public async Task AddMultipleFrames_Cancelled_AddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<NumericUpDown>().Single().Value = 5;
            return false;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Multiple Frames…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.Count.ShouldBe(1);
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task AdjustFrameTime_Cancelled_AfterLiveEdits_RestoresTheOriginalLengths_WithNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<FlankerNumericField>().Single().Value = 2m;
            walk.Frames[0].FrameLength.ShouldBe(1f, "the dialog previews its edit live");
            return false;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Frame Time…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.Select(frame => frame.FrameLength).ShouldBe(new[] { 0.1f, 0.1f });
        editor.UndoManager.CanUndo.ShouldBeFalse("a cancelled dialog leaves nothing to undo");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Select(frame => frame.FrameLength).ShouldBe(new[] { 0.1f, 0.1f });
    }

    [AvaloniaFact]
    public async Task AdjustFrameTime_KeepProportional_ScalesEveryFrame_AndOneUndoRestoresThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[1].FrameLength = 0.3f;
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Keep Proportional").IsChecked = true;
            content.GetVisualDescendants().OfType<FlankerNumericField>().Single().Value = 1m;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Frame Time…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames[0].FrameLength.ShouldBe(0.25f, 0.001f);
        walk.Frames[1].FrameLength.ShouldBe(0.75f, 0.001f);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Sum(frame => frame.FrameLength).ShouldBe(1f, 0.001f);
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Select(frame => frame.FrameLength).ShouldBe(new[] { 0.1f, 0.3f });
        editor.UndoManager.CanUndo.ShouldBeFalse("the dialog's live edits collapse into one undo step");
    }

    [AvaloniaFact]
    public async Task AdjustFrameTime_SetAllSame_DividesTheTotalEvenly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[1].FrameLength = 0.3f;
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Set All Frames Same").IsChecked = true;
            content.GetVisualDescendants().OfType<FlankerNumericField>().Single().Value = 0.5m;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Frame Time…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.Select(frame => frame.FrameLength).ShouldBe(new[] { 0.25f, 0.25f });
    }

    [AvaloniaFact]
    public async Task AdjustOffsets_AdjustAllAbsolute_ThenRelative_MoveEveryFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Adjust All (enter values)").IsChecked = true;
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Absolute").IsChecked = true;
            List<NumericUpDown> inputs = content.GetVisualDescendants().OfType<NumericUpDown>().ToList();
            inputs[0].Value = 5;
            inputs[1].Value = -3;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Offsets…");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        walk.Frames.Select(frame => (frame.RelativeX, frame.RelativeY)).ShouldBe(new[] { (5f, -3f), (5f, -3f) });

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Adjust All (enter values)").IsChecked = true;
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Relative").IsChecked = true;
            List<NumericUpDown> inputs = content.GetVisualDescendants().OfType<NumericUpDown>().ToList();
            inputs[0].Value = 1;
            inputs[1].Value = 1;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Offsets…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.Select(frame => (frame.RelativeX, frame.RelativeY)).ShouldBe(new[] { (6f, -2f), (6f, -2f) });
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Select(frame => (frame.RelativeX, frame.RelativeY)).ShouldBe(new[] { (5f, -3f), (5f, -3f) });
    }

    [AvaloniaFact]
    public async Task AdjustOffsets_JustifyBottom_PutsEveryFramesBottomEdgeAtZero()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Justify Bottom").IsChecked = true;
            return true;
        });
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Adjust Offsets…");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        // A frame drawn centred on its offset has its bottom edge at 0 when the offset is half its height.
        walk.Frames[0].RelativeY.ShouldBe(8f);
        walk.Frames[1].RelativeY.ShouldBe(16f);
        editor.UndoLabels.First().ShouldBe("Justify Bottom");
    }

    [AvaloniaFact]
    public async Task FilesTab_ListsTheProjectFolderPngs()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WritePng("extra.png", 8, 8);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);

        editor.Click(editor.Control<Control>("FilesTab"));

        FilesPanelControl panel = editor.Control<FilesPanelControl>("FilesPanel");
        List<string> names = Flatten(panel.TreeRoots).Where(node => node.IsFile).Select(node => node.Name).ToList();
        names.ShouldContain("sheet.png");
        names.ShouldContain("extra.png");
    }

    private static IEnumerable<PngFilesTreeNodeVm> Flatten(IEnumerable<PngFilesTreeNodeVm> nodes)
    {
        foreach (PngFilesTreeNodeVm node in nodes)
        {
            yield return node;
            foreach (PngFilesTreeNodeVm child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
