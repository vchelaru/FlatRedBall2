using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FactoryTests
{
    private class TestEntity : Entity { }
    private class TestScreen : Screen { }

    [Fact]
    public void Create_AddsToInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        factory.Create();

        factory.Instances.ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_CallsCustomInitialize()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<InitTrackingEntity>(screen);
        InitTrackingEntity.InitCount = 0;

        factory.Create();

        InitTrackingEntity.InitCount.ShouldBe(1);
    }

    [Fact]
    public void Destroy_CalledDirectlyOnEntity_RemovesFromFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();
        entity.Destroy(); // no factory reference needed

        factory.Instances.ShouldBeEmpty();
    }

    [Fact]
    public void Destroy_DuringEnumeration_DoesNotThrow()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);

        var e1 = factory.Create();
        factory.Create();

        // Simulates destroying inside CollisionOccurred while the collision loop iterates the factory
        Should.NotThrow(() =>
        {
            foreach (var _ in factory)
                factory.Destroy(e1);
        });

        factory.Instances.Count.ShouldBe(1);
    }

    [Fact]
    public void Destroy_RemovesFromInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();
        factory.Destroy(entity);

        factory.Instances.ShouldBeEmpty();
    }

    [Fact]
    public void DestroyAll_ClearsAllInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        factory.Create();
        factory.Create();
        factory.Create();
        factory.DestroyAll();

        factory.Instances.ShouldBeEmpty();
    }

    private class InitTrackingEntity : Entity
    {
        public static int InitCount;
        public override void CustomInitialize() => InitCount++;
    }
}
