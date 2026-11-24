using AnimationEditor.Core.Utilities;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NumericEditTests
{
    [Theory]
    [InlineData("3 + 4", 7)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("-4", -4)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("10 / 4", 2.5)]
    [InlineData(" 1.5 ", 1.5)]
    [InlineData("2 * -3", -6)]
    public void TryParse_AbsoluteExpression_EvaluatesIgnoringCurrent(string text, double expected)
    {
        NumericEdit.TryParse(text, out NumericEdit edit).ShouldBeTrue();

        edit.IsRelative.ShouldBeFalse();
        edit.Apply(100m).ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData("+ 4", 14)]
    [InlineData("+4", 14)]
    [InlineData("- 4", 6)]
    [InlineData("* 2", 20)]
    [InlineData("/ 2", 5)]
    [InlineData("* (1 + 2)", 30)]
    public void TryParse_LeadingOperator_AppliesToCurrent(string text, double expected)
    {
        NumericEdit.TryParse(text, out NumericEdit edit).ShouldBeTrue();

        edit.IsRelative.ShouldBeTrue();
        edit.Apply(10m).ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("3 +")]
    [InlineData("(3 + 4")]
    [InlineData("3 4")]
    [InlineData("1 / 0")]
    [InlineData("/ 0")]
    [InlineData("1,5")]
    public void TryParse_InvalidText_ReturnsFalse(string text)
    {
        NumericEdit.TryParse(text, out _).ShouldBeFalse();
    }

    /// <summary>1e30 does not fit the decimal the edit computes in.</summary>
    [Fact]
    public void Apply_CurrentPastDecimal_SetsAbsoluteAndLeavesRelativeUnchanged()
    {
        NumericEdit.Set(3m).Apply(1e30f).ShouldBe(3f);

        NumericEdit.TryParse("+4", out NumericEdit relative).ShouldBeTrue();
        relative.Apply(1e30f).ShouldBe(1e30f);
    }
}
