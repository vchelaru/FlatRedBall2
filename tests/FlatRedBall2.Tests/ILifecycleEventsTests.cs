using System;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class ILifecycleEventsTests
{
    private class TrackingEntity : Entity
    {
        public bool InitializedFired { get; private set; }
        public int UpdatedCount { get; private set; }
        public bool DestroyedFired { get; private set; }

        public TrackingEntity()
        {
            Initialized += () => InitializedFired = true;
            Updated += () => UpdatedCount++;
            Destroyed += () => DestroyedFired = true;
        }
    }

    private class TestScreen : Screen { }

    private static (TestScreen screen, Factory<TrackingEntity> factory) MakeScreenAndFactory()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<TrackingEntity>(screen);
        return (screen, factory);
    }

    private static FrameTime OneFrame() =>
        new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f),
            TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Initialized_FiresAfterEntityConstruction()
    {
        var (screen, factory) = MakeScreenAndFactory();

        var entity = factory.Create();

        entity.InitializedFired.ShouldBeTrue();
    }

    [Fact]
    public void Updated_FiresEachFrameDuringUpdate()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        int countBefore = entity.UpdatedCount;

        screen.Update(OneFrame());
        screen.Update(OneFrame());

        entity.UpdatedCount.ShouldBe(countBefore + 2);
    }

    [Fact]
    public void Destroyed_FiresOnEntityDestroy()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        entity.Destroy();

        entity.DestroyedFired.ShouldBeTrue();
    }
}
