using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The History panel and the undo stack together: rows appear as edits land, the panel's
/// buttons undo and redo, and a new edit after an undo drops the redo rows.
/// </summary>
public class HistoryScenarioTests
{
    [AvaloniaFact]
    public async Task EditingAfterAnUndo_DropsTheRedoRows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Frame");
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Frame");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.UndoManager.CanRedo.ShouldBeTrue();

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Flip Horizontally");

        editor.UndoManager.CanRedo.ShouldBeFalse();
        editor.HistoryRows.Count.ShouldBe(editor.UndoManager.UndoHistory.Count);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task HistoryButtons_UndoAndRedo_TheLastEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Click(editor.Control<Control>("HistoryTab"));
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Frame");
        walk.Frames.Count.ShouldBe(2);

        editor.Click(editor.Control<Button>("HistoryUndoButton"));
        walk.Frames.Count.ShouldBe(1);
        editor.Control<Button>("HistoryRedoButton").IsEnabled.ShouldBeTrue();

        editor.Click(editor.Control<Button>("HistoryRedoButton"));
        walk.Frames.Count.ShouldBe(2);
        editor.Control<Button>("HistoryRedoButton").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task HistoryRows_FollowEveryEdit_WithReadableLabels()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.HistoryRows.ShouldBeEmpty();

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Frame");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);
        editor.TypeFlanker("PropFrameLen", "0.5");
        editor.ClickRow(walk);
        editor.Click(editor.Control<CheckBox>("PropChainLoop"));
        // The History panel is a sidebar tab; it replaces the inspector while it shows.
        editor.Click(editor.Control<Control>("HistoryTab"));

        editor.HistoryRows.Count.ShouldBe(3);
        editor.HistoryRows.ShouldAllBe(label => !string.IsNullOrWhiteSpace(label));
        editor.HistoryRows.Distinct().Count().ShouldBe(3, "each edit has its own label");
    }
}
