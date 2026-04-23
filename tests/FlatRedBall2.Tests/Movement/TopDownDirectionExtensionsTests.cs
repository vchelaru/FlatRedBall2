using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class TopDownDirectionExtensionsTests
{
    [Theory]
    [InlineData(TopDownDirection.Right,     TopDownDirection.Right)]
    [InlineData(TopDownDirection.Up,        TopDownDirection.Up)]
    [InlineData(TopDownDirection.Left,      TopDownDirection.Left)]
    [InlineData(TopDownDirection.Down,      TopDownDirection.Down)]
    [InlineData(TopDownDirection.UpRight,   TopDownDirection.Right)]
    [InlineData(TopDownDirection.DownRight, TopDownDirection.Right)]
    [InlineData(TopDownDirection.UpLeft,    TopDownDirection.Left)]
    [InlineData(TopDownDirection.DownLeft,  TopDownDirection.Left)]
    public void ToCardinal_HorizontalAxis_CollapsesDiagonalsToLeftOrRight(TopDownDirection input, TopDownDirection expected)
    {
        input.ToCardinal(DiagonalAxis.Horizontal).ShouldBe(expected);
    }

    [Theory]
    [InlineData(TopDownDirection.Right,     TopDownDirection.Right)]
    [InlineData(TopDownDirection.Up,        TopDownDirection.Up)]
    [InlineData(TopDownDirection.Left,      TopDownDirection.Left)]
    [InlineData(TopDownDirection.Down,      TopDownDirection.Down)]
    [InlineData(TopDownDirection.UpRight,   TopDownDirection.Up)]
    [InlineData(TopDownDirection.UpLeft,    TopDownDirection.Up)]
    [InlineData(TopDownDirection.DownRight, TopDownDirection.Down)]
    [InlineData(TopDownDirection.DownLeft,  TopDownDirection.Down)]
    public void ToCardinal_VerticalAxis_CollapsesDiagonalsToUpOrDown(TopDownDirection input, TopDownDirection expected)
    {
        input.ToCardinal(DiagonalAxis.Vertical).ShouldBe(expected);
    }

    [Fact]
    public void ToCardinal_DefaultsToHorizontalAxis()
    {
        TopDownDirection.UpRight.ToCardinal().ShouldBe(TopDownDirection.Right);
        TopDownDirection.DownLeft.ToCardinal().ShouldBe(TopDownDirection.Left);
    }
}
