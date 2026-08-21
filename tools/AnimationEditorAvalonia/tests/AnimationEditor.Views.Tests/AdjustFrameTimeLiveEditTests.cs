using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// #956: "Adjust All Frame Time" should apply every field edit immediately (so the preview
/// updates live), Cancel should revert those live edits, and OK should keep them.
/// </summary>
public class AdjustFrameTimeLiveEditTests
{
    private sealed class FakeProjectManager : IProjectManager
    {
        public AnimationChainListSave? AnimationChainListSave { get; set; }
        public TileMapInformationList TileMapInformationList { get; set; } = new();
        public FilePath[] ReferencedPngs => Array.Empty<FilePath>();
        public string? FileName { get; set; }
        public string? ProjectFolderPath { get; set; }
        public TextureCoordinateType OnDiskCoordinateType { get; set; }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null) { }

        public void SaveAnimationChainList(string targetPath) { }
        public void SaveAnimationChainList(System.IO.Stream stream) { }
        public string? ResolveFilesPanelRoot() => null;
        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) => null;

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            Array.Empty<string>();
    }

    /// <summary>Runs <paramref name="interact"/> against the dialog's content while it is still
    /// open (so it can assert on the live-applied state), then resolves with Confirm or Cancel.</summary>
    private sealed class ScriptedDialogHost(Action<Control> interact, bool confirm) : IEditorDialogHost
    {
        public async Task<T> ShowAsync<T>(EditorDialog<T> dialog)
        {
            interact(dialog.Content);
            if (confirm) dialog.Confirm(); else dialog.Cancel();
            return await dialog.Result;
        }
    }

    private static (AnimationChainSave Chain, IAppCommands AppCommands, IUndoManager UndoManager) Setup()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.3f });
        acls.AnimationChains.Add(chain);

        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var selectedState = new SelectedState(pm);
        var events = new ApplicationEvents();
        var ioManager = new IoManager(new AppState(events, selectedState));
        var objectFinder = new ObjectFinder(pm);
        var undoManager = new UndoManager();
        var appCommands = new AppCommands(pm, selectedState, events, ioManager, objectFinder, undoManager);

        return (chain, appCommands, undoManager);
    }

    [AvaloniaFact]
    public async Task DurationEdit_AppliesLiveWhileDialogIsOpen()
    {
        var (chain, appCommands, _) = Setup();

        var host = new ScriptedDialogHost(
            interact: content =>
            {
                var durationInput = ((StackPanel)content).Children.OfType<NumericUpDown>().Single();
                durationInput.Value = 0.8m; // was 0.4 total (0.1 + 0.3) -> doubles proportionally
                Assert.Equal(0.2f, chain.Frames[0].FrameLength, 3);
                Assert.Equal(0.6f, chain.Frames[1].FrameLength, 3);
            },
            confirm: true);

        await EditorDialogs.ShowAdjustFrameTimeAsync(host, appCommands, chain);
    }

    [AvaloniaFact]
    public async Task Cancel_RevertsLiveEditsBackToOriginalFrameLengths()
    {
        var (chain, appCommands, undoManager) = Setup();

        var host = new ScriptedDialogHost(
            interact: content =>
            {
                var durationInput = ((StackPanel)content).Children.OfType<NumericUpDown>().Single();
                durationInput.Value = 0.8m;
            },
            confirm: false);

        await EditorDialogs.ShowAdjustFrameTimeAsync(host, appCommands, chain);

        Assert.Equal(0.1f, chain.Frames[0].FrameLength, 3);
        Assert.Equal(0.3f, chain.Frames[1].FrameLength, 3);
        Assert.False(undoManager.CanUndo);
    }

    [AvaloniaFact]
    public async Task Cancel_WithNoEdits_DoesNotTouchTheUndoStack()
    {
        var (chain, appCommands, undoManager) = Setup();
        appCommands.SetAllFrameLengths(chain, 0.5f);
        Assert.True(undoManager.CanUndo);

        var host = new ScriptedDialogHost(interact: _ => { }, confirm: false);
        await EditorDialogs.ShowAdjustFrameTimeAsync(host, appCommands, chain);

        // The unrelated entry pushed before the dialog opened must survive untouched.
        Assert.True(undoManager.CanUndo);
        Assert.Equal(0.5f, chain.Frames[0].FrameLength, 3);
    }

    [AvaloniaFact]
    public async Task Ok_SealsLiveEditsIntoASingleUndoEntry()
    {
        var (chain, appCommands, undoManager) = Setup();

        var host = new ScriptedDialogHost(
            interact: content =>
            {
                var durationInput = ((StackPanel)content).Children.OfType<NumericUpDown>().Single();
                durationInput.Value = 0.8m;
                durationInput.Value = 1.2m; // a second live edit; must coalesce, not add a 2nd entry
            },
            confirm: true);

        await EditorDialogs.ShowAdjustFrameTimeAsync(host, appCommands, chain);

        Assert.True(undoManager.CanUndo);
        undoManager.Undo();
        Assert.Equal(0.1f, chain.Frames[0].FrameLength, 3);
        Assert.Equal(0.3f, chain.Frames[1].FrameLength, 3);
        Assert.False(undoManager.CanUndo);
    }
}
