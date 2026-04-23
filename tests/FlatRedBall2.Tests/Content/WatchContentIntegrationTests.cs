using System;
using System.IO;
using FlatRedBall2;
using FlatRedBall2.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

public class WatchContentIntegrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _srcRoot;
    private readonly string _destRoot;

    public WatchContentIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "frb2-watch-tests-" + Guid.NewGuid().ToString("N"));
        _srcRoot = Path.Combine(_tempRoot, "src");
        _destRoot = Path.Combine(_tempRoot, "bin");
        Directory.CreateDirectory(_srcRoot);
        Directory.CreateDirectory(_destRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private FlatRedBallService MakeEngine()
    {
        var engine = new FlatRedBallService { SourceContentRoot = _srcRoot, OutputContentRoot = _destRoot };
        engine.Start<TestScreen>();
        return engine;
    }

    private class TestScreen : Screen { }

    [Fact]
    public void WatchContent_WithNullSourceContentRoot_ReturnsNullAndDoesNotRegister()
    {
        var engine = new FlatRedBallService { SourceContentRoot = null };
        engine.Start<TestScreen>();

        var watcher = engine.CurrentScreen.WatchContent("Content/foo.json", () => { });

        watcher.ShouldBeNull();
        engine.CurrentScreen.ContentWatchers.ShouldBeEmpty();
    }

    [Fact]
    public void WatchContent_WithExplicitDestination_CopiesToCustomDestPath()
    {
        var engine = MakeEngine();
        var srcFile = Path.Combine(_srcRoot, "Assets", "Configs", "player.json");
        var destFile = Path.Combine(_destRoot, "Content", "player.json");
        Directory.CreateDirectory(Path.GetDirectoryName(srcFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.WriteAllText(srcFile, "v1");
        File.WriteAllText(destFile, "v1"); // simulate prior MSBuild copy

        var fake = new FakeFileWatcher();
        var watcher = engine.CurrentScreen.WatchContent(
            fake,
            onChanged: () => { },
            sourceAbsolutePath: srcFile,
            destinationAbsolutePath: destFile);
        watcher.Debounce = TimeSpan.Zero;

        File.WriteAllText(srcFile, "v2");
        fake.Fire();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(destFile).ShouldBe("v2");
    }

    [Fact]
    public void WatchContent_DestinationFileMissing_SkipsCopyAndCallback()
    {
        // Models the "editor temp file" scenario: source file appears (e.g. Photoshop scratch
        // file) but was never built into the output. Engine should leave it alone — no copy,
        // no callback.
        var engine = MakeEngine();
        var srcFile = Path.Combine(_srcRoot, "Content", "~scratch.tmp");
        var destFile = Path.Combine(_destRoot, "Content", "~scratch.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(srcFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.WriteAllText(srcFile, "scratch");

        var fake = new FakeFileWatcher();
        bool called = false;
        var watcher = engine.CurrentScreen.WatchContent(
            fake,
            onChanged: () => called = true,
            sourceAbsolutePath: srcFile,
            destinationAbsolutePath: destFile);
        watcher.Debounce = TimeSpan.Zero;

        fake.Fire();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.Exists(destFile).ShouldBeFalse(); // Engine did not create dest
        called.ShouldBeFalse();
    }

    [Fact]
    public void WatchContent_WithSameSourceAndDestination_DoesNotErrorOnSelfCopy()
    {
        var engine = MakeEngine();
        var file = Path.Combine(_destRoot, "Content", "foo.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "v1");

        var fake = new FakeFileWatcher();
        var watcher = engine.CurrentScreen.WatchContent(
            fake,
            onChanged: () => { },
            sourceAbsolutePath: file,
            destinationAbsolutePath: file);
        watcher.Debounce = TimeSpan.Zero;

        fake.Fire();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(file).ShouldBe("v1");
    }

    [Fact]
    public void WatchContent_SourceFileDoesNotExist_SkipsCopySilently()
    {
        var engine = MakeEngine();
        var srcFile = Path.Combine(_srcRoot, "Content", "missing.json");
        var destFile = Path.Combine(_destRoot, "Content", "missing.json");

        var fake = new FakeFileWatcher();
        bool called = false;
        var watcher = engine.CurrentScreen.WatchContent(
            fake,
            onChanged: () => called = true,
            sourceAbsolutePath: srcFile,
            destinationAbsolutePath: destFile);
        watcher.Debounce = TimeSpan.Zero;

        fake.Fire();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.Exists(destFile).ShouldBeFalse();
        called.ShouldBeFalse(); // No source → no copy → no callback
    }

    [Fact]
    public void WatchContentDirectory_WithNullSourceContentRoot_ReturnsNull()
    {
        var engine = new FlatRedBallService { SourceContentRoot = null };
        engine.Start<TestScreen>();

        var w = engine.CurrentScreen.WatchContentDirectory("Content", _ => { });

        w.ShouldBeNull();
    }

    [Fact]
    public void WatchContentDirectory_FiresCallbackPerChangedFile_AfterCopy()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "a.json"), "A");
        File.WriteAllText(Path.Combine(srcDir, "b.json"), "B");
        // Pre-populate destinations to model prior MSBuild copy.
        File.WriteAllText(Path.Combine(destDir, "a.json"), "A0");
        File.WriteAllText(Path.Combine(destDir, "b.json"), "B0");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("a.json");
        fake.Fire("b.json");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.Count.ShouldBe(2);
        File.ReadAllText(Path.Combine(destDir, "a.json")).ShouldBe("A");
        File.ReadAllText(Path.Combine(destDir, "b.json")).ShouldBe("B");
    }

    [Fact]
    public void WatchContentDirectory_NestedRelativePath_CopyPreservesSubdirectories()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(Path.Combine(srcDir, "Configs"));
        Directory.CreateDirectory(Path.Combine(destDir, "Configs"));
        var srcNested = Path.Combine(srcDir, "Configs", "player.json");
        var destNested = Path.Combine(destDir, "Configs", "player.json");
        File.WriteAllText(srcNested, "P");
        File.WriteAllText(destNested, "P0"); // model prior MSBuild copy

        var fake = new FakeDirectoryWatcher();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: _ => { },
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire(Path.Combine("Configs", "player.json"));
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(destNested).ShouldBe("P");
    }

    [Fact]
    public void WatchContentDirectory_TempFileNotInOutput_SkipsCallbackAndDoesNotCopy()
    {
        // Editor temp / autosave file appears in source but was never built. Engine should
        // ignore it entirely so a directory-wide RestartScreen handler isn't triggered by
        // editor noise.
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "a.json"), "A");
        File.WriteAllText(Path.Combine(destDir, "a.json"), "A0");
        // Photoshop-style temp file: in source, not in dest.
        File.WriteAllText(Path.Combine(srcDir, "~scratch.tmp"), "scratch");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("~scratch.tmp");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBeEmpty();
        File.Exists(Path.Combine(destDir, "~scratch.tmp")).ShouldBeFalse();
    }

    [Fact]
    public void WatchContentDirectory_NewPngWithNoDestination_AutoCopiesAndFiresCallback()
    {
        // Scenario: TMX now references a newly-added enemy.png. The PNG didn't exist at last
        // build, so the dest-exists gate would normally filter it. .png is in the default
        // AutoCopyExtensions allowlist, so the engine creates the dest and fires the callback.
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "enemy.png"), "PNGDATA");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("enemy.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { "enemy.png" });
        File.ReadAllText(Path.Combine(destDir, "enemy.png")).ShouldBe("PNGDATA");
    }

    [Fact]
    public void WatchContentDirectory_NewTsxWithNoDestination_AutoCopiesAndFiresCallback()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "new.tsx"), "TSX");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("new.tsx");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { "new.tsx" });
        File.ReadAllText(Path.Combine(destDir, "new.tsx")).ShouldBe("TSX");
    }

    [Fact]
    public void WatchContentDirectory_NewJsonWithNoDestination_StillFiltered()
    {
        // JSON is not in the default AutoCopyExtensions allowlist — new .json files are still
        // treated as possibly-editor-temp and ignored until a rebuild copies them.
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "config.json"), "{}");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("config.json");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBeEmpty();
        File.Exists(Path.Combine(destDir, "config.json")).ShouldBeFalse();
    }

    [Fact]
    public void WatchContentDirectory_UserAddsExtension_NewFileFlowsThrough()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "music.ogg"), "OGG");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;
        w.AutoCopyExtensions.Add(".ogg");

        fake.Fire("music.ogg");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { "music.ogg" });
        File.ReadAllText(Path.Combine(destDir, "music.ogg")).ShouldBe("OGG");
    }

    [Fact]
    public void WatchContentDirectory_UserRemovesDefaultExtension_NewFileFiltered()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "new.png"), "PNG");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;
        w.AutoCopyExtensions.Remove(".png");

        fake.Fire("new.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBeEmpty();
        File.Exists(Path.Combine(destDir, "new.png")).ShouldBeFalse();
    }

    [Fact]
    public void WatchContentDirectory_NewPngInNestedSubdirectory_CreatesDirectoryAndCopies()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(Path.Combine(srcDir, "Tilesets"));
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "Tilesets", "art.png"), "ART");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        var rel = Path.Combine("Tilesets", "art.png");
        fake.Fire(rel);
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { rel });
        File.ReadAllText(Path.Combine(destDir, "Tilesets", "art.png")).ShouldBe("ART");
    }

    private class FakeFileWatcher : IFileWatcher
    {
        public event Action? Changed;
        public void Fire() => Changed?.Invoke();
        public void Dispose() { }
    }

    private class FakeDirectoryWatcher : IDirectoryWatcher
    {
        public event Action<string>? Changed;
        public void Fire(string relPath) => Changed?.Invoke(relPath);
        public void Dispose() { }
    }
}
