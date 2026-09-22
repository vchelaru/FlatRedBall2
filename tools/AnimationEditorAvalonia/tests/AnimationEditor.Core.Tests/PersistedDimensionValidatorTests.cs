using AnimationEditor.Core.Layout;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="PersistedDimensionValidator"/> — the safeguard that keeps a corrupt or
/// stale settings value from producing a broken window layout. Used for both the preview pane's
/// row height (#904) and the sidebar's column width (#1178).
/// </summary>
public class PersistedDimensionValidatorTests
{
    [Fact]
    public void Resolve_StoredWithinBounds_ReturnsStoredValue()
    {
        var resolved = PersistedDimensionValidator.Resolve(320.0, min: 80, max: 2000, fallback: 250);

        Assert.Equal(320.0, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(5000.0)]
    public void Resolve_InvalidStoredValue_ReturnsFallback(double? stored)
    {
        var resolved = PersistedDimensionValidator.Resolve(stored, min: 80, max: 2000, fallback: 250);

        Assert.Equal(250.0, resolved);
    }
}
