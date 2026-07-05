using AnimationEditor.App.Settings;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace AnimationEditor.App.Tests;

public class SettingsWindowBuilderTests
{
    [Fact]
    public void BuildSections_AlwaysIncludesCanvasColorsHeader()
    {
        var sections = SettingsWindowBuilder.BuildSections(
            new SettingsWindowModel { FileAssociationSupported = false },
            new SettingsWindowCallbacks());

        var header = Assert.IsType<TextBlock>(((StackPanel)sections.Children[0]).Children[0]);

        Assert.Equal("Canvas colors", header.Text);
    }

    [Fact]
    public void BuildSections_WithFileAssociation_IncludesSectionHeader()
    {
        var sections = SettingsWindowBuilder.BuildSections(
            new SettingsWindowModel
            {
                FileAssociationSupported = true,
                FileAssociationStatus = AchxFileAssociationStatus.Stale,
                SuppressDefaultHandlerPrompt = false,
            },
            new SettingsWindowCallbacks());

        // Canvas colors is always first; file association follows when supported.
        var header = Assert.IsType<TextBlock>(((StackPanel)sections.Children[1]).Children[0]);

        Assert.Equal("File association", header.Text);
    }

    [Fact]
    public void BuildSections_WithoutFileAssociation_OmitsFileAssociationSection()
    {
        var sections = SettingsWindowBuilder.BuildSections(
            new SettingsWindowModel { FileAssociationSupported = false },
            new SettingsWindowCallbacks());

        Assert.Single(sections.Children);
    }

    [AvaloniaFact]
    public void BuildSections_CanvasBackgroundRow_ThemeDefaultButton_InvokesCallbackWithNull()
    {
        uint? received = 0xFF123456;
        var sections = SettingsWindowBuilder.BuildSections(
            new SettingsWindowModel { CanvasBackgroundArgb = 0xFF123456, ThemeDefaultBackgroundArgb = 0xFF0E0F12 },
            new SettingsWindowCallbacks { OnCanvasBackgroundChanged = argb => received = argb });

        var canvasColors = (StackPanel)sections.Children[0];
        var backgroundRow = (StackPanel)canvasColors.Children[1];
        var buttonsRow = (StackPanel)backgroundRow.Children[1];
        // buttonsRow.Children[0] is the swatch; [1] is "Theme Default".
        var themeDefaultButton = Assert.IsType<Button>(buttonsRow.Children[1]);
        Assert.Equal("Theme Default", themeDefaultButton.Content);

        themeDefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(received);
    }

    [Fact]
    public void BuildSections_GuideLineRow_HasNoNamedPresets()
    {
        var sections = SettingsWindowBuilder.BuildSections(
            new SettingsWindowModel(),
            new SettingsWindowCallbacks());

        var canvasColors = (StackPanel)sections.Children[0];
        var guideLineRow = (StackPanel)canvasColors.Children[2];
        var buttonsRow = (StackPanel)guideLineRow.Children[1];

        // Swatch + "Theme Default" + "Custom…" only — no Black/White/Mid Gray presets.
        Assert.Equal(3, buttonsRow.Children.Count);
    }
}
