using FlatRedBall.AnimationChain.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Loads .achx animation files and converts them to runtime <see cref="AnimationChainList"/>
/// instances ready for use with <see cref="AnimationPlayer"/>.
/// <para>
/// <b>Texture caching:</b> <see cref="AchxLoader"/> caches loaded <see cref="Texture2D"/>s by
/// resolved file path, so multiple chains or files that reference the same spritesheet share one
/// GPU upload. Dispose the loader when it is no longer needed to release all cached textures.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // In LoadContent:
/// _loader = new AchxLoader(GraphicsDevice);
/// _animations = _loader.Load("Content/player.achx");
/// _player = new AnimationPlayer(_animations);
/// _player.Play("Run");
///
/// // In Update:
/// _player.Update(gameTime.ElapsedGameTime);
///
/// // In Draw:
/// spriteBatch.DrawAnimation(_player, position, Color.White);
///
/// // In Dispose / Game.UnloadContent:
/// _loader.Dispose();
/// </code>
/// </example>
public sealed class AchxLoader : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <param name="graphicsDevice">Used to upload texture data to the GPU.</param>
    public AchxLoader(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    /// <summary>
    /// Loads the .achx at <paramref name="achxPath"/> from the local filesystem and returns a
    /// ready-to-play <see cref="AnimationChainList"/>. Textures referenced by the file are loaded
    /// relative to the .achx location and cached for reuse.
    /// </summary>
    /// <param name="achxPath">Absolute or working-directory-relative path to the .achx file.</param>
    public AnimationChainList Load(string achxPath)
        => Load(achxPath, File.OpenRead!);

    /// <summary>
    /// Loads the .achx using a custom stream provider. Use this for non-filesystem environments
    /// (Blazor WASM, embedded resources, unit tests) where <see cref="File.OpenRead"/> is
    /// unavailable. The <paramref name="textureStreamProvider"/> is called with the resolved
    /// texture path; return <c>null</c> from the stream provider to skip a texture.
    /// </summary>
    /// <param name="achxPath">Path passed to <paramref name="achxStreamProvider"/>.</param>
    /// <param name="achxStreamProvider">Returns a readable stream for the .achx XML.</param>
    /// <param name="textureStreamProvider">
    /// Optional override for texture loading. When <c>null</c>, falls back to
    /// <see cref="File.OpenRead"/>. Return <c>null</c> to produce a frame with no texture.
    /// </param>
    public AnimationChainList Load(
        string achxPath,
        Func<string, Stream> achxStreamProvider,
        Func<string, Stream?>? textureStreamProvider = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var save = AnimationChainListSave.FromFile(achxPath, achxStreamProvider);
        string achxDir = Path.GetDirectoryName(Path.GetFullPath(achxPath)) ?? "";

        return save.ToAnimationChainList(texPath =>
        {
            string resolved = save.FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, texPath)
                : texPath;
            return GetOrLoadTexture(resolved, textureStreamProvider);
        });
    }

    /// <summary>
    /// Reloads an existing <see cref="AnimationChainList"/> in place from the .achx at
    /// <paramref name="achxPath"/>. Chains with matching names have their frames replaced
    /// (live <see cref="AnimationPlayer"/> references keep working); new chains are appended.
    /// Returns <c>false</c> if the file could not be read (e.g. mid-write) — callers should
    /// retry on the next file-watcher tick.
    /// </summary>
    public bool TryReload(AnimationChainList list, string achxPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return list.TryReloadFrom(achxPath, texPath => GetOrLoadTexture(texPath, null));
    }

    private Texture2D? GetOrLoadTexture(string resolvedPath, Func<string, Stream?>? streamProvider)
    {
        if (_textureCache.TryGetValue(resolvedPath, out var cached)) return cached;

        Stream? stream;
        if (streamProvider != null)
        {
            stream = streamProvider(resolvedPath);
        }
        else
        {
            if (!File.Exists(resolvedPath)) return null;
            stream = File.OpenRead(resolvedPath);
        }

        if (stream == null) return null;

        using (stream)
        {
            var tex = Texture2D.FromStream(_graphicsDevice, stream);
            _textureCache[resolvedPath] = tex;
            return tex;
        }
    }

    /// <summary>Releases all cached <see cref="Texture2D"/>s.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var tex in _textureCache.Values)
            tex.Dispose();
        _textureCache.Clear();
    }
}
