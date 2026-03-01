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

        Assert.Single(factory.Instances);
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

        Assert.Empty(factory.Instances);
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

        Assert.Empty(factory.Instances);
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

        Assert.Equal(1, InitTrackingEntity.InitCount);
    }

    private class InitTrackingEntity : Entity
    {
        public static int InitCount;
        public override void CustomInitialize() => InitCount++;
    }
}
