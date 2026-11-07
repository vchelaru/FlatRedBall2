using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class LibraryChipPlaybackTests
{
    [Fact]
    public void Idle_NothingPlays()
    {
        LibraryChipPlayback.ShouldPlay(autoplayAll: false, hovered: false, sourceCollapsed: false, frameCount: 3).ShouldBeFalse();
    }

    [Fact]
    public void Hovered_PlaysEvenWithAutoplayOff()
    {
        LibraryChipPlayback.ShouldPlay(autoplayAll: false, hovered: true, sourceCollapsed: false, frameCount: 3).ShouldBeTrue();
    }

    [Fact]
    public void Autoplay_PlaysAChipNobodyHovers()
    {
        LibraryChipPlayback.ShouldPlay(autoplayAll: true, hovered: false, sourceCollapsed: false, frameCount: 3).ShouldBeTrue();
    }

    [Fact]
    public void Autoplay_DoesNotTickAChipInACollapsedSource()
    {
        LibraryChipPlayback.ShouldPlay(autoplayAll: true, hovered: false, sourceCollapsed: true, frameCount: 3).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AChainWithFewerThanTwoFrames_HasNothingToPlay(int frameCount)
    {
        LibraryChipPlayback.ShouldPlay(autoplayAll: true, hovered: true, sourceCollapsed: false, frameCount).ShouldBeFalse();
    }
}
