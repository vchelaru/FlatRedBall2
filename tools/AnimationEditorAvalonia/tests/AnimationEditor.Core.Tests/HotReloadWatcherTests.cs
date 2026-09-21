using AnimationEditor.Core.HotReload;
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
            watcher.RecordOwnSave(achx);

            Assert.True(watcher.IsStillOwnContent(achx));

            File.WriteAllText(achx, "<AnimationChainArraySave><Edited/></AnimationChainArraySave>");

            Assert.False(watcher.IsStillOwnContent(achx));
        }
        finally { Directory.Delete(root, true); }
    }
}
