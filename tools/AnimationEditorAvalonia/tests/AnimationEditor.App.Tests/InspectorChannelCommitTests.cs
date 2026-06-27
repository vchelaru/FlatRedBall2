using AnimationEditor.Core;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The R/G/B/A inspector NumericUpDowns must commit a single undo entry on edit completion
/// (focus loss / Enter), not one per keystroke. Avalonia's NumericUpDown raises ValueChanged
/// on every keypress while typing, so wiring the undo command to ValueChanged spammed the undo
/// stack (#445 follow-up). This is genuinely UI-bound wiring, hence a headless-Avalonia test.
/// </summary>
public class InspectorChannelCommitTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static AnimationFrameSave SelectFreshFrame(TestServices ctx)
    {
        var chain = new AnimationChainSave { Name = "Fade" };
        var frame = new AnimationFrameSave { TextureName = "f.png", ShapesSave = new ShapesSave() };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();
        return frame;
    }

    [AvaloniaFact]
    public void AlphaField_ValueChurnThenEnter_RecordsSingleUndo()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = SelectFreshFrame(ctx);
            var alpha = window.FindControl<NumericUpDown>("PropAlpha")!;

            // Typing "30" churns the value 3 -> 30; nothing commits until focus loss / Enter.
            alpha.Value = 3;
            alpha.Value = 30;
            Assert.False(ctx.UndoManager.CanUndo);

            alpha.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal(30, frame.Alpha);
            Assert.Single(ctx.UndoManager.UndoHistory);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RedField_ValueChurnThenFocusLoss_RecordsSingleUndo()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = SelectFreshFrame(ctx);
            var red   = window.FindControl<NumericUpDown>("PropRed")!;
            var blue  = window.FindControl<NumericUpDown>("PropBlue")!;

            red.Focus();
            Dispatcher.UIThread.RunJobs();
            red.Value = 1;
            red.Value = 200;
            Assert.False(ctx.UndoManager.CanUndo);

            // Moving focus to another field raises LostFocus on Red, committing once.
            blue.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(200, frame.Red);
            Assert.Single(ctx.UndoManager.UndoHistory);
        }
        finally { window.Close(); }
    }
}
