using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #1114 follow-up: the field dims as one uniform unit via Opacity on disable (not a background
// color swap -- FluentTheme's TextControlBackgroundDisabled renders lighter than
// TextControlBackground in this app's Dark theme, the opposite of "muted"). The flanker buttons'
// own global Button:disabled Opacity rule (ThemeStyles.axaml) must be cancelled locally so they
// don't double-dim under the field's fade.
public class FlankerNumericFieldTests
{
    [AvaloniaFact]
    public void Disabling_DimsWholeField_ButNotButtonsIndividually()
    {
        var field = new FlankerNumericField();
        var window = new Window { Content = field };
        window.Show();

        Assert.Equal(1.0, field.Opacity);
        Assert.Equal(1.0, field.MinusBtn.Opacity);

        field.IsEnabled = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(field.Opacity < 1.0);
        Assert.Equal(1.0, field.MinusBtn.Opacity);
        Assert.Equal(1.0, field.PlusBtn.Opacity);
    }
}
