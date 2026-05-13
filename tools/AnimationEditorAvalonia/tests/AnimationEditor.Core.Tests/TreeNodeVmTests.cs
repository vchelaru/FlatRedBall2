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
}
