using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Loads the precompiled <c>ColorOperation.Add</c> pixel shader (see <c>src/Shaders/AddColor.fx</c>)
/// and caches one <see cref="Effect"/> instance per <see cref="GraphicsDevice"/>.
/// </summary>
internal static class AddColorEffect
{
    private static readonly ConditionalWeakTable<GraphicsDevice, Effect> _cache = new();

    /// <summary>Gets (or creates) the <see cref="Effect"/> for <paramref name="graphicsDevice"/>.</summary>
    public static Effect Get(GraphicsDevice graphicsDevice)
    {
        if (_cache.TryGetValue(graphicsDevice, out var existing))
            return existing;

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FlatRedBall2.Rendering.Shaders.AddColor.knifx")!;
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        var effect = new Effect(graphicsDevice, memoryStream.ToArray());
        _cache.Add(graphicsDevice, effect);
        return effect;
    }
}
