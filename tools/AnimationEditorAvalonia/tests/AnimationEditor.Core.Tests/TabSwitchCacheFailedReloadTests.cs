using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// A tab reactivation that fails to reload from disk (unreadable file, missing texture, declined
/// UV conversion) must not be treated as if it succeeded -- see PR #1186 follow-up. Before this
/// fix, <c>ActivateTabContentAsync</c> always ran <c>RestoreTabSelection</c>/<c>CaptureTabEditorState</c>
/// for the tab it was asked to activate, even when the reload underneath it failed and the live
/// document was still whatever the *previous* tab left behind -- silently copying that other
/// tab's document into the failed tab's cache.
/// </summary>
[Collection("SequentialSingletons")]
public class TabSwitchCacheFailedReloadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    private string WriteAchx(string fileName, string chainName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
    }

    [Fact]
    public async Task ActivateTabContentAsync_WhenReloadFails_ReturnsFalse_AndDoesNotPoisonTheTabsCache()
    {
        string pathA = WriteAchx("a.achx", "Walk");
        string pathB = WriteAchx("b.achx", "Run");
        var tabA = new TabEntry(new FilePath(pathA));
        var tabB = new TabEntry(new FilePath(pathB));

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathA);
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathB);
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        // tabA's cache is stale (its file changed on disk after it was cached) and the new
        // content on disk is unreadable, so reactivating it must fail.
        TabEditorCache.Invalidate(tabA);
        File.WriteAllText(pathA, "not a valid achx file{{{");

        bool activated = await _ctx.AppCommands.ActivateTabContentAsync(tabA);

        Assert.False(activated);
        Assert.Equal("Run", _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name);
        Assert.Null(tabA.CachedEditorModel);
    }
}
