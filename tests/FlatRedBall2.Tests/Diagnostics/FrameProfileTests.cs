using System;
using FlatRedBall2.Diagnostics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class FrameProfileTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void LastFrame_AfterScreenUpdate_PopulatesPerPhaseTimings()
    {
        // After one Screen.Update, the engine's LastFrame snapshot should reflect non-negative
        // wall-clock values for every per-phase timing Screen owns. UpdateTotalMs / DrawTotalMs /
        // FrameTotalMs are owned by FlatRedBallService.Update / Draw and stay zero here because
        // the test bypasses those (no Game / GraphicsDevice).
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;

        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));

        var profile = engine.LastFrame;
        profile.PhysicsMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.PartitionSortMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.LazySpawnMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.CollisionMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.ActivityMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.TweenMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.ContentWatcherMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.InputMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.AudioMs.ShouldBeGreaterThanOrEqualTo(0);
        profile.GumUpdateMs.ShouldBeGreaterThanOrEqualTo(0);
    }
}
