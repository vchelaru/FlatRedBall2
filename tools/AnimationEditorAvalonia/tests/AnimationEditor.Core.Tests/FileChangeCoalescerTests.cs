using AnimationEditor.Core.HotReload;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FileChangeCoalescerTests
{
    private static FileChangeCoalescer Make(long debounceMs = 200, long cooldownMs = 500, long atomicMs = 100)
    {
        return new FileChangeCoalescer
        {
            DebounceMs = debounceMs,
            CooldownMs = cooldownMs,
            AtomicWriteMs = atomicMs
        };
    }

    // 1. Event not yet past debounce window doesn't fire.
    [Fact]
    public void Debounce_EventWithinWindow_DoesNotFire()
    {
        var c = Make(debounceMs: 200);
        c.Record("a.png", WatcherChangeType.Modified, 0);
        var result = c.Flush(100);  // only 100ms elapsed, window is 200
        Assert.Empty(result);
    }

    // 1b. Event past the debounce window fires.
    [Fact]
    public void Debounce_EventPastWindow_Fires()
    {
        var c = Make(debounceMs: 200);
        c.Record("a.png", WatcherChangeType.Modified, 0);
        var result = c.Flush(201);
        Assert.Single(result);
        Assert.Equal("a.png", result[0].Path);
        Assert.Equal(WatcherChangeType.Modified, result[0].Type);
    }

    // 2. Second event on same path within debounce window extends deadline.
    [Fact]
    public void Debounce_ResetOnSecondEvent_ExtendDeadline()
    {
        var c = Make(debounceMs: 200);
        c.Record("a.png", WatcherChangeType.Modified, 0);
        c.Record("a.png", WatcherChangeType.Modified, 150);  // reset window
        var result = c.Flush(250);  // 250ms from first; only 100ms from second
        Assert.Empty(result);  // not yet ready — second event reset the debounce
    }

    // 2b. After reset, fires once second debounce expires.
    [Fact]
    public void Debounce_ResetOnSecondEvent_FiresAfterNewWindowExpires()
    {
        var c = Make(debounceMs: 200);
        c.Record("a.png", WatcherChangeType.Modified, 0);
        c.Record("a.png", WatcherChangeType.Modified, 150);
        var result = c.Flush(360);  // 360-150=210 > 200 → ready
        Assert.Single(result);
    }

    // 3. Delete + Create within atomic-write window → Modified.
    [Fact]
    public void AtomicWrite_DeleteThenCreateWithinWindow_CoalescesToModified()
    {
        var c = Make(atomicMs: 100);
        c.Record("a.png", WatcherChangeType.Deleted, 0);
        c.Record("a.png", WatcherChangeType.Created, 50);   // within 100ms
        // Flush past debounce
        var result = c.Flush(1000);
        Assert.Single(result);
        Assert.Equal(WatcherChangeType.Modified, result[0].Type);
    }

    // 4. Delete + Create too far apart → Delete fires as Deleted, Create as Created.
    [Fact]
    public void AtomicWrite_DeleteThenCreateOutsideWindow_FiringAsSeparate()
    {
        var c = Make(debounceMs: 50, atomicMs: 100);
        c.Record("a.png", WatcherChangeType.Deleted, 0);
        // Flush after atomic window expired but before Create arrives
        var afterDelete = c.Flush(200);  // 200ms > 100ms atomic window → delete promoted
        c.Record("a.png", WatcherChangeType.Created, 300);
        var afterCreate = c.Flush(400);  // 400-300=100 >= 50 debounce

        Assert.Single(afterDelete);
        Assert.Equal(WatcherChangeType.Deleted, afterDelete[0].Type);
        Assert.Single(afterCreate);
        Assert.Equal(WatcherChangeType.Created, afterCreate[0].Type);
    }

    // 5. RecordOwnSave suppresses events during cooldown.
    [Fact]
    public void Cooldown_OwnSaveSuppressesEvents()
    {
        var c = Make(debounceMs: 50, cooldownMs: 500);
        c.RecordOwnSave("a.achx", 0);
        c.Record("a.achx", WatcherChangeType.Modified, 10);
        var result = c.Flush(100);  // within cooldown window
        Assert.Empty(result);
    }

    // 6. After cooldown expires, events fire normally.
    [Fact]
    public void Cooldown_AfterExpiry_EventsFire()
    {
        var c = Make(debounceMs: 50, cooldownMs: 500);
        c.RecordOwnSave("a.achx", 0);
        c.Record("a.achx", WatcherChangeType.Modified, 600);  // after cooldown
        var result = c.Flush(700);  // 700-600=100 >= 50 debounce
        Assert.Single(result);
    }

    // 7. Multiple files tracked independently.
    [Fact]
    public void MultipleFiles_TrackedIndependently()
    {
        var c = Make(debounceMs: 200);
        c.Record("a.png", WatcherChangeType.Modified, 0);
        c.Record("b.png", WatcherChangeType.Modified, 0);
        var result = c.Flush(300);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Path == "a.png");
        Assert.Contains(result, r => r.Path == "b.png");
    }

    // 8. Deleted file past debounce fires as Deleted.
    [Fact]
    public void Deleted_PastDebounceWindow_FiresAsDeleted()
    {
        var c = Make(debounceMs: 200, atomicMs: 100);
        c.Record("a.png", WatcherChangeType.Deleted, 0);
        var result = c.Flush(400);  // 400ms > 100ms atomic + 200ms debounce
        Assert.Single(result);
        Assert.Equal(WatcherChangeType.Deleted, result[0].Type);
    }
}
