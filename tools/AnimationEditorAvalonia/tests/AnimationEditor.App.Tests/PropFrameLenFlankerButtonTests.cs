using System.Linq;
using AnimationEditor.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #957: PropFrameLen's ButtonSpinner is re-templated to flank the value with +/- buttons
/// (<c>[−][value][+]</c>) instead of Fluent's stacked chevrons. These tests drive a real click on
/// the re-templated <c>PART_IncreaseButton</c>/<c>PART_DecreaseButton</c> parts to confirm the
/// click reaches Avalonia's built-in NumericUpDown increment/decrement/clamp logic, not just that
/// the template compiles.
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

    private static RepeatButton FindTemplatePart(NumericUpDown numeric, string partName)
        => numeric.GetVisualDescendants().OfType<RepeatButton>().First(b => b.Name == partName);

    [AvaloniaFact]
    public void PartIncreaseButton_Clicked_IncrementsFrameLengthByIncrement()
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

            var propFrameLen = window.FindControl<NumericUpDown>("PropFrameLen")!;
            var increaseButton = FindTemplatePart(propFrameLen, "PART_IncreaseButton");

            RealClick(window, increaseButton);

            // Increment="0.05" on PropFrameLen (MainWindow.axaml).
            Assert.Equal(0.15f, frame.FrameLength, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PartDecreaseButton_ClickedBelowMinimum_ClampsToMinimum()
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

            var propFrameLen = window.FindControl<NumericUpDown>("PropFrameLen")!;
            var decreaseButton = FindTemplatePart(propFrameLen, "PART_DecreaseButton");

            // Minimum="0.001" on PropFrameLen (MainWindow.axaml); one 0.05 decrement from 0.03 must clamp, not go negative.
            RealClick(window, decreaseButton);

            Assert.Equal(0.001f, frame.FrameLength, 3);
        }
        finally { window.Close(); }
    }
}
