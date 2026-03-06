using System;
using System.Collections.Generic;
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

    internal void Initialize(ContentManager contentManager, GraphicsDevice graphicsDevice)
    {
        _contentManager = contentManager;
        _graphicsDevice = graphicsDevice;
    }

    public T Load<T>(string path)
    {
        if (_contentManager == null)
            throw new InvalidOperationException("ContentManagerService not initialized. Call Initialize first.");
        return _contentManager.Load<T>(path);
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
