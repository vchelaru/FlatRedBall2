using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Guards against the shared-recovery-file-path race (issue #703): every
/// <see cref="TestServices"/> instance must get its own <c>RecoveryFilePath</c>
/// so concurrent test classes never write to the same temp file.
/// </summary>
public class TestServicesRecoveryPathTests
{
    [Fact]
    public void TwoInstances_GetDifferentRecoveryFilePaths()
    {
        var first = TestHelpers.SetupFreshAcls();
        var second = TestHelpers.SetupFreshAcls();

        Assert.NotEqual(first.IoManager.RecoveryFilePath, second.IoManager.RecoveryFilePath);
    }

    [Fact]
    public void RecoveryFilePath_IsNotTheSharedDefaultTempPath()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        var sharedDefault = Path.Combine(Path.GetTempPath(), "AnimationEditor_Recovery.achx");

        Assert.NotEqual(sharedDefault, ctx.IoManager.RecoveryFilePath);
    }
}
