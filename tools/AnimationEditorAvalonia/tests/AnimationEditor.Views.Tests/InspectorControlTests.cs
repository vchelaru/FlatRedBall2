using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

// Phase 1 (#603): read-only property display driven by ISelectedState.SelectionChanged. No
// editable fields, no mutation -- see docs/BROWSER_TREE_INSPECTOR_DECISION.md.
public class InspectorControlTests
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

    private static (InspectorControl Control, SelectedState State) Build()
    {
        var pm = new FakeProjectManager();
        var state = new SelectedState(pm);
        var control = new InspectorControl();
        control.InitializeServices(state);
        return (control, state);
    }

    [AvaloniaFact]
    public void NoSelection_ShowsPlaceholder_HidesAllPanels()
    {
        var (control, _) = Build();

        Assert.True(control.NoSelectionPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
    }

    [AvaloniaFact]
    public void FrameSelected_ShowsFramePanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave
        {
            TextureName = "hero.png",
            FrameLength = 0.125f,
            LeftCoordinate = 0.1f,
            RightCoordinate = 0.9f,
            TopCoordinate = 0.2f,
            BottomCoordinate = 0.8f,
        };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        var pm = new FakeProjectManager
        {
            AnimationChainListSave = new AnimationChainListSave(),
        };
        pm.AnimationChainListSave!.AnimationChains.Add(chain);

        state.SelectedFrame = frame;

        Assert.True(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
        Assert.False(control.NoSelectionPanel.IsVisible);
        Assert.Contains("hero.png", control.FrameTextureText.Text);
        Assert.Contains("0.125", control.FrameLengthText.Text);
    }

    [AvaloniaFact]
    public void RectSelected_ShowsRectPanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Hitbox", X = 4f, Y = -2f, ScaleX = 8f, ScaleY = 16f };
        frame.ShapesSave!.Shapes.Add(rect);

        state.SelectedFrame = frame;
        state.SelectedRectangle = rect;

        Assert.True(control.RectPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
        Assert.Contains("Hitbox", control.RectNameText.Text);
        Assert.Contains("4", control.RectPositionText.Text);
    }

    [AvaloniaFact]
    public void CircleSelected_ShowsCirclePanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var circle = new CircleSave { Name = "Hurtbox", X = 1f, Y = 2f, Radius = 12f };
        frame.ShapesSave!.Shapes.Add(circle);

        state.SelectedFrame = frame;
        state.SelectedCircle = circle;

        Assert.True(control.CirclePanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.Contains("Hurtbox", control.CircleNameText.Text);
        Assert.Contains("12", control.CircleRadiusText.Text);
    }

    [AvaloniaFact]
    public void SelectionChangedDirectlyThroughSelectedState_UpdatesInspector_NotJustViaTreeClicks()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "direct.png" };

        // No tree involved at all -- InspectorControl must react to SelectedState.SelectionChanged
        // on its own, since Phase 1's tree and inspector are independent controls.
        state.SelectedFrame = frame;

        Assert.True(control.FramePanel.IsVisible);
        Assert.Contains("direct.png", control.FrameTextureText.Text);

        state.Reset();

        Assert.True(control.NoSelectionPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
    }
}
