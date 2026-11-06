using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>The preview's Shapes toggle: hides shape rendering and editing, re-shows on shape selection.</summary>
public class PreviewShowShapesTests
{
    private static (TestServices Ctx, Window Window, PreviewControl Preview, Point Center, CircleSave Circle) Build()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var circle = new CircleSave { Radius = 20f };
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Add(circle);
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
        return (ctx, window, preview, preview.TranslatePoint(local, window)!.Value, circle);
    }

    [AvaloniaFact]
    public void ShowShapes_DefaultsOn_AndToggleButtonIsChecked()
    {
        var (_, window, preview, _, _) = Build();
        Assert.True(preview.ShowShapes);
        Assert.True(window.FindControl<ToggleButton>("ShowShapesCheck")!.IsChecked);
        window.Close();
    }

    [AvaloniaFact]
    public void ToggleButton_HidesShapes_FromRenderSnapshot()
    {
        var (_, window, preview, _, _) = Build();
        Assert.Single(preview.GetShapeInfosForTest());

        window.FindControl<ToggleButton>("ShowShapesCheck")!.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(preview.ShowShapes);
        Assert.Empty(preview.GetShapeInfosForTest());
        window.Close();
    }

    [AvaloniaFact]
    public void HiddenShapes_CannotBeClickSelectedOrDragged()
    {
        var (ctx, window, preview, center, circle) = Build();
        preview.ShowShapes = false;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Assert.Null(ctx.SelectedState.SelectedShape);

        Assert.Null(preview.GetHoverCursorTypeForTest((float)center.X, (float)center.Y));
        window.Close();
    }

    [AvaloniaFact]
    public void HiddenShapes_AlreadySelected_IgnoreDrag()
    {
        var (ctx, window, preview, center, circle) = Build();
        ctx.SelectedState.SelectedCircle = circle;
        Dispatcher.UIThread.RunJobs();
        preview.ShowShapes = false;

        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Point(8, 8));
        window.MouseUp(center + new Point(8, 8), MouseButton.Left);

        Assert.Equal(0f, circle.X);
        Assert.Equal(0f, circle.Y);
        window.Close();
    }

    [AvaloniaFact]
    public void TurningShapesOff_WithShapeSelected_SelectsParentFrame()
    {
        var (ctx, window, _, _, circle) = Build();
        var frame = ctx.SelectedState.SelectedFrame;
        ctx.SelectedState.SelectedCircle = circle;
        Dispatcher.UIThread.RunJobs();

        window.FindControl<ToggleButton>("ShowShapesCheck")!.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(ctx.SelectedState.SelectedShape);
        Assert.Same(frame, ctx.SelectedState.SelectedFrame);
        Assert.False(window.FindControl<PreviewControl>("PreviewCtrl")!.ShowShapes);
        window.Close();
    }

    [AvaloniaFact]
    public void SelectingAShape_WhileHidden_ShowsShapesAgain_AndSyncsToggle()
    {
        var (ctx, window, preview, _, circle) = Build();
        window.FindControl<ToggleButton>("ShowShapesCheck")!.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        ctx.SelectedState.SelectedCircle = circle;
        Dispatcher.UIThread.RunJobs();

        Assert.True(preview.ShowShapes);
        Assert.True(window.FindControl<ToggleButton>("ShowShapesCheck")!.IsChecked);
        window.Close();
    }
}
