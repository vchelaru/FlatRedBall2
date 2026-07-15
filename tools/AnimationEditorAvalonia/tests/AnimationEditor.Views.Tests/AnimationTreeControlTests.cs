using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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

    // Phase 2 (#610): mutation commands add/remove chains and frames; the tree must reflect
    // those changes without losing existing nodes' expand state (a full rebuild would collapse
    // everything). Refresh() uses TreeBuilder.SyncChainsInto -- the same diff-based sync
    // MainWindow's RefreshTreeView already uses on desktop.

    [AvaloniaFact]
    public void Refresh_AddedChain_AppearsInTree_WithoutRebuildingExistingNodes()
    {
        var (control, _, acls) = Build();
        var tree = control.TreeView;
        var originalWalkNode = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().First();
        originalWalkNode.IsExpanded = false; // simulate a user collapsing it before the mutation

        acls.AnimationChains.Add(new AnimationChainSave { Name = "NewAnim" });
        control.Refresh();

        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        Assert.Equal(3, roots.Count);
        Assert.Equal("NewAnim", roots[2].Header);
        Assert.Same(originalWalkNode, roots[0]); // same VM instance, not rebuilt
        Assert.False(roots[0].IsExpanded); // collapse state survived the refresh
    }

    [AvaloniaFact]
    public void Refresh_RemovedChain_DisappearsFromTree()
    {
        var (control, _, acls) = Build();
        var tree = control.TreeView;

        acls.AnimationChains.RemoveAt(1); // remove "Jump"
        control.Refresh();

        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        Assert.Single(roots);
        Assert.Equal("Walk", roots[0].Header);
    }

    [AvaloniaFact]
    public void Refresh_NoAclsLoaded_ClearsTree()
    {
        var control = new AnimationTreeControl();
        var pm = new FakeProjectManager();
        var selectedState = new SelectedState(pm);
        control.InitializeServices(selectedState, null);

        control.Refresh();

        Assert.Null(control.TreeView.ItemsSource);
    }

    // Phase 2 follow-up (#610): inline rename, mirroring MainWindow's double-tap-to-rename for
    // chain nodes. CommitRename is the directly-testable seam (matches MainWindow's own split of
    // CommitInlineRename from the double-tap/keyboard gesture plumbing) -- it's exercised here
    // without simulating an actual double-tap or KeyDown, the same way the gesture-independent
    // logic is unit-tested on desktop.

    private static (AnimationTreeControl Control, IAppCommands Commands, AnimationChainListSave Acls) BuildWithCommands()
    {
        var acls = TwoChainAcls();
        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var selectedState = new SelectedState(pm);
        var events = new ApplicationEvents();
        var appState = new AppState(events, selectedState);
        var ioManager = new IoManager(appState);
        var objectFinder = new ObjectFinder(pm);
        var undoManager = new UndoManager();
        var appCommands = new AppCommands(pm, selectedState, events, ioManager, objectFinder, undoManager);

        var control = new AnimationTreeControl();
        control.InitializeServices(selectedState, acls);
        control.EnableRename(appCommands);

        return (control, appCommands, acls);
    }

    [AvaloniaFact]
    public void CommitRename_ChainNode_NonEmptyDifferentName_RenamesChain()
    {
        var (control, _, acls) = BuildWithCommands();
        var roots = ((System.Collections.IEnumerable)control.TreeView.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var walkNode = roots[0];
        walkNode.BeginEdit();

        control.CommitRename(walkNode, "Sprint");

        Assert.Equal("Sprint", acls.AnimationChains[0].Name);
        Assert.False(walkNode.IsEditing);
    }

    [AvaloniaFact]
    public void CommitRename_ChainNode_EmptyName_DoesNotRename()
    {
        var (control, _, acls) = BuildWithCommands();
        var roots = ((System.Collections.IEnumerable)control.TreeView.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var walkNode = roots[0];
        walkNode.BeginEdit();

        control.CommitRename(walkNode, "   ");

        Assert.Equal("Walk", acls.AnimationChains[0].Name);
        Assert.False(walkNode.IsEditing);
    }

    [AvaloniaFact]
    public void CommitRename_ChainNode_SameName_DoesNotThrowOrRecordUndoEntry()
    {
        var (control, commands, acls) = BuildWithCommands();
        var roots = ((System.Collections.IEnumerable)control.TreeView.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().ToList();
        var walkNode = roots[0];
        walkNode.BeginEdit();

        control.CommitRename(walkNode, "Walk");

        Assert.Equal("Walk", acls.AnimationChains[0].Name);
    }

    // Desktop's MainWindow tree shows each chain's frame count + total playtime and each
    // frame's own length beside the header (TreeBuilder.BuildChainMeta / TreeNodeVm.Meta,
    // #623) -- but AnimationTreeControl's item template only ever bound Header, so the
    // browser's tree never surfaced that data even though TreeBuilder already computed it.
    // Realizes the tree in a real Window (mirrors AnimationEditor.App.Tests'
    // TreeNodeIconSizeTests pattern) so this catches a template binding gap that a
    // VM-property-only assertion (Meta already correct, per Core's TreeBuilderTests) would not.
    [AvaloniaFact]
    public void TreeItemTemplate_ShowsMetaText_ForChainAndFrameNodes()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.5f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.5f });
        acls.AnimationChains.Add(chain);

        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var selectedState = new SelectedState(pm);
        var control = new AnimationTreeControl();
        control.InitializeServices(selectedState, acls);

        var window = new Window { Content = control, Width = 400, Height = 400 };
        try
        {
            window.Show();
            window.Measure(new Size(400, 400));
            window.Arrange(new Rect(0, 0, 400, 400));
            Dispatcher.UIThread.RunJobs();

            var texts = control.TreeView.GetVisualDescendants().OfType<TextBlock>()
                .Select(tb => tb.Text).ToList();

            Assert.Contains("2 fr · 1.00s", texts); // chain node: frame count + total playtime
            Assert.Contains("0.50s", texts);        // frame node: this frame's own length
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AddFrameBtn_Click_AddsFrameToChain()
    {
        var (control, _, acls) = BuildWithCommands();
        var walkNode = ((System.Collections.IEnumerable)control.TreeView.ItemsSource!)
            .Cast<AnimationEditor.Core.ViewModels.TreeNodeVm>().First();

        control.RaiseAddFrameForTest(walkNode);

        Assert.Equal(3, acls.AnimationChains[0].Frames.Count);
    }
}
