using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies <see cref="IAppCommands.RebuildTreeViewRequested"/> is raised with the companion
/// file's saved expand state already applied, instead of always empty (collapsed) and relying
/// on a later <see cref="AnimationEditor.Core.IO.IIoManager.SettingsLoaded"/> pass to correct it.
/// The two-pass version flickers on tab switch because the collapse and the re-expand are two
/// separately-dispatched UI updates (issue #547).
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsTreeExpandStateTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    private string WriteAchx(string fileName, params string[] chainNames)
    {
        var path = Path.Combine(_dir.Path, fileName);
        var acls = new AnimationChainListSave();
        foreach (var name in chainNames)
            acls.AnimationChains.Add(new AnimationChainSave { Name = name });
        acls.Save(path);
        return path;
    }

    [Fact]
    public void LoadAnimationChain_WithNoCompanionFile_FiresRebuildTreeViewRequestedWithEmptyExpandedSet()
    {
        var path = WriteAchx("fresh.achx", "Walk", "Run");
        IReadOnlyList<string>? observed = null;
        _ctx.AppCommands.RebuildTreeViewRequested += names => observed = names;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(observed);
        Assert.Empty(observed!);
    }

    [Fact]
    public void LoadAnimationChain_WithSavedExpandedChain_FiresRebuildTreeViewRequestedWithThatChainExpanded()
    {
        var path = WriteAchx("saved.achx", "Walk", "Run");
        _ctx.IoManager.SaveCompanionFileFor(new FilePath(path), new AESettingsSave { ExpandedNodes = new List<string> { "Walk" } });
        IReadOnlyList<string>? observed = null;
        _ctx.AppCommands.RebuildTreeViewRequested += names => observed = names;

        _ctx.AppCommands.LoadAnimationChain(path);

        Assert.NotNull(observed);
        Assert.Equal(new[] { "Walk" }, observed!.ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task TryActivateTabFromCache_WithSavedExpandedChain_FiresRebuildTreeViewRequestedWithThatChainExpanded()
    {
        var path = WriteAchx("cached.achx", "Walk", "Run");
        _ctx.IoManager.SaveCompanionFileFor(new FilePath(path), new AESettingsSave { ExpandedNodes = new List<string> { "Walk" } });
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);

        IReadOnlyList<string>? observed = null;
        _ctx.AppCommands.RebuildTreeViewRequested += names => observed = names;
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tab));

        Assert.NotNull(observed);
        Assert.Equal(new[] { "Walk" }, observed!.ToArray());
    }
}
