using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class SourceTitleTests
{
    [Theory]
    [InlineData("../ResonatorCoil/ResonatorAnimation.achj", "ResonatorAnimation.achj")]
    [InlineData(@"..\ResonatorCoil\ResonatorAnimation.achj", "ResonatorAnimation.achj")]
    [InlineData(@"chars\sub/hero.achx", "hero.achx")]
    [InlineData("../../hero.achx", "hero.achx")]
    [InlineData("hero.achx", "hero.achx")]
    public void Format_ShowsOnlyTheFileName(string source, string expected) =>
        SourceTitle.Format(source).ShouldBe(expected);

    [Theory]
    [InlineData("chars/")]
    [InlineData("..")]
    [InlineData("")]
    public void Format_WithNoFileName_ReturnsTheSourceAsWritten(string source) =>
        SourceTitle.Format(source).ShouldBe(source);
}
