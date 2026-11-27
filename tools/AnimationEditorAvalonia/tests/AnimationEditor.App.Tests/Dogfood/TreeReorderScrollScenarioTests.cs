using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A reorder in the ANIMATIONS tree keeps the moved row inside the tree's viewport, in both
/// directions, for chains, frames and shapes, from Alt+Up/Down and from the Move To menu items (#2198).
/// </summary>
public class TreeReorderScrollScenarioTests
{
    private const int RowCount = 40;

    [AvaloniaFact]
    public async Task AltDownAndUp_OnAChain_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenChainsAsync();
        AnimationChainSave moved = editor.ChainNamed("Chain00");
        editor.ClickRow(moved);

        StepAndAssertInView(editor, moved, Key.Down);
        editor.Project.AnimationChains.Last().ShouldBeSameAs(moved);
        StepAndAssertInView(editor, moved, Key.Up);
        editor.Project.AnimationChains.First().ShouldBeSameAs(moved);
    }

    [AvaloniaFact]
    public async Task AltDownAndUp_OnAFrame_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenFramesAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave moved = walk.Frames[0];
        editor.ClickRow(moved);

        StepAndAssertInView(editor, moved, Key.Down);
        walk.Frames.Last().ShouldBeSameAs(moved);
        StepAndAssertInView(editor, moved, Key.Up);
        walk.Frames.First().ShouldBeSameAs(moved);
    }

    [AvaloniaFact]
    public async Task AltDownAndUp_OnAShape_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenShapesAsync();
        AnimationFrameSave frame = editor.ChainNamed("Walk").Frames[0];
        object moved = frame.ShapesSave!.Shapes[0];
        editor.ClickRow(moved);

        StepAndAssertInView(editor, moved, Key.Down);
        frame.ShapesSave!.Shapes.Last().ShouldBeSameAs(moved);
        StepAndAssertInView(editor, moved, Key.Up);
        frame.ShapesSave!.Shapes.First().ShouldBeSameAs(moved);
    }

    [AvaloniaFact]
    public async Task MoveToBottomAndTop_OnAChain_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenChainsAsync();
        AnimationChainSave moved = editor.ChainNamed("Chain00");

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Bottom");
        editor.Project.AnimationChains.Last().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Bottom");

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Top");
        editor.Project.AnimationChains.First().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Top");
    }

    [AvaloniaFact]
    public async Task MoveToBottomAndTop_OnAFrame_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenFramesAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave moved = walk.Frames[0];

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Bottom");
        walk.Frames.Last().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Bottom");

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Top");
        walk.Frames.First().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Top");
    }

    [AvaloniaFact]
    public async Task MoveToBottomAndTop_OnAShape_KeepTheMovedRowInView()
    {
        using AnimationEditorHarness editor = await OpenShapesAsync();
        AnimationFrameSave frame = editor.ChainNamed("Walk").Frames[0];
        object moved = frame.ShapesSave!.Shapes[0];

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Bottom");
        frame.ShapesSave!.Shapes.Last().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Bottom");

        editor.RightClickRow(moved);
        editor.PickTreeMenuItem("Move To Top");
        frame.ShapesSave!.Shapes.First().ShouldBeSameAs(moved);
        AssertInView(editor, moved, "after Move To Top");
    }

    private static async Task<AnimationEditorHarness> OpenChainsAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave[] chains = Enumerable.Range(0, RowCount)
            .Select(i => AnimationEditorHarness.Chain($"Chain{i:00}", "sheet.png", (0, 0, 16, 16)))
            .ToArray();
        await editor.OpenAsync(editor.WriteAchx("many.achx", chains));
        return editor;
    }

    private static async Task<AnimationEditorHarness> OpenFramesAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var frames = Enumerable.Range(0, RowCount).Select(_ => (0, 0, 16, 16)).ToArray();
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", frames)));
        editor.Expand(editor.ChainNamed("Walk"));
        return editor;
    }

    private static async Task<AnimationEditorHarness> OpenShapesAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave chain = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        ShapesSave shapes = new ShapesSave();
        for (int i = 0; i < RowCount; i++)
            shapes.Add(new AARectSave { Name = $"Rect{i:00}", ScaleX = 4, ScaleY = 4 });
        chain.Frames[0].ShapesSave = shapes;
        await editor.OpenAsync(editor.WriteAchx("hero.achx", chain));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        return editor;
    }

    private static void StepAndAssertInView(AnimationEditorHarness editor, object moved, Key key)
    {
        for (int step = 1; step < RowCount; step++)
        {
            editor.Press(key, RawInputModifiers.Alt);
            AssertInView(editor, moved, $"after Alt+{key} #{step}");
        }
    }

    /// <summary>
    /// Fails unless the moved row's header label lies inside the tree's scroll viewport. Never calls
    /// <see cref="AnimationEditorHarness.RowFor"/>, which would scroll the row into view itself.
    /// </summary>
    private static void AssertInView(AnimationEditorHarness editor, object data, string when)
    {
        editor.Layout();
        ScrollViewer scroll = editor.AnimTree.GetVisualDescendants().OfType<ScrollViewer>().First();
        TreeViewItem? row = editor.AnimTree.GetVisualDescendants().OfType<TreeViewItem>()
            .FirstOrDefault(candidate => candidate.DataContext is AnimationEditor.Core.ViewModels.TreeNodeVm node && ReferenceEquals(node.Data, data));
        row.ShouldNotBeNull($"the moved row is not even realized {when}");
        TextBlock label = row.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Name == "RowHeaderLabel" && ReferenceEquals(block.DataContext, row.DataContext));
        // The presenter, not the ScrollViewer: its border insets the content.
        Visual viewport = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        Point top = label.TranslatePoint(new Point(0, 0), viewport)!.Value;
        double bottom = top.Y + label.Bounds.Height;
        // The last nested row's label overhangs the scroll extent by 1px, so even fully scrolled down
        // its bottom edge sits 1px past the viewport.
        const double ExtentOverhang = 1;
        (top.Y >= 0 && bottom <= scroll.Viewport.Height + ExtentOverhang).ShouldBeTrue(
            $"the moved row spans {top.Y:0.#}..{bottom:0.#} but the viewport is 0..{scroll.Viewport.Height:0.#} {when}");
    }
}
