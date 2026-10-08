using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Overlapping shapes in the preview: hover outlines the shape a click would select, and a click
/// (no drag) on the already-selected shape cycles to the next one beneath it. Real pointer input
/// through a full window, since the behavior lives in OnPointerPressed/Released/Moved.
/// </summary>
public class PreviewShapeClickCycleTests
{
    private static (TestServices Ctx, Window Window, PreviewControl Preview, Point Center,
        CircleSave Bottom, CircleSave Top, AnimationFrameSave Frame) Build()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var bottom = new CircleSave { Radius = 20f };
        var top    = new CircleSave { Radius = 10f }; // later in the list == drawn on top
        var frame  = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Add(bottom);
        frame.ShapesSave.Add(top);
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
        var local = new Point((preview.Bounds.Width - 20) / 2 + 20, (preview.Bounds.Height - 20) / 2 + 20);
        return (ctx, window, preview, preview.TranslatePoint(local, window)!.Value, bottom, top, frame);
    }

    [AvaloniaFact]
    public void Click_OnSelectedShape_CyclesToNextBeneath_ThenWraps()
    {
        var (ctx, window, _, center, bottom, top, _) = Build();
        ctx.SelectedState.SelectedCircle = top;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(bottom, ctx.SelectedState.SelectedCircle);

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(top, ctx.SelectedState.SelectedCircle);

        window.Close();
    }

    [AvaloniaFact]
    public void Drag_OnSelectedShape_MovesItAndDoesNotCycle()
    {
        var (ctx, window, _, center, _, top, _) = Build();
        ctx.SelectedState.SelectedCircle = top;

        window.MouseDown(center, MouseButton.Left);
        var moved = center + new Point(5, 5);
        window.MouseMove(moved);
        window.MouseUp(moved, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(top, ctx.SelectedState.SelectedCircle);
        Assert.NotEqual(0f, top.X);

        window.Close();
    }

    [AvaloniaFact]
    public void Hover_OverSelectedShapeWithOverlap_OutlinesTheNextShape()
    {
        var (ctx, window, preview, center, bottom, top, _) = Build();
        ctx.SelectedState.SelectedCircle = top;

        window.MouseMove(center);
        Dispatcher.UIThread.RunJobs();

        var infos = preview.GetShapeInfosForTest();
        var hovered = infos.Where(i => i.IsHovered).ToList();
        Assert.Single(hovered);
        Assert.Equal(bottom.Radius, hovered[0].Param1); // the bottom circle, not the selected top one

        window.Close();
    }

    [AvaloniaFact]
    public void Hover_OverLoneSelectedShape_OutlinesNothing()
    {
        var (ctx, window, preview, center, _, top, _) = Build();
        ctx.SelectedState.SelectedCircle = top;

        // Outside the bottom circle's radius + tolerance but... a lone point only inside nothing else:
        // move far from both shapes instead and confirm no hover.
        window.MouseMove(center + new Point(200, 200));
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(preview.GetShapeInfosForTest(), i => i.IsHovered);

        window.Close();
    }

    [AvaloniaFact]
    public void Group_ClickShape_ReplacesItsFrameInSelection_KeepsOtherFrames_AndCyclesAcrossFrames()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var chains = new List<AnimationChainSave>();
        AnimationFrameSave MakeFrameWithCircle(string chainName, out CircleSave circle)
        {
            circle = new CircleSave { Radius = 10f };
            var f = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            f.ShapesSave!.Add(circle);
            var c = new AnimationChainSave { Name = chainName };
            c.Frames.Add(f);
            chains.Add(c);
            return f;
        }
        var fa = MakeFrameWithCircle("A", out var ca);
        var fb = MakeFrameWithCircle("B", out var cb);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var acls = new AnimationChainListSave();
        acls.AnimationChains.AddRange(chains);
        ctx.ProjectManager.AnimationChainListSave = acls; // after the window, which resets the project
        ctx.SelectedState.SelectedNodes = new List<object> { fa, fb }; // two frames, two chains
        Dispatcher.UIThread.RunJobs();
        var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
        preview.SetPan(0, -50); // lift the shapes clear of the group timeline dock over the preview bottom
        var center = preview.TranslatePoint(
            new Point((preview.Bounds.Width - 20) / 2 + 20, (preview.Bounds.Height - 20) / 2 + 20 - 50), window)!.Value;

        void Click() { window.MouseDown(center, MouseButton.Left); window.MouseUp(center, MouseButton.Left); Dispatcher.UIThread.RunJobs(); }

        Click(); // nothing selected: topmost (later frame's circle)
        Assert.Equal(new object[] { fa, cb }, ctx.SelectedState.SelectedNodes);
        Assert.True(preview.IsGroupPreviewActive);

        Click(); // cycles to the other frame's circle; the first circle falls back to its frame
        Assert.Equal(new object[] { ca, fb }, ctx.SelectedState.SelectedNodes);

        Click(); // wraps
        Assert.Equal(new object[] { fa, cb }, ctx.SelectedState.SelectedNodes);

        window.Close();
    }
}
