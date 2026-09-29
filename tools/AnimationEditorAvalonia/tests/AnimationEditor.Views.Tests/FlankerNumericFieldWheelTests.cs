using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #1242: every numeric field steps its value on mouse wheel once focused, matching Avalonia's
// NumericUpDown (whose ButtonSpinner only spins while it has keyboard focus).
public class FlankerNumericFieldWheelTests
{
    [AvaloniaFact]
    public void Wheel_Focused_StepsByIncrementAndClamps()
    {
        var field = new FlankerNumericField { Minimum = 0m, Maximum = 1m, Increment = 0.25m, Value = 0.5m };
        var window = Show(field);
        field.ValueBox.Focus();

        window.MouseWheel(Center(field, window), new Vector(0, 1));
        Assert.Equal(0.75m, field.Value);

        window.MouseWheel(Center(field, window), new Vector(0, -1));
        window.MouseWheel(Center(field, window), new Vector(0, -1));
        window.MouseWheel(Center(field, window), new Vector(0, -1));
        window.MouseWheel(Center(field, window), new Vector(0, -1));
        Assert.Equal(0m, field.Value);
    }

    [AvaloniaFact]
    public void Wheel_NotFocused_LeavesValueUnchanged()
    {
        var field = new FlankerNumericField { Increment = 1m, Value = 5m };
        var window = Show(field);

        window.MouseWheel(Center(field, window), new Vector(0, 1));

        Assert.Equal(5m, field.Value);
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 200, Height = 60, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return window;
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}
