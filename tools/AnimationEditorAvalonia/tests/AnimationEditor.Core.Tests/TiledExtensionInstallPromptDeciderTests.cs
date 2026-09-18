using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests the prompt decision for the Tiled-install startup banner (#1128): only a detected
/// Tiled install with a missing or outdated extension, not yet dismissed by the user, prompts.
/// </summary>
public class TiledExtensionInstallPromptDeciderTests
{
    [Fact]
    public void ShouldPrompt_DetectedAndNotInstalled_ReturnsTrue()
    {
        bool result = TiledExtensionInstallPromptDecider.ShouldPrompt(
            tiledDetected: true, status: TiledExtensionInstallStatus.NotInstalled, isPromptSuppressed: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPrompt_DetectedAndOutdated_ReturnsTrue()
    {
        bool result = TiledExtensionInstallPromptDecider.ShouldPrompt(
            tiledDetected: true, status: TiledExtensionInstallStatus.Outdated, isPromptSuppressed: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPrompt_DetectedAndUpToDate_ReturnsFalse()
    {
        bool result = TiledExtensionInstallPromptDecider.ShouldPrompt(
            tiledDetected: true, status: TiledExtensionInstallStatus.UpToDate, isPromptSuppressed: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPrompt_NotDetected_ReturnsFalse()
    {
        bool result = TiledExtensionInstallPromptDecider.ShouldPrompt(
            tiledDetected: false, status: TiledExtensionInstallStatus.NotDetected, isPromptSuppressed: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPrompt_SuppressedByUser_ReturnsFalse()
    {
        bool result = TiledExtensionInstallPromptDecider.ShouldPrompt(
            tiledDetected: true, status: TiledExtensionInstallStatus.NotInstalled, isPromptSuppressed: true);

        Assert.False(result);
    }
}
