using System;
using System.Collections.Generic;
using System.IO;
using FlatRedBall2.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

public class ContentDirectoryWatcherTests
{
    private class FakeDirectoryWatcher : IDirectoryWatcher
    {
        public event Action<string>? Changed;
        public bool Disposed { get; private set; }
        public void Fire(string relativePath) => Changed?.Invoke(relativePath);
        public void Dispose() => Disposed = true;
    }

    private static ContentDirectoryWatcher Make(
        FakeDirectoryWatcher src,
        Action<string> onChanged,
        Func<string, bool>? copyToDestination = null)
        => new ContentDirectoryWatcher(src, onChanged, copyToDestination ?? (_ => true))
        { Debounce = TimeSpan.FromMilliseconds(150) };

    [Fact]
    public void Tick_BeforeAnyChange_DoesNotInvoke()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);

        w.Tick(DateTime.UtcNow);

        calls.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_BeforeDebounceWindow_DoesNotInvoke()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(50));

        calls.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_AfterDebounce_FiresCallbackOncePerDirtyFile()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        w.MarkChangedAt("b.json", t0 + TimeSpan.FromMilliseconds(20));
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        calls.Count.ShouldBe(2);
        calls.ShouldContain("a.json");
        calls.ShouldContain("b.json");
    }

    [Fact]
    public void GlobalDebounce_ExtendsWhenNewFileChangesMidWindow()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        // Tick at t0 + 100: still inside debounce, nothing fires.
        w.Tick(t0 + TimeSpan.FromMilliseconds(100));
        calls.ShouldBeEmpty();

        // New change at t0 + 100 bumps the global last-activity timestamp.
        w.MarkChangedAt("b.json", t0 + TimeSpan.FromMilliseconds(100));

        // Tick at t0 + 200: only 100ms since last activity; still quiet.
        w.Tick(t0 + TimeSpan.FromMilliseconds(200));
        calls.ShouldBeEmpty();

        // Tick at t0 + 300: 200ms since last activity → fire batch for both files.
        w.Tick(t0 + TimeSpan.FromMilliseconds(300));
        calls.Count.ShouldBe(2);
    }

    [Fact]
    public void MultipleEventsForSameFile_CollapseToOneCallback()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        w.MarkChangedAt("a.json", t0 + TimeSpan.FromMilliseconds(20));
        w.MarkChangedAt("a.json", t0 + TimeSpan.FromMilliseconds(40));
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        calls.ShouldBe(new[] { "a.json" });
    }

    [Fact]
    public void Tick_AfterBatchProcessed_DoesNotFireAgainWithoutNewChange()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));
        calls.Count.ShouldBe(1);

        w.Tick(t0 + TimeSpan.FromMilliseconds(500));
        calls.Count.ShouldBe(1);
    }

    [Fact]
    public void CopyThrowsForOneFile_OthersStillProcess_FailedFileRetried()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var failedOnce = new HashSet<string>();
        var w = Make(src,
            calls.Add,
            copyToDestination: relPath =>
            {
                if (relPath == "a.json" && !failedOnce.Contains(relPath))
                {
                    failedOnce.Add(relPath);
                    throw new IOException("locked");
                }
                return true;
            });
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("a.json", t0);
        w.MarkChangedAt("b.json", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        calls.ShouldBe(new[] { "b.json" }); // a.json failed → not invoked

        w.Tick(t0 + TimeSpan.FromMilliseconds(500));
        calls.ShouldContain("a.json");
        calls.Count.ShouldBe(2);
    }

    [Fact]
    public void CopyReturnsFalse_SkipsCallbackForThatFile()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src,
            calls.Add,
            copyToDestination: relPath => relPath != "skip.json");
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("skip.json", t0);
        w.MarkChangedAt("ok.json", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        calls.ShouldBe(new[] { "ok.json" });
    }

    [Fact]
    public void Dispose_DetachesAndDisposesUnderlyingWatcher()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);

        w.Dispose();
        src.Fire("a.json");
        w.Tick(DateTime.UtcNow + TimeSpan.FromSeconds(10));

        calls.ShouldBeEmpty();
        src.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void FireFromUnderlyingWatcher_MarksPathDirty()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);

        src.Fire("zoo.json");
        w.Tick(DateTime.UtcNow + TimeSpan.FromSeconds(1));

        calls.ShouldBe(new[] { "zoo.json" });
    }

    // Auto-reload: when a file whose extension is in AutoReloadExtensions changes, the watcher
    // invokes AutoReloadAction before firing onChanged. The Screen wires this to
    // Engine.Content.TryReload so PNG edits patch existing Texture2D instances without
    // requiring per-game boilerplate in HandleContentChanged.

    [Fact]
    public void AutoReload_PngChanged_InvokesAutoReloadActionThenOnChanged()
    {
        var src = new FakeDirectoryWatcher();
        var order = new List<string>();
        var w = Make(src, rel => order.Add($"onChanged:{rel}"));
        w.AutoReloadAction = rel => order.Add($"reload:{rel}");
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("ship.png", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        order.ShouldBe(new[] { "reload:ship.png", "onChanged:ship.png" });
    }

    [Fact]
    public void AutoReload_NonPngChanged_DoesNotInvokeAutoReloadAction()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var reloads = new List<string>();
        var w = Make(src, calls.Add);
        w.AutoReloadAction = reloads.Add;
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("config.json", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        reloads.ShouldBeEmpty();
        calls.ShouldBe(new[] { "config.json" });
    }

    [Fact]
    public void AutoReload_NullAction_PngChangeStillFiresOnChanged()
    {
        var src = new FakeDirectoryWatcher();
        var calls = new List<string>();
        var w = Make(src, calls.Add);
        // No AutoReloadAction set.
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("ship.png", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        calls.ShouldBe(new[] { "ship.png" });
    }

    [Fact]
    public void AutoReload_CopyFails_AutoReloadNotInvoked()
    {
        var src = new FakeDirectoryWatcher();
        var reloads = new List<string>();
        var w = Make(src, _ => { }, copyToDestination: _ => false);
        w.AutoReloadAction = reloads.Add;
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("ship.png", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        reloads.ShouldBeEmpty();
    }

    [Fact]
    public void AutoReload_CaseInsensitiveExtensionMatch()
    {
        var src = new FakeDirectoryWatcher();
        var reloads = new List<string>();
        var w = Make(src, _ => { });
        w.AutoReloadAction = reloads.Add;
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("SHIP.PNG", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        reloads.ShouldBe(new[] { "SHIP.PNG" });
    }

    [Fact]
    public void AutoReload_CustomExtension_InvokesAction()
    {
        var src = new FakeDirectoryWatcher();
        var reloads = new List<string>();
        var w = Make(src, _ => { });
        w.AutoReloadExtensions.Add(".achx");
        w.AutoReloadAction = reloads.Add;
        var t0 = DateTime.UtcNow;

        w.MarkChangedAt("player.achx", t0);
        w.Tick(t0 + TimeSpan.FromMilliseconds(250));

        reloads.ShouldBe(new[] { "player.achx" });
    }
}
