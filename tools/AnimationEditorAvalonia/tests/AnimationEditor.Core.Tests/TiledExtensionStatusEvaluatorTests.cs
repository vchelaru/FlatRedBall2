using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TiledExtensionStatusEvaluatorTests
{
    [Fact]
    public void Classify_FilesMissing_ReturnsNotInstalled()
    {
        var status = TiledExtensionStatusEvaluator.Classify(filesPresent: false, contentMatches: false);

        Assert.Equal(TiledExtensionInstallStatus.NotInstalled, status);
    }

    [Fact]
    public void Classify_FilesPresentAndContentDiffers_ReturnsOutdated()
    {
        var status = TiledExtensionStatusEvaluator.Classify(filesPresent: true, contentMatches: false);

        Assert.Equal(TiledExtensionInstallStatus.Outdated, status);
    }

    [Fact]
    public void Classify_FilesPresentAndContentMatches_ReturnsUpToDate()
    {
        var status = TiledExtensionStatusEvaluator.Classify(filesPresent: true, contentMatches: true);

        Assert.Equal(TiledExtensionInstallStatus.UpToDate, status);
    }
}
