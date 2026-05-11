using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall.Content.Math.Geometry;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class ApplicationEventsTests
{
    // ── AnimationChainsChanged ────────────────────────────────────────────────

    [Fact]
    public void RaiseAnimationChainsChanged_FiresEvent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.AnimationChainsChanged += () => fired = true;

        ctx.ApplicationEvents.RaiseAnimationChainsChanged();

        Assert.True(fired);
    }

    [Fact]
    public void RaiseAnimationChainsChanged_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        // No subscribers — should silently succeed
        var ex = Record.Exception(() => ctx.ApplicationEvents.RaiseAnimationChainsChanged());

        Assert.Null(ex);
    }

    [Fact]
    public void RaiseAnimationChainsChanged_NotifiesMultipleSubscribers()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int count = 0;
        ctx.ApplicationEvents.AnimationChainsChanged += () => count++;
        ctx.ApplicationEvents.AnimationChainsChanged += () => count++;

        ctx.ApplicationEvents.RaiseAnimationChainsChanged();

        Assert.Equal(2, count);
    }

    // ── AfterAxisAlignedRectangleChanged ──────────────────────────────────────

    [Fact]
    public void RaiseAfterAxisAlignedRectangleChanged_PassesRectangleToHandler()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        AxisAlignedRectangleSave? received = null;
        ctx.ApplicationEvents.AfterAxisAlignedRectangleChanged += r => received = r;
        var rect = new AxisAlignedRectangleSave { Name = "TestRect" };

        ctx.ApplicationEvents.RaiseAfterAxisAlignedRectangleChanged(rect);

        Assert.Same(rect, received);
    }

    [Fact]
    public void RaiseAfterAxisAlignedRectangleChanged_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() =>
            ctx.ApplicationEvents.RaiseAfterAxisAlignedRectangleChanged(
                new AxisAlignedRectangleSave()));

        Assert.Null(ex);
    }

    // ── AfterCircleChanged ────────────────────────────────────────────────────

    [Fact]
    public void RaiseAfterCircleChanged_PassesCircleToHandler()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        CircleSave? received = null;
        ctx.ApplicationEvents.AfterCircleChanged += c => received = c;
        var circle = new CircleSave { Name = "TestCircle", Radius = 10 };

        ctx.ApplicationEvents.RaiseAfterCircleChanged(circle);

        Assert.Same(circle, received);
    }

    [Fact]
    public void RaiseAfterCircleChanged_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() =>
            ctx.ApplicationEvents.RaiseAfterCircleChanged(new CircleSave()));

        Assert.Null(ex);
    }

    // ── AchxLoaded ────────────────────────────────────────────────────────────

    [Fact]
    public void CallAchxLoaded_PassesFileNameToHandler()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        string? received = null;
        ctx.ApplicationEvents.AchxLoaded += f => received = f;

        ctx.ApplicationEvents.CallAchxLoaded("C:/Game/hero.achx");

        Assert.Equal("C:/Game/hero.achx", received);
    }

    [Fact]
    public void CallAchxLoaded_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() =>
            ctx.ApplicationEvents.CallAchxLoaded("some.achx"));

        Assert.Null(ex);
    }

    // ── AfterZoomChange ───────────────────────────────────────────────────────

    [Fact]
    public void CallAfterZoomChange_FiresEvent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.AfterZoomChange += () => fired = true;

        ctx.ApplicationEvents.CallAfterZoomChange();

        Assert.True(fired);
    }

    [Fact]
    public void CallAfterZoomChange_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() => ctx.ApplicationEvents.CallAfterZoomChange());

        Assert.Null(ex);
    }

    // ── WireframePanning ──────────────────────────────────────────────────────

    [Fact]
    public void CallAfterWireframePanning_FiresEvent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.WireframePanning += () => fired = true;

        ctx.ApplicationEvents.CallAfterWireframePanning();

        Assert.True(fired);
    }

    [Fact]
    public void CallAfterWireframePanning_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() => ctx.ApplicationEvents.CallAfterWireframePanning());

        Assert.Null(ex);
    }

    // ── WireframeTextureChange ────────────────────────────────────────────────

    [Fact]
    public void CallWireframeTextureChange_FiresEvent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.ApplicationEvents.WireframeTextureChange += () => fired = true;

        ctx.ApplicationEvents.CallWireframeTextureChange();

        Assert.True(fired);
    }

    [Fact]
    public void CallWireframeTextureChange_WithNoSubscribers_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ex = Record.Exception(() => ctx.ApplicationEvents.CallWireframeTextureChange());

        Assert.Null(ex);
    }
}
