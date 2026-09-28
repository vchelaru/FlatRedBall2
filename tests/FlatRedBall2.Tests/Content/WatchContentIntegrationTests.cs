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
        var engine = new FlatRedBallService { OutputContentRoot = _destRoot };
        engine.SourceContentRoots.Clear();
        engine.SourceContentRoots.Add(_srcRoot);
        engine.Start<TestScreen>();
        return engine;
    }

    private class TestScreen : Screen { }

    [Fact]
    public void WatchContentDirectory_IgnoredExtensionAdded_FilesOfThatTypeSuppressed()
    {
        // IgnoredExtensions is empty by default — game opts in by adding extensions whose
        // hot-reload is owned by another pipeline (e.g. .gucx handled by Gum's reload event).
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "CardGum.gucx"), "<gucx/>");
        File.WriteAllText(Path.Combine(destDir, "CardGum.gucx"), "<gucx/>");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;
        w.IgnoredExtensions.Add(".gucx");

        fake.Fire("CardGum.gucx");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBeEmpty();
    }

    [Fact]
    public void WatchContentDirectory_WithGumProjectInTree_AutoEnablesGumHotReload()
    {
        // The engine's content watcher intentionally ignores .gumx/.gusx/.gucx
        // files because Gum runs its own hot-reload pipeline. That pipeline is
        // off by default — having to call Engine.Gum.EnableHotReload(...) at
        // every game's CustomInitialize is friction. Instead, when a screen
        // watches a directory that contains a Gum project, the engine should
        // auto-start the Gum pipeline for it.
        var gumxAbs = Path.Combine(_srcRoot, "Content", "GumProject", "GumProject.gumx");
        Directory.CreateDirectory(Path.GetDirectoryName(gumxAbs)!);
        File.WriteAllText(gumxAbs, "<GumProjectSave/>");

        var engine = MakeEngine();
        engine.IsGumHotReloadEnabled.ShouldBeFalse();

        engine.CurrentScreen.WatchContentDirectory("Content", _ => { });

        engine.IsGumHotReloadEnabled.ShouldBeTrue();
    }

    [Fact]
    public void WatchContentDirectory_WithSameSourceAndDestination_DoesNotErrorOnSelfCopy()
    {
        var engine = MakeEngine();
        var dir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "foo.json"), "v1");
        var fake = new FakeDirectoryWatcher();
        int calls = 0;
        engine.CurrentScreen.WatchContentDirectory(fake, _ => calls++,
            sourceAbsoluteRoot: dir, destinationAbsoluteRoot: dir).Debounce = TimeSpan.Zero;

        fake.Fire("foo.json");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(Path.Combine(dir, "foo.json")).ShouldBe("v1");
        calls.ShouldBe(1);
    }

    [Fact]
    public void WatchContentDirectory_SourceFileDeleted_SkipsCopyAndCallback()
    {
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "gone.json"), "old");
        var fake = new FakeDirectoryWatcher();
        int calls = 0;
        engine.CurrentScreen.WatchContentDirectory(fake, _ => calls++,
            sourceAbsoluteRoot: srcDir, destinationAbsoluteRoot: destDir).Debounce = TimeSpan.Zero;

        fake.Fire("gone.json");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(Path.Combine(destDir, "gone.json")).ShouldBe("old");
        calls.ShouldBe(0);
    }

    [Fact]
    public void WatchContentDirectory_WithEmptySourceContentRoots_ReturnsNull()
    {
        var engine = new FlatRedBallService();
        engine.SourceContentRoots.Clear();
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

    [Fact]
    public void WatchContentDirectory_PngChanged_AutoReloadsRegisteredTexture()
    {
        var engine = MakeEngine();

        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "ship.png"), "v2-src");
        File.WriteAllText(Path.Combine(destDir, "ship.png"), "v1-dest"); // simulate prior MSBuild copy

        // Register at the absolute dest path the watcher will pass to TryReload.
        engine.Content.TextureLoader = _ => null!;
        int reloaderCalls = 0;
        engine.Content.TextureReloader = (_, _) => { reloaderCalls++; return true; };
        engine.Content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(Path.Combine(destDir, "ship.png"));

        var fake = new FakeDirectoryWatcher();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: _ => { },
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("ship.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        reloaderCalls.ShouldBe(1);
    }

    [Fact]
    public void WatchContentDirectory_PngChanged_SameDimensions_SuppressesUserCallback()
    {
        // In-place reload contract: when AutoReloadAction handles the change (returns true),
        // the user callback is the fallback path — and shouldn't fire. Otherwise a typical
        // RestartScreen handler tears the screen down for nothing.
        var engine = MakeEngine();
        engine.Content.TextureLoader = _ => null!;
        engine.Content.TextureReloader = (_, _) => true; // pretend in-place patch succeeded

        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "ship.png"), "v2");
        File.WriteAllText(Path.Combine(destDir, "ship.png"), "v1");
        engine.Content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(Path.Combine(destDir, "ship.png"));

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("ship.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBeEmpty();
    }

    [Fact]
    public void WatchContentDirectory_PngChanged_DifferentDimensions_FiresUserCallbackFallback()
    {
        // When in-place reload reports it couldn't handle the change (e.g. dimensions differ),
        // the user callback runs as the fallback so the screen can RestartScreen.
        var engine = MakeEngine();
        engine.Content.TextureLoader = _ => null!;
        engine.Content.TextureReloader = (_, _) => false; // pretend dimensions differ

        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "ship.png"), "v2");
        File.WriteAllText(Path.Combine(destDir, "ship.png"), "v1");
        engine.Content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(Path.Combine(destDir, "ship.png"));

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("ship.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { "ship.png" });
    }

    [Fact]
    public void WatchContentDirectory_PngChanged_TextureNotRegistered_FiresUserCallbackFallback()
    {
        // No texture was loaded at this path, so TryReload returns false (nothing to patch).
        // The user callback must fire so the game can restart and pick up the new file.
        var engine = MakeEngine();
        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "unloaded.png"), "v2");
        File.WriteAllText(Path.Combine(destDir, "unloaded.png"), "v1");

        var fake = new FakeDirectoryWatcher();
        var calls = new System.Collections.Generic.List<string>();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: calls.Add,
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("unloaded.png");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        calls.ShouldBe(new[] { "unloaded.png" });
    }

    [Fact]
    public void WatchContentDirectory_JsonChanged_DoesNotInvokeTextureReloader()
    {
        var engine = MakeEngine();
        engine.Content.TextureLoader = _ => null!;
        int reloaderCalls = 0;
        engine.Content.TextureReloader = (_, _) => { reloaderCalls++; return true; };

        var srcDir = Path.Combine(_srcRoot, "Content");
        var destDir = Path.Combine(_destRoot, "Content");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(srcDir, "config.json"), "v2");
        File.WriteAllText(Path.Combine(destDir, "config.json"), "v1");

        var fake = new FakeDirectoryWatcher();
        var w = engine.CurrentScreen.WatchContentDirectory(
            fake,
            onChanged: _ => { },
            sourceAbsoluteRoot: srcDir,
            destinationAbsoluteRoot: destDir);
        w.Debounce = TimeSpan.Zero;

        fake.Fire("config.json");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        reloaderCalls.ShouldBe(0);
    }

    // A source content root can exist and still not contain the requested path. That happens in
    // real shipping builds: DetectSourceContentRoots walks UP from the executable looking for a
    // solution or project file, so an unrelated ancestor project (e.g. a stray *.csproj in a home
    // directory) can be selected as the only root. The fallback then constructed a real
    // FileSystemDirectoryWatcher on a directory that does not exist, and that constructor throws
    // ArgumentException - killing the process, because nothing inside the engine guards it.
    [Fact]
    public void TryWatchContentDirectory_WhenNoRootContainsTheDirectory_DoesNotThrow()
    {
        var engine = MakeEngine();   // _srcRoot exists but has no "Content" child

        Should.NotThrow(() => engine.CurrentScreen.TryWatchContentDirectory("Content", _ => { }, out _));
    }

    [Fact]
    public void TryWatchContentDirectory_WhenNoRootContainsTheDirectory_ReportsUnavailableAndRegistersNothing()
    {
        var engine = MakeEngine();

        var status = engine.CurrentScreen.TryWatchContentDirectory("Content", _ => { }, out var watcher);

        watcher.ShouldBeNull();
        status.ShouldBe(ContentWatchRegistrationStatus.SourceContentRootUnavailable);
        engine.CurrentScreen.ContentDirectoryWatchers.ShouldBeEmpty();
    }

    // WatchContentDirectory is the nullable wrapper; it must report "no watcher" rather than
    // hand back a watcher that can never fire.
    [Fact]
    public void WatchContentDirectory_WhenNoRootContainsTheDirectory_ReturnsNull()
    {
        var engine = MakeEngine();

        var watcher = engine.CurrentScreen.WatchContentDirectory("Content", _ => { });

        watcher.ShouldBeNull();
        engine.CurrentScreen.ContentDirectoryWatchers.ShouldBeEmpty();
    }

    // Guards the fix from over-reaching: a root that DOES contain the path must still register.
    [Fact]
    public void TryWatchContentDirectory_WhenARootContainsTheDirectory_StillRegisters()
    {
        var engine = MakeEngine();
        Directory.CreateDirectory(Path.Combine(_srcRoot, "Content"));

        var status = engine.CurrentScreen.TryWatchContentDirectory("Content", _ => { }, out var watcher);

        watcher.ShouldNotBeNull();
        status.ShouldBe(ContentWatchRegistrationStatus.Registered);
        engine.CurrentScreen.ContentDirectoryWatchers.ShouldNotBeEmpty();
    }

    private class FakeDirectoryWatcher : IDirectoryWatcher
    {
        public event Action<string>? Changed;
        public void Fire(string relPath) => Changed?.Invoke(relPath);
        public void Dispose() { }
    }
}
