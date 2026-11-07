using AnimationEditor.Core.Navigation;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Navigation;

public class NavigationHistoryTests
{
    private static NavigationLocation At(string path, string? chain = null) => new(path, ChainName: chain);

    [Fact]
    public void Back_AfterPush_ReturnsPushedLocation_AndFillsForward()
    {
        var history = new NavigationHistory();
        history.Push(At("a.seqj", "Walk"));

        var target = history.Back(At("b.achx", "Walk"), _ => true);

        target.ShouldBe(At("a.seqj", "Walk"));
        history.CanGoBack.ShouldBeFalse();
        history.CanGoForward.ShouldBeTrue();
    }

    [Fact]
    public void Forward_AfterBack_ReturnsTheLocationBackLeft()
    {
        var history = new NavigationHistory();
        history.Push(At("a.seqj"));
        history.Back(At("b.achx", "Run"), _ => true);

        var target = history.Forward(At("a.seqj"), _ => true);

        target.ShouldBe(At("b.achx", "Run"));
        history.CanGoBack.ShouldBeTrue();
    }

    [Fact]
    public void Push_AfterBack_TruncatesForward()
    {
        var history = new NavigationHistory();
        history.Push(At("a"));
        history.Back(At("b"), _ => true);

        history.Push(At("c"));

        history.CanGoForward.ShouldBeFalse();
    }

    [Fact]
    public void Back_WhenEmpty_ReturnsNull()
    {
        new NavigationHistory().Back(At("a"), _ => true).ShouldBeNull();
    }

    [Fact]
    public void Back_SkipsInvalidEntries_AndDiscardsThem()
    {
        var history = new NavigationHistory();
        history.Push(At("good"));
        history.Push(At("gone1"));
        history.Push(At("gone2"));

        var target = history.Back(At("now"), location => location.Path == "good");

        target.ShouldBe(At("good"));
        history.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Back_WhenNoEntryIsValid_ReturnsNull_AndLeavesForwardAlone()
    {
        var history = new NavigationHistory();
        history.Push(At("gone"));

        history.Back(At("now"), _ => false).ShouldBeNull();

        history.CanGoBack.ShouldBeFalse();
        history.CanGoForward.ShouldBeFalse();
    }

    [Fact]
    public void Push_SameAsTop_IsIgnored()
    {
        var history = new NavigationHistory();
        history.Push(At("a", "Walk"));
        history.Push(At("a", "Walk"));

        history.Back(At("b"), _ => true).ShouldNotBeNull();
        history.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Push_PastCapacity_DropsTheOldest()
    {
        var history = new NavigationHistory(capacity: 3);
        for (int i = 0; i < 5; i++)
            history.Push(At("p" + i));

        history.Back(At("now"), _ => true).ShouldBe(At("p4"));
        history.Back(At("p4"), _ => true).ShouldBe(At("p3"));
        history.Back(At("p3"), _ => true).ShouldBe(At("p2"));
        history.Back(At("p2"), _ => true).ShouldBeNull();
    }

    [Fact]
    public void Forward_WhenEmpty_ReturnsNull()
    {
        new NavigationHistory().Forward(At("a"), _ => true).ShouldBeNull();
    }

    [Fact]
    public void Changed_RaisesOnPush_AndOnASuccessfulStep()
    {
        var history = new NavigationHistory();
        int raised = 0;
        history.Changed += () => raised++;

        history.Push(At("a.seqj"));
        history.Back(At("b.achx"), _ => true);
        history.Back(At("a.seqj"), _ => true);

        raised.ShouldBe(2);
    }
}
