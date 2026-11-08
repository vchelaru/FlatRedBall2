using AnimationEditor.Core.HotReload;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HotReloadWatcherTests
{
    [Fact]
    public void StartWatching_PngPathWithDotDotSegments_DoesNotThrow()
    {
        // Repro: opening an .achx whose texture paths resolve with embedded "../" segments
        // (achxDir + "../../../tex.png") produced a directory like ...\Dagon\..\..\.. that
        // passed Directory.Exists (it resolves to an existing ancestor) but the unresolved
        // string reached the FileSystemWatcher ctor and threw FileNotFoundException.
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var deep = Path.Combine(root, "Entities", "Bosses", "Dagon");
            Directory.CreateDirectory(deep);
            var achx = Path.Combine(root, "hero.achx");
            File.WriteAllText(achx, "");
            // Resolves to <root>\hero.png but carries ".." segments like the real bug.
            var pngWithDotDot = Path.Combine(deep, "..", "..", "..", "hero.png");

            using var watcher = new HotReloadWatcher();
            var ex = Record.Exception(() => watcher.StartWatching(achx, new[] { pngWithDotDot }));

            Assert.Null(ex);
        }
        finally { Directory.Delete(root, true); }
    }

    // #1147 pass #22: RecordOwnSave remembers what the file held when we wrote it, so an event
    // inside the cooldown window is only our own echo while the file still holds that content.
    [Fact]
    public void IsStillOwnContent_FileUnchangedSinceOwnSave_True_AndFalseAfterExternalWrite()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            File.WriteAllText(achx, "<AnimationChainArraySave/>");
            using var watcher = new HotReloadWatcher();
            watcher.RunOwnSave(achx, () => { });

            Assert.True(watcher.IsStillOwnContent(achx));

            File.WriteAllText(achx, "<AnimationChainArraySave><Edited/></AnimationChainArraySave>");

            Assert.False(watcher.IsStillOwnContent(achx));
        }
        finally { Directory.Delete(root, true); }
    }

    // #1223: another process holding the file (antivirus, indexer) while the flush hashes it says
    // nothing about who wrote it -- "unknown", not "external".
    [Fact]
    public void IsStillOwnContent_FileLockedAtCheck_IsUnknown()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            using var watcher = new HotReloadWatcher();
            watcher.RunOwnSave(achx, () => File.WriteAllText(achx, "<A/>"));

            using (new FileStream(achx, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Null(watcher.IsStillOwnContent(achx));

            Assert.True(watcher.IsStillOwnContent(achx));
        }
        finally { Directory.Delete(root, true); }
    }

    // #1223: the file was locked right after our own write, so there was no hash to record. The
    // first successful read afterward is taken as ours rather than every later check saying
    // "external".
    [Fact]
    public void IsStillOwnContent_FileLockedRightAfterOwnSave_TreatsFirstReadableContentAsOwn()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            using var watcher = new HotReloadWatcher();
            FileStream? scanner = null;

            watcher.RunOwnSave(achx, () =>
            {
                File.WriteAllText(achx, "<A/>");
                scanner = new FileStream(achx, FileMode.Open, FileAccess.Read, FileShare.None);
            });
            scanner!.Dispose();

            Assert.True(watcher.IsStillOwnContent(achx));
            File.WriteAllText(achx, "<B/>");
            Assert.False(watcher.IsStillOwnContent(achx));
        }
        finally { Directory.Delete(root, true); }
    }

    // #1223: the flush timer hashes the file on a background thread. Sampling it while our own
    // save is half-written (or written but not yet recorded) mistook the editor's own auto-save
    // for an external change, reloading the document mid-edit and wiping undo history.
    [Fact]
    public async Task IsStillOwnContent_DuringOwnSave_WaitsForTheSaveInsteadOfReportingExternal()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            using var watcher = new HotReloadWatcher();
            watcher.RunOwnSave(achx, () => File.WriteAllText(achx, "<A/>"));
            using var halfWritten = new ManualResetEventSlim();
            using var finish = new ManualResetEventSlim();

            Task save = Task.Run(() => watcher.RunOwnSave(achx, () =>
            {
                File.WriteAllText(achx, "<AnimationChainArr");
                halfWritten.Set();
                finish.Wait();
                File.WriteAllText(achx, "<AnimationChainArraySave/>");
            }));
            Assert.True(halfWritten.Wait(5000));
            Task<bool?> check = Task.Run(() => watcher.IsStillOwnContent(achx));
            await Task.Delay(50);
            finish.Set();

            Assert.True(await check);
            await save;
        }
        finally { Directory.Delete(root, true); }
    }

    // Copying a texture (APFS clones on macOS) or touching its timestamp raises a change event on
    // the unchanged file; only a content change should reload it.
    [Fact]
    public async Task PngChangedOnDisk_ContentUnchanged_NotRaised_AndRaisedOnceContentChanges()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            var png = Path.Combine(root, "sheet.png");
            File.WriteAllText(achx, "<AnimationChainArraySave/>");
            File.WriteAllBytes(png, new byte[] { 1, 2, 3 });
            using var watcher = new HotReloadWatcher();
            var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            watcher.PngChangedOnDisk += changed.Enqueue;
            watcher.StartWatching(achx, new[] { png });

            Directory.CreateDirectory(Path.Combine(root, "export"));
            File.Copy(png, Path.Combine(root, "export", "sheet.png"));
            File.SetLastWriteTimeUtc(png, DateTime.UtcNow.AddMinutes(1));
            await Task.Delay(1000);

            Assert.Empty(changed);

            File.WriteAllBytes(png, new byte[] { 4, 5, 6 });
            for (int i = 0; i < 50 && changed.IsEmpty; i++)
                await Task.Delay(100);

            Assert.Single(changed);
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        for (int waited = 0; !condition() && waited < timeoutMs; waited += 50)
            await Task.Delay(50);
        return condition();
    }

    // A .seqj lists source files in other folders; each one changing must reach the consumer,
    // and a file that was not listed (or no longer is) must stay silent.
    [Fact]
    public async Task SourceChangedOnDisk_ListedSourceInAnotherFolder_RaisedOnRewriteAndDelete_ButNotWhenUnlistedOrUnchanged()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var seqj = Path.Combine(root, "boss.seqj");
            var art = Directory.CreateDirectory(Path.Combine(root, "art")).FullName;
            var source = Path.Combine(art, "hero.achj");
            var other = Path.Combine(art, "other.achj");
            File.WriteAllText(seqj, "{}");
            File.WriteAllText(source, "one");
            File.WriteAllText(other, "one");
            using var watcher = new HotReloadWatcher();
            var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            watcher.SourceChangedOnDisk += changed.Enqueue;
            watcher.StartWatching(seqj, System.Array.Empty<string>());
            watcher.UpdateSourceList(new[] { source });

            File.WriteAllText(other, "two");
            File.WriteAllText(source, "same");
            File.WriteAllText(source, "one");
            await Task.Delay(800);
            Assert.True(changed.IsEmpty, "an unlisted file, and a listed one back to the content it had, must not raise");
            changed.Clear();

            File.WriteAllText(source, "two");
            Assert.True(await WaitForAsync(() => !changed.IsEmpty), "a rewritten source should raise");
            changed.Clear();

            File.Delete(source);
            Assert.True(await WaitForAsync(() => !changed.IsEmpty), "a deleted source should raise");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UpdateSourceList_DroppedSource_StopsRaising_AndAddedSourceStartsRaising()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var seqj = Path.Combine(root, "boss.seqj");
            var a = Directory.CreateDirectory(Path.Combine(root, "a")).FullName;
            var b = Directory.CreateDirectory(Path.Combine(root, "b")).FullName;
            var first = Path.Combine(a, "first.achj");
            var second = Path.Combine(b, "second.achj");
            File.WriteAllText(seqj, "{}");
            File.WriteAllText(first, "one");
            File.WriteAllText(second, "one");
            using var watcher = new HotReloadWatcher();
            var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            watcher.SourceChangedOnDisk += changed.Enqueue;
            watcher.StartWatching(seqj, System.Array.Empty<string>());
            watcher.UpdateSourceList(new[] { first });

            watcher.UpdateSourceList(new[] { second });
            File.WriteAllText(first, "two");
            await Task.Delay(800);
            Assert.True(changed.IsEmpty, "a source dropped from the list must stay silent");

            File.WriteAllText(second, "two");
            Assert.True(await WaitForAsync(() => !changed.IsEmpty), "a newly listed source should raise");
        }
        finally { Directory.Delete(root, true); }
    }

    // Sources must keep their folders watched when the PNG list is refreshed, and vice versa.
    [Fact]
    public async Task UpdatePngList_DoesNotStopWatchingASourceFolder()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var seqj = Path.Combine(root, "boss.seqj");
            var art = Directory.CreateDirectory(Path.Combine(root, "art")).FullName;
            var source = Path.Combine(art, "hero.achj");
            File.WriteAllText(seqj, "{}");
            File.WriteAllText(source, "one");
            using var watcher = new HotReloadWatcher();
            var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            watcher.SourceChangedOnDisk += changed.Enqueue;
            watcher.StartWatching(seqj, System.Array.Empty<string>());
            watcher.UpdateSourceList(new[] { source });

            watcher.UpdatePngList(new[] { Path.Combine(root, "sheet.png") });
            File.WriteAllText(source, "two");

            Assert.True(await WaitForAsync(() => !changed.IsEmpty));
        }
        finally { Directory.Delete(root, true); }
    }

    // Git checkout and most editors save by delete + create (or rename over): that is a change, not a deletion.
    [Fact]
    public async Task AchxChangedOnDisk_AtomicDeleteThenCreate_RaisesChangedNotDeleted()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var seqj = Path.Combine(root, "boss.seqj");
            File.WriteAllText(seqj, "one");
            using var watcher = new HotReloadWatcher();
            var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var deleted = new System.Collections.Concurrent.ConcurrentQueue<string>();
            watcher.AchxChangedOnDisk += changed.Enqueue;
            watcher.AchxDeletedOnDisk += deleted.Enqueue;
            watcher.StartWatching(seqj, System.Array.Empty<string>());

            File.Delete(seqj);
            File.WriteAllText(seqj, "two");

            Assert.True(await WaitForAsync(() => !changed.IsEmpty));
            await Task.Delay(500);
            Assert.True(deleted.IsEmpty);
        }
        finally { Directory.Delete(root, true); }
    }
}
