using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The grey text after a sequence's name in the list, in the style of the ANIMATIONS rows' "3 fr · 0.30s".</summary>
public class SequenceListSummaryTests
{
    [Theory]
    [InlineData(2, 0.6, "2 tracks · 0.60s")]
    [InlineData(1, 0.3, "1 track · 0.30s")]
    [InlineData(0, 0, "0 tracks · 0.00s")]
    [InlineData(3, 12.345, "3 tracks · 12.35s")]
    public void Format_CountsTracks_AndShowsTheLengthLikeAChainRow(int tracks, double seconds, string expected) =>
        SequenceListSummary.Format(tracks, seconds).ShouldBe(expected);
}
