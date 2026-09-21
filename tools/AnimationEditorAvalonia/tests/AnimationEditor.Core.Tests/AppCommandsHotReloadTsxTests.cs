using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Fresh-eyes pass #6 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): <c>SyncHotReloadWatcher</c>
/// watches whatever path <see cref="IProjectManager.FileName"/> currently is, with no extension
/// check -- including a native <c>.tsx</c> tab (<c>TryActivateTabFromCache</c> calls it after
/// restoring a tsx tab from the cache). But <see cref="IAppCommands.ReloadAchxFromDisk"/>, the
/// handler wired to the watcher's changed-on-disk event, unconditionally calls
/// <c>IProjectManager.LoadAnimationChain</c> -- never <c>LoadTsxProject</c>. <c>AnimationChainListSave
/// .FromString</c>'s hand-rolled XML parser doesn't validate the root element name, so parsing a
/// tsx's <c>&lt;tileset&gt;</c> XML as an achx doesn't throw -- it silently succeeds with an empty
/// <c>AnimationChainListSave</c> (zero chains), wiping the tab's animation data with no error.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsHotReloadTsxTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteTsx(string fileName, string xml)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    /// <summary>
    /// Live bug: opening a .tsx never started the hot-reload watcher at all, so an external edit
    /// to the file (hand-editing the raw XML, another tab's "sync associated Tiled tilesets"
    /// writing to it) was never picked up no matter how long you waited or how many times you
    /// saved -- AchxChangedOnDisk can only fire for a path the watcher was told to watch, and
    /// OpenTsxWorkflowAsync never called HotReloadWatcher.StartWatching (it hand-duplicates
    /// FinishLoadIntoEditor's steps instead of calling it, and dropped that one on the way).
    /// </summary>
    [Fact]
    public async Task OpenTsxWorkflowAsync_StartsWatchingTheOpenedTsxPath()
    {
        var spy = new AppCommandsHotReloadTests.SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;

        string tsxPath = WriteTsx("Heroes.tsx", TsxFixtureXml);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);

        // Compares against IProjectManager.FileName (the authoritative "what's open now"),
        // not the raw tsxPath string -- the watcher is started via SyncHotReloadWatcher, which
        // reads the normalized FileName rather than passing the caller's literal path through.
        Assert.Equal(_ctx.ProjectManager.FileName, spy.LastStartAchxPath);
    }

    [Fact]
    public async Task ReloadAchxFromDisk_NativeTsxPath_ReloadsAsTsxNotAsAnEmptyAchx()
    {
        string tsxPath = WriteTsx("Heroes.tsx", TsxFixtureXml);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.NotEmpty(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains);

        // Simulates the tsx changing on disk while this tab is the active/watched one (e.g. a
        // different tab's "sync associated Tiled tilesets" writing to this same file, or a hand
        // edit) -- the watcher fires AchxChangedOnDisk for whatever path IProjectManager.FileName
        // currently is, tsx or achx alike.
        _ctx.AppCommands.ReloadAchxFromDisk(tsxPath);

        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.NotEmpty(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
    }
}
