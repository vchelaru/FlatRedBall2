using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Rendering;

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
    /// Re-parses the .achx/.achj at <paramref name="path"/> and applies the result in place so any
    /// live <see cref="FlatRedBall2.Rendering.Sprite.CurrentAnimation"/> reference keeps working.
    /// For each chain in the reloaded file, matches by <see cref="AnimationChain.Name"/> (the nth chain
    /// of a name matches the nth): if the name exists in this list the existing chain takes the file's
    /// frames and Loop, PingPong and Reverse (instance identity preserved); otherwise the fresh chain is
    /// appended. Chains only in this list (removed from
    /// the file) are left alone — sprites still playing them keep rendering their old art until
    /// the caller switches them.
    /// <para>
    /// Every chain in the file ends up as the file has it, also where game code set its Loop, PingPong
    /// or Reversed. Where that changes one of them on the chain, the current screen's sprites playing
    /// the chain take it too, replacing a value game code set; a one-shot effect
    /// (<see cref="Screen.CreateFireAndForget(AnimationChainList, string, float, float)"/>) still plays once.
    /// </para>
    /// <para>
    /// Returns <c>false</c> on I/O or parse failure (e.g. file mid-write) — XML or JSON, depending
    /// on <paramref name="path"/>'s extension. Callers should fall back to
    /// <c>RestartScreen(RestartMode.HotReload)</c> in that case, or retry after the next debounce
    /// window.
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
        catch (InvalidDataException) { return false; }

        var before = ConvertAll(chain => (chain, chain.Loop, chain.PingPong, chain.Reversed));
        ChainReload.Apply<AnimationChain, AnimationFrame>(this, fresh);
        if (content.LiveRenderables is not { } renderables) return true;

        // A sprite took these from the chain when it started playing. Only what the reload changed is set again.
        foreach (var (chain, loop, pingPong, reversed) in before)
            foreach (var sprite in renderables().OfType<Sprite>())
            {
                if (sprite.CurrentAnimation != chain || sprite.IsOneShot) continue;
                if (chain.Loop != loop) sprite.IsLooping = chain.Loop;
                if (chain.PingPong != pingPong) sprite.IsPingPong = chain.PingPong;
                if (chain.Reversed != reversed) sprite.IsReversed = chain.Reversed;
            }
        return true;
    }
}
