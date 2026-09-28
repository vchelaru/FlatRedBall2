using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for the single-instance lifecycle. The second process to
/// launch never owns the named mutex, so disposing it must not throw
/// "Object synchronization method was called from an unsynchronized block of code"
/// (which happens when ReleaseMutex is called by a thread that doesn't own it).
/// </summary>
public class SingleInstanceServerTests
{
    // Unique per test run so the named system mutex can't collide with a real
    // running AnimationEditor instance on the dev machine.
    private static string UniqueMutexName(string suffix)
        => $"AnimationEditorAvalonia_Test_{suffix}";

    [Fact]
    public void Dispose_NonOwner_DoesNotThrow()
    {
        var name = UniqueMutexName("NonOwner");

        using var owner = new SingleInstanceServer(name);
        Assert.True(owner.IsOwner);

        // Second instance in the same process finds the mutex already held.
        var second = new SingleInstanceServer(name);
        Assert.False(second.IsOwner);

        var ex = Record.Exception(() => second.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Owner_DoesNotThrow()
    {
        var owner = new SingleInstanceServer(UniqueMutexName("Owner"));
        Assert.True(owner.IsOwner);

        var ex = Record.Exception(() => owner.Dispose());
        Assert.Null(ex);
    }
}
