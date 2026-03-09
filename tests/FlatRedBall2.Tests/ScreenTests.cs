using System;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class ScreenTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void Register_AddsRenderableChildrenToRenderList()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();
        var rect = new AxisAlignedRectangle();
        entity.Add(rect);

        screen.Register(entity);

        screen.RenderList.ShouldContain(rect);
    }

    [Fact]
    public void Register_SetsEngineOnEntity()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();

        screen.Register(entity);

        entity.Engine.ShouldBe(engine);
    }

    [Fact]
    public void Update_CallsCustomActivityOnRegisteredEntities()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new ActivityTrackingEntity();
        screen.Register(entity);
        int expectedActivityCount = 1;

        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));

        entity.ActivityCount.ShouldBe(expectedActivityCount);
    }

    [Fact]
    public void Update_WhenPaused_DoesNotCallEntityCustomActivity()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new ActivityTrackingEntity();
        screen.Register(entity);
        screen.PauseThisScreen();
        int expectedActivityCount = 0;

        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));

        entity.ActivityCount.ShouldBe(expectedActivityCount);
    }

    [Fact]
    public void Update_WhenPaused_StillCallsScreenCustomActivity()
    {
        var engine = new FlatRedBallService();
        var screen = new ScreenActivityTrackingScreen();
        screen.Engine = engine;
        screen.PauseThisScreen();
        int expectedActivityCount = 1;

        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));

        screen.ActivityCount.ShouldBe(expectedActivityCount);
    }

    [Fact]
    public void UnpauseThisScreen_ResumesEntityCustomActivity()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new ActivityTrackingEntity();
        screen.Register(entity);
        screen.PauseThisScreen();
        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));
        screen.UnpauseThisScreen();
        int expectedActivityCount = 1;

        screen.Update(new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero));

        entity.ActivityCount.ShouldBe(expectedActivityCount);
    }

    [Fact]
    public void ScreenTransition_CancelsTimedDelayTask()
    {
        var engine = new FlatRedBallService();
        var task = engine.Time.DelaySeconds(10.0);

        engine.RequestScreenChange<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ScreenTransition_CancelsPredicateDelayTask()
    {
        var engine = new FlatRedBallService();
        var task = engine.Time.DelayUntil(() => false);

        engine.RequestScreenChange<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ScreenTransition_CancelsFrameDelayTask()
    {
        var engine = new FlatRedBallService();
        var task = engine.Time.DelayFrames(100);

        engine.RequestScreenChange<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ScreenTransition_CancelsTasksRegisteredWithScreenToken()
    {
        var engine = new FlatRedBallService();
        var task = engine.Time.DelaySeconds(10.0, engine.CurrentScreen.Token);

        engine.RequestScreenChange<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ScreenTransition_ResetsScreenTime()
    {
        var engine = new FlatRedBallService();
        engine.Update(new Microsoft.Xna.Framework.GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0)));
        engine.Update(new Microsoft.Xna.Framework.GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0)));

        engine.RequestScreenChange<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        engine.Time.CurrentScreenTimeSeconds.ShouldBe(0.0, tolerance: 0.0001);
    }

    private class ActivityTrackingEntity : Entity
    {
        public int ActivityCount { get; private set; }
        public override void CustomActivity(FrameTime time) => ActivityCount++;
    }

    private class ScreenActivityTrackingScreen : Screen
    {
        public int ActivityCount { get; private set; }
        public override void CustomActivity(FrameTime time) => ActivityCount++;
    }
}
