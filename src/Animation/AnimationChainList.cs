using System.Collections.Generic;
using System.IO;
using System.Xml;
using FlatRedBall2.Animation.Content;

namespace FlatRedBall2.Animation;

/// <summary>
/// A named collection of <see cref="AnimationChain"/>s. Assign to
/// <see cref="FlatRedBall2.Rendering.Sprite.AnimationChains"/>.
/// Supports lookup by chain name via the string indexer.
/// </summary>
public class AnimationChainList : List<AnimationChain>
{
    /// <summary>Optional identifier for this list — typically the source file name (e.g. <c>"Player.achx"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c> (default), a frame that names a shape not present on the entity will
    /// auto-create that shape and attach it. When <c>false</c>, the missing shape throws —
    /// useful for typo-detection in tightly-authored projects.
    /// </summary>
    public bool AutoCreateShapes { get; set; } = true;

    /// <summary>
    /// All shape names referenced by any frame of any chain in this list. The ownership set the
    /// sprite reconciles against — names in the set get hidden when absent from the current
    /// frame; names outside the set are never touched. Recomputed on each call (typically called
    /// once per chainlist assignment).
    /// </summary>
    public HashSet<string> GetOwnedShapeNames()
    {
        var names = new HashSet<string>();
        foreach (var chain in this)
            foreach (var frame in chain)
                foreach (var shape in frame.Shapes)
                    if (!string.IsNullOrEmpty(shape.Name))
                        names.Add(shape.Name);
        return names;
    }

    /// <summary>
    /// Returns the chain whose <see cref="AnimationChain.Name"/> matches <paramref name="name"/>,
    /// or <c>null</c> if no chain with that name exists. Linear scan — O(n) in the chain count;
    /// fine for typical animation lists (rarely more than a handful of chains per entity).
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
    /// Re-parses the .achx at <paramref name="path"/> and applies the result in place so any
    /// live <see cref="FlatRedBall2.Rendering.Sprite.CurrentAnimation"/> reference keeps working.
    /// For each chain in the reloaded file, matches by <see cref="AnimationChain.Name"/>: if the
    /// name exists in this list the existing chain's frames are replaced (instance identity
    /// preserved); otherwise the fresh chain is appended. Chains only in this list (removed from
    /// the file) are left alone — sprites still playing them keep rendering their old art until
    /// the caller switches them.
    /// <para>
    /// Returns <c>false</c> on I/O or XML parse failure (e.g. file mid-write). Callers should
    /// fall back to <c>RestartScreen(RestartMode.HotReload)</c> in that case, or retry after
    /// the next debounce window.
    /// </para>
    /// </summary>
    public bool TryReloadFrom(string path, ContentLoader content)
    {
        AnimationChainList fresh;
        try
        {
            fresh = content.LoadAnimationChainList(path);
        }
        catch (IOException) { return false; }
        catch (XmlException) { return false; }

        foreach (var freshChain in fresh)
        {
            var existing = this[freshChain.Name];
            if (existing != null)
            {
                // Replace frames in place — preserves the AnimationChain instance and therefore
                // any Sprite.CurrentAnimation reference that points at it.
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
