using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The text on a timeline block: name, repeats and length, dropped in that reverse order as the block narrows.</summary>
public class BlockLabelTests
{
    // One pixel per character keeps the widths readable.
    private static double Chars(string text) => text.Length;

    [Theory]
    [InlineData("Walk", 2, 1.2, "Walk ×2 (1.20 s)")]
    [InlineData("Walk", 1, 0.5, "Walk (0.50 s)")]
    [InlineData("Walk", 2.5, 1.0 / 3, "Walk ×2.5 (0.33 s)")]
    [InlineData("Walk", 3, 0.30000000000000004, "Walk ×3 (0.30 s)")]
    public void Full_ShowsNameLoopsAndSeconds_HidingASingleLoop(string name, double loops, double seconds, string expected) =>
        BlockLabel.Full(name, loops, seconds).ShouldBe(expected);

    [Fact]
    public void Tooltip_AlwaysShowsTheLoopCount() =>
        BlockLabel.Tooltip("Walk", 1, 0.5).ShouldBe("Walk ×1 (0.50 s)");

    [Fact]
    public void Fit_WideBlock_ShowsEverything() =>
        BlockLabel.Fit("Walk", 2, 1.2, 100, Chars).ShouldBe("Walk ×2 (1.20 s)");

    [Fact]
    public void Fit_DropsTheLengthFirst() =>
        BlockLabel.Fit("Walk", 2, 1.2, 10, Chars).ShouldBe("Walk ×2");

    [Fact]
    public void Fit_ThenDropsTheLoopCount() =>
        BlockLabel.Fit("Walk", 2, 1.2, 5, Chars).ShouldBe("Walk");

    [Fact]
    public void Fit_ThenLeavesTheNameForTheCallerToTruncate() =>
        BlockLabel.Fit("Walk", 2, 1.2, 2, Chars).ShouldBe("Walk");

    [Fact]
    public void Fit_SingleLoop_SkipsTheLoopStep() =>
        BlockLabel.Fit("Walk", 1, 0.5, 12, Chars).ShouldBe("Walk");

    [Fact]
    public void Fit_ExactWidth_Fits() =>
        BlockLabel.Fit("Walk", 2, 1.2, 16, Chars).ShouldBe("Walk ×2 (1.20 s)");
}
