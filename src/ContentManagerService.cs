using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

namespace FlatRedBall2;

/// <summary>
/// Per-screen content loader. Wraps MonoGame's <see cref="ContentManager"/> with extension-based
/// routing so <c>Load&lt;Texture2D&gt;("ship.png")</c> bypasses the XNB pipeline and loads PNG
/// files directly from disk; tracked PNG textures additionally support in-place hot-reload via
/// <see cref="TryReload"/>.
/// <para>
/// Each <see cref="Screen"/> owns its own <see cref="ContentManagerService"/> via
/// <c>Screen.ContentManager</c>. <see cref="UnloadAll"/> is invoked automatically on screen
/// transition — explicit unloads are only needed mid-screen.
/// </para>
/// </summary>
public class ContentManagerService
{
    private ContentManager? _contentManager;
    private GraphicsDevice? _graphicsDevice;
    private readonly List<IDisposable> _tracked = new();

    // Path-keyed registry for textures loaded via Load<Texture2D>("file.png"). Enables
    // same-dimension in-place PNG hot-reload via TryReload(path). Case-insensitive to
    // match the Windows filesystem; keys are normalized full paths.
    private readonly Dictionary<string, Texture2D> _textureRegistry =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loader used by <see cref="Load{T}"/> when routing a texture-from-file call.
    /// Production default: <c>Texture2D.FromFile</c> against the engine's
    /// <see cref="GraphicsDevice"/>. Tests override to avoid disk + GPU.
    /// </summary>
    internal Func<string, Texture2D> TextureLoader { get; set; }

    // Single I/O seam for every loader that reads bytes through this service: textures, .achx,
    // Adobe atlas XML, future .tmx / .gum / etc. Production default is TitleContainer.OpenStream
    // (resolves against the title location on every backend — working dir on DesktopGL,
    // HTTP fetch on Blazor). Tests swap in an in-memory provider per service instance, so two
    // tests running in parallel can't trample each other.
    internal Func<string, Stream> StreamProvider { get; set; } = XnaTitleContainer.OpenStream;

    /// <summary>
    /// Per-texture in-place reloader. Given the live tracked texture and a source path,
    /// loads the new file and calls <c>SetData</c> on the existing instance if the
    /// dimensions match. Returns <c>true</c> on in-place apply, <c>false</c> if dims
    /// differ (caller falls back to <c>RestartScreen(RestartMode.HotReload)</c>).
    /// </summary>
    internal Func<Texture2D, string, bool> TextureReloader { get; set; }

    /// <summary>Constructs a content service with the default texture loader/reloader. Call <c>Initialize</c> before use.</summary>
    public ContentManagerService()
    {
        TextureLoader = DefaultTextureLoader;
        TextureReloader = DefaultTextureReloader;
    }

    internal void Initialize(ContentManager contentManager, GraphicsDevice graphicsDevice)
    {
        _contentManager = contentManager;
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// Loads content by path. Extension-based routing:
    /// <list type="bullet">
    /// <item><description><c>Load&lt;Texture2D&gt;("Content/ship.png")</c> (has extension) —
    /// loads the PNG directly from disk via <c>Texture2D.FromFile</c> and tracks
    /// it for hot-reload via <see cref="TryReload"/>. Second call with the same path
    /// returns the cached instance.</description></item>
    /// <item><description><c>Load&lt;Texture2D&gt;("ship_0001")</c> (no extension) — goes
    /// through MonoGame's compiled xnb pipeline. Not tracked for hot-reload (xnb is a
    /// build artifact and can't be reloaded at runtime).</description></item>
    /// <item><description>Any other <c>T</c> — delegates to the MonoGame content pipeline.</description></item>
    /// </list>
    /// <para>
    /// <b>Cannot load JSON or save data.</b> JSON has no XNB representation; attempting
    /// to load a <c>.json</c> file through this method throws
    /// <see cref="Microsoft.Xna.Framework.Content.ContentLoadException"/>. For game data
    /// files and save data, use <see cref="System.IO.File.ReadAllText(string)"/> +
    /// <c>System.Text.Json.JsonSerializer.Deserialize</c> directly.
    /// </para>
    /// </summary>
    public T Load<T>(string path)
    {
        if (typeof(T) == typeof(Texture2D) && Path.HasExtension(path))
            return (T)(object)LoadTextureFromFile(path);

        if (_contentManager == null)
            throw new InvalidOperationException("ContentManagerService not initialized. Call Initialize first.");
        return _contentManager.Load<T>(path);
    }

    private Texture2D LoadTextureFromFile(string path)
    {
        var canonical = CanonicalizeSlashes(path);
        if (_textureRegistry.TryGetValue(canonical, out var cached))
            return cached;
        var texture = TextureLoader(canonical);
        _textureRegistry[canonical] = texture;
        return texture;
    }

    /// <summary>
    /// Attempts an in-place hot-reload of a texture previously loaded via
    /// <c>Load&lt;Texture2D&gt;(path)</c>. Returns <c>true</c> if the new file has the
    /// same dimensions and was applied via <c>SetData</c> (existing <see cref="FlatRedBall2.Rendering.Sprite"/>
    /// references stay valid), <c>false</c> otherwise — the caller should fall back to
    /// <c>RestartScreen(RestartMode.HotReload)</c>. Returns <c>false</c> if the path was
    /// never loaded through this service.
    /// </summary>
    public bool TryReload(string path)
    {
        var canonical = CanonicalizeSlashes(path);
        if (!_textureRegistry.TryGetValue(canonical, out var live))
            return false;
        return TextureReloader(live, canonical);
    }

    /// <summary>
    /// Registers a resource for disposal when <see cref="UnloadAll"/> is called.
    /// Use this when you create a <see cref="Texture2D"/> or other disposable asset manually
    /// and want it cleaned up automatically with the rest of the screen's content.
    /// </summary>
    public void Track(IDisposable resource) => _tracked.Add(resource);

    /// <summary>
    /// Creates a solid-color texture and registers it for automatic disposal on <see cref="UnloadAll"/>.
    /// </summary>
    public Texture2D CreateSolidColor(int width, int height, Color color)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("ContentManagerService not initialized.");
        var tex = new Texture2D(_graphicsDevice, width, height);
        tex.SetData(Enumerable.Repeat(color, width * height).ToArray());
        Track(tex);
        return tex;
    }

    /// <summary>
    /// Unloads a single asset previously loaded through the MonoGame content pipeline (no-op on KNI,
    /// where <c>UnloadAsset</c> is unsupported). Does not affect file-loaded textures tracked via
    /// <see cref="TryReload"/> — those persist until <see cref="UnloadAll"/>.
    /// </summary>
    public void Unload(string path)
    {
#if !KNI
        _contentManager?.UnloadAsset(path);
#endif
    }

    /// <summary>
    /// Disposes every asset this service owns: pipeline-loaded content, manually <see cref="Track"/>ed
    /// resources, and file-loaded texture cache. Called automatically on screen transition.
    /// </summary>
    public void UnloadAll()
    {
        _contentManager?.Unload();
        foreach (var resource in _tracked)
            resource.Dispose();
        _tracked.Clear();
        foreach (var tex in _textureRegistry.Values)
            tex?.Dispose();
        _textureRegistry.Clear();
    }

    // Slash-only canonicalization. Must NOT call Path.GetFullPath (on WASM the CWD is "/", so
    // GetFullPath prepends a leading slash that breaks TitleContainer.OpenStream) and must NOT
    // lowercase (the result is passed straight to TitleContainer/HTTP, which on case-sensitive
    // hosts like GitHub Pages 404s on a mismatch). Cache de-duplication of case variants is
    // handled by _textureRegistry's OrdinalIgnoreCase comparer, not by mangling the I/O path.
    private static string CanonicalizeSlashes(string path) => path.Replace('\\', '/');

    private Texture2D DefaultTextureLoader(string path)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("ContentManagerService not initialized.");
        using var stream = StreamProvider(path);
        var texture = Texture2D.FromStream(_graphicsDevice, stream);
        PremultiplyIfNeeded(texture);
        return texture;
    }

    private bool DefaultTextureReloader(Texture2D existing, string path)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("ContentManagerService not initialized.");
        using var stream = StreamProvider(path);
        using var incoming = Texture2D.FromStream(_graphicsDevice, stream);
        if (incoming.Width != existing.Width || incoming.Height != existing.Height)
            return false;
        var buffer = new Color[incoming.Width * incoming.Height];
        incoming.GetData(buffer);
        if (OperatingSystem.IsBrowser())
            PremultiplyAlpha(buffer);
        existing.SetData(buffer);
        return true;
    }

    // KNI BlazorGL's Texture2D.FromStream returns raw unpremultiplied RGBA (browser image
    // decode), but the engine renders with BlendState.AlphaBlend which expects premultiplied
    // alpha — without this pass, antialiased PNG edges blend their full RGB and show as a
    // light halo. DesktopGL's FromStream already premultiplies internally, so we'd
    // double-premultiply and darken edges if we did this unconditionally.
    private static void PremultiplyIfNeeded(Texture2D texture)
    {
        if (!OperatingSystem.IsBrowser())
            return;
        var buffer = new Color[texture.Width * texture.Height];
        texture.GetData(buffer);
        PremultiplyAlpha(buffer);
        texture.SetData(buffer);
    }

    private static void PremultiplyAlpha(Color[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var c = buffer[i];
            if (c.A == 255) continue;
            if (c.A == 0) { buffer[i] = new Color(0, 0, 0, 0); continue; }
            buffer[i] = new Color(
                (byte)(c.R * c.A / 255),
                (byte)(c.G * c.A / 255),
                (byte)(c.B * c.A / 255),
                c.A);
        }
    }

    /// <summary>
    /// Creates a content service whose load methods all return defaults — useful for headless
    /// testing where no <see cref="GraphicsDevice"/> or <see cref="ContentManager"/> exists.
    /// </summary>
    public static ContentManagerService CreateNull() => new NullContentManagerService();

    private class NullContentManagerService : ContentManagerService
    {
        /// <inheritdoc/>
        public new T Load<T>(string path) => default!;
        /// <inheritdoc/>
        public new void Unload(string path) { }
        /// <inheritdoc/>
        public new void UnloadAll() { }
        /// <inheritdoc/>
        public new Texture2D CreateSolidColor(int width, int height, Color color) => null!;
    }
}
