using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Data;

/// <summary>
/// Builds the list of textures referenced by the current .achx file (WF10).
///
/// Mirrors the logic in <c>MainWindow.RefreshTextureCombo()</c>:
/// walk all chains/frames, collect non-empty <c>TextureName</c> values,
/// return sorted and de-duplicated.
///
/// This class works with relative texture names only; path resolution to absolute
/// paths is the responsibility of the calling (UI) layer because it requires
/// knowledge of the .achx folder, which is a runtime/IO concern.
/// </summary>
public static class TextureListBuilder
{
    /// <summary>
    /// Returns a sorted, de-duplicated list of relative texture names referenced
    /// by any frame in <paramref name="acls"/>.
    /// </summary>
    /// <param name="acls">The animation chain list; may be <c>null</c>.</param>
    public static IReadOnlyList<string> GetAvailableTextures(AnimationChainListSave? acls)
    {
        if (acls is null)
            return [];

        return acls.AnimationChains
            .SelectMany(c => c.Frames)
            .Select(f => f.TextureName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    /// <summary>
    /// Returns the first frame — in chain-then-frame list order — that references a non-empty
    /// texture, or <c>null</c> when nothing in <paramref name="acls"/> does. The single source of
    /// truth behind both <see cref="GetFirstTextureName"/> (the wireframe/combo fallback) and
    /// <c>AppCommands.ResolveInheritedFrame</c>'s cross-chain borrow (which also needs the frame's
    /// region, not just its texture name) — kept as one scan so "first available texture in this
    /// document" can't drift between the two call sites.
    /// </summary>
    /// <param name="acls">The animation chain list; may be <c>null</c>.</param>
    public static AnimationFrameSave? FindFirstTexturedFrame(AnimationChainListSave? acls) =>
        acls?.AnimationChains
            .SelectMany(c => c.Frames)
            .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.TextureName));

    /// <summary>
    /// Returns the <c>TextureName</c> of <see cref="FindFirstTexturedFrame"/>, or <c>null</c> when
    /// nothing in <paramref name="acls"/> has one. Used to borrow a texture for a chain that has no
    /// frames of its own, so the wireframe shows something the user can Ctrl+click to seed the
    /// first frame, and so the +Add button can inherit a texture (issues #618 / #617).
    /// </summary>
    /// <param name="acls">The animation chain list; may be <c>null</c>.</param>
    public static string? GetFirstTextureName(AnimationChainListSave? acls) =>
        FindFirstTexturedFrame(acls)?.TextureName;
}
