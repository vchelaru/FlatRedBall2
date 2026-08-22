using AnimationEditor.Core.Utilities;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NumericToolbarInputTests
{
    [Fact]
    public void ParseClamp_AboveMax_ClampsToMax()
    {
        var result = NumericToolbarInput.ParseClamp("999", min: 0m, max: 60m, fallback: 1m);

        Assert.Equal(60m, result);
    }

    [Fact]
    public void ParseClamp_BelowMin_ClampsToMin()
    {
        var result = NumericToolbarInput.ParseClamp("-5", min: 0.001m, max: 60m, fallback: 1m);

        Assert.Equal(0.001m, result);
    }

    [Fact]
    public void ParseClamp_NonNumericText_ReturnsFallback()
    {
        var result = NumericToolbarInput.ParseClamp("abc", min: 0m, max: 60m, fallback: 0.1m);

        Assert.Equal(0.1m, result);
    }

    [Fact]
    public void ParseClamp_NullText_ReturnsFallback()
    {
        var result = NumericToolbarInput.ParseClamp(null, min: 0m, max: 60m, fallback: 0.1m);

        Assert.Equal(0.1m, result);
    }

    [Fact]
    public void ParseClamp_ValidText_ReturnsParsedValue()
    {
        var result = NumericToolbarInput.ParseClamp("0.15", min: 0m, max: 60m, fallback: 1m);

        Assert.Equal(0.15m, result);
    }
}
