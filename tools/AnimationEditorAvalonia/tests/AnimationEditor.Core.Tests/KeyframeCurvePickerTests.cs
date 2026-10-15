using AnimationEditor.Core.Keyframes;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class KeyframeCurvePickerTests
{
    [Theory]
    [InlineData(KeyframeCurveChoice.Snap, null, null)]
    [InlineData(KeyframeCurveChoice.Linear, "Linear", "Out")]
    [InlineData(KeyframeCurveChoice.EaseIn, "Quadratic", "In")]
    [InlineData(KeyframeCurveChoice.EaseOut, "Quadratic", "Out")]
    [InlineData(KeyframeCurveChoice.EaseInOut, "Quadratic", "InOut")]
    [InlineData(KeyframeCurveChoice.Anticipate, "Back", "In")]
    [InlineData(KeyframeCurveChoice.Overshoot, "Back", "Out")]
    [InlineData(KeyframeCurveChoice.Bounce, "Bounce", "Out")]
    public void ToStored_WritesTheFixedPair_AndFromStoredReadsItBack(
        KeyframeCurveChoice choice, string? type, string? easing)
    {
        var (storedType, storedEasing) = KeyframeCurvePicker.ToStored(choice);

        Assert.Equal(type, storedType);
        Assert.Equal(easing, storedEasing);
        Assert.Equal(choice, KeyframeCurvePicker.FromStored(storedType, storedEasing));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Instant", null)]
    [InlineData("instant", "In")]
    public void FromStored_OmittedOrInstant_IsSnap(string? type, string? easing)
    {
        Assert.Equal(KeyframeCurveChoice.Snap, KeyframeCurvePicker.FromStored(type, easing));
    }

    [Fact]
    public void FromStored_OmittedEasing_MeansOut()
    {
        Assert.Equal(KeyframeCurveChoice.EaseOut, KeyframeCurvePicker.FromStored("Quadratic", null));
        Assert.Equal(KeyframeCurveChoice.Overshoot, KeyframeCurvePicker.FromStored("Back", ""));
        Assert.Equal(KeyframeCurveChoice.Bounce, KeyframeCurvePicker.FromStored("Bounce", null));
    }

    [Fact]
    public void FromStored_NamesAreCaseInsensitive()
    {
        Assert.Equal(KeyframeCurveChoice.EaseInOut, KeyframeCurvePicker.FromStored("quadratic", "inout"));
    }

    [Theory]
    [InlineData("Linear", null)]
    [InlineData("Linear", "In")]
    [InlineData("Linear", "InOut")]
    public void FromStored_Linear_IgnoresEasing(string type, string? easing)
    {
        Assert.Equal(KeyframeCurveChoice.Linear, KeyframeCurvePicker.FromStored(type, easing));
    }

    [Theory]
    [InlineData("Cubic", "In")]
    [InlineData("Cubic", null)]
    [InlineData("Back", "InOut")]
    [InlineData("Bounce", "In")]
    [InlineData("Bounce", "InOut")]
    [InlineData("Quadratic", "Sideways")]
    [InlineData("Wobble", "Out")]
    [InlineData("3", null)]
    public void FromStored_OutsideTheEight_IsNull(string type, string? easing)
    {
        Assert.Null(KeyframeCurvePicker.FromStored(type, easing));
    }

    [Theory]
    [InlineData("Cubic", "In", "Cubic In")]
    [InlineData("Wobble", null, "Wobble")]
    [InlineData("Back", "InOut", "Back InOut")]
    [InlineData(null, "In", "Snap")]
    public void DescribeStored_NamesTheStoredCurve(string? type, string? easing, string expected)
    {
        Assert.Equal(expected, KeyframeCurvePicker.DescribeStored(type, easing));
    }

    [Fact]
    public void DisplayNames_AreTheTooltips()
    {
        Assert.Equal("Snap", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.Snap));
        Assert.Equal("Ease In", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.EaseIn));
        Assert.Equal("Ease In-Out", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.EaseInOut));
        Assert.Equal("Anticipate", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.Anticipate));
        Assert.Equal("Overshoot", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.Overshoot));
        Assert.Equal("Bounce", KeyframeCurvePicker.DisplayName(KeyframeCurveChoice.Bounce));
    }
}
