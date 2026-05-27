using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// A named collection of <see cref="AnimationChain"/>s loaded from a single .achx file.
/// Assign to an <see cref="AnimationPlayer"/> to play animations.
/// Supports lookup by chain name via the string indexer.
/// </summary>
public class AnimationChainList : List<AnimationChain>
{
    /// <summary>Optional identifier — typically the source file name (e.g. <c>"Player.achx"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Returns the chain whose <see cref="AnimationChain.Name"/> matches <paramref name="name"/>,
    /// or <c>null</c> if not found. Linear scan — fine for typical animation lists.
    /// </summary>
    public AnimationChain? this[string name]
    {
        get
        {
            foreach (var chain in this)
                if (chain.Name == name) return chain;
            return null;
        }
    }

    /// <summary>
    /// Re-parses the .achx at <paramref name="achxPath"/> and applies changes in place so any
    /// live <see cref="AnimationPlayer"/> reference keeps working. For each chain in the reloaded
    /// file, matches by <see cref="AnimationChain.Name"/>: existing chains have their frames
    /// replaced in place (instance identity preserved); new chains are appended.
    /// <para>
    /// Returns <c>false</c> on I/O or XML parse failure (e.g. file mid-write). Callers should
    /// retry after the next file-system debounce window.
    /// </para>
    /// </summary>
    /// <param name="achxPath">Absolute path to the .achx file to reload from.</param>
    /// <param name="textureLoader">
    /// Called with the resolved absolute path of each texture file. May return <c>null</c>
    /// if the texture is unavailable — affected frames keep their existing texture.
    /// Typically supplied by <see cref="AchxLoader"/>, which reuses its internal cache
    /// and only loads new textures.
    /// </param>
    public bool TryReloadFrom(string achxPath, Func<string, Texture2D?> textureLoader)
    {
        AnimationChainList fresh;
        try
        {
            var save = Content.AnimationChainListSave.FromFile(achxPath);
            fresh = save.ToAnimationChainList(textureLoader);
        }
        catch (IOException) { return false; }
        catch (System.Xml.XmlException) { return false; }

        foreach (var freshChain in fresh)
        {
            var existing = this[freshChain.Name];
            if (existing != null)
            {
                existing.Clear();
                existing.AddRange(freshChain);
            }
            else
            {
                Add(freshChain);
            }
        }
        return true;
    }
}
