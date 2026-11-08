using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

public class ErrorTextBrushScenarioTests
{
    [AvaloniaFact]
    public void TsxOwnerErrorText_IsDrawnInTheThemesErrorRed_NotAnUnresolvedResource()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var error = editor.Control<TextBlock>("PropChainTsxOwnerError");

        editor.Window.TryFindResource("Accent", editor.Window.ActualThemeVariant, out object? accent).ShouldBeTrue();
        // An unresolved DynamicResource leaves the inherited foreground, which is never the accent red.
        ((SolidColorBrush)error.Foreground!).Color.ShouldBe(((SolidColorBrush)accent!).Color);
    }
}
