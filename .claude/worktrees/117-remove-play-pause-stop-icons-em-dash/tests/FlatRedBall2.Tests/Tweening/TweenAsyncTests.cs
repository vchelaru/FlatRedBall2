using System;
using System.Threading.Tasks;
using FlatRedBall2.Tweening;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class TweenAsyncTests
{
    private class TestEntity : Entity { }
    private class TestScreen : Screen { }

    private static (TestScreen screen, Factory<TestEntity> factory) MakeScreenAndFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);
        return (screen, factory);
    }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public async Task TweenAsync_ColorOverload_TerminalValueObservedBeforeAwaitResumes()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Black;
        var task = entity.TweenAsync(c => last = c, Color.Red, Color.Blue, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));
        await task;

        last.ShouldBe(Color.Blue);
    }

    [Fact]
    public async Task TweenAsync_EntityDestroyedMidTween_ThrowsTaskCanceled()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        var task = entity.TweenAsync(_ => { }, 0f, 1f, TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));
        entity.Destroy();

        await Should.ThrowAsync<TaskCanceledException>(async () => await task);
    }

    [Fact]
    public void TweenAsync_NullSetter_ThrowsArgumentNullException()
    {
        var (_, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        Should.Throw<ArgumentNullException>(() =>
            entity.TweenAsync((Action<float>)null!, 0f, 1f, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TweenAsync_NaturalCompletion_SetterSnapsToBeforeAwaitResumes()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float last = float.NaN;
        var task = entity.TweenAsync(v => last = v, 0f, 10f, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));
        await task;

        last.ShouldBe(10f);
    }

    [Fact]
    public async Task TweenAsync_ScreenDestroyedMidTween_ThrowsTaskCanceled()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var task = screen.TweenAsync(_ => { }, 0f, 1f, TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));
        // StopAllTweens mirrors what screen teardown does for tween cleanup.
        screen.StopAllTweens();

        await Should.ThrowAsync<TaskCanceledException>(async () => await task);
    }

    [Fact]
    public async Task TweenAsync_Vector2Overload_TerminalValueObservedBeforeAwaitResumes()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Vector2 last = Vector2.Zero;
        var task = entity.TweenAsync(v => last = v, new Vector2(1, 2), new Vector2(7, 9), TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));
        await task;

        last.ShouldBe(new Vector2(7, 9));
    }
}
