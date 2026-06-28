using AnimationEditor.App.Settings;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Xunit;

namespace AnimationEditor.App.Tests;

public class SettingsWindowBuilderTests
{
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

        var header = Assert.IsType<TextBlock>(((StackPanel)sections.Children[0]).Children[0]);

        Assert.Equal("File association", header.Text);
    }
}
