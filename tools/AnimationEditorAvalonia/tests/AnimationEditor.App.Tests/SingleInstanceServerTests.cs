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

    private static string UniquePipeName() => $"AnimationEditorAvalonia_TestPipe_{Guid.NewGuid():N}";

    [Fact]
    public async Task HandOffWithoutAFile_RaisesActivationRequested()
    {
        var pipe = UniquePipeName();
        using var owner = new SingleInstanceServer(UniqueMutexName($"Activate_{Guid.NewGuid():N}"), pipe);
        var activated = new TaskCompletionSource();
        owner.ActivationRequested += () => activated.TrySetResult();
        owner.StartListening();

        bool reached = await SingleInstanceServer.SendToRunningInstanceAsync(null, pipe);
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(reached);
    }

    [Fact]
    public async Task HandOffWithAFile_RaisesFileOpenRequested_AndNotActivationRequested()
    {
        var pipe = UniquePipeName();
        using var owner = new SingleInstanceServer(UniqueMutexName($"OpenFile_{Guid.NewGuid():N}"), pipe);
        var opened = new TaskCompletionSource<string>();
        bool activated = false;
        owner.FileOpenRequested += path => opened.TrySetResult(path);
        owner.ActivationRequested += () => activated = true;
        owner.StartListening();

        await SingleInstanceServer.SendToRunningInstanceAsync("some.achx", pipe);

        string received = await opened.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("some.achx", received);
        Assert.False(activated);
    }
}
