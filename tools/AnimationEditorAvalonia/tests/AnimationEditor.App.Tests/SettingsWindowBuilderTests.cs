using AnimationEditor.App.Settings;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace AnimationEditor.App.Tests;

public class SettingsWindowBuilderTests
{
    private static StackPanel SectionOf(TabItem tab) => (StackPanel)((ScrollViewer)tab.Content!).Content!;

    [Fact]
    public void BuildTabs_AlwaysIncludesColorsTab()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { FileAssociationSupported = false },
            new SettingsWindowCallbacks());

        var colorsTab = Assert.IsType<TabItem>(tabs.Items[0]);

        Assert.Equal("Colors", colorsTab.Header);
    }

    [Fact]
    public void BuildTabs_WithFileAssociation_IncludesFileAssociationTab()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel
            {
                FileAssociationSupported = true,
                FileAssociationStatus = AchxFileAssociationStatus.Stale,
                SuppressDefaultHandlerPrompt = false,
            },
            new SettingsWindowCallbacks());

        // Colors is always first; File Association follows when supported.
        var fileAssocTab = Assert.IsType<TabItem>(tabs.Items[1]);

        Assert.Equal("File Association", fileAssocTab.Header);
    }

    [Fact]
    public void BuildTabs_WithoutFileAssociation_OnlyHasColorsTab()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { FileAssociationSupported = false },
            new SettingsWindowCallbacks());

        Assert.Single(tabs.Items);
    }

    [AvaloniaFact]
    public void BuildTabs_CanvasBackgroundRow_ThemeDefaultButton_InvokesCallbackWithNull()
    {
        uint? received = 0xFF123456;
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel { CanvasBackgroundArgb = 0xFF123456, ThemeDefaultBackgroundArgb = 0xFF0E0F12 },
            new SettingsWindowCallbacks { OnCanvasBackgroundChanged = argb => received = argb });

        var colorsSection = SectionOf((TabItem)tabs.Items[0]!);
        var backgroundRow = (StackPanel)colorsSection.Children[0];
        var buttonsRow = (StackPanel)backgroundRow.Children[1];
        // buttonsRow.Children[0] is the swatch; [1] is "Theme Default".
        var themeDefaultButton = Assert.IsType<Button>(buttonsRow.Children[1]);
        Assert.Equal("Theme Default", themeDefaultButton.Content);

        themeDefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(received);
    }

    [Fact]
    public void BuildTabs_GuideLineRow_HasNoNamedPresets()
    {
        var tabs = SettingsWindowBuilder.BuildTabs(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks());

        var colorsSection = SectionOf((TabItem)tabs.Items[0]!);
        var guideLineRow = (StackPanel)colorsSection.Children[1];
        var buttonsRow = (StackPanel)guideLineRow.Children[1];

        // Swatch + "Theme Default" + "Custom…" only — no Black/White/Mid Gray presets.
        Assert.Equal(3, buttonsRow.Children.Count);
    }
}
