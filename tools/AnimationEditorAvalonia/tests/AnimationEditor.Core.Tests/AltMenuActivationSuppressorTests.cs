using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Issue #488: Alt+Arrow tree reorder arms suppression of the next Alt KeyUp so the
/// title-bar menu is not activated when Alt is released.
/// </summary>
public class AltMenuActivationSuppressorTests
{
    [Fact]
    public void TryConsumeIfArmed_AfterArm_ClearsArmAndReturnsTrue()
    {
        var suppressor = new AltMenuActivationSuppressor();
        suppressor.ArmFromAltArrowReorder();

        Assert.True(suppressor.TryConsumeIfArmed());
        Assert.False(suppressor.TryConsumeIfArmed());
    }

    [Fact]
    public void TryConsumeIfArmed_MultipleArmsBeforeRelease_ConsumesOnce()
    {
        var suppressor = new AltMenuActivationSuppressor();
        suppressor.ArmFromAltArrowReorder();
        suppressor.ArmFromAltArrowReorder();

        Assert.True(suppressor.TryConsumeIfArmed());
        Assert.False(suppressor.TryConsumeIfArmed());
    }

    [Fact]
    public void TryConsumeIfArmed_NotArmed_ReturnsFalse()
    {
        var suppressor = new AltMenuActivationSuppressor();

        Assert.False(suppressor.TryConsumeIfArmed());
    }
}
