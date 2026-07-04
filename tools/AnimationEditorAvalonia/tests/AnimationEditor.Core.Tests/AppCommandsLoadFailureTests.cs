using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsLoadFailureTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // ── Missing file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_MissingFile_FiresLoadFailed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        string? capturedPath = null;
        Exception? capturedEx = null;
        ctx.AppCommands.LoadFailed += (path, ex) => { capturedPath = path; capturedEx = ex; };

        ctx.AppCommands.LoadAnimationChain(TestPaths.Abs("does", "not", "exist.achx"));

        Assert.NotNull(capturedPath);
        Assert.NotNull(capturedEx);
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_FiresLoadFailed_ExactlyOnce()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int fireCount = 0;
        ctx.AppCommands.LoadFailed += (_, __) => fireCount++;

        ctx.AppCommands.LoadAnimationChain(TestPaths.Abs("does", "not", "exist.achx"));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotFireRebuildTreeViewRequested()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.AppCommands.RebuildTreeViewRequested += _ => fired = true;

        ctx.AppCommands.LoadAnimationChain(TestPaths.Abs("does", "not", "exist.achx"));

        Assert.False(fired);
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotFireRefreshWireframeRequested()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.AppCommands.RefreshWireframeRequested += () => fired = true;

        ctx.AppCommands.LoadAnimationChain(TestPaths.Abs("does", "not", "exist.achx"));

        Assert.False(fired);
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotChangeAnimationChainListSave()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var sentinel = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave = sentinel;

        ctx.AppCommands.LoadAnimationChain(TestPaths.Abs("does", "not", "exist.achx"));

        Assert.Same(sentinel, ctx.ProjectManager.AnimationChainListSave);
    }

    // ── Corrupt file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_CorruptFile_FiresLoadFailed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var badFile = Path.Combine(_dir.Path, "bad.achx");
        File.WriteAllText(badFile, "this is not valid xml");
        string? capturedPath = null;
        ctx.AppCommands.LoadFailed += (path, _) => capturedPath = path;

        ctx.AppCommands.LoadAnimationChain(badFile);

        Assert.NotNull(capturedPath);
    }

    [Fact]
    public void LoadAnimationChain_CorruptFile_DoesNotFireRebuildTreeViewRequested()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var badFile = Path.Combine(_dir.Path, "bad2.achx");
        File.WriteAllText(badFile, "this is not valid xml");
        bool fired = false;
        ctx.AppCommands.RebuildTreeViewRequested += _ => fired = true;

        ctx.AppCommands.LoadAnimationChain(badFile);

        Assert.False(fired);
    }
}
