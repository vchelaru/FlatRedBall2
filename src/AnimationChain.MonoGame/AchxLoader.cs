using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Loads .achx animation files and converts them to runtime <see cref="AnimationChainList{TFrame}"/>
/// instances (closed over <see cref="AnimationFrame"/>) ready for use with
/// <see cref="AnimationPlayer{TFrame}"/>.
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
/// _player = new AnimationPlayer&lt;AnimationFrame&gt;(_animations);
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
    /// Loads the .achx (XML) or .achj (JSON) at <paramref name="achxPath"/> — dialect chosen by
    /// its extension — and returns a ready-to-play <see cref="AnimationChainList{TFrame}"/>.
    /// Textures referenced by the file are loaded relative to the file's location and cached for reuse.
    /// </summary>
    /// <param name="achxPath">
    /// A relative path is read through <c>TitleContainer</c> (relative to the game's content
    /// location, not the working directory). A rooted path is read from the file system.
    /// </param>
    public AnimationChainList<AnimationFrame> Load(string achxPath)
        => Load(achxPath, ContentFile.Open);

    /// <summary>
    /// Loads the .achx/.achj using a custom stream provider — dialect chosen by
    /// <paramref name="achxPath"/>'s extension. Use this for non-filesystem environments
    /// (Blazor WASM, embedded resources, unit tests) where <see cref="File.OpenRead(string)"/> is
    /// unavailable. The <paramref name="textureStreamProvider"/> is called with the resolved
    /// texture path; return <c>null</c> from the stream provider to skip a texture.
    /// </summary>
    /// <param name="achxPath">Path passed to <paramref name="achxStreamProvider"/>.</param>
    /// <param name="achxStreamProvider">Returns a readable stream for the .achx/.achj file.</param>
    /// <param name="textureStreamProvider">
    /// Optional override for texture loading. When <c>null</c>, relative paths are read through
    /// <c>TitleContainer</c> and rooted paths from the file system; a missing file produces a frame
    /// with no texture. Return <c>null</c> to produce a frame with no texture.
    /// </param>
    public AnimationChainList<AnimationFrame> Load(
        string achxPath,
        Func<string, Stream> achxStreamProvider,
        Func<string, Stream?>? textureStreamProvider = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var save = ContentFile.ReadSave(achxPath, achxStreamProvider);
        return save.ToAnimationChainList(texPath => GetOrLoadTexture(texPath, textureStreamProvider));
    }

    /// <summary>
    /// Loads animation data from an already-open <paramref name="achxStream"/> containing either
    /// .achx (XML) or .achj (JSON) -- the dialect is auto-detected from the content, since no file
    /// path is available to inspect an extension (see <see cref="AnimationChainListSave.FromStream"/>).
    /// Because no file path is available, <see cref="AnimationChainListSave.FileRelativeTextures"/>
    /// resolution is skipped — texture paths stored in the file are passed directly to
    /// <paramref name="textureStreamProvider"/>. Supply a <paramref name="textureStreamProvider"/>
    /// that handles those paths as written in the file.
    /// </summary>
    /// <param name="achxStream">A readable stream containing .achx or .achj data. The caller retains ownership.</param>
    /// <param name="textureStreamProvider">
    /// Optional override for texture loading. When <c>null</c>, relative paths are read through
    /// <c>TitleContainer</c> and rooted paths from the file system; a missing file produces a frame
    /// with no texture. Return <c>null</c> to produce a frame with no texture.
    /// </param>
    public AnimationChainList<AnimationFrame> Load(Stream achxStream, Func<string, Stream?>? textureStreamProvider = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var save = AnimationChainListSave.FromStream(achxStream);
        return save.ToAnimationChainList(texPath => GetOrLoadTexture(texPath, textureStreamProvider));
    }

    /// <summary>
    /// Reloads an existing <see cref="AnimationChainList{TFrame}"/> in place from the .achx/.achj at
    /// <paramref name="achxPath"/>. Chains with matching names have their frames replaced
    /// (live <see cref="AnimationPlayer{TFrame}"/> references keep working); new chains are appended.
    /// Returns <c>false</c> if the file could not be read (e.g. mid-write) — callers should
    /// retry on the next file-watcher tick.
    /// </summary>
    public bool TryReload(AnimationChainList<AnimationFrame> list, string achxPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return list.TryReload(achxPath, texPath => GetOrLoadTexture(texPath, null));
    }

    private Texture2D? GetOrLoadTexture(string resolvedPath, Func<string, Stream?>? streamProvider)
    {
        if (_textureCache.TryGetValue(resolvedPath, out var cached)) return cached;

        Stream? stream = streamProvider != null
            ? streamProvider(resolvedPath)
            : ContentFile.TryOpen(resolvedPath);

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
