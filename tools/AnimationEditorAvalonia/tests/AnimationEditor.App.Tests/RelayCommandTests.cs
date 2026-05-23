using AnimationEditor.App;
using Xunit;

namespace AnimationEditor.App.Tests;

public sealed class RelayCommandTests
{
    [Fact]
    public void Execute_CallsProvidedAction()
    {
        bool called = false;
        var cmd = new RelayCommand(() => called = true);
        cmd.Execute(null);
        Assert.True(called);
    }

    [Fact]
    public void CanExecute_AlwaysReturnsTrue()
    {
        var cmd = new RelayCommand(() => { });
        Assert.True(cmd.CanExecute(null));
        Assert.True(cmd.CanExecute("anything"));
    }
}
