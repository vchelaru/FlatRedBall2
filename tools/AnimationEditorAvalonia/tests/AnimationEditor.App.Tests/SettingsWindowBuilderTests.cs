using AnimationEditor.App.Settings;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace AnimationEditor.App.Tests;

public class SettingsWindowBuilderTests
{
    private static StackPanel SectionOf(TabItem tab) => (StackPanel)((ScrollViewer)tab.Content!).Content!;

    [AvaloniaFact]
    public void BuildTabs_AlwaysIncludesAppearanceTab()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks());

        var appearanceTab = Assert.IsType<TabItem>(tabs.Items[0]);

        Assert.Equal("Appearance", appearanceTab.Header);
        Assert.Single(tabs.Items);
    }

    // Color rows (section children 1 = Background, 2 = Guide line) are label + [swatch button, dropdown].
    private static ComboBox ColorCombo(TabControl tabs, int rowIndex) =>
        (ComboBox)((StackPanel)((StackPanel)SectionOf((TabItem)tabs.Items[0]!).Children[rowIndex]).Children[1]).Children[1];

    private static Button ColorSwatchButton(TabControl tabs, int rowIndex) =>
        (Button)((StackPanel)((StackPanel)SectionOf((TabItem)tabs.Items[0]!).Children[rowIndex]).Children[1]).Children[0];

    private static Avalonia.Media.Color SwatchColor(Button swatchButton) =>
        ((Avalonia.Media.SolidColorBrush)((Border)swatchButton.Content!).Background!).Color;

    [AvaloniaFact]
    public void BuildTabs_CanvasBackgroundRow_OffersThemeDefaultPresetsAndCustom()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(new SettingsWindowModel(), new SettingsWindowCallbacks());

        Assert.Equal(
            new object?[] { "Theme Default", "Black", "White", "Mid Gray", "Custom…" },
            ColorCombo(tabs, 1).Items.Cast<object?>().ToArray());
    }

    [AvaloniaFact]
    public void BuildTabs_CanvasBackgroundRow_ShowsTheCurrentChoice()
    {
        ComboBox Combo(uint? argb) => ColorCombo(
            SettingsWindowBuilder.BuildTabs(new SettingsWindowModel { CanvasBackgroundArgb = argb }, new SettingsWindowCallbacks()), 1);

        Assert.Equal("Theme Default", Combo(null).SelectedItem);
        Assert.Equal("White", Combo(0xFFFFFFFF).SelectedItem);
        Assert.Equal("Custom…", Combo(0xFF123456).SelectedItem);
    }

    [AvaloniaFact]
    public void BuildTabs_CanvasBackgroundRow_ChoosingThemeDefault_InvokesCallbackWithNull()
    {
        uint? received = 0xFF123456;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { CanvasBackgroundArgb = 0xFF123456, ThemeDefaultBackgroundArgb = 0xFF0E0F12 },
            new SettingsWindowCallbacks { OnCanvasBackgroundChanged = argb => received = argb });

        ColorCombo(tabs, 1).SelectedItem = "Theme Default";

        Assert.Null(received);
    }

    [AvaloniaFact]
    public void BuildTabs_CanvasBackgroundRow_ChoosingAPreset_InvokesCallbackAndUpdatesSwatch()
    {
        uint? received = null;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks { OnCanvasBackgroundChanged = argb => received = argb });

        ColorCombo(tabs, 1).SelectedItem = "Mid Gray";

        Assert.Equal(0xFF808080u, received);
        Assert.Equal(Avalonia.Media.Color.FromUInt32(0xFF808080), SwatchColor(ColorSwatchButton(tabs, 1)));
    }

    [AvaloniaFact]
    public void BuildTabs_CustomChoice_AppliesThePickedColor()
    {
        uint? received = null;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks
            {
                OnGuideLineChanged = argb => received = argb,
                OnPickCustomGuideLine = () => Task.FromResult<uint?>(0xFF336699),
            });

        ColorCombo(tabs, 2).SelectedItem = "Custom…";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0xFF336699u, received);
        Assert.Equal("Custom…", ColorCombo(tabs, 2).SelectedItem);
    }

    [AvaloniaFact]
    public void BuildTabs_CustomChoice_CancelledPicker_KeepsThePreviousChoice()
    {
        uint? received = 0xFFFFFFFF;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { CanvasBackgroundArgb = 0xFFFFFFFF },
            new SettingsWindowCallbacks
            {
                OnCanvasBackgroundChanged = argb => received = argb,
                OnPickCustomCanvasBackground = () => Task.FromResult<uint?>(null),
            });

        ColorCombo(tabs, 1).SelectedItem = "Custom…";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0xFFFFFFFFu, received);
        Assert.Equal("White", ColorCombo(tabs, 1).SelectedItem);
    }

    [AvaloniaFact]
    public void BuildTabs_SwatchButton_OpensTheCustomPicker_SoACustomColorCanBeReEdited()
    {
        uint? received = null;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { CanvasBackgroundArgb = 0xFF123456 },
            new SettingsWindowCallbacks
            {
                OnCanvasBackgroundChanged = argb => received = argb,
                OnPickCustomCanvasBackground = () => Task.FromResult<uint?>(0xFF654321),
            });

        ColorSwatchButton(tabs, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0xFF654321u, received);
    }

    private static RadioButton[] ThemeRadios(TabControl tabs)
    {
        var themeRow = (StackPanel)SectionOf((TabItem)tabs.Items[0]!).Children[0];
        var radios = (StackPanel)themeRow.Children[1];
        return radios.Children.Cast<RadioButton>().ToArray();
    }

    [AvaloniaFact]
    public void BuildTabs_ThemeRow_OffersLightDarkSystem_AndChecksTheModelTheme()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { Theme = AppTheme.System },
            new SettingsWindowCallbacks());

        var radios = ThemeRadios(tabs);

        Assert.Equal(new object?[] { "Light", "Dark", "Follow System" }, radios.Select(r => r.Content).ToArray());
        Assert.Equal(new bool?[] { false, false, true }, radios.Select(r => r.IsChecked).ToArray());
    }

    [AvaloniaFact]
    public void BuildTabs_ThemeRow_ClickingARadio_InvokesCallbackWithThatTheme()
    {
        AppTheme? received = null;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { Theme = AppTheme.Dark },
            new SettingsWindowCallbacks
            {
                OnThemeChanged = theme =>
                {
                    received = theme;
                    return (0xFFFFFFFF, 0xFF000000);
                },
            });

        ThemeRadios(tabs)[0].IsChecked = true;

        Assert.Equal(AppTheme.Light, received);
    }

    [AvaloniaFact]
    public void BuildTabs_ThemeRow_ChangingTheme_RefreshesThemeDefaultSwatches()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { Theme = AppTheme.Dark, ThemeDefaultBackgroundArgb = 0xFF0E0F12, ThemeDefaultGuideLineArgb = 0xFF111111 },
            new SettingsWindowCallbacks { OnThemeChanged = _ => (0xFFF0F0F0, 0xFF222222) });

        ThemeRadios(tabs)[0].IsChecked = true;

        Assert.Equal(Avalonia.Media.Color.FromUInt32(0xFFF0F0F0), SwatchColor(ColorSwatchButton(tabs, 1)));
        Assert.Equal(Avalonia.Media.Color.FromUInt32(0xFF222222), SwatchColor(ColorSwatchButton(tabs, 2)));
    }

    [AvaloniaFact]
    public void BuildTabs_GuideLineRow_HasNoNamedPresets()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks());

        // "Theme Default" + "Custom…" only — no Black/White/Mid Gray presets.
        Assert.Equal(new object?[] { "Theme Default", "Custom…" }, ColorCombo(tabs, 2).Items.Cast<object?>().ToArray());
    }

    [AvaloniaFact]
    public void BuildTabs_FillFrameRectanglesCheckBox_ReflectsModelValue()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { FillFrameRectangles = false },
            new SettingsWindowCallbacks());

        var colorsSection = SectionOf((TabItem)tabs.Items[0]!);
        var fillCheck = Assert.IsType<CheckBox>(colorsSection.Children[3]);

        Assert.Equal(false, fillCheck.IsChecked);
    }

    [AvaloniaFact]
    public void BuildTabs_FillFrameRectanglesCheckBox_ToggleInvokesCallback()
    {
        bool? received = null;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { FillFrameRectangles = true },
            new SettingsWindowCallbacks { OnFillFrameRectanglesChanged = v => received = v });

        var colorsSection = SectionOf((TabItem)tabs.Items[0]!);
        var fillCheck = (CheckBox)colorsSection.Children[3];

        fillCheck.IsChecked = false;

        Assert.Equal(false, received);
    }
}
