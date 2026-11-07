using AnimationEditor.Core.Navigation;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Navigation;

public class NavigationTrackerTests
{
    private sealed class Rig
    {
        public NavigationLocation? Here;
        public TimeSpan Now = TimeSpan.FromMinutes(10);
        public Func<NavigationLocation, bool> IsValid = _ => true;
        public readonly NavigationHistory History = new();
        public readonly NavigationTracker Tracker;

        public Rig(NavigationLocation? start)
        {
            Here = start;
            Tracker = new NavigationTracker(History, () => Here, l => IsValid(l), () => Now);
        }

        public void UserMoves(NavigationLocation? to)
        {
            using (Tracker.BeginUserAction()) Here = to;
        }

        /// <summary>Steps Back from the current place, which consumes the entry.</summary>
        public NavigationLocation? PeekBack() => History.Back(Here ?? new NavigationLocation(""), _ => true);
    }

    private static NavigationLocation Chain(string name, int? frame = null) =>
        new("hero.achx", ChainName: name, FrameIndex: frame);

    [Fact]
    public void UserAction_ThatMovesTheSelection_PushesTheLocationItLeft()
    {
        var rig = new Rig(Chain("Walk"));

        rig.UserMoves(Chain("Run"));

        rig.PeekBack().ShouldBe(Chain("Walk"));
    }

    [Fact]
    public void UserAction_ThatChangesNothing_PushesNothing()
    {
        var rig = new Rig(Chain("Walk"));

        rig.UserMoves(Chain("Walk"));

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void ChangesOutsideAUserAction_PushNothing()
    {
        var rig = new Rig(Chain("Walk"));

        rig.Here = Chain("Run");

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void NestedUserActions_PushOnceWhenTheOutermostEnds()
    {
        var rig = new Rig(Chain("Walk"));

        using (rig.Tracker.BeginUserAction())
        {
            using (rig.Tracker.BeginUserAction()) rig.Here = Chain("Run");
            rig.Here = Chain("Idle");
        }

        rig.PeekBack().ShouldBe(Chain("Walk"));
        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void ActionsOverlappingOutOfOrder_PushOnceWhenTheLastOneEnds()
    {
        var rig = new Rig(Chain("Walk"));

        var first = rig.Tracker.BeginUserAction();
        var second = rig.Tracker.BeginUserAction();
        rig.Here = Chain("Run");
        first.Dispose();
        rig.History.CanGoBack.ShouldBeFalse();
        second.Dispose();

        rig.PeekBack().ShouldBe(Chain("Walk"));
    }

    [Fact]
    public void FramesOfOneChain_ClickedInQuickSuccession_AreOneEntry()
    {
        var rig = new Rig(Chain("Run"));
        rig.UserMoves(Chain("Walk"));
        rig.Now += TimeSpan.FromMilliseconds(200);
        rig.UserMoves(Chain("Walk", 0));
        rig.Now += TimeSpan.FromMilliseconds(200);
        rig.UserMoves(Chain("Walk", 1));
        rig.Now += TimeSpan.FromMilliseconds(200);
        rig.UserMoves(Chain("Walk", 2));

        rig.History.Back(Chain("Walk", 2), _ => true).ShouldBe(Chain("Run"));
        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void FrameChange_AfterAPause_IsItsOwnEntry()
    {
        var rig = new Rig(Chain("Walk", 0));
        rig.UserMoves(Chain("Walk", 1));
        rig.Now += NavigationCoalescing.QuietWindow + TimeSpan.FromSeconds(1);
        rig.UserMoves(Chain("Walk", 2));

        rig.History.Back(Chain("Walk", 2), _ => true).ShouldBe(Chain("Walk", 1));
    }

    [Fact]
    public void ACoalescedRun_KeepsExtendingWhileTheUserKeepsMoving()
    {
        var rig = new Rig(Chain("Walk", 0));
        for (int frame = 1; frame <= 6; frame++)
        {
            rig.Now += TimeSpan.FromMilliseconds(900);
            rig.UserMoves(Chain("Walk", frame));
        }

        // Only the first move, which came with no earlier change, was recorded.
        rig.History.Back(Chain("Walk", 6), _ => true).ShouldBe(Chain("Walk", 0));
        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Suppress_StopsAMoveInsideItFromPushing()
    {
        var rig = new Rig(Chain("Walk"));

        using (rig.Tracker.Suppress())
        using (rig.Tracker.BeginUserAction())
            rig.Here = Chain("Run");

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Suppress_StartedInsideAnOpenAction_CancelsThatAction()
    {
        var rig = new Rig(Chain("Walk"));

        using (rig.Tracker.BeginUserAction())
        {
            using (rig.Tracker.Suppress()) rig.Here = Chain("Run");
        }

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void AfterSuppress_ALaterUserActionPushesAgain()
    {
        var rig = new Rig(Chain("Walk"));
        using (rig.Tracker.Suppress()) rig.Here = Chain("Run");

        rig.UserMoves(Chain("Idle"));

        rig.PeekBack().ShouldBe(Chain("Run"));
    }

    [Fact]
    public void AfterSuppress_AFrameMoveStartsAFreshRunInsteadOfJoiningTheOldOne()
    {
        var rig = new Rig(Chain("Walk", 0));
        rig.UserMoves(Chain("Walk", 1));
        using (rig.Tracker.Suppress()) rig.Here = Chain("Walk", 3);

        rig.UserMoves(Chain("Walk", 4));

        rig.PeekBack().ShouldBe(Chain("Walk", 3));
    }

    [Fact]
    public void UserAction_LeavingAPlaceThatNoLongerExists_PushesNothing()
    {
        var rig = new Rig(Chain("Old"));
        rig.IsValid = l => l.ChainName != "Old";

        rig.UserMoves(Chain("New"));

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void UserAction_WithNothingOpenBefore_PushesNothing()
    {
        var rig = new Rig(null);

        rig.UserMoves(Chain("Walk"));

        rig.History.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void UserAction_ThatClosesTheLastTab_PushesTheTabItLeft()
    {
        var rig = new Rig(Chain("Walk"));

        rig.UserMoves(null);

        rig.PeekBack().ShouldBe(Chain("Walk"));
    }

    [Fact]
    public void ANewUserMove_AfterGoingBack_DropsForward()
    {
        var rig = new Rig(Chain("Walk"));
        rig.UserMoves(Chain("Run"));
        rig.Here = rig.History.Back(Chain("Run"), _ => true);
        rig.History.CanGoForward.ShouldBeTrue();

        rig.UserMoves(Chain("Idle"));

        rig.History.CanGoForward.ShouldBeFalse();
    }
}
