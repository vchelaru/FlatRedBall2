namespace FlatRedBall2;

/// <summary>
/// Loads a single asset of type <typeparamref name="T"/> from the content pipeline.
/// Register custom loaders via <see cref="ContentLoader.RegisterLoader{T}"/>.
/// </summary>
public interface IAssetLoader<T>
{
    /// <summary>Loads an asset of type <typeparamref name="T"/> from <paramref name="assetPath"/>.</summary>
    T Load(Microsoft.Xna.Framework.Content.ContentManager content, string assetPath);
}
