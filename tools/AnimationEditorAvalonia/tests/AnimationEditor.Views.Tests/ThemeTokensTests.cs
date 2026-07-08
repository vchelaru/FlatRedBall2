using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Phase 6 (#630): shared theme-token foundation -- see docs/BROWSER_THEME_TOKENS_DECISION.md.
// Loads ThemeTokens.axaml directly rather than baking it into TestApp, so this test's failure
// mode before the file exists is "resource not found" (the right reason), not a mass failure of
// every other Views.Tests test that would result from wiring a missing avares:// URI into the
// shared test app.
public class ThemeTokensTests
{
    [AvaloniaFact]
    public void ThemeTokens_ResolveUnderBothDarkAndLight()
    {
        var themeTokens = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new Uri("avares://AnimationEditor.Views/Theming/ThemeTokens.axaml"))!;
        var app = Application.Current!;
        app.Resources.MergedDictionaries.Add(themeTokens);
        try
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.True(app.TryFindResource("BgCanvas", ThemeVariant.Dark, out var darkBg));
            Assert.NotNull(darkBg);
            Assert.True(app.TryFindResource("LineBrush", ThemeVariant.Dark, out var darkLine));
            Assert.NotNull(darkLine);

            app.RequestedThemeVariant = ThemeVariant.Light;
            Assert.True(app.TryFindResource("BgCanvas", ThemeVariant.Light, out var lightBg));
            Assert.NotNull(lightBg);
            Assert.True(app.TryFindResource("LineBrush", ThemeVariant.Light, out var lightLine));
            Assert.NotNull(lightLine);

            // The two variants must actually differ, not just both resolve to a hardcoded value.
            Assert.NotEqual(darkBg, lightBg);
        }
        finally
        {
            app.Resources.MergedDictionaries.Remove(themeTokens);
        }
    }
}
