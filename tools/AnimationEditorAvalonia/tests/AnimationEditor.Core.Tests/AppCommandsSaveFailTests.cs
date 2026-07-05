using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests that AppCommands.SaveCurrentAnimationChainList transitions the undo
/// manager to SaveState.Failed when the underlying save throws.
/// </summary>
public class AppCommandsSaveFailTests
{
    [Fact]
    public void SaveCurrentAnimationChainList_SetsFailedState_WhenSaveThrows()
    {
        var throwingPm = new ThrowingProjectManager();
        throwingPm.FileName = "/some/file.achx";
        throwingPm.AnimationChainListSave = new AnimationChainListSave();

        var events     = new ApplicationEvents();
        var selected   = new SelectedState(throwingPm);
        var appState   = new AppState(events, selected);
        var io         = new IoManager(appState);
        var finder     = new ObjectFinder(throwingPm);
        var undo       = new UndoManager();
        var commands   = new AppCommands(throwingPm, selected, events, io, finder, undo);
        commands.FileDialogService = NullFileDialogService.Instance;

        commands.SaveCurrentAnimationChainList();

        Assert.Equal(SaveState.Failed, undo.SaveState);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_SetsAutoSaveOn_WhenSaveSucceeds()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = "/some/file.achx";

        // Point to a real writable temp file so the save actually succeeds.
        var tmpPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".achx");
        try
        {
            ctx.AppCommands.SaveCurrentAnimationChainList(tmpPath);

            Assert.Equal(SaveState.AutoSaveOn, ctx.UndoManager.SaveState);
        }
        finally
        {
            if (System.IO.File.Exists(tmpPath))
                System.IO.File.Delete(tmpPath);
        }
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class ThrowingProjectManager : IProjectManager
    {
        public AnimationChainListSave? AnimationChainListSave { get; set; }
        public TileMapInformationList TileMapInformationList { get; set; } = new();
        public FilePath[] ReferencedPngs => Array.Empty<FilePath>();
        public string? FileName { get; set; }
        public TextureCoordinateType OnDiskCoordinateType { get; set; }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null) { }

        public string? ResolveFilesPanelRoot() => null;

        public void SaveAnimationChainList(string targetPath)
            => throw new InvalidOperationException("Simulated save failure");

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory)
            => Array.Empty<string>();

        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) => null;
    }
}
