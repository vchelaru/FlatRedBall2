using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Guards the Avalonia version: before 12.0.5 a dispatcher job left the thread in the execution context it was posted
/// from. When that was one without xUnit's context (the continuation of any awaited Task.Run is posted so), and it was
/// the last job of a test, every later test failed with "Cannot get KeyValueStorage on the idle test context".
/// </summary>
public class HeadlessSessionContextTests
{
    [AvaloniaFact]
    public void RunJobs_AJobPostedWithoutAContext_LeavesTheThreadInItsOwn()
    {
        var local = new AsyncLocal<string?> { Value = "test" };
        var poster = new Thread(() => Dispatcher.UIThread.Post(() => { }));
        poster.UnsafeStart();
        poster.Join();

        Dispatcher.UIThread.RunJobs();

        local.Value.ShouldBe("test");
    }
}
