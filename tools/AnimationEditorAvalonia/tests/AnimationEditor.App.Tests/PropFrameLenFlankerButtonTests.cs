using System.Linq;
using AnimationEditor.Core;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #963: PropFrameLen is a <see cref="FlankerNumericField"/> (the reusable "[−][value][+]" pill
/// GridSize/Speed pioneered) rather than a re-templated NumericUpDown/ButtonSpinner. These tests
/// drive a real click on its internal MinusBtn/PlusBtn parts to confirm the click reaches
/// FlankerNumericField's own increment/decrement/clamp logic, not just that the markup compiles.
/// </summary>
public class PropFrameLenFlankerButtonTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void RealClick(MainWindow window, Control target)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Button FindPart(FlankerNumericField field, string partName)
        => field.GetVisualDescendants().OfType<Button>().First(b => b.Name == partName);

    [AvaloniaFact]
    public void PlusBtn_Clicked_IncrementsFrameLengthByIncrement()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "f0.png", ShapesSave = new ShapesSave(), FrameLength = 0.1f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propFrameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;
            var plusBtn = FindPart(propFrameLen, "PlusBtn");

            RealClick(window, plusBtn);

            // Increment="0.05" on PropFrameLen (MainWindow.axaml).
            Assert.Equal(0.15f, frame.FrameLength, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MinusBtn_ClickedBelowMinimum_ClampsToMinimum()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "f0.png", ShapesSave = new ShapesSave(), FrameLength = 0.03f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propFrameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;
            var minusBtn = FindPart(propFrameLen, "MinusBtn");

            // Minimum="0.001" on PropFrameLen (MainWindow.axaml); one 0.05 decrement from 0.03 must clamp, not go negative.
            RealClick(window, minusBtn);

            Assert.Equal(0.001f, frame.FrameLength, 3);
        }
        finally { window.Close(); }
    }
}
