using AnimationEditor.Core.HotReload;
using System;
using System.IO;
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
}
