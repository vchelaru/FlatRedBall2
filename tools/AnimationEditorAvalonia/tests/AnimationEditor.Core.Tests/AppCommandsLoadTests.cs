using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies that <see cref="IAppCommands.LoadAnimationChain"/> pre-selects the
/// first chain after loading so the wireframe panel shows content immediately
/// instead of flashing then clearing.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsLoadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string WriteAchx(params string[] chainNames)
    {
        var path = Path.Combine(_dir.Path, "test.achx");
        var acls = new AnimationChainListSave();
        foreach (var name in chainNames)
            acls.AnimationChains.Add(new AnimationChainSave { Name = name });
        acls.Save(path);
        return path;
    }

    private string WriteAchxWithFrame(string chainName, string textureName)
    {
        var path = Path.Combine(_dir.Path, "test.achx");
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave { TextureName = textureName, FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    // ── First-chain pre-selection ─────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_WithChains_SetsSelectedChainToFirst()
    {
        var path = WriteAchx("Walk", "Run");

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(_ctx.SelectedState.SelectedChain);
        Assert.Equal("Walk", _ctx.SelectedState.SelectedChain!.Name);
    }

    [Fact]
    public void LoadAnimationChain_WithChains_DoesNotSelectSecondChain()
    {
        var path = WriteAchx("Walk", "Run");

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotEqual("Run", _ctx.SelectedState.SelectedChain?.Name);
    }

    [Fact]
    public void LoadAnimationChain_WithSingleChainAndFrame_SetsSelectedChain()
    {
        var path = WriteAchxWithFrame("Idle", "hero.png");

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(_ctx.SelectedState.SelectedChain);
        Assert.Equal("Idle", _ctx.SelectedState.SelectedChain!.Name);
    }

    [Fact]
    public void LoadAnimationChain_EmptyAcls_SelectedChainIsNull()
    {
        var path = WriteAchx(); // zero chains

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.Null(_ctx.SelectedState.SelectedChain);
    }

    // ── Frame is cleared ─────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_SelectedFrameIsNull()
    {
        // Pre-populate a frame selection
        var priorChain = new AnimationChainSave { Name = "Prior" };
        var priorFrame = new AnimationFrameSave { FrameLength = 0.1f };
        priorChain.Frames.Add(priorFrame);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(priorChain);
        _ctx.SelectedState.SelectedFrame = priorFrame;

        var path = WriteAchx("NewWalk");
        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.Null(_ctx.SelectedState.SelectedFrame);
    }

    // ── Event-ordering regression guard ──────────────────────────────────────

    /// <summary>
    /// SelectedChain must be set BEFORE RefreshWireframeRequested fires so
    /// the wireframe sees the correct chain and does not clear the display.
    /// </summary>
    [Fact]
    public void LoadAnimationChain_SelectedChainIsSetBeforeWireframeRefresh()
    {
        var path = WriteAchx("Idle");
        AnimationChainSave? observedDuringRefresh = null;
        _ctx.AppCommands.RefreshWireframeRequested += () =>
            observedDuringRefresh = _ctx.SelectedState.SelectedChain;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(observedDuringRefresh);
        Assert.Equal("Idle", observedDuringRefresh!.Name);
    }

    /// <summary>
    /// SelectedChain must be set BEFORE RebuildTreeViewRequested fires so
    /// any tree subscriber can reflect the selection immediately.
    /// </summary>
    [Fact]
    public void LoadAnimationChain_SelectedChainIsSetBeforeTreeRebuild()
    {
        var path = WriteAchx("Walk");
        AnimationChainSave? observedDuringRebuild = null;
        _ctx.AppCommands.RebuildTreeViewRequested += _ =>
            observedDuringRebuild = _ctx.SelectedState.SelectedChain;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(observedDuringRebuild);
        Assert.Equal("Walk", observedDuringRebuild!.Name);
    }

    // ── Tree rebuild (collapsed-on-load) ─────────────────────────────────────

    /// <summary>
    /// Loading a file must raise <see cref="IAppCommands.RebuildTreeViewRequested"/>
    /// — the rebuild path collapses every chain — rather than the diff-update
    /// <see cref="IAppCommands.RefreshTreeViewRequested"/> path, which would
    /// re-expand every freshly-built chain node.
    /// </summary>
    [Fact]
    public void LoadAnimationChain_FiresRebuildTreeViewRequested()
    {
        var path = WriteAchx("Walk");
        bool rebuildFired = false;
        _ctx.AppCommands.RebuildTreeViewRequested += _ => rebuildFired = true;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.True(rebuildFired);
    }

    [Fact]
    public void LoadAnimationChain_DoesNotFireRefreshTreeViewRequested()
    {
        var path = WriteAchx("Walk");
        bool refreshFired = false;
        _ctx.AppCommands.RefreshTreeViewRequested += () => refreshFired = true;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.False(refreshFired);
    }
}
