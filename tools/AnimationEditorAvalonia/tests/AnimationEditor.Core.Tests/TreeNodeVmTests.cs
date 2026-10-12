using AnimationEditor.Core.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreeNodeVmTests
{
    // ── Meta ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Meta_FiresPropertyChanged()
    {
        var vm = new TreeNodeVm();
        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;

        vm.Meta = "0.55s";

        Assert.Equal(nameof(TreeNodeVm.Meta), changed);
    }

    // ── Row icons: one kind icon + one warning icon per row, not one control per possibility ──

    private const string IconRoot = "avares://AnimationEditor.Views/Assets/icons/svg/";

    [Theory]
    [InlineData("frame", "IconFrame.svg")]
    [InlineData("rect", "IconShape.svg")]
    [InlineData("circle", "IconCircle.svg")]
    [InlineData("polygon", "IconPolygon.svg")]
    [InlineData("chain", "IconChain.svg")]
    public void KindIconPath_IsTheSvgForTheNodeKind(string kind, string file)
    {
        var vm = new TreeNodeVm
        {
            IsFrameNode = kind == "frame",
            IsRectNode = kind == "rect",
            IsCircleNode = kind == "circle",
            IsPolygonNode = kind == "polygon",
            IsChainNode = kind == "chain",
        };

        Assert.Equal(IconRoot + file, vm.KindIconPath);
    }

    [Fact]
    public void KindIconPath_ChainWithThumbnail_IsNullSoTheThumbnailShowsInstead()
    {
        var vm = new TreeNodeVm { IsChainNode = true };
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Thumbnail = new object();

        Assert.Null(vm.KindIconPath);
        Assert.Contains(nameof(TreeNodeVm.KindIconPath), changed);
    }

    [Fact]
    public void WarningIcon_ValidationIssue_ShowsWarningWithItsTooltip()
    {
        var vm = new TreeNodeVm { IsChainNode = true };
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.HasValidationIssue = true;

        Assert.Equal(IconRoot + "IconWarning.svg", vm.WarningIconPath);
        Assert.Contains("tsx file", vm.WarningTooltip);
        Assert.Contains(nameof(TreeNodeVm.WarningIconPath), changed);
    }

    [Fact]
    public void WarningIcon_EmptyAnimation_ShowsWarningWithItsTooltip()
    {
        var vm = new TreeNodeVm { IsChainNode = true };

        vm.HasEmptyAnimationWarning = true;

        Assert.Equal(IconRoot + "IconWarning.svg", vm.WarningIconPath);
        Assert.Contains("no frames", vm.WarningTooltip);
    }

    [Fact]
    public void WarningIcon_NoWarning_IsNull()
    {
        var vm = new TreeNodeVm { IsChainNode = true };

        Assert.Null(vm.WarningIconPath);
        Assert.Null(vm.WarningTooltip);
    }

    // ── IsEditing ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEditing_DefaultsFalse()
    {
        var vm = new TreeNodeVm();
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void IsEditing_CanBeSetTrue()
    {
        var vm = new TreeNodeVm { Header = "Walk" };
        vm.IsEditing = true;
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void IsEditing_FiresPropertyChanged()
    {
        var vm = new TreeNodeVm();
        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;

        vm.IsEditing = true;

        Assert.Equal(nameof(TreeNodeVm.IsEditing), changed);
    }

    [Fact]
    public void IsEditing_SetToSameValue_DoesNotFirePropertyChanged()
    {
        var vm = new TreeNodeVm();
        int count = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TreeNodeVm.IsEditing)) count++; };

        vm.IsEditing = false; // same as default

        Assert.Equal(0, count);
    }

    // ── EditingText ───────────────────────────────────────────────────────────

    [Fact]
    public void EditingText_DefaultsEmpty()
    {
        var vm = new TreeNodeVm();
        Assert.Equal(string.Empty, vm.EditingText);
    }

    [Fact]
    public void EditingText_CanBeSet()
    {
        var vm = new TreeNodeVm();
        vm.EditingText = "NewName";
        Assert.Equal("NewName", vm.EditingText);
    }

    [Fact]
    public void EditingText_FiresPropertyChanged()
    {
        var vm = new TreeNodeVm();
        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;

        vm.EditingText = "Test";

        Assert.Equal(nameof(TreeNodeVm.EditingText), changed);
    }

    // ── CanAddFrame ───────────────────────────────────────────────────────────

    [Fact]
    public void CanAddFrame_ChainNodeUnlocked_ReturnsTrue()
    {
        var vm = new TreeNodeVm { IsChainNode = true, IsLocked = false };
        Assert.True(vm.CanAddFrame);
    }

    [Fact]
    public void CanAddFrame_ChainNodeLocked_ReturnsFalse()
    {
        var vm = new TreeNodeVm { IsChainNode = true, IsLocked = true };
        Assert.False(vm.CanAddFrame);
    }

    [Fact]
    public void CanAddFrame_NonChainNode_ReturnsFalse()
    {
        var vm = new TreeNodeVm { IsFrameNode = true, IsLocked = false };
        Assert.False(vm.CanAddFrame);
    }

    [Fact]
    public void CanAddFrame_FiresPropertyChangedWhenIsLockedChanges()
    {
        var vm = new TreeNodeVm { IsChainNode = true };
        var fired = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.IsLocked = true;

        Assert.Contains(nameof(TreeNodeVm.CanAddFrame), fired);
    }

    // ── BeginEdit ─────────────────────────────────────────────────────────────

    [Fact]
    public void BeginEdit_SetsIsEditingTrue()
    {
        var vm = new TreeNodeVm { Header = "Run" };
        vm.BeginEdit();
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void BeginEdit_SetsEditingTextFromHeader()
    {
        var vm = new TreeNodeVm { Header = "Run" };
        vm.BeginEdit();
        Assert.Equal("Run", vm.EditingText);
    }

    // ── CancelEdit ────────────────────────────────────────────────────────────

    [Fact]
    public void CancelEdit_SetsIsEditingFalse()
    {
        var vm = new TreeNodeVm { Header = "Run" };
        vm.BeginEdit();
        vm.CancelEdit();
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void CancelEdit_DoesNotChangeHeader()
    {
        var vm = new TreeNodeVm { Header = "Run" };
        vm.BeginEdit();
        vm.EditingText = "Modified";
        vm.CancelEdit();
        Assert.Equal("Run", vm.Header);
    }

    // ── SetExpandedRecursive ──────────────────────────────────────────────────

    [Fact]
    public void SetExpandedRecursive_SetsRootNode()
    {
        var root = new TreeNodeVm { IsExpanded = false };

        TreeNodeVm.SetExpandedRecursive(root, true);

        Assert.True(root.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_SetsDirectChildren()
    {
        var root = new TreeNodeVm();
        var child1 = new TreeNodeVm { IsExpanded = false };
        var child2 = new TreeNodeVm { IsExpanded = false };
        root.Children.Add(child1);
        root.Children.Add(child2);

        TreeNodeVm.SetExpandedRecursive(root, true);

        Assert.True(child1.IsExpanded);
        Assert.True(child2.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_SetsGrandchildren()
    {
        // chain → frame → rect  (3-level tree)
        var chain = new TreeNodeVm { IsChainNode = true };
        var frame = new TreeNodeVm { IsFrameNode = true };
        var rect  = new TreeNodeVm { IsRectNode  = true };
        frame.Children.Add(rect);
        chain.Children.Add(frame);

        TreeNodeVm.SetExpandedRecursive(chain, true);

        Assert.True(chain.IsExpanded);
        Assert.True(frame.IsExpanded);
        Assert.True(rect.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_CollapseAll_SetsAllFalse()
    {
        var chain = new TreeNodeVm { IsExpanded = true };
        var frame = new TreeNodeVm { IsExpanded = true };
        var rect  = new TreeNodeVm { IsExpanded = true };
        frame.Children.Add(rect);
        chain.Children.Add(frame);

        TreeNodeVm.SetExpandedRecursive(chain, false);

        Assert.False(chain.IsExpanded);
        Assert.False(frame.IsExpanded);
        Assert.False(rect.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_LeafNode_IsHarmlessNoop()
    {
        var leaf = new TreeNodeVm { IsRectNode = true };  // no children

        var ex = Record.Exception(() => TreeNodeVm.SetExpandedRecursive(leaf, true));

        Assert.Null(ex);
        Assert.True(leaf.IsExpanded);
    }

    // ── IsLeafFrameNode ───────────────────────────────────────────────────────

    [Fact]
    public void IsLeafFrameNode_TrueWhenFrameNodeWithNoChildren()
    {
        var vm = new TreeNodeVm { IsFrameNode = true };
        Assert.True(vm.IsLeafFrameNode);
    }

    [Fact]
    public void IsLeafFrameNode_FalseWhenFrameNodeHasChildren()
    {
        var vm = new TreeNodeVm { IsFrameNode = true };
        vm.Children.Add(new TreeNodeVm { IsRectNode = true });
        Assert.False(vm.IsLeafFrameNode);
    }

    [Fact]
    public void IsLeafFrameNode_FalseForNonFrameNode()
    {
        var vm = new TreeNodeVm { IsChainNode = true };
        Assert.False(vm.IsLeafFrameNode);
    }

    [Fact]
    public void IsLeafFrameNode_FiresPropertyChangedWhenChildAdded()
    {
        var vm = new TreeNodeVm { IsFrameNode = true };
        var fired = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.Children.Add(new TreeNodeVm { IsRectNode = true });

        Assert.Contains(nameof(TreeNodeVm.IsLeafFrameNode), fired);
    }

    [Fact]
    public void IsLeafFrameNode_FiresPropertyChangedWhenChildRemoved()
    {
        var vm = new TreeNodeVm { IsFrameNode = true };
        var child = new TreeNodeVm { IsRectNode = true };
        vm.Children.Add(child);

        var fired = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.Children.Remove(child);

        Assert.Contains(nameof(TreeNodeVm.IsLeafFrameNode), fired);
    }
}
