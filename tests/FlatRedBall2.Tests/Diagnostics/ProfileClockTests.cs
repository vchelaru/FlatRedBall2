using FlatRedBall2.Diagnostics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class ProfileClockTests
{
    [Fact]
    public void MeasureResolutionMs_AnyPlatform_ReturnsPositiveValue()
    {
        // The probe spins until the clock ticks, so the smallest observable step is always > 0.
        // The magnitude is platform-dependent (nanoseconds on desktop, ~1ms under a browser
        // clamp), so only positivity is asserted here.
        ProfileClock.MeasureResolutionMs().ShouldBeGreaterThan(0);
    }
}
