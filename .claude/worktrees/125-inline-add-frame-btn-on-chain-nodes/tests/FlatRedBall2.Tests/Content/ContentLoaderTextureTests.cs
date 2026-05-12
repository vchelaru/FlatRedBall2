using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

/// <summary>
/// Tests for <see cref="ContentLoader"/>'s texture-from-file load/reload path
/// (PNG hot-reload support). Uses injected loader / reloader delegates so the tests
/// don't need a real <see cref="GraphicsDevice"/> or disk I/O.
/// </summary>
public class ContentLoaderTextureTests
{
    // Texture2D is sealed and requires a GraphicsDevice to construct. In these tests
    // the loader returns null! — we never dereference the Texture2D, we only check
    // registry bookkeeping (call counts, arguments, return values).

    [Fact]
    public void TryReload_PathNotRegistered_ReturnsFalse()
    {
        var svc = new ContentLoader();

        svc.TryReload("Content/missing.png").ShouldBeFalse();
    }

    [Fact]
    public void Load_TextureWithExtension_InvokesLoader()
    {
        var svc = new ContentLoader();
        string? receivedPath = null;
        svc.TextureLoader = p => { receivedPath = p; return null!; };

        svc.Load<Texture2D>("Content/ship.png");

        receivedPath.ShouldNotBeNull();
        Path.GetFileName(receivedPath).ShouldBe("ship.png");
    }

    [Fact]
    public void Load_TextureWithExtension_LoaderReceivesInputPathWithoutFilesystemResolution()
    {
        // Regression: NormalizePath used Path.GetFullPath which prepended the working
        // directory, producing "/Content/..." on browsers (WASM CWD == "/"). The loader
        // must receive the input path unchanged so TitleContainer.OpenStream can resolve
        // it relative to the title location on every backend.
        var svc = new ContentLoader();
        string? receivedPath = null;
        svc.TextureLoader = p => { receivedPath = p; return null!; };

        svc.Load<Texture2D>("Content/ship.png");

        receivedPath.ShouldBe("Content/ship.png");
    }

    [Fact]
    public void Load_TextureWithExtension_LoaderReceivesOriginalCasePath()
    {
        // Regression: a previous NormalizePath lowercased the path before passing it to
        // the loader, which works on Windows (case-insensitive FS) but 404s on case-
        // sensitive HTTP servers (KNI BlazorGL on GitHub Pages). The cache may dedupe
        // case-insensitively, but the loader must receive the original-case path so the
        // underlying TitleContainer.OpenStream / HTTP fetch hits the actual file.
        var svc = new ContentLoader();
        string? receivedPath = null;
        svc.TextureLoader = p => { receivedPath = p; return null!; };

        svc.Load<Texture2D>("Content/Animations/Arcade_Space_Shooter.PNG");

        receivedPath.ShouldBe("Content/Animations/Arcade_Space_Shooter.PNG");
    }

    [Fact]
    public void TryReload_ReloaderReceivesOriginalCasePath()
    {
        // Same case-preservation contract as the loader — a hot-reload triggered by a
        // ContentWatcher event hands back the original-case path the file was loaded with.
        var svc = new ContentLoader();
        svc.TextureLoader = p => null!;
        string? reloaderPath = null;
        svc.TextureReloader = (existing, path) => { reloaderPath = path; return true; };

        svc.Load<Texture2D>("Content/Animations/Arcade_Space_Shooter.PNG");
        svc.TryReload("Content/Animations/Arcade_Space_Shooter.PNG").ShouldBeTrue();

        reloaderPath.ShouldBe("Content/Animations/Arcade_Space_Shooter.PNG");
    }

    [Fact]
    public void Load_TextureWithExtension_MixedSlashes_HitSameRegistryEntry()
    {
        // Path.GetFullPath happened to normalize backslashes on Windows; the replacement
        // normalization must preserve that registry-hit semantics so a second Load with
        // a different slash style returns the cached texture.
        var svc = new ContentLoader();
        int loadCount = 0;
        svc.TextureLoader = p => { loadCount++; return null!; };

        svc.Load<Texture2D>("Content/ship.png");
        svc.Load<Texture2D>("Content\\ship.png");

        loadCount.ShouldBe(1);
    }

    [Fact]
    public void Load_TextureWithExtension_CachesBySamePath_LoaderCalledOnce()
    {
        var svc = new ContentLoader();
        int loadCount = 0;
        svc.TextureLoader = p => { loadCount++; return null!; };

        svc.Load<Texture2D>("Content/ship.png");
        svc.Load<Texture2D>("Content/ship.png");

        loadCount.ShouldBe(1);
    }

    [Fact]
    public void Load_TextureWithExtension_CacheIsCaseInsensitive()
    {
        var svc = new ContentLoader();
        int loadCount = 0;
        svc.TextureLoader = p => { loadCount++; return null!; };

        svc.Load<Texture2D>("Content/ship.png");
        svc.Load<Texture2D>("Content/SHIP.PNG");

        loadCount.ShouldBe(1);
    }

    [Fact]
    public void TryReload_AfterLoad_InvokesReloaderWithTrackedTexture()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = p => null!;
        bool reloaderCalled = false;
        string? reloaderPath = null;
        svc.TextureReloader = (existing, path) =>
        {
            reloaderCalled = true;
            reloaderPath = path;
            return true;
        };

        svc.Load<Texture2D>("Content/ship.png");
        var result = svc.TryReload("Content/ship.png");

        result.ShouldBeTrue();
        reloaderCalled.ShouldBeTrue();
        Path.GetFileName(reloaderPath).ShouldBe("ship.png");
    }

    [Fact]
    public void TryReload_WhenReloaderReturnsFalse_ReturnsFalse()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = p => null!;
        svc.TextureReloader = (existing, path) => false;

        svc.Load<Texture2D>("Content/ship.png");

        svc.TryReload("Content/ship.png").ShouldBeFalse();
    }

    [Fact]
    public void TryReload_DifferentCaseSamePath_FindsTrackedTexture()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = p => null!;
        int reloadCount = 0;
        svc.TextureReloader = (existing, path) => { reloadCount++; return true; };

        svc.Load<Texture2D>("Content/ship.png");
        svc.TryReload("Content/SHIP.PNG").ShouldBeTrue();

        reloadCount.ShouldBe(1);
    }

    [Fact]
    public void UnloadAll_ClearsTextureRegistry_SubsequentTryReloadReturnsFalse()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = p => null!;
        svc.TextureReloader = (existing, path) => true;

        svc.Load<Texture2D>("Content/ship.png");
        svc.UnloadAll();

        svc.TryReload("Content/ship.png").ShouldBeFalse();
    }

    [Fact]
    public void UnloadAll_ClearsTextureRegistry_SubsequentLoadCallsLoaderAgain()
    {
        var svc = new ContentLoader();
        int loadCount = 0;
        svc.TextureLoader = p => { loadCount++; return null!; };

        svc.Load<Texture2D>("Content/ship.png");
        svc.UnloadAll();
        svc.Load<Texture2D>("Content/ship.png");

        loadCount.ShouldBe(2);
    }

    [Fact]
    public void Load_GenericNonTextureType_DoesNotRouteToTextureLoader()
    {
        // Non-Texture2D types should delegate to MonoGame's ContentLoader path.
        // Without Initialize() that path throws — confirms routing doesn't hit the loader.
        var svc = new ContentLoader();
        svc.TextureLoader = p => throw new Exception("should not be called");

        Should.Throw<InvalidOperationException>(() => svc.Load<string>("foo"));
    }

    [Fact]
    public void Load_TextureWithoutExtension_DoesNotRouteToTextureLoader()
    {
        // Bare xnb key (no extension) → MonoGame pipeline, not file-on-disk.
        var svc = new ContentLoader();
        svc.TextureLoader = p => throw new Exception("should not be called");

        Should.Throw<InvalidOperationException>(() => svc.Load<Texture2D>("ship_0001"));
    }
}
