using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Inspector behavior when the selection holds several shapes or a mix of kinds/levels. The
/// rules these pin down: shapes beat frames beat chains; the panel shown is for the kind of the
/// primary (last-clicked) shape; rect/circle panels bulk-edit every selected shape of that kind;
/// the polygon panel edits only the primary polygon.
/// </summary>
public class ShapeSelectionInspectorTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static AnimationFrameSave NewFrame(params ShapeSave[] shapes)
    {
        var f = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        foreach (var s in shapes) f.ShapesSave!.Shapes.Add(s);
        return f;
    }

    private static AnimationChainSave NewChain(string name, params AnimationFrameSave[] frames)
    {
        var c = new AnimationChainSave { Name = name };
        c.Frames.AddRange(frames);
        return c;
    }

    private static TreeNodeVm FindNode(IEnumerable<TreeNodeVm> nodes, object data)
    {
        foreach (var n in nodes)
        {
            if (ReferenceEquals(n.Data, data)) return n;
            var found = FindNodeOrNull(n.Children, data);
            if (found is not null) return found;
        }
        throw new Xunit.Sdk.XunitException("Node not found in tree");
    }

    private static TreeNodeVm? FindNodeOrNull(IEnumerable<TreeNodeVm> nodes, object data)
    {
        foreach (var n in nodes)
        {
            if (ReferenceEquals(n.Data, data)) return n;
            var found = FindNodeOrNull(n.Children, data);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Ctrl+click multi-select of the given data objects in the animation tree, in order.</summary>
    private static void TreeSelect(MainWindow window, params object[] data)
    {
        typeof(MainWindow).GetMethod("RebuildTreeView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(window, new object[] { System.Array.Empty<string>() });
        FlushUi();
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = ((System.Collections.IEnumerable)tree.ItemsSource!).Cast<TreeNodeVm>().ToList();
        void ExpandAll(IEnumerable<TreeNodeVm> ns) { foreach (var n in ns) { n.IsExpanded = true; ExpandAll(n.Children); } }
        ExpandAll(roots);
        FlushUi();
        tree.SelectedItems!.Clear();
        foreach (var d in data) tree.SelectedItems.Add(FindNode(roots, d));
        FlushUi();
    }

    [AvaloniaFact]
    public void TwoCirclesAcrossFrames_RadiusEditAppliesToBoth()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var c0 = new CircleSave { Name = "C0", Radius = 5f };
            var c1 = new CircleSave { Name = "C1", Radius = 5f };
            var chain = NewChain("Walk", NewFrame(c0), NewFrame(c1));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TreeSelect(window, c0, c1);

            Assert.Equal(2, ctx.SelectedState.SelectedCircles.Count);
            window.FindControl<NumericUpDown>("PropCircleRadius")!.Value = 30m;
            FlushUi();

            Assert.Equal(30f, c0.Radius);
            Assert.Equal(30f, c1.Radius);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Pins today's behavior: with two polygons selected the panel shows and edits only the
    /// primary (<c>SelectedPolygon</c>); the other is left untouched and nothing says "mixed".
    /// </summary>
    [AvaloniaFact]
    public void TwoPolygonsSelected_PanelEditsOnlyThePrimaryPolygon()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var p0 = new PolygonSave { Name = "P0" };
            var p1 = new PolygonSave { Name = "P1" };
            var chain = NewChain("Walk", NewFrame(p0), NewFrame(p1));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TreeSelect(window, p0, p1);

            var primary = ctx.SelectedState.SelectedPolygon!;
            var other = ReferenceEquals(primary, p0) ? p1 : p0;
            var nameBox = window.FindControl<TextBox>("PropPolygonName")!;
            Assert.Equal(primary.Name, nameBox.Text);

            var otherName = other.Name;
            nameBox.Focus();
            nameBox.Text = "Renamed";
            window.FindControl<NumericUpDown>("PropPolygonX")!.Focus(); // raises LostFocus on the name box
            FlushUi();

            Assert.Equal("Renamed", primary.Name);
            Assert.Equal(otherName, other.Name);
        }
        finally { window.Close(); }
    }

    /// <summary>A rect and a circle selected together: only the primary shape's kind has a panel and gets edited.</summary>
    [AvaloniaFact]
    public void RectAndCircleSelected_OnlyPrimaryKindPanelShowsAndEdits()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var rect = new AARectSave { Name = "R", ScaleX = 8f, ScaleY = 8f };
            var circle = new CircleSave { Name = "C", Radius = 5f };
            var chain = NewChain("Walk", NewFrame(rect), NewFrame(circle));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TreeSelect(window, rect, circle);

            // Whichever the tree routed as primary is the only kind with a visible panel and edit.
            bool circlePrimary = ctx.SelectedState.SelectedShape is CircleSave;
            Assert.Equal(circlePrimary, window.FindControl<Control>("PropCirclePanel")!.IsVisible);
            Assert.Equal(!circlePrimary, window.FindControl<Control>("PropRectPanel")!.IsVisible);

            if (circlePrimary) window.FindControl<NumericUpDown>("PropCircleRadius")!.Value = 40m;
            else window.FindControl<NumericUpDown>("PropRectScaleX")!.Value = 40m;
            FlushUi();

            Assert.Equal(circlePrimary ? 40f : 5f, circle.Radius);
            Assert.Equal(circlePrimary ? 8f : 40f, rect.ScaleX); // the non-primary kind is not edited
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// A group selection holding another chain's frame plus one shape (what clicking a shape in the
    /// preview produces): the inspector shows the shape, an edit lands on it alone, and undo
    /// restores it.
    /// </summary>
    [AvaloniaFact]
    public void GroupSelection_FrameAndShape_InspectorEditsTheShapeAndUndoRestoresIt()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var rect = new AARectSave { Name = "R", ScaleX = 8f, ScaleY = 8f };
            var frameA = NewFrame();
            var frameB = NewFrame(rect);
            var chainA = NewChain("A", frameA);
            var chainB = NewChain("B", frameB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange(new[] { chainA, chainB });

            ctx.SelectedState.SelectedNodes = new List<object> { frameA, rect };
            ctx.SelectedState.SelectShape(rect);
            FlushUi();

            Assert.True(window.FindControl<Control>("PropRectPanel")!.IsVisible);
            Assert.False(window.FindControl<Control>("PropFramePanel")!.IsVisible);

            window.FindControl<NumericUpDown>("PropRectScaleX")!.Value = 20m;
            FlushUi();
            Assert.Equal(20f, rect.ScaleX);

            ctx.UndoManager.Undo();
            FlushUi();
            Assert.Equal(8f, rect.ScaleX);
        }
        finally { window.Close(); }
    }
}
