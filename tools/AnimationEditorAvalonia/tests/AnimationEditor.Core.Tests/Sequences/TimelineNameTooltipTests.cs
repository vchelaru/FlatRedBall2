using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A track name in the timeline's name column gets a tooltip only when the column cut it short.</summary>
public class TimelineNameTooltipTests
{
    [Fact]
    public void NameWider_ThanTheRoom_IsTruncated()
    {
        TimelineTooltips.IsTruncated(textWidth: 120, availableWidth: 90).ShouldBeTrue();
    }

    [Fact]
    public void NameThatFits_OrFillsTheRoomExactly_IsNotTruncated()
    {
        TimelineTooltips.IsTruncated(textWidth: 60, availableWidth: 90).ShouldBeFalse();
        TimelineTooltips.IsTruncated(textWidth: 90, availableWidth: 90).ShouldBeFalse();
    }

    [Fact]
    public void WideningTheColumn_StopsTheTruncation()
    {
        double room(double column) => TimelineTooltips.NameRoom(column);
        TimelineTooltips.IsTruncated(120, room(100)).ShouldBeTrue();
        TimelineTooltips.IsTruncated(120, room(400)).ShouldBeFalse();
    }

    [Fact]
    public void NameRoom_IsTheColumn_MinusTheEyeSideAndTheRightPadding()
    {
        TimelineTooltips.NameRoom(200).ShouldBe(200 - TimelineLayout.HeaderNameLeft - TimelineTooltips.NameRightPadding);
    }

    [Fact]
    public void ShowDelay_IsShorterThanTheDefaultSecond_ButNotInstant()
    {
        TimelineTooltips.ShowDelay.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(100));
        TimelineTooltips.ShowDelay.ShouldBeLessThan(TimeSpan.FromMilliseconds(400));
    }
}
