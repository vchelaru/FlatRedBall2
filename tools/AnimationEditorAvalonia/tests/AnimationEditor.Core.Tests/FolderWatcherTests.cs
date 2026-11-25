using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FolderWatcherTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static bool PathsEqual(string a, string b) =>
        a.Replace('\\', '/').Equals(b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static async Task<IReadOnlyList<(string Path, WatcherChangeType Type)>?> WaitForChangeAsync(FolderWatcher watcher)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<(string Path, WatcherChangeType Type)>>();
        watcher.Changed += changes => tcs.TrySetResult(changes);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(WaitTimeout));
        return completed == tcs.Task ? tcs.Task.Result : null;
    }

    // Windows stops a watcher whose folder goes away; a share that drops out and comes back does the same.
    [Fact]
    public async Task RestartIfStopped_FolderCameBack_IsWatchedAgain()
    {
        if (!OperatingSystem.IsWindows()) return; // only Windows stops the watcher
        string dir = Directory.CreateTempSubdirectory("ae-watch").FullName;
        using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
        var stopped = new TaskCompletionSource();
        var changed = new TaskCompletionSource();
        watcher.Overflowed += () => stopped.TrySetResult();
        watcher.Changed += _ => changed.TrySetResult();
        watcher.Watch(dir);
        Directory.Delete(dir);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        while (Directory.Exists(dir)) await Task.Delay(10);
        Directory.CreateDirectory(dir);
        await Task.Delay(50);

        watcher.RestartIfStopped();
        File.WriteAllText(Path.Combine(dir, "a.achx"), "");

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        watcher.Dispose();
        Directory.Delete(dir, recursive: true);
    }

    /// <summary>
    /// #2140: <c>rm -rf</c> then an unzip. The editor calls <see cref="FolderWatcher.RestartIfStopped"/> on every change.
    /// Windows does not let the folder be made again before the watcher stopped, the test above.
    /// </summary>
    [Fact]
    public async Task RestartIfStopped_FolderDeletedAndMadeAgainAtOnce_IsWatchedAgain()
    {
        if (OperatingSystem.IsWindows()) return;
        string dir = Directory.CreateTempSubdirectory("ae-watch").FullName;
        File.WriteAllText(Path.Combine(dir, "a.achx"), "");
        using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
        var seen = new System.Collections.Concurrent.ConcurrentQueue<string>();
        watcher.Overflowed += () => { seen.Enqueue("overflowed"); watcher.RestartIfStopped(); };
        watcher.Changed += changes =>
        {
            foreach (var change in changes) seen.Enqueue($"{change.Type} {Path.GetFileName(change.Path)}");
            watcher.RestartIfStopped();
        };
        watcher.Watch(dir);
        seen.Enqueue($"created {Directory.GetCreationTimeUtc(dir):mm:ss.fffffff}");
        await Task.Delay(50);

        Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        seen.Enqueue($"created {Directory.GetCreationTimeUtc(dir):mm:ss.fffffff}");
        File.WriteAllText(Path.Combine(dir, "a.achx"), "");
        await Task.Delay(1000);
        seen.Enqueue($"created {Directory.GetCreationTimeUtc(dir):mm:ss.fffffff}");
        File.WriteAllText(Path.Combine(dir, "b.achx"), "");
        await Task.Delay(1000);

        Directory.Delete(dir, recursive: true);
        Assert.True(seen.Any(e => e.EndsWith("b.achx")), string.Join("; ", seen));
    }

    [Fact]
    public async Task Changed_DeletingMatchingFile_RaisesDeleted()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var file = Path.Combine(root, "hero.achx");
            File.WriteAllText(file, "");

            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            File.Delete(file);

            var changes = await WaitForChangeAsync(watcher);
            Assert.NotNull(changes);
            Assert.Contains(changes!, c => PathsEqual(c.Path, file) && c.Type == WatcherChangeType.Deleted);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Changed_FileInSubdirectory_Raised()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var subDir = Path.Combine(root, "Entities");
            Directory.CreateDirectory(subDir);

            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            var file = Path.Combine(subDir, "hero.achx");
            File.WriteAllText(file, "");

            var changes = await WaitForChangeAsync(watcher);
            Assert.NotNull(changes);
            Assert.Contains(changes!, c => PathsEqual(c.Path, file));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Changed_MatchingFileModified_RaisesModified()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var file = Path.Combine(root, "hero.achx");
            File.WriteAllText(file, "");

            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            File.WriteAllText(file, "changed");

            var changes = await WaitForChangeAsync(watcher);
            Assert.NotNull(changes);
            // macOS FSEvents keeps a file's recent "created" flag on its next event, so a write
            // right after creating the file arrives as Created there. Consumers rescan either way.
            Assert.Contains(changes!, c => PathsEqual(c.Path, file) &&
                (c.Type == WatcherChangeType.Modified || (OperatingSystem.IsMacOS() && c.Type == WatcherChangeType.Created)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Changed_NewMatchingFile_IsReported()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            var file = Path.Combine(root, "hero.achx");
            File.WriteAllText(file, "");

            var changes = await WaitForChangeAsync(watcher);
            Assert.NotNull(changes);
            // Not asserting WatcherChangeType.Created specifically: File.WriteAllText performs a
            // create-then-write, and on some platforms (observed on Linux/inotify, not Windows)
            // both land inside the same debounce window -- FileChangeCoalescer keeps only the
            // *last* event's type, so a brand-new file can legitimately coalesce to Modified
            // instead of Created. Consumers that need "is this genuinely new" must check ground
            // truth (e.g. MainWindow.HandleProjectFolderChangesAsync does File.Exists + tree
            // membership) rather than trust the reported type for that distinction.
            Assert.Contains(changes!, c => PathsEqual(c.Path, file));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Changed_NonMatchingFile_NeverRaised()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");

            var changes = await WaitForChangeAsync(watcher);
            Assert.Null(changes);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Changed_RapidWritesToSameFile_CoalesceToSingleEvent()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var file = Path.Combine(root, "hero.achx");
            File.WriteAllText(file, "0");

            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);

            for (int i = 0; i < 5; i++)
            {
                File.WriteAllText(file, i.ToString());
                await Task.Delay(20); // stay within the debounce window
            }

            var changes = await WaitForChangeAsync(watcher);
            Assert.NotNull(changes);
            Assert.Single(changes!);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Dispose_StopsRaisingChanged()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(root);
            watcher.Dispose();

            File.WriteAllText(Path.Combine(root, "hero.achx"), "");

            var changes = await WaitForChangeAsync(watcher);
            Assert.Null(changes);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Watch_CalledTwice_OldFolderNoLongerWatched()
    {
        var oldRoot = Directory.CreateTempSubdirectory().FullName;
        var newRoot = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
            watcher.Watch(oldRoot);
            watcher.Watch(newRoot);

            File.WriteAllText(Path.Combine(oldRoot, "hero.achx"), "");

            var changes = await WaitForChangeAsync(watcher);
            Assert.Null(changes);
        }
        finally
        {
            Directory.Delete(oldRoot, true);
            Directory.Delete(newRoot, true);
        }
    }

    [Fact]
    public void Watch_NonexistentDirectory_DoesNotThrow()
    {
        using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);
        var missing = Path.Combine(Path.GetTempPath(), "ae-folder-watch-missing-" + Guid.NewGuid().ToString("N"));

        var ex = Record.Exception(() => watcher.Watch(missing));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Watch_NullOrEmptyFolder_DoesNotThrow(string? folder)
    {
        using var watcher = new FolderWatcher(AchxFolderScanner.IsAchxPath);

        var ex = Record.Exception(() => watcher.Watch(folder));

        Assert.Null(ex);
    }
}
