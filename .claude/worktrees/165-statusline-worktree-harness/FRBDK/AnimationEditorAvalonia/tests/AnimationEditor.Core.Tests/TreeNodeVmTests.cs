using AnimationEditor.Core.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreeNodeVmTests
{
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
}
