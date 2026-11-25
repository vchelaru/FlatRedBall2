using AnimationEditor.Core.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>What a cut and paste in the same file leaves selected, and what its undo and redo do.</summary>
public class CutPasteSelectionScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, Dictionary<string, object> Rows)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit" });
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hurt" });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Circle" });
        string path = editor.WriteAchx("hero.achx", walk,
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (48, 0, 16, 16)));
        await editor.OpenAsync(path);
        var rows = new Dictionary<string, object>();
        foreach (var chain in editor.Project.AnimationChains)
        {
            editor.Expand(chain);
            rows[chain.Name] = chain;
            for (int i = 0; i < chain.Frames.Count; i++)
                rows[$"{chain.Name}/{i}"] = chain.Frames[i];
        }
        editor.Expand(rows["Walk/0"]);
        foreach (ShapeSave shape in ((AnimationFrameSave)rows["Walk/0"]).ShapesSave!.Shapes)
            rows[shape.Name] = shape;
        return (editor, rows);
    }

    private static void Select(AnimationEditorHarness editor, params object[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
            editor.ClickRow(rows[i], i == 0 ? RawInputModifiers.None : RawInputModifiers.Control);
    }

    private static void Press(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(150));
    }

    private static void ShouldBeSelected(AnimationEditorHarness editor, IEnumerable<object> rows, string when)
    {
        editor.Services.SelectedState.SelectedNodes.ShouldBe(rows, ignoreOrder: true, when);
        editor.AnimTree.SelectedItems!.OfType<TreeNodeVm>().Select(n => n.Data!).ShouldBe(rows, ignoreOrder: true, when + ", tree");
    }

    [AvaloniaTheory]
    [InlineData("Walk", "Run")]
    [InlineData("Walk", "Idle")]
    [InlineData("Walk,Run", "Idle")]
    [InlineData("Walk/0", "Run")]
    [InlineData("Walk/0", "Run/0")]
    [InlineData("Walk/0", "Walk")]
    [InlineData("Walk/0,Walk/1", "Run")]
    [InlineData("Walk/0,Run/0", "Idle/0")]
    [InlineData("Hit", "Run/0")]
    [InlineData("Hit", "Walk/0")]
    [InlineData("Hit,Hurt", "Run/0")]
    [InlineData("Hit,Hurt,Circle", "Run/0")]
    [InlineData("Hit,Circle", "Walk/1")]
    public async Task PasteSelectsWhatMoved_UndoSelectsWhatWasSelected_RedoSelectsWhatMoved(string cut, string target)
    {
        var (editor, rows) = await OpenAsync();
        using var _ = editor;
        var moved = cut.Split(',').Select(name => rows[name]).ToArray();
        Select(editor, moved);
        Press(editor, Key.X);
        Select(editor, rows[target]);

        Press(editor, Key.V);
        ShouldBeSelected(editor, moved, "after the paste");
        Press(editor, Key.Z);
        ShouldBeSelected(editor, [rows[target]], "after undo");
        Press(editor, Key.Y);
        ShouldBeSelected(editor, moved, "after redo");
    }

    // Undoing an earlier move between the cut and the paste puts the cut shapes into two frames.
    [AvaloniaFact]
    public async Task CutShapesNoLongerInOneFrame_AreEachMovedOutOfTheirOwn()
    {
        var (editor, rows) = await OpenAsync();
        using var _ = editor;
        foreach (string shape in new[] { "Hit", "Hurt" })
        {
            Select(editor, rows[shape]);
            Press(editor, Key.X);
            Select(editor, rows["Walk/1"]);
            Press(editor, Key.V);
        }
        editor.Expand(rows["Walk/1"]);
        Select(editor, rows["Hit"], rows["Hurt"]);
        Press(editor, Key.X);
        Press(editor, Key.Z);
        Select(editor, rows["Run/0"]);
        string Shapes() => string.Join(" ", new[] { "Walk/0", "Walk/1", "Run/0" }.Select(name =>
            string.Join(",", ((AnimationFrameSave)rows[name]).ShapesSave?.Shapes.Cast<ShapeSave>().Select(s => s.Name) ?? []) + "|"));
        Shapes().ShouldBe("Hurt,Circle| Hit| |");

        Press(editor, Key.V);
        Shapes().ShouldBe("Circle| | Hit,Hurt|");
        Press(editor, Key.Z);
        Shapes().ShouldBe("Hurt,Circle| Hit| |");
        Press(editor, Key.Y);
        Shapes().ShouldBe("Circle| | Hit,Hurt|");
    }

    // Putting the row selection back after a refresh must not open what the user closed.
    [AvaloniaTheory]
    [InlineData("Walk/0", "Walk", "lengths")]
    [InlineData("Walk/0,Walk/1", "Walk", "lengths")]
    [InlineData("Hit", "Walk", "lengths")]
    [InlineData("Hit", "Walk/0", "rename")]
    [InlineData("Hit,Circle", "Walk/0", "rename")]
    public async Task RefreshOfAFrame_LeavesACollapsedRowCollapsed(string selected, string collapsed, string edit)
    {
        var (editor, rows) = await OpenAsync();
        using var _ = editor;
        Select(editor, selected.Split(',').Select(name => rows[name]).ToArray());
        var row = editor.NodeFor(rows[collapsed]);
        row.IsExpanded = false;
        editor.Wait(TimeSpan.FromMilliseconds(50));

        if (edit == "lengths")
            editor.Services.AppCommands.SetAllFrameLengths((AnimationChainSave)rows["Walk"], 0.3f);
        else
            editor.Services.AppCommands.SetShapePropsBulk([(ShapeSave)rows["Hit"]], "Hot", null, null);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        row.IsExpanded.ShouldBeFalse("after the edit");
        foreach (var key in new[] { Key.Z, Key.Y })
        {
            Press(editor, key);
            row.IsExpanded.ShouldBeFalse("after " + key);
        }
    }
}
