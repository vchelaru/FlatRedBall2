using System.Linq;
using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Utilities;

public class GameRandomTests
{
    [Fact]
    public void In_ReturnsSomeElementFromList()
    {
        var random = new GameRandom(seed: 42);
        var list = new[] { "a", "b", "c", "d" };

        var result = random.In(list);

        list.ShouldContain(result);
    }

    [Fact]
    public void MultipleIn_ReturnsCorrectCountWithNoDuplicates()
    {
        var random = new GameRandom(seed: 42);
        var list = new[] { 1, 2, 3, 4, 5 };

        var result = random.MultipleIn(list, 3);

        result.Count.ShouldBe(3);
        result.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Between_Float_ReturnsValueInRange()
    {
        var random = new GameRandom(seed: 42);
        float lower = 5f, upper = 10f;

        for (int i = 0; i < 100; i++)
        {
            var value = random.Between(lower, upper);
            value.ShouldBeGreaterThanOrEqualTo(lower);
            value.ShouldBeLessThanOrEqualTo(upper);
        }
    }

    [Fact]
    public void PointInCircle_ReturnsPointWithinRadius()
    {
        var random = new GameRandom(seed: 42);
        float radius = 50f;

        for (int i = 0; i < 100; i++)
        {
            var point = random.PointInCircle(radius);
            (point.Length() <= radius + 0.001f).ShouldBeTrue();
        }
    }
}
