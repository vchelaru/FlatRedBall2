using Shouldly;
using Solitaire.Entities;
using Xunit;

namespace Solitaire.Tests;

public class AppliedStateCacheTests
{
    [Fact]
    public void Apply_SameValueTwice_AppliesOnce()
    {
        var cache = new AppliedStateCache<bool>();
        int applies = 0;

        cache.Apply(true, _ => applies++);
        cache.Apply(true, _ => applies++);

        applies.ShouldBe(1);
    }

    [Fact]
    public void Apply_ChangedValue_AppliesAgain()
    {
        var cache = new AppliedStateCache<bool>();
        var applied = new System.Collections.Generic.List<bool>();

        cache.Apply(true, applied.Add);
        cache.Apply(false, applied.Add);

        applied.ShouldBe(new[] { true, false });
    }

    [Fact]
    public void Apply_AfterInvalidate_AppliesSameValueAgain()
    {
        var cache = new AppliedStateCache<bool>();
        int applies = 0;

        cache.Apply(true, _ => applies++);
        cache.Invalidate();
        cache.Apply(true, _ => applies++);

        applies.ShouldBe(2);
    }
}
