using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

// Phase 1 (#603): read-only chain/frame/shape browsing for the browser build, which today
// always shows AnimationChains[0]. AnimationTreeControl wraps a TreeView bound to the already
// portable, already-tested TreeBuilder/TreeNodeVm and routes selection through the already
// portable TreeBuilder.RouteNodeSelection -- this control adds no new selection logic of its
// own, only the Avalonia wiring around it.
public class AnimationTreeControlTests
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

    private static AnimationChainListSave TwoChainAcls()
    {
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox" };
        var frame1 = new AnimationFrameSave
        {
            TextureName = "a.png",
            ShapesSave = new ShapesSave(),
        };
        frame1.ShapesSave!.Shapes.Add(rect);
        frame1.ShapesSave!.Shapes.Add(circle);

        var frame2 = new AnimationFrameSave { TextureName = "b.png" };

        var chain1 = new AnimationChainSave { Name = "Walk" };
        chain1.Frames.Add(frame1);
        chain1.Frames.Add(frame2);

        var chain2 = new AnimationChainSave { Name = "Jump" };
        chain2.Frames.Add(new AnimationFrameSave { TextureName = "c.png" });

        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain1);
        acls.AnimationChains.Add(chain2);
        return acls;
    }

    private static (AnimationTreeControl Control, ISelectedState SelectedState, AnimationChainListSave Acls) Build()
    {
        var acls = TwoChainAcls();
        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var selectedState = new SelectedState(pm);
        var control = new AnimationTreeControl();
        control.InitializeServices(selectedState, acls);
        return (control, selectedState, acls);
    }

    [AvaloniaFact]
    public void InitializeServices_PopulatesTreeWithEveryChain_NotJustTheFirst()
    {
        var (control, _, acls) = Build();
        var tree = control.TreeView;

        var roots = Assert.IsAssignableFrom<System.Collections.IEnumerable>(tree.ItemsSource)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>()
            .ToList();

        Assert.Equal(2, roots.Count);
        Assert.Equal("Walk", roots[0].Header);
        Assert.Equal("Jump", roots[1].Header);
    }

    [AvaloniaFact]
    public void SelectingChainRow_SetsSelectedChain()
    {
        var (control, selectedState, acls) = Build();
        var tree = control.TreeView;
        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();

        tree.SelectedItem = roots[1]; // "Jump"

        Assert.Same(acls.AnimationChains[1], selectedState.SelectedChain);
        Assert.Null(selectedState.SelectedFrame);
    }

    [AvaloniaFact]
    public void SelectingFrameRow_SetsSelectedFrameAndClearsShapeSelection()
    {
        var (control, selectedState, acls) = Build();
        var tree = control.TreeView;
        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var frameNode = roots[0].Children[1]; // Walk's second frame ("b.png", no shapes)

        tree.SelectedItem = frameNode;

        Assert.Same(acls.AnimationChains[0].Frames[1], selectedState.SelectedFrame);
        Assert.Null(selectedState.SelectedRectangle);
        Assert.Null(selectedState.SelectedCircle);
    }

    [AvaloniaFact]
    public void SelectingRectRow_SetsSelectedRectangleAndParentFrame()
    {
        var (control, selectedState, acls) = Build();
        var tree = control.TreeView;
        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var rectNode = roots[0].Children[0].Children[0]; // Walk > frame 1 > "Hitbox"

        tree.SelectedItem = rectNode;

        Assert.Same(acls.AnimationChains[0].Frames[0], selectedState.SelectedFrame);
        Assert.Same(acls.AnimationChains[0].Frames[0].ShapesSave!.Shapes[0], selectedState.SelectedRectangle);
    }

    [AvaloniaFact]
    public void SelectingCircleRow_SetsSelectedCircleAndParentFrame()
    {
        var (control, selectedState, acls) = Build();
        var tree = control.TreeView;
        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var circleNode = roots[0].Children[0].Children[1]; // Walk > frame 1 > "Hurtbox"

        tree.SelectedItem = circleNode;

        Assert.Same(acls.AnimationChains[0].Frames[0], selectedState.SelectedFrame);
        Assert.Same(acls.AnimationChains[0].Frames[0].ShapesSave!.Shapes[1], selectedState.SelectedCircle);
    }
}
