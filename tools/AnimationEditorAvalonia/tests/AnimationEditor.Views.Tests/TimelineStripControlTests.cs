using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

// Phase 14: portable timeline strip -- frame-count and scrub hit-testing before the control exists.
public class TimelineStripControlTests
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

    private static AnimationChainSave BuildChain(int frameCount, float frameLength = 0.1f)
    {
        var chain = new AnimationChainSave { Name = "Run" };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"f{i}.png", FrameLength = frameLength });
        return chain;
    }

    [AvaloniaFact]
    public void ResolveScrubAt_ContentXInSecondCell_SelectsFrameOne()
    {
        var control = new TimelineStripControl();
        control.InitializeServices(new ThumbnailService(new FakeProjectManager()));
        var chain = BuildChain(3, frameLength: 0.2f);
        control.SetChain(chain);
        var items = TimelineBuilder.BuildFrameItems(chain);
        double contentX = items[0].Width + items[1].Width / 2;

        var result = control.ResolveScrubAt(contentX);

        Assert.Equal(1, result.FrameIndex);
    }

    [AvaloniaFact]
    public void ScrubAt_ContentXInSecondCell_RaisesFrameScrubbedWithIndexOne()
    {
        var control = new TimelineStripControl();
        control.InitializeServices(new ThumbnailService(new FakeProjectManager()));
        var chain = BuildChain(3, frameLength: 0.2f);
        control.SetChain(chain);
        var items = TimelineBuilder.BuildFrameItems(chain);
        double contentX = items[0].Width + items[1].Width / 2;

        int? scrubbedIndex = null;
        control.FrameScrubbed += (idx, _) => scrubbedIndex = idx;
        control.ScrubAt(contentX);

        Assert.Equal(1, scrubbedIndex);
    }

    [AvaloniaFact]
    public void SetChain_WithThreeFrames_FrameCountMatchesChain()
    {
        var control = new TimelineStripControl();
        control.InitializeServices(new ThumbnailService(new FakeProjectManager()));

        control.SetChain(BuildChain(3));

        Assert.Equal(3, control.FrameCount);
    }
}
