using AnimationEditor.App.Services;
using AnimationEditor.Core.Data;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 12 (#655): "This File" scope Files panel -- lists exactly the active tab's referenced
/// texture names, with thumbnails. Rebuild, not port: desktop's <c>FilesPanelControl</c> needs a
/// real <see cref="Avalonia.Controls.Window"/> and a real disk folder scan for its "Project"
/// scope, neither of which survives the browser (see docs/BROWSER_FILES_PANEL_DECISION.md).
/// Driven explicitly via <see cref="SetAnimationChainList"/> rather than a selection-changed
/// event -- there is no single event that fires for "the loaded file changed" the way
/// <see cref="AnimationEditor.Core.CommandsAndState.ISelectedState.SelectionChanged"/> does for
/// shape selection, so the caller re-pushes the current <c>AnimationChainListSave</c> at the same
/// points it already refreshes <c>AnimationTreeControl</c> (tab switch, Open Folder load,
/// <c>AnimationChainsChanged</c>).
/// </summary>
public partial class TextureListPanel : UserControl
{
    private AnimationChainListSave? _acls;
    private ThumbnailService? _thumbnailService;

    public TextureListPanel() => InitializeComponent();

    /// <summary>The active tab's referenced texture names, sorted and de-duplicated.</summary>
    public IReadOnlyList<string> TextureNames { get; private set; } = System.Array.Empty<string>();

    /// <summary>
    /// Binds the panel to <paramref name="thumbnailService"/> (shared with the rest of the app --
    /// this class never seeds or invalidates it) and populates the list from
    /// <paramref name="acls"/>. Call once, then <see cref="SetAnimationChainList"/> for every
    /// subsequent file/tab change.
    /// </summary>
    public void InitializeServices(AnimationChainListSave? acls, ThumbnailService thumbnailService)
    {
        _thumbnailService = thumbnailService;
        SetAnimationChainList(acls);
    }

    /// <summary>Re-populates the list for a newly active tab (or after frames are added/removed).</summary>
    public void SetAnimationChainList(AnimationChainListSave? acls)
    {
        _acls = acls;
        Refresh();
    }

    private void Refresh()
    {
        TextureNames = TextureListBuilder.GetAvailableTextures(_acls);
        EmptyLabel.IsVisible = TextureNames.Count == 0;
        TextureItems.ItemsSource = TextureNames
            .Select(name => new TextureRowVm(name, _thumbnailService?.GetFullImageThumbnail(name, 24, 24)))
            .ToList();
    }
}

/// <summary>Row view-model for <see cref="TextureListPanel"/>'s <c>ItemsControl</c>.</summary>
public sealed record TextureRowVm(string Name, Bitmap? Thumbnail);
