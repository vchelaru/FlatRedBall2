using System;
using System.Collections.Generic;
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

    private class ConfigurableTestScreen : Screen
    {
        public int X { get; set; }
        public List<string> Lifecycle { get; } = new();
        public override void CustomInitialize() => Lifecycle.Add("init");
        public override void CustomDestroy() => Lifecycle.Add("destroy");
    }

    [Fact]
    public void RestartScreen_RecreatesSameScreenType()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>();
        var original = engine.CurrentScreen;

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        engine.CurrentScreen.ShouldBeOfType<ConfigurableTestScreen>();
        engine.CurrentScreen.ShouldNotBeSameAs(original);
    }

    [Fact]
    public void RestartScreen_ReplaysOriginalConfigureCallback_IgnoresMidGameMutation()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>(s => s.X = 3);
        ((ConfigurableTestScreen)engine.CurrentScreen).X = 99;

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(3);
    }

    [Fact]
    public void RestartScreen_NoConfigure_PropertiesReturnToTypeDefaults()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>();
        ((ConfigurableTestScreen)engine.CurrentScreen).X = 99;

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(0);
    }

    [Fact]
    public void RestartScreen_WithNewConfigure_AppliesIt()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>(s => s.X = 3);

        ((ConfigurableTestScreen)engine.CurrentScreen).RestartScreen(s => s.X = 7);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(7);
    }

    [Fact]
    public void RestartScreen_WithNewConfigure_ReplacesRetainedConfigureForFutureRestarts()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>(s => s.X = 3);

        ((ConfigurableTestScreen)engine.CurrentScreen).RestartScreen(s => s.X = 7);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        // Plain RestartScreen() now replays the most recent configure, not the original.
        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(7);
    }

    [Fact]
    public void RestartScreen_RunsCustomDestroyOnOldThenCustomInitializeOnNew()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>();
        var original = (ConfigurableTestScreen)engine.CurrentScreen;
        original.Lifecycle.Clear();

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        original.Lifecycle.ShouldBe(new[] { "destroy" });
        ((ConfigurableTestScreen)engine.CurrentScreen).Lifecycle.ShouldBe(new[] { "init" });
    }

    [Fact]
    public void RestartScreen_DeferredUntilNextFrame()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>();
        var original = engine.CurrentScreen;

        engine.CurrentScreen.RestartScreen();

        engine.CurrentScreen.ShouldBeSameAs(original);
    }

    [Fact]
    public void RestartScreen_PreservesCallbackAcrossMultipleRestarts()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>(s => s.X = 3);

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        ((ConfigurableTestScreen)engine.CurrentScreen).X = 99;
        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(3);
    }

    [Fact]
    public void RestartScreen_AfterMoveToScreen_ReplaysMostRecentConfigure()
    {
        var engine = new FlatRedBallService();
        engine.Start<ConfigurableTestScreen>(s => s.X = 1);
        engine.CurrentScreen.MoveToScreen<ConfigurableTestScreen>(s => s.X = 5);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(5);
    }

    // Closure-staleness probes: these PIN current behavior so we notice if it changes.
    // A configure callback is an Action<T> that closes over its enclosing scope.
    // C# closures capture variables by reference, so values read inside the callback
    // reflect the variable's value AT INVOCATION TIME, not at definition time.

    [Fact]
    public void RestartScreen_ClosureCapturedLocal_RestartReadsCurrentValueNotOriginal()
    {
        var engine = new FlatRedBallService();
        int captured = 5;
        engine.Start<ConfigurableTestScreen>(s => s.X = captured);
        captured = 99;

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(99);
    }

    [Fact]
    public void RestartScreen_ClosureCapturedMutableReference_RestartReadsMutatedState()
    {
        var engine = new FlatRedBallService();
        var box = new[] { 5 };
        engine.Start<ConfigurableTestScreen>(s => s.X = box[0]);
        box[0] = 99;

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((ConfigurableTestScreen)engine.CurrentScreen).X.ShouldBe(99);
    }

    // ---------- Increment 2: Hot-reload restart ----------

    private class HotReloadTrackingScreen : Screen
    {
        public int Score { get; set; }
        public List<string> Lifecycle { get; } = new();

        public override void CustomInitialize() => Lifecycle.Add("init");
        public override void CustomDestroy() => Lifecycle.Add("destroy");

        public override void SaveHotReloadState(HotReloadState state)
        {
            Lifecycle.Add("save");
            state.Set("score", Score);
        }

        public override void RestoreHotReloadState(HotReloadState state)
        {
            Lifecycle.Add("restore");
            Score = state.Get<int>("score");
        }
    }

    [Fact]
    public void HotReloadState_RoundTripsTypedValues()
    {
        var state = new HotReloadState();
        state.Set("score", 42);
        state.Set("name", "alice");

        state.Get<int>("score").ShouldBe(42);
        state.Get<string>("name").ShouldBe("alice");
    }

    [Fact]
    public void HotReloadState_TryGet_ReturnsFalseWhenMissing()
    {
        var state = new HotReloadState();
        state.TryGet<int>("missing", out var value).ShouldBeFalse();
        value.ShouldBe(0);
    }

    [Fact]
    public void HotReloadState_Get_ThrowsWhenMissing()
    {
        var state = new HotReloadState();
        Should.Throw<KeyNotFoundException>(() => state.Get<int>("missing"));
    }

    [Fact]
    public void RestartScreen_DeathRetry_DoesNotCallSaveOrRestore()
    {
        var engine = new FlatRedBallService();
        engine.Start<HotReloadTrackingScreen>();
        var original = (HotReloadTrackingScreen)engine.CurrentScreen;
        original.Lifecycle.Clear();

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        original.Lifecycle.ShouldNotContain("save");
        ((HotReloadTrackingScreen)engine.CurrentScreen).Lifecycle.ShouldNotContain("restore");
    }

    [Fact]
    public void RestartScreen_HotReload_CallsSaveOnOldInstanceBeforeDestroy()
    {
        var engine = new FlatRedBallService();
        engine.Start<HotReloadTrackingScreen>();
        var original = (HotReloadTrackingScreen)engine.CurrentScreen;
        original.Lifecycle.Clear();

        engine.CurrentScreen.RestartScreen(RestartMode.HotReload);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        original.Lifecycle.ShouldBe(new[] { "save", "destroy" });
    }

    [Fact]
    public void RestartScreen_HotReload_CallsRestoreOnNewInstanceAfterCustomInitialize()
    {
        var engine = new FlatRedBallService();
        engine.Start<HotReloadTrackingScreen>();

        engine.CurrentScreen.RestartScreen(RestartMode.HotReload);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((HotReloadTrackingScreen)engine.CurrentScreen).Lifecycle.ShouldBe(new[] { "init", "restore" });
    }

    [Fact]
    public void RestartScreen_HotReload_RoundTripsCustomState()
    {
        var engine = new FlatRedBallService();
        engine.Start<HotReloadTrackingScreen>();
        ((HotReloadTrackingScreen)engine.CurrentScreen).Score = 47;

        engine.CurrentScreen.RestartScreen(RestartMode.HotReload);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((HotReloadTrackingScreen)engine.CurrentScreen).Score.ShouldBe(47);
    }

    [Fact]
    public void RestartScreen_HotReload_StillReplaysRetainedConfigure()
    {
        var engine = new FlatRedBallService();
        engine.Start<HotReloadTrackingScreen>(s => s.Score = 3);
        // Mid-game, score gets bumped:
        ((HotReloadTrackingScreen)engine.CurrentScreen).Score = 99;

        engine.CurrentScreen.RestartScreen(RestartMode.HotReload);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        // Configure runs (Score=3), then CustomInitialize, then Restore overwrites with saved 99.
        ((HotReloadTrackingScreen)engine.CurrentScreen).Score.ShouldBe(99);
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
