using System;
using Microsoft.Xna.Framework.Content;

namespace FlatRedBall2;

public class ContentManagerService
{
    private ContentManager? _contentManager;

    internal void Initialize(ContentManager contentManager) => _contentManager = contentManager;

    public T Load<T>(string path)
    {
        if (_contentManager == null)
            throw new InvalidOperationException("ContentManagerService not initialized. Call Initialize first.");
        return _contentManager.Load<T>(path);
    }

    public void Unload(string path) => _contentManager?.UnloadAsset(path);

    public void UnloadAll() => _contentManager?.Unload();

    public static ContentManagerService CreateNull() => new NullContentManagerService();

    private class NullContentManagerService : ContentManagerService
    {
        public new T Load<T>(string path) => default!;
        public new void Unload(string path) { }
        public new void UnloadAll() { }
    }
}
