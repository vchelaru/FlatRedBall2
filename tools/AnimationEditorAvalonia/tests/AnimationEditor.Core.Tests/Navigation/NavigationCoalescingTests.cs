using AnimationEditor.Core.Navigation;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Navigation;

public class NavigationCoalescingTests
{
    private static readonly TimeSpan Recent = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Long = NavigationCoalescing.QuietWindow + TimeSpan.FromSeconds(1);

    [Fact]
    public void ShouldRecord_FrameMoveExactlyAtTheQuietWindow_Records_AndJustUnderItDoesNot()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 1);
        var arriving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 2);

        NavigationCoalescing.ShouldRecord(leaving, arriving, NavigationCoalescing.QuietWindow).ShouldBeTrue();
        NavigationCoalescing.ShouldRecord(leaving, arriving, NavigationCoalescing.QuietWindow - TimeSpan.FromMilliseconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void ShouldRecord_DifferentTab_AlwaysRecords()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 0);
        var arriving = new NavigationLocation("b.achx", ChainName: "Walk", FrameIndex: 1);

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_DifferentChainInSameTab_Records()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 1);
        var arriving = new NavigationLocation("a.achx", ChainName: "Run", FrameIndex: 1);

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_ChainRowThenItsFrame_IsOneVisit()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk");
        var arriving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 0);

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeFalse();
    }

    [Fact]
    public void ShouldRecord_FrameToFrameOfOneChain_CollapsesWhileTheUserKeepsMoving()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 1);
        var arriving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 2);

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeFalse();
    }

    [Fact]
    public void ShouldRecord_FrameToFrameAfterAPause_Records()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 1);
        var arriving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 2);

        NavigationCoalescing.ShouldRecord(leaving, arriving, Long).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_FrameToFrameWithNoEarlierChange_Records()
    {
        var leaving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 1);
        var arriving = new NavigationLocation("a.achx", ChainName: "Walk", FrameIndex: 2);

        NavigationCoalescing.ShouldRecord(leaving, arriving, sinceLastChange: null).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_SequenceChange_Records()
    {
        var leaving = new NavigationLocation("a.seqj", SequenceIndex: 0, TimelineTrack: 0, TimelineChain: 0);
        var arriving = leaving with { SequenceIndex = 1 };

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_TrackChange_Records()
    {
        var leaving = new NavigationLocation("a.seqj", SequenceIndex: 0, TimelineTrack: 0, TimelineChain: null);
        var arriving = leaving with { TimelineTrack = 1 };

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_ChainWithinTrackChange_Records()
    {
        var leaving = new NavigationLocation("a.seqj", SequenceIndex: 0, TimelineTrack: 0, TimelineChain: 0);
        var arriving = leaving with { TimelineChain = 1 };

        NavigationCoalescing.ShouldRecord(leaving, arriving, Recent).ShouldBeTrue();
    }
}
