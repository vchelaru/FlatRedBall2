using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A named collection of <see cref="AnimationChain{TFrame}"/>s, typically loaded from a single
/// .achx/.achj file. Assign to an <see cref="AnimationPlayer{TFrame}"/> to play animations.
/// Supports lookup by chain name via the string indexer.
/// </summary>
public class AnimationChainList<TFrame> : List<AnimationChain<TFrame>> where TFrame : AnimationFrameBase
{
    /// <summary>Optional identifier — typically the source file name (e.g. <c>"Player.achx"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Returns the chain whose <see cref="AnimationChain{TFrame}.Name"/> matches <paramref name="name"/>,
    /// or <c>null</c> if not found. Linear scan — fine for typical animation lists.
    /// </summary>
    public AnimationChain<TFrame>? this[string name]
    {
        get
        {
            foreach (var chain in this)
                if (chain.Name == name) return chain;
            return null;
        }
    }

    /// <summary>
    /// All shape names referenced by any frame of any chain in this list. Recomputed on each
    /// call — typically called once per chain-list assignment by an ownership-reconciliation
    /// system (e.g. deciding which entity shapes an animation is allowed to touch).
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
    /// Re-parses the .achx/.achj at <paramref name="achxPath"/> from disk and applies changes in
    /// place — see <see cref="TryReloadFrom(string, Func{string, Stream}, Func{AnimationFrameSave, TFrame})"/>
    /// for the merge semantics and the testable stream-provider overload.
    /// </summary>
    public bool TryReloadFrom(string achxPath, Func<AnimationFrameSave, TFrame> frameFactory)
        => TryReloadFrom(achxPath, File.OpenRead!, frameFactory);

    /// <summary>
    /// Re-parses the .achx/.achj read from <paramref name="streamProvider"/> and applies changes
    /// in place so any live <see cref="AnimationPlayer{TFrame}"/> reference keeps working. For
    /// each chain in the reloaded file, matches by <see cref="AnimationChain{TFrame}.Name"/>:
    /// existing chains have their frames replaced in place (instance identity preserved); new
    /// chains are appended.
    /// <para>
    /// Returns <c>false</c> on I/O or parse failure (e.g. file mid-write) — XML or JSON, depending
    /// on <paramref name="achxPath"/>'s extension. Callers should retry after the next
    /// file-system debounce window.
    /// </para>
    /// </summary>
    /// <param name="achxPath">Path passed to <paramref name="streamProvider"/> to open the file.</param>
    /// <param name="streamProvider">Byte source for <paramref name="achxPath"/>. No default, so this
    /// method has no IL-level reference to a specific platform's file API — see
    /// <see cref="AnimationChainListSave.FromFile(string, Func{string, Stream})"/>.</param>
    /// <param name="frameFactory">
    /// Builds a <typeparamref name="TFrame"/> from a parsed <see cref="AnimationFrameSave"/> —
    /// the renderer-specific step (computing a <see cref="PixelRectangle"/> source region from
    /// UV/pixel coordinates, loading the texture) that this generic layer cannot do itself.
    /// </param>
    public bool TryReloadFrom(string achxPath, Func<string, Stream> streamProvider, Func<AnimationFrameSave, TFrame> frameFactory)
    {
        AnimationChainListSave save;
        try
        {
            save = AnimationChainListSave.FromFile(achxPath, streamProvider);
        }
        catch (IOException) { return false; }
        catch (XmlException) { return false; }
        catch (JsonException) { return false; }

        var fresh = new AnimationChainList<TFrame> { Name = save.FileName };
        foreach (var chainSave in save.AnimationChains)
        {
            var chain = new AnimationChain<TFrame> { Name = chainSave.Name, Loop = chainSave.Loop };
            foreach (var frameSave in chainSave.Frames)
                chain.Add(frameFactory(frameSave));
            fresh.Add(chain);
        }

        ApplyReloadedChains(fresh);
        return true;
    }

    private void ApplyReloadedChains(AnimationChainList<TFrame> fresh)
    {
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
    }
}
