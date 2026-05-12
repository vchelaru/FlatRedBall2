using System;
using System.IO;
using FlatRedBall2.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

public class ContentWatcherTests
{
    private class FakeFileWatcher : IFileWatcher
    {
        public event Action? Changed;
        public bool Disposed { get; private set; }
        public void Fire() => Changed?.Invoke();
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Tick_BeforeAnyChange_DoesNotInvokeCallback()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };

        watcher.Tick(DateTime.UtcNow);

        calls.ShouldBe(0);
    }

    [Fact]
    public void Tick_BeforeDebounceWindow_DoesNotInvokeCallback()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(50));

        calls.ShouldBe(0);
    }

    [Fact]
    public void Tick_AfterDebounceWindow_InvokesCallbackOnce()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(200));

        calls.ShouldBe(1);
    }

    [Fact]
    public void MultipleChangesWithinDebounce_CollapseToOneCallback()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.MarkChangedAt(t0 + TimeSpan.FromMilliseconds(40));
        watcher.MarkChangedAt(t0 + TimeSpan.FromMilliseconds(80));

        // Tick at t0 + 90: latest change at t0+80, only 10ms ago — still inside window.
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(90));
        calls.ShouldBe(0);

        // Tick at t0 + 250: latest change is 170ms ago — past window.
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(250));
        calls.ShouldBe(1);
    }

    [Fact]
    public void Tick_AfterCallbackFired_DoesNotFireAgainWithoutNewChange()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(200));
        calls.ShouldBe(1);

        watcher.Tick(t0 + TimeSpan.FromMilliseconds(500));
        calls.ShouldBe(1);
    }

    [Fact]
    public void Callback_ThrowingIOException_RetriesNextWindow()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        bool throwIo = true;
        var watcher = new ContentWatcher(src, () =>
        {
            calls++;
            if (throwIo) throw new IOException("file mid-write");
        }) { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(200));
        calls.ShouldBe(1);

        // Retry: re-marked dirty at the failed tick time. Wait another window.
        throwIo = false;
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(400));
        calls.ShouldBe(2);
    }

    [Fact]
    public void Dispose_DetachesAndDisposesUnderlyingWatcher()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++);

        watcher.Dispose();
        src.Fire();
        watcher.Tick(DateTime.UtcNow + TimeSpan.FromSeconds(10));

        calls.ShouldBe(0);
        src.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void FireFromUnderlyingWatcher_MarksDirtyForNextTick()
    {
        var src = new FakeFileWatcher();
        int calls = 0;
        var watcher = new ContentWatcher(src, () => calls++) { Debounce = TimeSpan.FromMilliseconds(150) };

        src.Fire();
        // Far enough in the future that any reasonable mark will be stale.
        watcher.Tick(DateTime.UtcNow + TimeSpan.FromSeconds(1));

        calls.ShouldBe(1);
    }

    // ---------- Copy delegate ----------

    [Fact]
    public void Tick_AfterDebounce_RunsCopyBeforeCallback()
    {
        var src = new FakeFileWatcher();
        var order = new System.Collections.Generic.List<string>();
        var watcher = new ContentWatcher(src,
            onChanged: () => order.Add("callback"),
            copyToDestination: () => { order.Add("copy"); return true; })
        { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(200));

        order.ShouldBe(new[] { "copy", "callback" });
    }

    [Fact]
    public void Tick_CopyThrowsIOException_RetriesNextWindowAndDoesNotInvokeCallback()
    {
        var src = new FakeFileWatcher();
        int callbacks = 0;
        int copyAttempts = 0;
        bool throwIo = true;
        var watcher = new ContentWatcher(src,
            onChanged: () => callbacks++,
            copyToDestination: () =>
            {
                copyAttempts++;
                if (throwIo) throw new IOException("file mid-write");
                return true;
            })
        { Debounce = TimeSpan.FromMilliseconds(150) };
        var t0 = DateTime.UtcNow;

        watcher.MarkChangedAt(t0);
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(200));

        copyAttempts.ShouldBe(1);
        callbacks.ShouldBe(0); // copy failed → callback skipped

        throwIo = false;
        watcher.Tick(t0 + TimeSpan.FromMilliseconds(400));

        copyAttempts.ShouldBe(2);
        callbacks.ShouldBe(1);
    }
}
