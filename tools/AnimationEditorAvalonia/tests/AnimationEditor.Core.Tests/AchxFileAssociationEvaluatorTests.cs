using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxFileAssociationEvaluatorTests
{
    private const string CurrentExe = @"C:\Build\AnimationEditor.exe";
    private const string OtherExe = @"C:\OldWorktree\AnimationEditor.exe";

    [Fact]
    public void Classify_ForeignProgId_ReturnsNotAssociated()
    {
        var status = AchxFileAssociationEvaluator.Classify(
            isOurProgId: false,
            registeredExePath: OtherExe,
            currentExePath: CurrentExe,
            registeredExeExists: true);

        Assert.Equal(AchxFileAssociationStatus.NotAssociated, status);
    }

    [Fact]
    public void Classify_OurProgIdAndMatchingExe_ReturnsAssociatedWithThisBuild()
    {
        var status = AchxFileAssociationEvaluator.Classify(
            isOurProgId: true,
            registeredExePath: CurrentExe,
            currentExePath: CurrentExe,
            registeredExeExists: true);

        Assert.Equal(AchxFileAssociationStatus.AssociatedWithThisBuild, status);
    }

    [Fact]
    public void Classify_OurProgIdButDifferentExe_ReturnsStale()
    {
        var status = AchxFileAssociationEvaluator.Classify(
            isOurProgId: true,
            registeredExePath: OtherExe,
            currentExePath: CurrentExe,
            registeredExeExists: true);

        Assert.Equal(AchxFileAssociationStatus.Stale, status);
    }

    [Fact]
    public void Classify_OurProgIdButMissingExe_ReturnsStale()
    {
        var status = AchxFileAssociationEvaluator.Classify(
            isOurProgId: true,
            registeredExePath: OtherExe,
            currentExePath: CurrentExe,
            registeredExeExists: false);

        Assert.Equal(AchxFileAssociationStatus.Stale, status);
    }

    [Fact]
    public void IsDefaultForCurrentBuild_StaleAssociation_ReturnsFalse()
    {
        bool isDefault = AchxFileAssociationEvaluator.IsDefaultForCurrentBuild(
            isOurProgId: true,
            registeredExePath: OtherExe,
            currentExePath: CurrentExe,
            registeredExeExists: false);

        Assert.False(isDefault);
    }
}
