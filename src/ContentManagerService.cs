using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2;

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
    /// Production default: <see cref="Texture2D.FromFile"/> against the engine's
    /// <see cref="GraphicsDevice"/>. Tests override to avoid disk + GPU.
    /// </summary>
    internal Func<string, Texture2D> TextureLoader { get; set; }

    /// <summary>
    /// Per-texture in-place reloader. Given the live tracked texture and a source path,
    /// loads the new file and calls <c>SetData</c> on the existing instance if the
    /// dimensions match. Returns <c>true</c> on in-place apply, <c>false</c> if dims
    /// differ (caller falls back to <c>RestartScreen(RestartMode.HotReload)</c>).
    /// </summary>
    internal Func<Texture2D, string, bool> TextureReloader { get; set; }

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
    /// loads the PNG directly from disk via <see cref="Texture2D.FromFile"/> and tracks
    /// it for hot-reload via <see cref="TryReload"/>. Second call with the same path
    /// returns the cached instance.</description></item>
    /// <item><description><c>Load&lt;Texture2D&gt;("ship_0001")</c> (no extension) — goes
    /// through MonoGame's compiled xnb pipeline. Not tracked for hot-reload (xnb is a
    /// build artifact and can't be reloaded at runtime).</description></item>
    /// <item><description>Any other <c>T</c> — delegates to the MonoGame content pipeline.</description></item>
    /// </list>
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
        var key = NormalizePath(path);
        if (_textureRegistry.TryGetValue(key, out var cached))
            return cached;
        var texture = TextureLoader(key);
        _textureRegistry[key] = texture;
        return texture;
    }

    /// <summary>
    /// Attempts an in-place hot-reload of a texture previously loaded via
    /// <c>Load&lt;Texture2D&gt;(path)</c>. Returns <c>true</c> if the new file has the
    /// same dimensions and was applied via <c>SetData</c> (existing <see cref="Sprite"/>
    /// references stay valid), <c>false</c> otherwise — the caller should fall back to
    /// <c>RestartScreen(RestartMode.HotReload)</c>. Returns <c>false</c> if the path was
    /// never loaded through this service.
    /// </summary>
    public bool TryReload(string path)
    {
        var key = NormalizePath(path);
        if (!_textureRegistry.TryGetValue(key, out var live))
            return false;
        return TextureReloader(live, key);
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

    public void Unload(string path) => _contentManager?.UnloadAsset(path);

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

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private Texture2D DefaultTextureLoader(string path)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("ContentManagerService not initialized.");
        return Texture2D.FromFile(_graphicsDevice, path);
    }

    private bool DefaultTextureReloader(Texture2D existing, string path)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("ContentManagerService not initialized.");
        using var incoming = Texture2D.FromFile(_graphicsDevice, path);
        if (incoming.Width != existing.Width || incoming.Height != existing.Height)
            return false;
        var buffer = new Color[incoming.Width * incoming.Height];
        incoming.GetData(buffer);
        existing.SetData(buffer);
        return true;
    }

    public static ContentManagerService CreateNull() => new NullContentManagerService();

    private class NullContentManagerService : ContentManagerService
    {
        public new T Load<T>(string path) => default!;
        public new void Unload(string path) { }
        public new void UnloadAll() { }
        public new Texture2D CreateSolidColor(int width, int height, Color color) => null!;
    }
}
