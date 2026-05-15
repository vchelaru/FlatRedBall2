using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

public class DeleteShapeInlineConfirmTests
{
    private static (MainWindow Window, TestServices Ctx, AnimationFrameSave Frame) CreateWindowWithFrame()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        MainWindow window = ctx.CreateMainWindow();
        window.Show();
        // OnOpened is queued asynchronously during Show(). Drain it now so the
        // new empty AnimationChainListSave is already in place before we add data.
        Dispatcher.UIThread.RunJobs();

        // Add chain/frame to the list that OnOpened just installed
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        return (window, ctx, frame);
    }

    [AvaloniaFact]
    public void InlineShapeConfirmPanel_HiddenInitially()
    {
        var (window, _, _) = CreateWindowWithFrame();
        try
        {
            Border panel = window.FindControl<Border>("DeleteShapeConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineShapeConfirmPanel_ShowsWhenShapeDeleteRequested()
    {
        var (window, _, frame) = CreateWindowWithFrame();
        try
        {
            var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
            frame.ShapesSave!.Shapes.Add(circle);

            window.ShowDeleteShapeConfirmForTest(frame, new(), new() { circle });
            Dispatcher.UIThread.RunJobs();

            Border panel = window.FindControl<Border>("DeleteShapeConfirmPanel")!;
            Assert.True(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineShapeConfirmPanel_ConfirmDeletesShape()
    {
        var (window, _, frame) = CreateWindowWithFrame();
        try
        {
            var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
            frame.ShapesSave!.Shapes.Add(circle);

            window.ShowDeleteShapeConfirmForTest(frame, new(), new() { circle });
            Dispatcher.UIThread.RunJobs();

            Button confirmBtn = window.FindControl<Button>("DeleteShapeConfirmBtn")!;
            confirmBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(frame.ShapesSave!.CircleSaves);
            Border panel = window.FindControl<Border>("DeleteShapeConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineShapeConfirmPanel_CancelPreservesShape()
    {
        var (window, _, frame) = CreateWindowWithFrame();
        try
        {
            var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
            frame.ShapesSave!.Shapes.Add(circle);

            window.ShowDeleteShapeConfirmForTest(frame, new(), new() { circle });
            Dispatcher.UIThread.RunJobs();

            Button cancelBtn = window.FindControl<Button>("DeleteShapeCancelBtn")!;
            cancelBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(frame.ShapesSave!.CircleSaves);
            Border panel = window.FindControl<Border>("DeleteShapeConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineShapeConfirmPanel_ConfirmDeletesMixedRectAndCircle()
    {
        var (window, _, frame) = CreateWindowWithFrame();
        try
        {
            var rect   = new AARectSave { Name = "Hitbox" };
            var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
            frame.ShapesSave!.Shapes.Add(rect);
            frame.ShapesSave!.Shapes.Add(circle);

            window.ShowDeleteShapeConfirmForTest(frame, new() { rect }, new() { circle });
            Dispatcher.UIThread.RunJobs();

            Button confirmBtn = window.FindControl<Button>("DeleteShapeConfirmBtn")!;
            confirmBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(frame.ShapesSave!.AARectSaves);
            Assert.Empty(frame.ShapesSave!.CircleSaves);
        }
        finally { window.Close(); }
    }
}
