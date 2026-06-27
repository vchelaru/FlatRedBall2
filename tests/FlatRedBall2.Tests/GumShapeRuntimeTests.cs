using System.Reflection;
using Gum.GueDeriving;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

// Regression guard for issue #437. The Apos.Shapes-backed fill / corner-radius / gradient /
// drop-shadow properties on RectangleRuntime and CircleRuntime did not exist in the Gum version
// FRB2 previously pinned (2026.5.8.1) — they only appear from the 2026.6 line onward. If the Gum
// packages are ever downgraded below that, these assertions go red.
//
// The properties are asserted via reflection rather than by constructing the runtimes: the shape
// ctors instantiate a backing renderable (e.g. LineCircle) that dereferences SystemManagers.Default,
// which is null in the headless test host. Reflection checks the API surface — exactly what the issue
// was about ("... do not seem to be available") — without needing an initialized GraphicsDevice.
public class GumShapeRuntimeTests
{
    [Theory]
    [InlineData("FillColor")]
    [InlineData("CornerRadius")]
    [InlineData("UseGradient")]
    [InlineData("DropshadowBlur")]
    public void RectangleRuntime_ExposesAposShapeProperty(string propertyName) =>
        typeof(RectangleRuntime)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            .ShouldNotBeNull();

    // CircleRuntime intentionally omits CornerRadius — a circle has no corners. The issue reporter
    // expected it there by analogy with RectangleRuntime; it is a Rectangle-only property.
    [Theory]
    [InlineData("FillColor")]
    [InlineData("UseGradient")]
    [InlineData("DropshadowBlur")]
    public void CircleRuntime_ExposesAposShapeProperty(string propertyName) =>
        typeof(CircleRuntime)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            .ShouldNotBeNull();
}
