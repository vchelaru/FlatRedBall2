using AnimationEditor.Core.HotReload;
using System;
using System.IO;
using System.Threading;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HotReloadWatcherTests
{
    /// <summary>
    /// The real trigger is a background <see cref="FileSystemWatcher"/> plus the watcher's own
    /// 100ms debounce timer, not a synchronous in-process call -- poll until <paramref
    /// name="condition"/> holds or <paramref name="timeout"/> elapses (mirrors
    /// ProjectFolderExternalWatchTests.PumpUntilAsync's reasoning for the same kind of watcher).
    /// </summary>
    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            Thread.Sleep(50);
        }
    }

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
            var ex = Record.Exception(() => watcher.StartWatching(achx, new[] { pngWithDotDot }, []));

            Assert.Null(ex);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void StartWatching_AssociatedTsxFileModifiedOnDisk_FiresAssociatedTsxChangedOnDisk()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            File.WriteAllText(achx, "");
            var tsxDir = Path.Combine(root, "Tilesets");
            Directory.CreateDirectory(tsxDir);
            var tsx = Path.Combine(tsxDir, "Heroes.tsx");
            File.WriteAllText(tsx, "<tileset/>");

            using var watcher = new HotReloadWatcher();
            string? firedPath = null;
            watcher.AssociatedTsxChangedOnDisk += p => firedPath = p;
            watcher.StartWatching(achx, [], [tsx]);

            File.WriteAllText(tsx, "<tileset changed=\"1\"/>");
            PumpUntil(() => firedPath != null, TimeSpan.FromSeconds(5));

            Assert.NotNull(firedPath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void StartWatching_TiledSyncFileModifiedOnDisk_FiresTiledSyncChangedOnDisk()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var achx = Path.Combine(root, "hero.achx");
            File.WriteAllText(achx, "");
            var tiledSync = Path.Combine(root, "hero.tiledsync");
            File.WriteAllText(tiledSync, "{}");

            using var watcher = new HotReloadWatcher();
            string? firedPath = null;
            watcher.TiledSyncChangedOnDisk += p => firedPath = p;
            watcher.StartWatching(achx, [], []);

            File.WriteAllText(tiledSync, "{ \"TiledTilesetPaths\": [] }");
            PumpUntil(() => firedPath != null, TimeSpan.FromSeconds(5));

            Assert.NotNull(firedPath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void StartWatching_TsxPathWithDotDotSegments_DoesNotThrow()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var deep = Path.Combine(root, "Entities", "Bosses", "Dagon");
            Directory.CreateDirectory(deep);
            var achx = Path.Combine(root, "hero.achx");
            File.WriteAllText(achx, "");
            var tsxWithDotDot = Path.Combine(deep, "..", "..", "..", "Heroes.tsx");

            using var watcher = new HotReloadWatcher();
            var ex = Record.Exception(() => watcher.StartWatching(achx, [], new[] { tsxWithDotDot }));

            Assert.Null(ex);
        }
        finally { Directory.Delete(root, true); }
    }
}
