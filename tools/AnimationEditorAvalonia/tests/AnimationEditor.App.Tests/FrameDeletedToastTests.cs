using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

public class FrameDeletedToastTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        MainWindow window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void Toast_HiddenInitially()
    {
        var (window, _) = CreateWindow();
        try
        {
            Border toast = window.FindControl<Border>("FrameDeletedToastPanel")!;
            Assert.False(toast.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Toast_ShowsAfterDeleteFrames()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "Tex.png" };
            chain.Frames.Add(frame);
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex2.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
            Dispatcher.UIThread.RunJobs();

            Border toast = window.FindControl<Border>("FrameDeletedToastPanel")!;
            Assert.True(toast.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Toast_ShowsCorrectLabel()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "Tex.png" };
            chain.Frames.Add(frame);
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex2.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
            Dispatcher.UIThread.RunJobs();

            TextBlock label = window.FindControl<TextBlock>("FrameDeletedToastLabel")!;
            Assert.Contains("Frame 1", label.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Toast_UndoBtn_RestoresDeletedFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "Tex.png" };
            chain.Frames.Add(frame);
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex2.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
            Dispatcher.UIThread.RunJobs();

            Button undoBtn = window.FindControl<Button>("FrameDeletedToastUndoBtn")!;
            undoBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, chain.Frames.Count);
            Assert.Contains(frame, chain.Frames);

            Border toast = window.FindControl<Border>("FrameDeletedToastPanel")!;
            Assert.False(toast.IsVisible);
        }
        finally { window.Close(); }
    }
}
