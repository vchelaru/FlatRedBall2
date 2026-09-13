using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #1114 follow-up: the field's Border background must dim on disable independently of the
// buttons' own Opacity-based dimming (see FlankerNumericField.axaml's Border.Styles comment) --
// otherwise a disabled field with no text (nothing for a foreground-color cue to mute) looks
// identical to an enabled one.
public class FlankerNumericFieldTests
{
    [AvaloniaFact]
    public void Disabling_ChangesBorderBackground_FromEnabledState()
    {
        var field = new FlankerNumericField();
        var window = new Window { Content = field };
        window.Show();

        var enabledBackground = field.RootBorder.Background;

        field.IsEnabled = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var disabledBackground = field.RootBorder.Background;

        Assert.NotNull(enabledBackground);
        Assert.NotNull(disabledBackground);
        Assert.NotEqual(
            ((ISolidColorBrush)enabledBackground!).Color,
            ((ISolidColorBrush)disabledBackground!).Color);
    }
}
