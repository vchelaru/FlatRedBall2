using AnimationEditor.App;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests the pure decision logic on <see cref="WindowsTaskbarIdentity"/> (whether to apply
/// its own fixed AppUserModelID over whatever Velopack already assigned). The P/Invoke call
/// and the real Velopack locator lookup are thin, untested wiring around this (#1179).
/// </summary>
public class WindowsTaskbarIdentityTests
{
    [Fact]
    public void ShouldSetExplicitId_VelopackAssignedNone_ReturnsTrue()
    {
        bool result = WindowsTaskbarIdentity.ShouldSetExplicitId(velopackAssignedAppUserModelId: null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSetExplicitId_VelopackAlreadyAssignedOne_ReturnsFalse()
    {
        bool result = WindowsTaskbarIdentity.ShouldSetExplicitId(
            velopackAssignedAppUserModelId: "velopack.FlatRedBall2.AnimationEditor");

        Assert.False(result);
    }
}
