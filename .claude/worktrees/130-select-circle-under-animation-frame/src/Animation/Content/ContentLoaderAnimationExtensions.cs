namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Extension methods on <see cref="ContentLoader"/> for loading animation content.
/// One-call replacements for the two-step <c>FromFile(...).ToAnimationChainList(...)</c> pattern,
/// and the preferred entry point in gameplay code — they route reads through the service's
/// stream seam so the same call works on every backend (DesktopGL, KNI Blazor / WASM) and lets
/// tests inject in-memory bytes without touching global state.
/// </summary>
public static class ContentLoaderAnimationExtensions
{
    /// <summary>
    /// Loads a .achx file and converts it to a runtime <see cref="AnimationChainList"/>. The .achx
    /// XML is read through the service's stream seam; referenced textures are loaded through
    /// <see cref="ContentLoader.Load{T}"/> (so PNG hot-reload still applies).
    /// </summary>
    public static AnimationChainList LoadAnimationChainList(this ContentLoader content, string path)
        => AnimationChainListSave.FromFile(path, content.StreamProvider).ToAnimationChainList(content);

    /// <summary>
    /// Loads an Adobe Animate TextureAtlas XML and converts it to a runtime
    /// <see cref="AnimationChainList"/>. The atlas XML is read through the service's stream seam;
    /// the sibling atlas PNG is loaded through <see cref="ContentLoader.Load{T}"/>.
    /// </summary>
    /// <param name="content">The content service to read through.</param>
    /// <param name="path">Path to the atlas XML, relative to the title container.</param>
    /// <param name="frameRate">Frames per second applied to every frame. Defaults to 30.</param>
    public static AnimationChainList LoadAdobeAnimateAtlas(this ContentLoader content, string path, float frameRate = 30f)
        => AdobeAnimateAtlasSave.FromFile(path, content.StreamProvider).ToAnimationChainList(content, frameRate);
}
