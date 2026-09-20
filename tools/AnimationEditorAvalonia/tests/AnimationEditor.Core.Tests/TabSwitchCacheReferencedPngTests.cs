using AnimationEditor.Core;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Reproduces the TODO item in plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md:
/// <c>ProjectManager.ReferencedPngs</c> has the same "private per-load state
/// <see cref="TabEditorCache"/> can't see" shape as the already-fixed native-tsx and
/// known-texture-size fields -- a cache-hit tab switch (<see
/// cref="AnimationEditor.Core.CommandsAndState.AppCommands.TryActivateTabFromCache"/>) never
/// round-trips it, so reactivating a tab whose achx has a <c>ProjectFile</c> after visiting a
/// tab with none leaves <c>ReferencedPngs</c> reporting the other tab's textures.
/// </summary>
[Collection("SequentialSingletons")]
public class TabSwitchCacheReferencedPngTests
{
    private readonly TestServices _ctx = new();

    [Fact]
    public void TryActivateTabFromCache_SwitchBackAfterAnotherTabLoaded_RestoresThisTabsReferencedPngsNotTheOtherTabs()
    {
        using var temp = new TempDir();
        var contentDir = Path.Combine(temp.Path, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "Hero.png"), "");

        var projectFile = Path.Combine(temp.Path, "Game.gluj");
        File.WriteAllText(projectFile,
            """
            <Project>
              <GlobalFiles>
                <ReferencedFileSave><Name>Hero.png</Name></ReferencedFileSave>
              </GlobalFiles>
            </Project>
            """);

        var achxWithProject = Path.Combine(temp.Path, "WithProject.achx");
        new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            ProjectFile = "Game.gluj",
        }.Save(achxWithProject);

        var achxWithoutProject = Path.Combine(temp.Path, "Plain.achx");
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(achxWithoutProject);

        var tabA = new TabEntry(new FilePath(achxWithProject));
        var tabB = new TabEntry(new FilePath(achxWithoutProject));

        _ctx.ProjectManager.LoadAnimationChain(new FilePath(achxWithProject));
        Assert.NotEmpty(_ctx.ProjectManager.ReferencedPngs);
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        // Loading tab B (no ProjectFile) clears ProjectManager's live ReferencedPngs.
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(achxWithoutProject));
        Assert.Empty(_ctx.ProjectManager.ReferencedPngs);
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        // Switching back to tab A via the cache path must restore tab A's own referenced
        // PNGs, not leave tab B's (empty) behind.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));

        Assert.NotEmpty(_ctx.ProjectManager.ReferencedPngs);
        Assert.Contains(_ctx.ProjectManager.ReferencedPngs, p => p.NoPath.Equals("Hero.png", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
