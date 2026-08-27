using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

public class UpdateSourceConfigurationTests
{
    [Fact]
    public void ForCurrentBuild_NoTestSource_ReturnsProductionSource()
    {
        // Arrange
        const string productionSource = "https://github.com/vchelaru/FlatRedBall2";

        // Act
        var result = ApplicationUpdateSource.ForCurrentBuild();

        // Assert
        Assert.Equal(productionSource, result);
    }

    [Fact]
    public void Resolve_TestSourceNotAllowed_ReturnsProductionSource()
    {
        // Arrange
        const string testSource = @"C:\\AnimationEditorTestFeed";
        const string productionSource = "https://github.com/vchelaru/FlatRedBall2";

        // Act
        var result = ApplicationUpdateSource.Resolve(testSource, isTestBuild: false);

        // Assert
        Assert.Equal(productionSource, result);
    }

    [Fact]
    public void Resolve_TestSourceProvided_ReturnsTestSource()
    {
        // Arrange
        const string testSource = @"C:\\AnimationEditorTestFeed";

        // Act
        var result = ApplicationUpdateSource.Resolve(testSource, isTestBuild: true);

        // Assert
        Assert.Equal(testSource, result);
    }
}
