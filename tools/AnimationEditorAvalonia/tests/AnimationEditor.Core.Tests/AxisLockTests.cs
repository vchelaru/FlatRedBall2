using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="AxisLock"/> — the Shift-constrained-drag math behind issue #1022
/// (hold Shift while dragging a frame/animation offset to lock to the larger-delta axis).
/// </summary>
public class AxisLockTests
{
    [Fact]
    public void Apply_NotLocked_ReturnsDeltasUnchanged()
    {
        var (dx, dy) = AxisLock.Apply(3f, 9f, locked: false);

        Assert.Equal(3f, dx, precision: 3);
        Assert.Equal(9f, dy, precision: 3);
    }

    [Fact]
    public void Apply_Locked_HorizontalDeltaLarger_ZeroesVertical()
    {
        var (dx, dy) = AxisLock.Apply(10f, 4f, locked: true);

        Assert.Equal(10f, dx, precision: 3);
        Assert.Equal(0f, dy, precision: 3);
    }

    [Fact]
    public void Apply_Locked_VerticalDeltaLarger_ZeroesHorizontal()
    {
        var (dx, dy) = AxisLock.Apply(4f, 10f, locked: true);

        Assert.Equal(0f, dx, precision: 3);
        Assert.Equal(10f, dy, precision: 3);
    }

    [Fact]
    public void Apply_Locked_ComparesMagnitudeNotSign()
    {
        var (dx, dy) = AxisLock.Apply(-10f, 4f, locked: true);

        Assert.Equal(-10f, dx, precision: 3);
        Assert.Equal(0f, dy, precision: 3);
    }

    [Fact]
    public void Apply_Locked_EqualMagnitude_KeepsBothAxesFree()
    {
        var (dx, dy) = AxisLock.Apply(5f, -5f, locked: true);

        Assert.Equal(5f, dx, precision: 3);
        Assert.Equal(-5f, dy, precision: 3);
    }
}
