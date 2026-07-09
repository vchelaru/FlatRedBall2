using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

// Phase 12 (#655): "This File" scope Files panel -- rebuild, not port, since desktop's
// FilesPanelControl needs a real Window and a real disk folder scan (neither survives the
// browser; see docs/BROWSER_TABSTRIP_CONTEXT_MENU_DECISION.md's port-vs-rebuild precedent).
// Built directly on TextureListBuilder.GetAvailableTextures -- pure, already-tested, zero disk
// access -- so this control's own job is just presentation + thumbnails.
public class TextureListPanelTests
{
    private sealed class FakeProjectManager : IProjectManager
    {
        public AnimationChainListSave? AnimationChainListSave { get; set; }
        public TileMapInformationList TileMapInformationList { get; set; } = new();
        public FilePath[] ReferencedPngs => Array.Empty<FilePath>();
        public string? FileName { get; set; }
        public TextureCoordinateType OnDiskCoordinateType { get; set; }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null) { }

        public void SaveAnimationChainList(string targetPath) { }
        public void SaveAnimationChainList(System.IO.Stream stream) { }
        public string? ResolveFilesPanelRoot() => null;
        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) => null;

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            Array.Empty<string>();
    }

    private static AnimationChainListSave BuildAcls(params string[] textureNames)
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Chain" };
        foreach (var name in textureNames)
            chain.Frames.Add(new AnimationFrameSave { TextureName = name });
        acls.AnimationChains.Add(chain);
        return acls;
    }

    [AvaloniaFact]
    public void NoAnimationChainList_ShowsEmptyState_NoTextures()
    {
        var control = new TextureListPanel();
        var thumbnailService = new ThumbnailService(new FakeProjectManager());

        control.InitializeServices(null, thumbnailService);

        Assert.True(control.EmptyLabel.IsVisible);
        Assert.Empty(control.TextureNames);
    }

    [AvaloniaFact]
    public void AnimationChainListWithFrames_ListsDistinctSortedTextureNames()
    {
        var control = new TextureListPanel();
        var thumbnailService = new ThumbnailService(new FakeProjectManager());
        var acls = BuildAcls("b.png", "a.png", "a.png");

        control.InitializeServices(acls, thumbnailService);

        Assert.Equal(new[] { "a.png", "b.png" }, control.TextureNames);
        Assert.False(control.EmptyLabel.IsVisible);
    }

    [AvaloniaFact]
    public void SetAnimationChainList_OnTabSwitch_ReplacesTextureList()
    {
        var control = new TextureListPanel();
        var thumbnailService = new ThumbnailService(new FakeProjectManager());
        control.InitializeServices(BuildAcls("a.png"), thumbnailService);
        Assert.Equal(new[] { "a.png" }, control.TextureNames);

        control.SetAnimationChainList(BuildAcls("c.png", "b.png"));

        Assert.Equal(new[] { "b.png", "c.png" }, control.TextureNames);
        Assert.False(control.EmptyLabel.IsVisible);
    }

    [AvaloniaFact]
    public void SetAnimationChainList_BackToNull_ShowsEmptyStateAgain()
    {
        var control = new TextureListPanel();
        var thumbnailService = new ThumbnailService(new FakeProjectManager());
        control.InitializeServices(BuildAcls("a.png"), thumbnailService);

        control.SetAnimationChainList(null);

        Assert.True(control.EmptyLabel.IsVisible);
        Assert.Empty(control.TextureNames);
    }
}
