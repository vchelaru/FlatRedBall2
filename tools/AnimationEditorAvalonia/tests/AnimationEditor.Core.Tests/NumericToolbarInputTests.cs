using AnimationEditor.Core.Utilities;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NumericToolbarInputTests
{
    [Fact]
    public void FormatSpeed_FractionalValue_ReturnsTrimmedDecimal()
    {
        var result = NumericToolbarInput.FormatSpeed(1.25);

        Assert.Equal("1.25", result);
    }

    [Fact]
    public void ParseGridSize_AboveMax_ClampsTo512()
    {
        var result = NumericToolbarInput.ParseGridSize("9999");

        Assert.Equal(512, result);
    }

    [Fact]
    public void ParseGridSize_NonNumericText_FallsBackTo16()
    {
        var result = NumericToolbarInput.ParseGridSize("abc");

        Assert.Equal(16, result);
    }

    [Fact]
    public void ParseGridSize_ValidText_ReturnsParsedValue()
    {
        var result = NumericToolbarInput.ParseGridSize("32");

        Assert.Equal(32, result);
    }

    [Fact]
    public void ParseSpeed_BelowMin_ClampsToPoint1()
    {
        var result = NumericToolbarInput.ParseSpeed("0.01");

        Assert.Equal(0.1, result, precision: 6);
    }

    [Fact]
    public void ParseSpeed_NonNumericText_FallsBackTo1()
    {
        var result = NumericToolbarInput.ParseSpeed("nope");

        Assert.Equal(1.0, result, precision: 6);
    }

    [Fact]
    public void ParseSpeed_ValidText_ReturnsParsedValue()
    {
        var result = NumericToolbarInput.ParseSpeed("2.5");

        Assert.Equal(2.5, result, precision: 6);
    }
}
