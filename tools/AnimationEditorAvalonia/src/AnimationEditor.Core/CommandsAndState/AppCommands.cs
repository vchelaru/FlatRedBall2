using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.DragDrop;
using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Tiled;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using StringFunctions = AnimationEditor.Core.Utilities.StringFunctions;

namespace AnimationEditor.Core.CommandsAndState
{
    /// <summary>
    /// Platform-independent command hub. UI-thread marshalling and dialog confirmations
    /// are handled by the registered delegates so this class stays free of Avalonia/WinForms.
    /// </summary>
    /// <remarks>
    /// Every mutating method here is a thin coordinator: it gathers any pre-state the
    /// operation needs (selection, dialog results, texture-height resolvers, unique
    /// names) and hands the actual mutation to an <see cref="IUndoableCommand"/> run
    /// through <see cref="IUndoManager.Execute"/>. The command owns the do/undo/redo and
    /// the refresh/save/event boilerplate — so a method cannot mutate project state
    /// without producing an undo entry.
    /// </remarks>
    public class AppCommands : IAppCommands
    {
        private readonly IProjectManager _pm;

        /// <summary>The unit of the open file's frame lengths (#1829). Every frame length the commands create or edit is in it.</summary>
        private FrameLengthUnit CurrentFrameLengthUnit => FrameLengthUnit.Of(_pm.AnimationChainListSave);
        private readonly ISelectedState _selectedState;
        private readonly IApplicationEvents _events;
        private readonly IObjectFinder _objectFinder;
        private readonly IIoManager _ioManager;
        private readonly IUndoManager _undoManager;

        public AppCommands(
            IProjectManager pm,
            ISelectedState selectedState,
            IApplicationEvents events,
            IIoManager ioManager,
            IObjectFinder objectFinder,
            IUndoManager undoManager)
        {
            _pm = pm;
            _selectedState = selectedState;
            _events = events;
            _ioManager = ioManager;
            _objectFinder = objectFinder;
            _undoManager = undoManager;

            // Autosave policy: every command that mutates the animation data raises
            // AnimationChainsChanged (constructors/Do() of ~30 IUndoableCommand types, plus a
            // couple of direct call sites in this class). This is the ONLY place that reacts
            // to it by writing the change to disk -- it used to live in MainWindow (the Avalonia
            // app layer), which meant "does an edit actually get saved" had zero test coverage
            // and was twice misdiagnosed while investigating issue #839. Living here instead
            // means AppCommandsSaveOnChangeTests exercises it with no Avalonia/UI involved.
            _events.AnimationChainsChanged += OnAnimationChainsChanged;
            _undoManager.CommandRan += DeselectWhatLeftTheDocument;
        }

        // For every command, undo and redo: a command only knows the items it removes, not what else inside them is selected.
        private void DeselectWhatLeftTheDocument()
        {
            if (_pm.AnimationChainListSave is not { } acls) return;
            var frames = acls.AnimationChains.SelectMany(c => c.Frames).ToList();
            var present = acls.AnimationChains.Cast<object>().Concat(frames)
                .Concat(frames.SelectMany(f => f.ShapesSave?.Shapes ?? [])).ToHashSet();

            _selectedState.SelectedNodes = _selectedState.SelectedNodes.Where(present.Contains).ToList();
            if (_selectedState.SelectedChain is { } chain && !present.Contains(chain))
                _selectedState.SelectedChain = null;
            if (_selectedState.SelectedFrame is { } frame && !present.Contains(frame))
                _selectedState.SelectedFrame = null;
            if (_selectedState.SelectedShape is { } shape && !present.Contains(shape))
                _selectedState.SelectShape(null);
        }

        // The one save per edit. SaveCurrentAnimationChainList itself writes the crash-recovery
        // snapshot when there is no file yet, so an untitled document is covered too. Commands
        // must NOT call SaveCurrentAnimationChainList on their own as well: every edit, undo and
        // redo used to be written twice (two disk writes, two toasts).
        private void OnAnimationChainsChanged()
        {
            if (AutosaveOnChange) SaveCurrentAnimationChainList();
        }

        /// <summary>
        /// When false, an edit raises its events but writes nothing, so a headless caller (the CLI) can apply
        /// a batch of edits and save once, or not at all. The editor leaves it true.
        /// </summary>
        public bool AutosaveOnChange { get; set; } = true;

        // ── Chain lock (#1032) ────────────────────────────────────────────────────
        // A locked chain's frame/shape *content* cannot be edited (add/delete/move/duplicate/
        // flip/reorder frames; add/delete/move/resize/duplicate shapes; any property edit on a
        // frame or shape belonging to the chain). Chain-level container operations (rename,
        // delete, reorder, duplicate-the-whole-chain) are NOT gated here — only content edits.

        /// <summary>True when <paramref name="chain"/> is non-null and locked.</summary>
        private static bool IsChainLocked(AnimationChainSave? chain) => chain?.IsLocked == true;

        /// <summary>True when <paramref name="frame"/>'s owning chain is locked.</summary>
        private bool IsFrameLocked(AnimationFrameSave frame) =>
            IsChainLocked(_objectFinder.GetAnimationChainContaining(frame));

        /// <summary>
        /// True when the shape's owning chain is locked. <paramref name="knownFrame"/> is used
        /// when the caller already resolved it (avoids a second lookup); falls back to resolving
        /// the frame from <paramref name="shape"/> when <c>null</c>.
        /// </summary>
        private bool IsShapeLocked(AnimationFrameSave? knownFrame, ShapeSave shape)
        {
            var frame = knownFrame ?? _objectFinder.GetAnimationFrameContaining(shape);
            return frame is not null && IsFrameLocked(frame);
        }

        public void SetChainLocked(AnimationChainSave chain, bool locked) =>
            _undoManager.Execute(new SetChainLockedCommand(chain, locked, this, _events));

        public void SetChainsLocked(IReadOnlyList<AnimationChainSave> chains, bool locked)
        {
            var commands = chains
                .Where(c => c.IsLocked != locked)
                .Select(c => (IUndoableCommand)new SetChainLockedCommand(c, locked, this, _events))
                .ToArray();
            if (commands.Length == 0) return;
            _undoManager.Execute(commands.Length == 1
                ? commands[0]
                : new CompositeCommand(commands, $"Set Locked on {commands.Length} Animations", _events));
        }

        public void SetChainLoop(AnimationChainSave chain, bool loop)
        {
            if (IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new SetChainLoopCommand(chain, loop, this, _events));
        }

        public void SetChainsLoop(IReadOnlyList<AnimationChainSave> chains, bool loop)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var commands = chains
                .Distinct()
                .Where(c => c.Loop != loop)
                .Select(c => (IUndoableCommand)new SetChainLoopCommand(c, loop, this, _events))
                .ToArray();
            if (commands.Length == 0) return;
            _undoManager.Execute(commands.Length == 1
                ? commands[0]
                : new CompositeCommand(commands, $"Set Loop on {commands.Length} Animations", _events));
        }

        public void SetChainsPingPong(IReadOnlyList<AnimationChainSave> chains, bool pingPong) =>
            SetChainsPlaybackMode(chains, ChainPlaybackMode.PingPong, pingPong);

        public void SetChainsReverse(IReadOnlyList<AnimationChainSave> chains, bool reverse) =>
            SetChainsPlaybackMode(chains, ChainPlaybackMode.Reverse, reverse);

        private void SetChainsPlaybackMode(IReadOnlyList<AnimationChainSave> chains, ChainPlaybackMode mode, bool value)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var commands = chains
                .Distinct()
                .Where(c => SetChainPlaybackModeCommand.Get(c, mode) != value)
                .Select(c => (IUndoableCommand)new SetChainPlaybackModeCommand(c, mode, value, this, _events))
                .ToArray();
            if (commands.Length == 0) return;
            _undoManager.Execute(commands.Length == 1
                ? commands[0]
                : new CompositeCommand(commands,
                    $"Set {(mode == ChainPlaybackMode.PingPong ? "Ping-Pong" : "Reverse")} on {commands.Length} Animations", _events));
        }

        public string? SetChainTsxOwnerTileId(AnimationChainSave chain, uint tileId)
        {
            var command = new SetChainTsxOwnerTileIdCommand(chain, tileId, _pm, this, _events);
            _undoManager.Execute(command);
            return command.Error;
        }

        // -- Native tsx: achx-only data ------------------------------------------------------
        // A Tiled tile animation holds rects and durations, nothing else (see
        // Tiled.TsxLossyDataCheck, which warns on save about whatever slipped through). Every
        // command that would create shapes, flips, sprite offsets, color, a non-looping chain or
        // another texture no-ops in a native tsx project, so no entry point (tree menu, inspector, keyboard)
        // can produce data the file can't keep. Same pattern as the locked-chain guards.

        /// <summary>True when the current project is a native tsx, i.e. the edit must not happen.</summary>
        private bool IsAchxOnlyEditBlocked() => _pm.IsNativeTsxProject;

        /// <summary>True when <paramref name="textureName"/> is not the open tsx's image: a tileset has one, and its frames keep it.</summary>
        private bool IsTextureBlocked(string? textureName) => _pm.TsxTileset is { } tileset && !tileset.IsImage(textureName ?? "");

        /// <summary>A frame showing the open tsx's tile 0, or null in an achx/achj project.</summary>
        private AnimationFrameSave? FirstTsxTileFrame()
        {
            if (_pm.TsxTileset is not { } tileset) return null;
            var frame = new AnimationFrameSave
            {
                TextureName = tileset.ImageFileName,
                FrameLength = CurrentFrameLengthUnit.DefaultFrameLength,
            };
            new FrameRegion(tileset.Margin, tileset.Margin, tileset.Margin + tileset.TileWidth, tileset.Margin + tileset.TileHeight).ApplyTo(frame);
            return frame;
        }
        // Delegates wired up by the Avalonia app layer ----------------------------

        /// <summary>
        /// Run <paramref name="action"/> on the UI thread. Wired to
        /// <c>Dispatcher.UIThread.InvokeAsync</c> by the app layer.
        /// </summary>
        public Action<Action> DoOnUiThread { get; set; } = action => action();

        /// <summary>
        /// Show a yes/no confirmation dialog and return the user's answer.
        /// Wired by the app layer to an Avalonia MessageBox or equivalent.
        /// </summary>
        public Func<string, string, Task<bool>> ConfirmAsync { get; set; } =
            (message, title) => Task.FromResult(true);

        /// <summary>
        /// Show a text-input dialog and return the typed value, or <c>null</c> if the user
        /// cancelled. Parameters: title, prompt, initial value.
        /// Wired by the app layer to an Avalonia text-input dialog.
        /// </summary>
        public Func<string, string, string, Task<string?>> PromptStringAsync { get; set; } =
            (title, prompt, initial) => Task.FromResult<string?>(initial);

        /// <summary>
        /// Request a full tree-view refresh. Raised instead of calling TreeViewManager directly.
        /// </summary>
        public event Action? RefreshTreeViewRequested;

        /// <inheritdoc cref="IAppCommands.RebuildTreeViewRequested"/>
        public event Action<IReadOnlyList<string>>? RebuildTreeViewRequested;

        /// <summary>
        /// Request a single chain's tree node to refresh.
        /// </summary>
        public event Action<AnimationChainSave>? RefreshChainNodeRequested;

        /// <summary>
        /// Request a single frame's tree node to refresh.
        /// </summary>
        public event Action<AnimationFrameSave>? RefreshFrameNodeRequested;

        /// <summary>
        /// Request the preview/animation-frame display to refresh.
        /// </summary>
        public event Action? RefreshAnimationFrameDisplayRequested;

        /// <summary>
        /// Request the wireframe to refresh.
        /// </summary>
        public event Action? RefreshWireframeRequested;

        /// <summary>
        /// File dialog abstraction. Wired to Avalonia's <c>StorageProvider</c> by the
        /// app layer. Defaults to <see cref="NullFileDialogService"/> (always cancels).
        /// </summary>
        public IFileDialogService FileDialogService { get; set; } = NullFileDialogService.Instance;

        /// <inheritdoc cref="IAppCommands.HotReloadWatcher"/>
        public IHotReloadWatcher HotReloadWatcher { get; set; } = NullHotReloadWatcher.Instance;

        /// <summary>
        /// Raised after a frame, shape, or animation chain is deleted. The argument is a
        /// short label for the deleted item(s) (e.g. <c>"Frame 2"</c>, <c>"Hitbox"</c>,
        /// <c>"3 shapes"</c>). The app layer shows an undo toast in response.
        /// </summary>
        public event Action<string>? ItemsDeleted;

        /// <inheritdoc cref="IAppCommands.ChainNamesRemoved"/>
        public event Action<RemovedChainNames>? ChainNamesRemoved;

        /// <summary>
        /// Fired after <see cref="SaveCurrentAnimationChainListAsync"/> successfully saves a file.
        /// The argument is the full path of the saved file.
        /// </summary>
        public event Action<string>? SaveAsCompleted;

        /// <inheritdoc cref="IAppCommands.ExportCompleted"/>
        public event Action<string, IReadOnlyList<string>>? ExportCompleted;

        /// <inheritdoc cref="IAppCommands.TsxSaveCompletedWithWarnings"/>
        public event Action<string, IReadOnlyList<string>>? TsxSaveCompletedWithWarnings;

        /// <inheritdoc cref="IAppCommands.SaveFailed"/>
        public event Action<string>? SaveFailed;

        /// <inheritdoc cref="IAppCommands.IsPathOpenInAnotherTab"/>
        public Func<string, bool>? IsPathOpenInAnotherTab { get; set; }

        /// <inheritdoc cref="IAppCommands.IsDocumentTabInFront"/>
        public Func<bool>? IsDocumentTabInFront { get; set; }

        /// <inheritdoc cref="IAppCommands.SaveAsRefused"/>
        public event Action<string>? SaveAsRefused;

        /// <inheritdoc cref="IAppCommands.LoadFailed"/>
        public event Action<string, Exception>? LoadFailed;

        /// <inheritdoc cref="IAppCommands.HotReloadFailed"/>
        public event Action<string, string>? HotReloadFailed;

        /// <inheritdoc cref="IAppCommands.EditorProjectModelChanged"/>
        public event Action<string?>? EditorProjectModelChanged;

        // ── Open workflow ─────────────────────────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.OpenAchxWorkflowAsync"/>
        public async Task<bool> OpenAchxWorkflowAsync(string path)
        {
            // Quick-parse to read CoordinateType without committing to a full load. Must dispatch
            // on extension the same way ProjectManager.LoadAnimationChain does (#878) -- a .achj
            // (JSON) file hits the XML parser otherwise and fails with an XmlException.
            AnimationChainListSave preview;
            var diskState = ReadDiskState(path);
            try
            {
                preview = ProjectManager.IsJsonPath(path)
                    ? AnimationChainListSave.FromJsonFile(path)
                    : AnimationChainListSave.FromFile(path);
            }
            catch (Exception ex) { LoadFailed?.Invoke(path, ex); return false; }

            string achxDir = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var missing = _pm.FindMissingTextures(preview, achxDir);

            var outcome = IO.UvLoadGate.DecideOutcome(
                preview.CoordinateType,
                allTexturesResolvable: missing.Count == 0,
                userConfirmed: false);

            if (outcome == IO.UvLoadOutcome.RefuseMissingTextures)
            {
                var names = string.Join(", ", missing);
                LoadFailed?.Invoke(path,
                    new InvalidOperationException(
                        $"Cannot open '{System.IO.Path.GetFileName(path)}' — the following {Plural.Noun(missing.Count, "texture")} could not be found or decoded: {names}. " +
                        "All textures must be present to convert UV coordinates to pixel coordinates."));
                return false;
            }

            if (outcome == IO.UvLoadOutcome.ConvertAndLoad || outcome == IO.UvLoadOutcome.RefuseUserDeclined)
            {
                // UV file, textures all present — ask user.
                bool confirmed = await ConfirmAsync(
                    "This animation uses legacy texture-coordinate (UV) data and must be converted " +
                    "to pixel coordinates before editing. Convert and open? " +
                    "(No leaves the file untouched and does not open it.)",
                    "Convert to Pixel Coordinates");

                outcome = IO.UvLoadGate.DecideOutcome(preview.CoordinateType, allTexturesResolvable: true, userConfirmed: confirmed);
            }

            if (outcome == IO.UvLoadOutcome.RefuseUserDeclined) return false;

            bool failed = false;
            void OnFail(string _, Exception __) => failed = true;
            LoadFailed += OnFail;
            try { LoadAnimationChainFromParsed(path, preview, diskState); }
            finally { LoadFailed -= OnFail; }

            if (failed) return false;

            if (outcome == IO.UvLoadOutcome.ConvertAndLoad)
                _pm.OnDiskCoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel;

            _events.CallAchxLoaded(path);
            _events.RaiseCurrentFileChanged(path);
            _events.RaiseAvailableTexturesChanged();
            return true;
        }

        /// <inheritdoc cref="IAppCommands.OpenTsxWorkflowAsync"/>
        public Task<bool> OpenTsxWorkflowAsync(string path)
        {
            var diskState = ReadDiskState(path);
            try
            {
                _pm.LoadTsxProject(new FilePath(path));
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(path, ex);
                return Task.FromResult(false);
            }

            // Was hand-duplicating FinishLoadIntoEditor's steps here instead of calling it, and
            // had drifted out of sync with it: missing HotReloadWatcher.StartWatching (a tsx's
            // external changes -- hand edits, a save from Tiled --
            // were never picked up, no matter how long you waited) and missing
            // LoadAndApplyCompanionFileFor (saved grid/zoom/expanded-tree-state silently dropped
            // on every tsx open). Calling the shared helper, same as the achx open path
            // (OpenAchxWorkflowAsync -> LoadAnimationChainFromParsed -> FinishLoadIntoEditor)
            // below, makes this impossible to drift out of sync with again.
            FinishLoadIntoEditor(path, diskState);

            // Reuses the achx-named event -- both mean "a project file finished loading,"
            // and every current subscriber (recent files, window title, etc.) treats it generically.
            _events.CallAchxLoaded(path);
            _events.RaiseCurrentFileChanged(path);
            _events.RaiseAvailableTexturesChanged();
            return Task.FromResult(true);
        }

        /// <inheritdoc cref="IAppCommands.OpenProjectWorkflowAsync"/>
        public Task<bool> OpenProjectWorkflowAsync(string path) =>
            new FilePath(path).Extension == "tsx" ? OpenTsxWorkflowAsync(path) : OpenAchxWorkflowAsync(path);

        // -------------------------------------------------------------------------

        public void LoadAnimationChain(string fileName)
        {
            var diskState = ReadDiskState(fileName);
            try
            {
                _pm.LoadAnimationChain(new AnimationEditor.Core.Paths.FilePath(fileName));
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(fileName, ex);
                return;
            }

            FinishLoadIntoEditor(fileName, diskState);
        }

        private void LoadAnimationChainFromParsed(string fileName, AnimationChainListSave preParsed, DiskState? diskState)
        {
            try
            {
                _pm.LoadAnimationChain(new AnimationEditor.Core.Paths.FilePath(fileName), preParsed);
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(fileName, ex);
                return;
            }

            FinishLoadIntoEditor(fileName, diskState);
        }

        /// <param name="diskState">Taken before the file was read, so an edit made while it loaded is not missed.</param>
        private void FinishLoadIntoEditor(string fileName, DiskState? diskState)
        {
            // A fresh, successful load of this path is by definition in sync with disk again.
            _staleOnDiskReasons.Remove(new AnimationEditor.Core.Paths.FilePath(fileName).FullPath);
            _diskState = diskState;
            _undoManager.Clear();
            _undoManager.MarkSaved();
            _selectedState.Reset();
            _selectedState.SelectedChain = _pm.AnimationChainListSave?.AnimationChains.FirstOrDefault();
            RefreshEditorSurfaceForActiveProject(fileName);
        }

        /// <summary>
        /// Refreshes everything about the editor's UI/watcher state that depends only on "this is
        /// now the active project" — not on how it got there. Shared by <see
        /// cref="FinishLoadIntoEditor"/> (a fresh disk load) and <see
        /// cref="TryActivateTabFromCache"/> (reactivating an already-loaded tab), which is why it
        /// deliberately does NOT touch undo history or selection: those two callers disagree on
        /// what to do with them (a fresh load resets both; reactivating a cached tab must preserve
        /// undo history and restore the tab's own prior selection instead), so each decides that
        /// for itself before calling this.
        /// </summary>
        private void RefreshEditorSurfaceForActiveProject(string fileName)
        {
            // Rebuild (not refresh): a freshly-opened file should present a collapsed,
            // scannable overview rather than every chain's frames expanded — unless a
            // companion file already recorded which chains were expanded, in which case
            // build the tree pre-expanded to that state in one pass. Building collapsed
            // and correcting it once LoadAndApplyCompanionFileFor's settings land would
            // flicker (two separately-dispatched UI updates).
            RebuildTreeViewRequested?.Invoke((IReadOnlyList<string>?)_ioManager.TryLoadCompanionSettings(fileName)?.ExpandedNodes ?? Array.Empty<string>());
            _ioManager.LoadAndApplyCompanionFileFor(fileName);
            RefreshWireframeRequested?.Invoke();
            RefreshAnimationFrameDisplayRequested?.Invoke();

            // Start (or restart) watching the active file and its referenced PNGs. _pm.FileName is
            // already fileName by the time either caller reaches this point.
            SyncHotReloadWatcher();

            EditorProjectModelChanged?.Invoke(fileName);
        }

        /// <inheritdoc cref="IAppCommands.CaptureTabEditorState"/>
        public void CaptureTabEditorState(TabEntry tab)
        {
            TabEditorCache.CaptureFromProject(tab, _pm);
            tab.CachedDiskState = _diskState;
            tab.SaveFailed = _undoManager.SaveState == SaveState.Failed;
            tab.CachedSelectedChainName = _selectedState.SelectedChain?.Name;
            tab.CachedSelectedFrameIndex = _selectedState.SelectedFrame != null
                ? _selectedState.SelectedChain?.Frames.IndexOf(_selectedState.SelectedFrame)
                : null;
        }

        /// <inheritdoc cref="IAppCommands.TryActivateTabFromCache"/>
        public bool TryActivateTabFromCache(TabEntry tab)
        {
            if (tab.CachedEditorModel == null || (!tab.SaveFailed && tab.CachedDiskState?.HasChanged(tab.Path) == true))
            {
                TabEditorCache.Invalidate(tab);
                return false;
            }

            TabEditorCache.ApplyToProject(tab, _pm);
            _diskState = tab.CachedDiskState;
            // The undo manager is shared by every tab, so the save state shown (and asked about on close) must be this tab's.
            if (tab.SaveFailed)
                _undoManager.MarkSaveFailed();
            else
                _undoManager.MarkSaved();
            RestoreTabSelection(tab);
            RefreshEditorSurfaceForActiveProject(tab.Path.FullPath);

            _events.RaiseCurrentFileChanged(tab.Path.FullPath);
            _events.RaiseAvailableTexturesChanged();
            if (tab.SaveFailed)
                RefuseIfChangedOnDisk(tab.Path.FullPath, _diskState);
            return true;
        }

        /// <inheritdoc cref="IAppCommands.ActivateTabContentAsync"/>
        public async Task<bool> ActivateTabContentAsync(TabEntry tab)
        {
            if (TryActivateTabFromCache(tab))
                return true;

            // FinishLoadIntoEditor resets selection to the first chain; keep the prior
            // per-tab selection so we can restore it after the disk load.
            string? chainName = tab.CachedSelectedChainName;
            int? frameIndex = tab.CachedSelectedFrameIndex;

            bool loaded = await OpenProjectWorkflowAsync(tab.Path.FullPath);
            if (!loaded)
            {
                // The live document is still whatever the previous tab left behind -- capturing
                // it here would poison *this* tab's cache with someone else's document.
                return false;
            }

            tab.CachedSelectedChainName = chainName;
            tab.CachedSelectedFrameIndex = frameIndex;
            RestoreTabSelection(tab);
            CaptureTabEditorState(tab);
            return true;
        }

        /// <inheritdoc cref="IAppCommands.RestoreTabSelection"/>
        public void RestoreTabSelection(TabEntry tab) =>
            RestoreSelection(tab.CachedSelectedChainName, tab.CachedSelectedFrameIndex);

        /// <summary>
        /// Applies chain/frame selection by name and frame index. Sets chain before frame
        /// because <see cref="ISelectedState.SelectedChain"/> clears the frame.
        /// </summary>
        private void RestoreSelection(string? selectedChainName, int? selectedFrameIndex)
        {
            _selectedState.Reset();
            var acls = _pm.AnimationChainListSave;
            if (acls == null || selectedChainName == null)
            {
                if (acls != null)
                    _selectedState.SelectedChain = acls.AnimationChains.FirstOrDefault();
                return;
            }

            var restoredChain = acls.AnimationChains
                .FirstOrDefault(c => c.Name == selectedChainName);
            if (restoredChain == null)
            {
                _selectedState.SelectedChain = acls.AnimationChains.FirstOrDefault();
                return;
            }

            _selectedState.SelectedChain = restoredChain;
            if (selectedFrameIndex.HasValue &&
                selectedFrameIndex.Value >= 0 &&
                selectedFrameIndex.Value < restoredChain.Frames.Count)
            {
                _selectedState.SelectedFrame = restoredChain.Frames[selectedFrameIndex.Value];
            }
        }

        public void RefreshTreeNode(AnimationChainSave animationChain) =>
            RefreshChainNodeRequested?.Invoke(animationChain);

        public void RefreshTreeNode(AnimationFrameSave animationFrame) =>
            RefreshFrameNodeRequested?.Invoke(animationFrame);

        public void RefreshAnimationFrameDisplay() =>
            RefreshAnimationFrameDisplayRequested?.Invoke();

        public void RefreshWireframe() =>
            RefreshWireframeRequested?.Invoke();

        public void RefreshTreeView() =>
            RefreshTreeViewRequested?.Invoke();

        /// <summary>
        /// Files whose on-disk content changed under an open tab and could not be reloaded
        /// (<see cref="ReloadAchxFromDisk"/> threw), keyed by full path with the failure reason.
        /// The in-memory model is stale relative to disk from that moment, so writing it back
        /// would destroy whatever the other program (Tiled, a text editor) just saved. Cleared
        /// when a later reload of that path succeeds.
        /// </summary>
        private readonly Dictionary<string, string> _staleOnDiskReasons = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The open document's <see cref="TabEntry.CachedDiskState"/>.</summary>
        private DiskState? _diskState;

        // Null in the CLI: it has no tabs to switch back to, and hashing would slow its startup.
        private DiskState? ReadDiskState(string path) => AutosaveOnChange ? DiskState.Read(new FilePath(path)) : null;

        private bool RefuseIfStale(string path, bool toAnotherFile = false)
        {
            if (!_staleOnDiskReasons.TryGetValue(new AnimationEditor.Core.Paths.FilePath(path).FullPath, out var staleReason))
                return false;
            if (toAnotherFile)
            {
                SaveAsRefused?.Invoke(
                    $"Could not save {new FilePath(path).NoPath}: it changed on disk and couldn't be reloaded ({staleReason}). Pick another name.");
                return true;
            }
            SaveFailed?.Invoke(SaveTarget.UnreadableOnDiskMessage(path, staleReason));
            return true;
        }

        /// <summary>
        /// Reports and returns true when <paramref name="state"/> is for <paramref name="path"/> and the file no longer
        /// holds it. A document moved to another path (Save As) has seen nothing there, so it is not refused.
        /// </summary>
        private bool RefuseIfChangedOnDisk(string path, DiskState? state)
        {
            if (!HasChangedOnDisk(path, state)) return false;
            SaveFailed?.Invoke(SaveTarget.ChangedOnDiskMessage(path));
            return true;
        }

        private static bool HasChangedOnDisk(string path, DiskState? state)
        {
            var file = new FilePath(path);
            return state != null && state.Path == file && state.HasChanged(file);
        }

        /// <inheritdoc/>
        public string? SaveRefusal(string path, TabEntry? background)
        {
            if (_staleOnDiskReasons.TryGetValue(new FilePath(path).FullPath, out var staleReason))
                return SaveTarget.UnreadableOnDiskMessage(path, staleReason);
            return HasChangedOnDisk(path, background == null ? _diskState : background.CachedDiskState) ? SaveTarget.ChangedOnDiskMessage(path) : null;
        }

        /// <inheritdoc/>
        public bool SaveCurrentAnimationChainList(string? fileName = null) => SaveOpenDocument(fileName ?? _pm.FileName);

        private bool SaveOpenDocument(string? target, bool toAnotherFile = false)
        {
            if (string.IsNullOrEmpty(target))
            {
                _ioManager.WriteRecoveryFile(_pm.AnimationChainListSave);
                return true;
            }
            var document = TabEditorCache.FromProject(_pm, target);
            document.CachedDiskState = _diskState;
            return WriteDocument(document, target, isOpen: true, toAnotherFile);
        }

        /// <inheritdoc cref="IAppCommands.SaveDocument"/>
        public bool SaveDocument(TabEntry tab) => WriteDocument(tab, tab.Path.FullPath, isOpen: false);

        /// <summary>The one save of a document, the open one (as a tab) or a background tab's. Reports what went wrong.</summary>
        /// <param name="toAnotherFile">Save As. A failure there says nothing about the document's own file, which is as saved as it was.</param>
        private bool WriteDocument(TabEntry document, string target, bool isOpen, bool toAnotherFile = false)
        {
            void MarkFailed()
            {
                if (toAnotherFile) return;
                if (isOpen) _undoManager.MarkSaveFailed();
                else document.SaveFailed = true;
            }

            // The open one only after a failed save: otherwise the live reload takes the file's change.
            bool checkDisk = !isOpen || _undoManager.SaveState == SaveState.Failed;
            if (RefuseIfStale(target, toAnotherFile) || (checkDisk && RefuseIfChangedOnDisk(target, document.CachedDiskState)))
            {
                MarkFailed();
                return false;
            }

            try
            {
                if (!isOpen && document.CachedEditorModel == null)
                    throw new InvalidOperationException("it is not loaded.");
                IReadOnlyList<string> tsxWarnings = [];
                HotReloadWatcher.RunOwnSave(target, () => tsxWarnings = ProjectManager.WriteDocument(document, target));

                if (document.CachedTsxState is ProjectManager.TsxState { File.LacksOutsideChange: true })
                    HotReloadWatcher.ReportChange(target);
                else
                    document.CachedDiskState = ReadDiskState(target);
                if (isOpen)
                {
                    _pm.RestoreTsxState(document.CachedTsxState);
                    _diskState = document.CachedDiskState;
                    _undoManager.MarkSaved();
                }
                else
                    document.SaveFailed = false;

                // A listener that throws fails the save too: escaping, it would keep the edit out of the undo history.
                if (tsxWarnings.Count > 0)
                    TsxSaveCompletedWithWarnings?.Invoke(target, tsxWarnings);
                if (isOpen)
                    EditorProjectModelChanged?.Invoke(target);
            }
            catch (Exception ex)
            {
                MarkFailed();
                if (toAnotherFile) SaveAsRefused?.Invoke($"Could not save {new FilePath(target).NoPath}: {ex.Message}");
                else SaveFailed?.Invoke(isOpen ? ex.Message : $"\"{document.Path.NoPath}\": {ex.Message}");
                return false;
            }

            return true;
        }

        /// <inheritdoc cref="IAppCommands.LastSaveStored"/>
        public bool LastSaveStored(IEnumerable<AnimationChainSave> chains) =>
            _undoManager.SaveState != SaveState.Failed
            && !(_pm.IsNativeTsxProject && chains.Any(_pm.TsxChainsNotFullySaved.Contains));

        /// <summary>
        /// Show a Save-As file picker via <see cref="FileDialogService"/>, save the
        /// current animation chain list to the chosen path, update
        /// <see cref="ProjectManager.FileName"/>, and fire <see cref="SaveAsCompleted"/>
        /// and <see cref="IApplicationEvents.CurrentFileChanged"/>.
        /// Does nothing if the user cancels the dialog, and only reports why when the path is refused or the write fails.
        /// </summary>
        /// <returns>Whether the document was saved there.</returns>
        /// <remarks>
        /// The dialog's default extension follows the currently-loaded file: <c>achj</c> (JSON)
        /// for a brand-new, never-saved file (#872 -- .achj is the default going forward), or
        /// whatever extension the loaded file already has (typically <c>achx</c>, preserving the
        /// on-disk format of an opened legacy file). The user can still pick the other format
        /// from the dialog's file-type dropdown (#973).
        /// </remarks>
        public async Task<bool> SaveCurrentAnimationChainListAsync()
        {
            var currentExtension = string.IsNullOrEmpty(_pm.FileName)
                ? null
                : new FilePath(_pm.FileName).Extension;
            var defaultExtension = string.IsNullOrEmpty(currentExtension) ? "achj" : currentExtension;

            // A native tsx project's Save As must offer only its own format -- a save
            // always writes Tiled tileset XML regardless of the target path's extension (see
            // SaveCurrentAnimationChainList below), so letting the achj/achx choices through here
            // would let the user save tsx content under a misleading .achx/.achj name.
            var fileTypeChoices = _pm.IsNativeTsxProject
                ? new[] { new FileTypeChoice("tsx", "Tiled Tileset (*.tsx)") }
                : new[]
                {
                    new FileTypeChoice("achj", "Animation Chain JSON (*.achj)"),
                    new FileTypeChoice("achx", "Animation Chain XML (*.achx)")
                };

            var path = await FileDialogService.PickSaveFileAsync(
                "Save Animation Chain", defaultExtension, fileTypeChoices, SaveDialogStart.For(_pm.FileName));

            if (string.IsNullOrEmpty(path)) return false;

            if (SaveTarget.IsAnotherType(path, fileTypeChoices.Select(choice => choice.Extension).ToArray()))
            {
                string kind = _pm.IsNativeTsxProject ? "a .tsx" : "an .achj or .achx";
                SaveAsRefused?.Invoke(SaveTarget.WrongTypeMessage(path, kind));
                return false;
            }
            if (IsPathOpenInAnotherTab?.Invoke(path) == true)
            {
                SaveAsRefused?.Invoke(SaveTarget.OpenInAnotherTabMessage(path));
                return false;
            }

            bool toAnotherFile = string.IsNullOrEmpty(_pm.FileName) || new FilePath(_pm.FileName) != new FilePath(path);
            if (!SaveOpenDocument(path, toAnotherFile)) return false;
            _pm.MoveTsxProject(new FilePath(path));
            if (!string.IsNullOrEmpty(_pm.FileName)
                && new FilePath(_pm.FileName).GetDirectoryContainingThis() != new FilePath(path).GetDirectoryContainingThis())
            {
                // The textures in the undo history are named relative to the old folder.
                _undoManager.Clear();
                _undoManager.MarkSaved();
            }
            _pm.FileName = path;
            SyncHotReloadWatcher();  // pick up the saved achx path + any PNG dirs
            _ioManager.DeleteRecoveryFile(_pm.AnimationChainListSave);
            SaveAsCompleted?.Invoke(path);
            _events.RaiseCurrentFileChanged(path);
            return true;
        }

        /// <summary>
        /// Show a file picker and export the current animation chain list in <paramref name="format"/>.
        /// Does nothing if there is no project or the user cancels, and refuses another type's extension
        /// (<see cref="SaveTarget.IsAnotherType"/>) through <see cref="SaveAsRefused"/>. Copies the referenced textures
        /// next to the export, asking before one replaces a different image, and fires <see cref="ExportCompleted"/> with the path and any
        /// non-fatal warnings (data the format can't carry, textures that couldn't be copied).
        /// </summary>
        public async Task ExportAsync(Export.ExportFormat format)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            var path = await FileDialogService.PickSaveFileAsync(
                format.DialogTitle, format.Extension, new[] { new FileTypeChoice(format.Extension, format.FileTypeDescription) });
            if (string.IsNullOrEmpty(path)) return;
            if (SaveTarget.IsAnotherType(path, format.Extension))
            {
                SaveAsRefused?.Invoke(SaveTarget.WrongTypeMessage(path, "a ." + format.Extension));
                return;
            }

            var result = format.Export(acls, _pm.GetTextureSizeInPixels);

            // Every format references textures relative to the exported file, so when exporting
            // elsewhere the textures must travel with it. Copy each relative texture (preserving
            // any subdirectory) from the .achx's directory into the export directory.
            var exportDir = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var sourceDir = string.IsNullOrEmpty(_pm.FileName)
                ? string.Empty
                : System.IO.Path.GetDirectoryName(_pm.FileName) ?? string.Empty;
            // The export is written last, so one that names textures it could not bring along is never left behind.
            var copyWarnings = await CopyReferencedTexturesAsync(result.ReferencedTextures, sourceDir, exportDir);
            System.IO.File.WriteAllText(path, result.Text);

            ExportCompleted?.Invoke(path, result.Warnings.Concat(copyWarnings).ToList());
        }

        /// <summary>
        /// Copies each relative texture in <paramref name="textureNames"/> from
        /// <paramref name="sourceDir"/> to <paramref name="exportDir"/>, preserving any
        /// subdirectory. No-op when the directories are the same or the source is unknown
        /// (unsaved project). Rooted (absolute) texture names are left in place. Asks once before
        /// replacing different images; on No they stay. Returns a warning per texture that could
        /// not be found at the source, and throws when one cannot be copied.
        /// </summary>
        private async Task<IReadOnlyList<string>> CopyReferencedTexturesAsync(
            IReadOnlyList<string> textureNames, string sourceDir, string exportDir)
        {
            var warnings = new List<string>();

            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(exportDir)) return warnings;
            if (string.Equals(System.IO.Path.GetFullPath(sourceDir),
                              System.IO.Path.GetFullPath(exportDir),
                              StringComparison.OrdinalIgnoreCase))
                return warnings;

            var plans = new List<IO.TextureCopyPlan>();
            foreach (var name in textureNames)
            {
                if (string.IsNullOrEmpty(name) || System.IO.Path.IsPathRooted(name)) continue;

                var src = System.IO.Path.Combine(sourceDir, name);
                if (!System.IO.File.Exists(src))
                {
                    warnings.Add($"Texture '{name}' was not found next to the .achx, so it was not copied.");
                    continue;
                }

                var dest = System.IO.Path.GetFullPath(System.IO.Path.Combine(exportDir, name));
                plans.Add(IO.TextureCopyDecider.PlanCopy(src, System.IO.Path.GetDirectoryName(dest)!));
            }

            var different = plans.Where(plan => plan.Conflict == IO.TextureCopyConflict.DifferentFileExists).ToList();
            bool overwrite = different.Count == 0 || await ConfirmAsync(
                $"The export would overwrite {(different.Count == 1 ? "a different image" : $"{different.Count} different images")}: " +
                $"{string.Join(", ", different.Take(MaxListedOverwrites).Select(plan => plan.DestinationPath))}" +
                (different.Count > MaxListedOverwrites ? $" and {different.Count - MaxListedOverwrites} more" : "") +
                $". Overwrite {(different.Count == 1 ? "it? (No exports without copying it.)" : "them? (No exports without copying them.)")}",
                "Overwrite Texture");

            foreach (var plan in plans)
            {
                if (plan.Conflict == IO.TextureCopyConflict.IdenticalFileExists) continue;
                if (plan.Conflict == IO.TextureCopyConflict.DifferentFileExists && !overwrite) continue;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(plan.DestinationPath)!);
                IO.TextureCopyDecider.Apply(plan, IO.TextureCopyChoice.Overwrite);
            }

            return warnings;
        }

        private const int MaxListedOverwrites = 5;

        public void DeleteAnimationChains(List<AnimationChainSave> animationChains)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            // Capture which chains will actually be removed (so the toast count is honest)
            // before the command runs and removes them.
            var valid = animationChains.Where(c => acls.AnimationChains.Contains(c)).ToList();
            ExecuteRemovingChainNames("Deleting", acls, new DeleteChainsCommand(animationChains, acls, this, _events, _selectedState));
            if (valid.Count > 0)
            {
                string label = valid.Count == 1 ? valid[0].Name : $"{valid.Count} animations";
                ItemsDeleted?.Invoke(label);
            }
        }

        // The one path for a command that renames or deletes a chain. Undo and redo do not come through here, so they report nothing.
        private void ExecuteRemovingChainNames(string verb, AnimationChainListSave acls, IUndoableCommand command)
        {
            var before = acls.AnimationChains.Select(c => c.Name).ToList();
            _undoManager.Execute(command);
            var removed = before.Except(acls.AnimationChains.Select(c => c.Name)).ToList();
            if (removed.Count > 0) ChainNamesRemoved?.Invoke(new RemovedChainNames(acls, verb, removed));
        }

        public void AddAxisAlignedRectangle(AnimationFrameSave frame)
        {
            if (IsFrameLocked(frame) || IsAchxOnlyEditBlocked()) return;

            var name = StringFunctions.MakeStringUnique(NewShapeDefaults.RectName, GetSelectedFrameShapeNames());
            _undoManager.Execute(new AddShapeCommand(NewShapeDefaults.Rect(frame, name), frame, this, _events, _selectedState));
        }

        public void AddCircle(AnimationFrameSave frame)
        {
            if (IsFrameLocked(frame) || IsAchxOnlyEditBlocked()) return;

            var name = StringFunctions.MakeStringUnique(NewShapeDefaults.CircleName, GetSelectedFrameShapeNames());
            _undoManager.Execute(new AddShapeCommand(NewShapeDefaults.Circle(frame, name), frame, this, _events, _selectedState));
        }

        /// <summary>
        /// Adds a 16x16 square polygon centred on the frame's offset. It is stored closed (the first
        /// point repeated at the end), the FRB1 convention runtimes reading .achx expect.
        /// </summary>
        public void AddPolygon(AnimationFrameSave frame)
        {
            if (IsFrameLocked(frame) || IsAchxOnlyEditBlocked()) return;

            var name = StringFunctions.MakeStringUnique(NewShapeDefaults.PolygonName, GetSelectedFrameShapeNames());
            _undoManager.Execute(new AddShapeCommand(NewShapeDefaults.Polygon(frame, name), frame, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.AddShape"/>
        public bool AddShape(AnimationFrameSave frame, ShapeSave shape)
        {
            if (IsFrameLocked(frame) || IsAchxOnlyEditBlocked()) return false;
            _undoManager.Execute(new AddShapeCommand(shape, frame, this, _events, _selectedState));
            return true;
        }

        /// <summary>
        /// Moves <paramref name="rectangle"/> to <paramref name="animationFrame"/>'s offset
        /// (the "Match Frame Size" command). Records an undo entry; callers refresh the UI.
        /// </summary>
        public void MatchRectangleToFrame(AARectSave rectangle, AnimationFrameSave animationFrame)
        {
            if (IsFrameLocked(animationFrame)) return;
            _undoManager.Execute(MatchRectangleToFrameCommand(rectangle, animationFrame));
        }

        /// <summary>
        /// Centres <paramref name="rectangle"/> on <paramref name="animationFrame"/>'s offset and
        /// sizes it to the frame as drawn (scale is the half-size in pixels). The size is left alone when
        /// the frame's region is not set.
        /// </summary>
        private IUndoableCommand MatchRectangleToFrameCommand(AARectSave rectangle, AnimationFrameSave animationFrame)
        {
            bool unset = FrameRegion.IsUnset(animationFrame, GetTextureSize);
            var (width, height) = FrameDisplayValues.DisplayedSize(
                animationFrame,
                Math.Abs(animationFrame.RightCoordinate - animationFrame.LeftCoordinate),
                Math.Abs(animationFrame.BottomCoordinate - animationFrame.TopCoordinate));
            float scaleX = unset ? rectangle.ScaleX : width / 2f;
            float scaleY = unset ? rectangle.ScaleY : height / 2f;
            return SetShapePropsCommand.ForRect(
                animationFrame, rectangle, rectangle.Name ?? "",
                animationFrame.RelativeX, animationFrame.RelativeY, scaleX, scaleY,
                this, _events, $"Match {ShapeUndoLabel.Format(rectangle)} to Frame");
        }

        /// <summary>
        /// Moves <paramref name="circle"/> to <paramref name="animationFrame"/>'s offset.
        /// Records an undo entry; callers refresh the UI.
        /// </summary>
        public void MatchCircleToFrame(CircleSave circle, AnimationFrameSave animationFrame)
        {
            if (IsFrameLocked(animationFrame)) return;
            _undoManager.Execute(new MoveShapeCommand(
                animationFrame, circle, circle.X, circle.Y,
                animationFrame.RelativeX, animationFrame.RelativeY, this, _events));
        }

        /// <summary>
        /// Batched "Match Frame Size" for a multi-selection of rectangles. Each rectangle is
        /// matched to its own containing frame (via <see cref="ObjectFinder"/>), not a single
        /// shared frame, so a selection spanning multiple frames still resizes correctly.
        /// Records one undo entry for the whole batch.
        /// </summary>
        public void MatchRectanglesToFrames(List<AARectSave> rectangles)
        {
            var commands = new List<IUndoableCommand>();
            foreach (var rect in rectangles.ToArray())
            {
                var ownerFrame = _objectFinder.GetAnimationFrameContaining(rect);
                if (ownerFrame is null || IsFrameLocked(ownerFrame)) continue;
                commands.Add(MatchRectangleToFrameCommand(rect, ownerFrame));
            }
            if (commands.Count == 0) return;

            string desc = commands.Count == 1 ? commands[0].Description : $"Match {commands.Count} Shapes to Frame";
            _undoManager.Execute(new CompositeCommand(commands, desc, _events));
        }

        // Raw position assignment used by the internal shape-creation paths to set a
        // new shape's initial offset before it is handed to its Add command.
        private static void ApplyRectangleMatch(AARectSave rectangle, AnimationFrameSave animationFrame)
        {
            rectangle.X = animationFrame.RelativeX;
            rectangle.Y = animationFrame.RelativeY;
        }

        private static void ApplyCircleMatch(CircleSave circle, AnimationFrameSave animationFrame)
        {
            circle.X = animationFrame.RelativeX;
            circle.Y = animationFrame.RelativeY;
        }

        public void DeleteCircle(CircleSave circle, AnimationFrameSave owner)
        {
            if (IsFrameLocked(owner)) return;
            _undoManager.Execute(new DeleteShapeCommand(circle, owner, this, _events, _selectedState));
        }

        public void DeleteAxisAlignedRectangle(AARectSave rectangle, AnimationFrameSave owner)
        {
            if (IsFrameLocked(owner)) return;
            _undoManager.Execute(new DeleteShapeCommand(rectangle, owner, this, _events, _selectedState));
        }

        /// <summary>
        /// Deletes a multi-selection of shapes. Each shape's own owning frame is resolved via
        /// <see cref="ObjectFinder"/> rather than assuming a single shared frame, so a selection
        /// spanning multiple frames (e.g. Ctrl+click across frames, then Delete) deletes correctly
        /// instead of silently dropping shapes that don't belong to one caller-supplied frame
        /// (#1102). A shape whose owning chain is locked is skipped; the rest of the batch still
        /// proceeds -- mirrors <see cref="MatchRectanglesToFrames"/>.
        /// </summary>
        public void DeleteShapes(IReadOnlyList<object> shapes)
        {
            var commands = new List<IUndoableCommand>();
            var deleted = new List<ShapeSave>();
            foreach (var shape in shapes.OfType<ShapeSave>().ToArray())
            {
                var ownerFrame = _objectFinder.GetAnimationFrameContaining(shape);
                if (ownerFrame is null || IsFrameLocked(ownerFrame)) continue;
                commands.Add(new DeleteShapeCommand(shape, ownerFrame, this, _events, _selectedState));
                deleted.Add(shape);
            }
            if (commands.Count == 0) return;

            int total = commands.Count;
            // Single shape reuses its own command's "Delete Rectangle 'X'" / "Delete Circle 'X'"
            // text so the History panel reads naturally instead of "Composite Action".
            string desc = total == 1 ? commands[0].Description : $"Delete {total} Shapes";
            _undoManager.Execute(new DeleteWithSelectionCommand(
                new CompositeCommand(commands, desc, _events), deleted, _selectedState));

            ItemsDeleted?.Invoke(total == 1 ? deleted[0].Name : $"{total} shapes");
        }

        public void DeleteSelection(
            IReadOnlyList<AnimationChainSave> chains, IReadOnlyList<AnimationFrameSave> frames, IReadOnlyList<object> shapes)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            // A locked animation can itself be deleted (container operation); frames and shapes
            // inside one are modifications and stay. Children of an animation that is also being
            // deleted go with it, so they are not deleted separately.
            var chainList = chains.Where(acls.AnimationChains.Contains).Distinct().ToList();
            bool InDeletedChain(AnimationFrameSave f) => chainList.Any(c => c.Frames.Contains(f));

            var frameList = frames.Distinct()
                .Where(f => !InDeletedChain(f)
                    && _objectFinder.GetAnimationChainContaining(f) is { } owner
                    && owner.Frames.Contains(f) && !IsChainLocked(owner))
                .ToList();
            var shapeList = shapes.OfType<ShapeSave>().Distinct()
                .Where(s => _objectFinder.GetAnimationFrameContaining(s) is { } owner
                    && !IsFrameLocked(owner) && !InDeletedChain(owner) && !frameList.Contains(owner))
                .ToList();

            int kinds = (chainList.Count > 0 ? 1 : 0) + (frameList.Count > 0 ? 1 : 0) + (shapeList.Count > 0 ? 1 : 0);
            if (kinds == 0) return;
            if (kinds == 1)
            {
                // One kind keeps its own history text and toast ("Delete Animation 'Walk'").
                if (chainList.Count > 0) DeleteAnimationChains(chainList);
                else if (frameList.Count > 0) DeleteFrames(frameList);
                else DeleteShapes(shapeList.Cast<object>().ToList());
                return;
            }

            // Children first: undo runs in reverse, so the animations come back before their frames.
            var commands = new List<IUndoableCommand>();
            foreach (var shape in shapeList)
                commands.Add(new DeleteShapeCommand(
                    shape, _objectFinder.GetAnimationFrameContaining(shape)!, this, _events, _selectedState));
            foreach (var group in frameList.GroupBy(f => _objectFinder.GetAnimationChainContaining(f)!))
                commands.Add(new DeleteFramesCommand(group.ToList(), group.Key, this, _events, _selectedState));
            if (chainList.Count > 0)
                commands.Add(new DeleteChainsCommand(chainList, acls, this, _events, _selectedState));

            int total = chainList.Count + frameList.Count + shapeList.Count;
            ExecuteRemovingChainNames("Deleting", acls, new DeleteWithSelectionCommand(
                new CompositeCommand(commands, $"Delete {total} Items", _events),
                shapes.OfType<ShapeSave>().ToList(), _selectedState));

            ItemsDeleted?.Invoke($"{total} items");
            RefreshWireframeRequested?.Invoke();
        }

        public void DeleteFrames(List<AnimationFrameSave> frames)
        {
            // The incoming frames can span multiple chains (a cross-chain tree multi-select,
            // MainWindow.HandleDelete). A locked chain among them keeps its own frames --
            // group by owning chain and skip only the locked groups, rather than vetoing the
            // whole delete when any one chain involved is locked (bulk skip-locked-entries
            // pattern, matching PasteFramesCut).
            var groups = frames
                .Select(f => (Frame: f, Chain: _objectFinder.GetAnimationChainContaining(f)))
                .Where(x => x.Chain is not null && x.Chain.Frames.Contains(x.Frame) && !IsChainLocked(x.Chain))
                .GroupBy(x => x.Chain!)
                .ToList();
            if (groups.Count == 0) return;

            var validFrames = groups.SelectMany(g => g).Select(x => x.Frame).ToList();
            string label = validFrames.Count == 1
                ? $"Frame {groups[0].Key.Frames.IndexOf(validFrames[0]) + 1}"
                : $"{validFrames.Count} frames";

            var commands = groups
                .Select(g => (IUndoableCommand)new DeleteFramesCommand(
                    g.Select(x => x.Frame).ToList(), g.Key, this, _events, _selectedState))
                .ToList();
            string desc = commands.Count == 1 ? commands[0].Description : $"Delete {validFrames.Count} Frames";
            _undoManager.Execute(new CompositeCommand(commands, desc, _events));

            ItemsDeleted?.Invoke(label);
            RefreshWireframeRequested?.Invoke();
        }

        private List<string> GetSelectedFrameShapeNames()
        {
            var frame = _selectedState.SelectedFrame;
            if (frame?.ShapesSave == null) return new List<string>();

            return frame.ShapesSave!.Shapes.OfType<ShapeSave>().Select(s => s.Name).ToList();
        }

        // ── Chain / Frame operations ──────────────────────────────────────────

        public async Task AddAnimationChain()
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return;

            var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
            var defaultName = StringFunctions.MakeStringUnique("NewAnimation", existingNames);

            var name = await PromptStringAsync("Add Animation", "Animation name:", defaultName);
            if (name is null) return;
            AddAnimationChainWithName(name);
        }

        /// <summary>
        /// Adds a new chain named "NewAnimation" (or a de-duped variant, e.g. "NewAnimation2")
        /// without prompting — the toolbar/menu "Add Animation" entry point shared by both hosts.
        /// Returns <c>null</c> when no project is loaded.
        /// </summary>
        public AnimationChainSave? AddNewAnimationChain()
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return null;

            var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
            var defaultName = StringFunctions.MakeStringUnique("NewAnimation", existingNames);
            return AddAnimationChainWithName(defaultName);
        }

        public AnimationChainSave? AddAnimationChainWithName(string name)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return null;

            name = name.Trim();
            if (string.IsNullOrEmpty(name)) return null;

            name = StringFunctions.MakeStringUnique(name, acls.AnimationChains.Select(c => c.Name).ToList());
            var chain = new AnimationChainSave { Name = name };
            _undoManager.Execute(new AddChainCommand(chain, acls, this, _events, _selectedState));
            return chain;
        }

        /// <summary>
        /// Rename a chain.  Returns <c>false</c> (no-op) when another chain in the same ACLS
        /// already uses <paramref name="newName"/>; returns <c>true</c> on success. An empty name
        /// reverts a native-tsx chain to its auto "ID:{tileId}" name (removing <c>Name</c> from the
        /// tsx); outside a native-tsx project it's rejected.
        /// </summary>
        public bool RenameChain(AnimationChainSave chain, string newName)
        {
            newName = newName.Trim();
            bool NameTaken(string name) => _pm.AnimationChainListSave?.HasOtherChainNamed(chain, name) == true;

            if (newName.Length == 0)
            {
                if (_pm.IsChainNameAuto(chain)) return true;
                if (_pm.GetTsxOwnerTileId(chain) is not { } tileId || NameTaken(TiledAnimationToAchjMapper.SyntheticChainName(tileId))) return false;
                Rename(null);
                return true;
            }
            if (chain.Name == newName) return true;
            if (NameTaken(newName)) return false;

            Rename(newName);
            return true;

            void Rename(string? name)
            {
                var command = new RenameChainCommand(chain, chain.Name, name, _pm, this, _events);
                if (_pm.AnimationChainListSave is { } document) ExecuteRemovingChainNames("Renaming", document, command);
                else _undoManager.Execute(command);
            }
        }

        public Func<string?>? CanvasDefaultTexturePath { get; set; }

        public Func<string, (int Width, int Height)?>? DecodedTextureSize { get; set; }

        /// <inheritdoc cref="IAppCommands.GetTextureSize"/>
        public (int Width, int Height)? GetTextureSize(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;
            if (_pm.GetTextureSizeInPixels(textureName) is { } size) return size;

            var folder = string.IsNullOrEmpty(_pm.FileName) ? string.Empty : System.IO.Path.GetDirectoryName(_pm.FileName) ?? string.Empty;
            return DecodedTextureSize?.Invoke(TexturePathHelper.ResolveDisplayPath(textureName, folder));
        }

        public void AddFrame(AnimationChainSave chain, string? textureName = null)
        {
            if (textureName is not null)
            {
                if (IsTextureBlocked(textureName)) return;
                // A tsx has one image and its frames are cells of it, so naming it asks for no other region.
                if (_pm.IsNativeTsxProject) textureName = null;
            }

            // When no texture is passed, inherit both the texture and the sub-region from a
            // source frame so the new frame lands on the same sheet cell the user is working in,
            // rather than snapping back to the whole texture. An explicit texture (e.g. drag-drop
            // of a different image) keeps the default full-texture region.
            var source = textureName is null
                ? ResolveInheritedFrame(chain, _pm.AnimationChainListSave)
                : null;
            if (source is null || IsTextureBlocked(source.TextureName))
                source = FirstTsxTileFrame() ?? source;

            // Nothing in the document to inherit from (a brand-new document, or a chain-less
            // selection) -- fall back to whatever the wireframe canvas is already showing rather
            // than leaving the new frame textureless.
            var resolvedTextureName = textureName ?? source?.TextureName;
            if (string.IsNullOrEmpty(resolvedTextureName))
                resolvedTextureName = CanvasDefaultTexturePath?.Invoke();

            // A region nobody has set shows the whole texture once there is one of a known size.
            var region = source is null ? FrameRegion.Unset : FrameRegion.Of(source);
            if ((source is null || FrameRegion.IsUnset(source, GetTextureSize))
                && !string.IsNullOrEmpty(resolvedTextureName) && GetTextureSize(resolvedTextureName) is { } size)
                region = FrameRegion.Whole(size);

            CreateAndAddFrame(chain, resolvedTextureName ?? string.Empty, region);
        }

        /// <summary>
        /// Shared tail end of <see cref="AddFrame"/> and <see cref="AddFrameFromPixelBounds"/>:
        /// builds the <see cref="AnimationFrameSave"/> from an already-resolved region and
        /// executes the undoable add.
        /// </summary>
        private void CreateAndAddFrame(AnimationChainSave chain, string textureName, FrameRegion region)
        {
            if (IsChainLocked(chain) || IsTextureBlocked(textureName)) return;

            var frame = new AnimationFrameSave
            {
                TextureName = textureName,
                FrameLength = CurrentFrameLengthUnit.DefaultFrameLength,
            };
            region.ApplyTo(frame);
            _undoManager.Execute(new AddFrameCommand(frame, chain, this, _events, _selectedState));
        }

        /// <summary>
        /// Picks the frame a newly added frame should inherit its texture and sub-region from
        /// when the caller supplies no texture. Resolution order: (1) the chain's current last
        /// frame, so a new frame continues on the same sheet cell the animation already uses;
        /// (2) the first frame with a non-empty texture from any chain in list order, so a
        /// still-empty chain borrows a sensible default; (3) <c>null</c> when no texture exists
        /// anywhere, in which case the new frame defaults to the whole texture.
        /// </summary>
        public static AnimationFrameSave? ResolveInheritedFrame(
            AnimationChainSave chain, AnimationChainListSave? chainList)
        {
            if (chain.Frames.Count > 0)
                return chain.Frames[^1];

            return TextureListBuilder.FindFirstTexturedFrame(chainList);
        }

        public void MoveChain(AnimationChainSave chain, int delta)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            int idx    = chains.IndexOf(chain);
            int newIdx = Math.Clamp(idx + delta, 0, chains.Count - 1);
            if (newIdx == idx) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.RemoveAt(idx); chains.Insert(newIdx, chain); },
                this, _events, RefreshTreeView,
                delta > 0
                    ? $"Move Animation '{chain.Name}' Down"
                    : $"Move Animation '{chain.Name}' Up"));
        }

        /// <inheritdoc cref="IAppCommands.MoveChainToIndex"/>
        public void MoveChainToIndex(AnimationChainSave chain, int insertIndex)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            int currentIndex = chains.IndexOf(chain);
            if (currentIndex < 0) return;

            // insertIndex is measured against the list before removal; removing the chain from
            // ahead of the target shifts the landing spot left by one (mirrors MoveFramesCommand).
            int insertAt = insertIndex;
            if (insertIndex > currentIndex) insertAt--;
            insertAt = Math.Clamp(insertAt, 0, chains.Count - 1);
            if (insertAt == currentIndex) return;

            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.RemoveAt(currentIndex); chains.Insert(insertAt, chain); },
                this, _events, RefreshTreeView,
                $"Move Animation '{chain.Name}'"));
        }

        /// <inheritdoc cref="IAppCommands.MoveChainsToIndex"/>
        public void MoveChainsToIndex(IReadOnlyList<AnimationChainSave> chains, int insertIndex)
        {
            if (BuildMoveChainsToIndex(chains, insertIndex, "Move") is { } command)
                _undoManager.Execute(command);
        }

        private IUndoableCommand? BuildMoveChainsToIndex(IReadOnlyList<AnimationChainSave> chains, int insertIndex, string verb)
        {
            var list = _pm.AnimationChainListSave?.AnimationChains;
            if (list is null || chains.Count == 0) return null;

            var indices = chains
                .Select(c => list.IndexOf(c))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return null;

            var moved = indices.Select(i => list[i]).ToArray();

            // insertIndex is measured against the list before removal; removing the moved
            // chains shifts the landing spot left by however many of them sat ahead of it
            // (mirrors MoveFramesCommand).
            int insertAt = insertIndex - indices.Count(i => i < insertIndex);

            return new ReorderCommand<AnimationChainSave>(
                list,
                () =>
                {
                    foreach (var chain in moved)
                        list.Remove(chain);
                    int at = Math.Clamp(insertAt, 0, list.Count);
                    for (int i = 0; i < moved.Length; i++)
                        list.Insert(at + i, moved[i]);
                },
                this, _events, RefreshTreeView,
                moved.Length == 1
                    ? $"{verb} Animation '{moved[0].Name}'"
                    : $"{verb} {moved.Length} Animations");
        }

        public void MoveChainToTop(AnimationChainSave chain)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.Remove(chain); chains.Insert(0, chain); },
                this, _events, RefreshTreeView,
                $"Move Animation '{chain.Name}' to Top"));
        }

        public void MoveChainToBottom(AnimationChainSave chain)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.Remove(chain); chains.Add(chain); },
                this, _events, RefreshTreeView,
                $"Move Animation '{chain.Name}' to Bottom"));
        }

        public void MoveFrame(AnimationFrameSave frame, AnimationChainSave chain, int delta)
        {
            if (IsChainLocked(chain)) return;
            int idx    = chain.Frames.IndexOf(frame);
            int newIdx = Math.Clamp(idx + delta, 0, chain.Frames.Count - 1);
            if (newIdx == idx) return;
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.RemoveAt(idx); chain.Frames.Insert(newIdx, frame); },
                this, _events, () => RefreshTreeNode(chain),
                delta > 0
                    ? $"Move Frame in '{chain.Name}' Down"
                    : $"Move Frame in '{chain.Name}' Up"));
        }

        public void MoveFrameToTop(AnimationFrameSave frame, AnimationChainSave chain)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.Remove(frame); chain.Frames.Insert(0, frame); },
                this, _events, () => RefreshTreeNode(chain),
                $"Move Frame in '{chain.Name}' to Top"));
        }

        public void MoveFrameToBottom(AnimationFrameSave frame, AnimationChainSave chain)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.Remove(frame); chain.Frames.Add(frame); },
                this, _events, () => RefreshTreeNode(chain),
                $"Move Frame in '{chain.Name}' to Bottom"));
        }

        /// <inheritdoc cref="IAppCommands.MoveFrames"/>
        public void MoveFrames(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave sourceChain, AnimationChainSave targetChain, int insertIndex)
        {
            if (frames.Count == 0 || IsChainLocked(sourceChain) || IsChainLocked(targetChain)) return;
            _undoManager.Execute(new MoveFramesCommand(
                frames, sourceChain, targetChain, insertIndex, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.MoveFramesRelative"/>
        public void MoveFramesRelative(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave chain, int delta)
        {
            if (BuildMoveFramesRelative(frames, chain, delta) is { } command)
                _undoManager.Execute(command);
        }

        private IUndoableCommand? BuildMoveFramesRelative(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave chain, int delta)
        {
            if (delta == 0 || frames.Count == 0 || IsChainLocked(chain)) return null;

            var indices = frames
                .Select(f => chain.Frames.IndexOf(f))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return null;

            // Rigid group: if shifting either edge by delta would cross a boundary, no-op.
            if (indices[0] + delta < 0) return null;
            if (indices[^1] + delta > chain.Frames.Count - 1) return null;

            var selected = new HashSet<int>(indices);

            return new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () =>
                {
                    var reordered = new AnimationFrameSave[chain.Frames.Count];
                    // Selected frames land at their shifted slots (gaps preserved)...
                    foreach (int i in indices)
                        reordered[i + delta] = chain.Frames[i];
                    // ...and the rest slot into the remaining holes in original order.
                    int cursor = 0;
                    for (int i = 0; i < chain.Frames.Count; i++)
                    {
                        if (selected.Contains(i)) continue;
                        while (reordered[cursor] is not null) cursor++;
                        reordered[cursor] = chain.Frames[i];
                    }
                    chain.Frames.Clear();
                    foreach (var frame in reordered)
                        chain.Frames.Add(frame);
                },
                this, _events, () => RefreshTreeNode(chain),
                CountAwareMoveDescription(indices.Count, "Frame", "Frames",
                    delta > 0 ? "Down" : "Up", frameContextName: chain.Name));
        }

        /// <inheritdoc cref="IAppCommands.MoveChainsRelative"/>
        public void MoveChainsRelative(IReadOnlyList<AnimationChainSave> chains, int delta)
        {
            if (BuildMoveChainsRelative(chains, delta) is { } command)
                _undoManager.Execute(command);
        }

        private IUndoableCommand? BuildMoveChainsRelative(IReadOnlyList<AnimationChainSave> chains, int delta)
        {
            var list = _pm.AnimationChainListSave?.AnimationChains;
            if (list is null || delta == 0 || chains.Count == 0) return null;

            var indices = chains
                .Select(c => list.IndexOf(c))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return null;

            // Rigid group: if shifting either edge by delta would cross a boundary, no-op.
            if (indices[0] + delta < 0) return null;
            if (indices[^1] + delta > list.Count - 1) return null;

            var selected = new HashSet<int>(indices);

            return new ReorderCommand<AnimationChainSave>(
                list,
                () =>
                {
                    var reordered = new AnimationChainSave[list.Count];
                    // Selected chains land at their shifted slots (gaps preserved)...
                    foreach (int i in indices)
                        reordered[i + delta] = list[i];
                    // ...and the rest slot into the remaining holes in original order.
                    int cursor = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (selected.Contains(i)) continue;
                        while (reordered[cursor] is not null) cursor++;
                        reordered[cursor] = list[i];
                    }
                    list.Clear();
                    foreach (var chain in reordered)
                        list.Add(chain);
                },
                this, _events, RefreshTreeView,
                CountAwareMoveDescription(indices.Count, "Animation", "Animations",
                    delta > 0 ? "Down" : "Up",
                    itemName: indices.Count == 1 ? list[indices[0]].Name : null));
        }

        /// <summary>
        /// Undo label for a relative reorder. Plural includes the count
        /// (<c>Move 3 Frames Down</c>). Singular names the target when known
        /// (<c>Move Animation 'Walk' Down</c>, <c>Move Frame in 'Walk' Down</c>).
        /// </summary>
        private static string CountAwareMoveDescription(
            int count, string singularNoun, string pluralNoun, string direction,
            string? itemName = null, string? frameContextName = null)
        {
            if (count != 1)
                return $"Move {count} {pluralNoun} {direction}";

            if (frameContextName is not null)
                return $"Move Frame in '{frameContextName}' {direction}";

            if (itemName is not null)
                return $"Move {singularNoun} '{itemName}' {direction}";

            return $"Move {singularNoun} {direction}";
        }
        public void MoveShape(object shape, AnimationFrameSave frame, int delta)
        {
            if (IsFrameLocked(frame)) return;
            if (frame.ShapesSave is not { } shapes) return;
            // Shapes only reorder within their own type: the file groups them by type, so a cross-type move would be lost on save.
            ExecuteShapeReorder(shapes, frame, () => shapes.Move(shape, delta),
                delta > 0
                    ? $"Move {ShapeReorderLabel(shape)} Down"
                    : $"Move {ShapeReorderLabel(shape)} Up");
        }

        public void MoveShapeToTop(object shape, AnimationFrameSave frame)
        {
            if (IsFrameLocked(frame)) return;
            if (frame.ShapesSave is not { } shapes) return;
            ExecuteShapeReorder(shapes, frame, () => shapes.MoveToEdge(shape, toStart: true),
                $"Move {ShapeReorderLabel(shape)} to Top");
        }

        public void MoveShapeToBottom(object shape, AnimationFrameSave frame)
        {
            if (IsFrameLocked(frame)) return;
            if (frame.ShapesSave is not { } shapes) return;
            ExecuteShapeReorder(shapes, frame, () => shapes.MoveToEdge(shape, toStart: false),
                $"Move {ShapeReorderLabel(shape)} to Bottom");
        }

        public void MoveShapeToIndex(object shape, AnimationFrameSave frame, int insertIndex)
        {
            if (IsFrameLocked(frame)) return;
            if (frame.ShapesSave is not { } shapes) return;
            var order = ShapeDropResolver.ApplyDrop(shapes, shape, insertIndex);
            ExecuteShapeReorder(shapes, frame, () => shapes.SetOrder(order), $"Move {ShapeReorderLabel(shape)}");
        }

        private void ExecuteShapeReorder(ShapesSave shapes, AnimationFrameSave frame, Action reorder, string description)
        {
            _undoManager.Execute(new ReorderCommand<object>(
                () => shapes.Shapes.ToArray(),
                order => shapes.SetOrder(order),
                reorder,
                this, _events, () => RefreshTreeNode(frame),
                description));
        }

        private static string ShapeReorderLabel(object shape) => ShapeUndoLabel.Format(shape);

        /// <summary>
        /// Moves the currently-selected shape, frame, or chain up (<paramref name="delta"/> = -1)
        /// or down (<paramref name="delta"/> = +1) in the tree.
        /// Shape selection takes highest priority: if a shape is selected it is reordered within
        /// its frame's shape list. Frame selection takes next priority; chain is last. When more
        /// than one frame or chain is selected, the whole group moves together as a rigid,
        /// gap-preserving block (<see cref="MoveFramesRelative"/> / <see cref="MoveChainsRelative"/>).
        /// No-op when nothing is selected or when the item is already at the boundary.
        /// </summary>
        public void HandleReorder(int delta)
        {
            var shape  = _selectedState.SelectedShape as ShapeSave;
            var frame  = _selectedState.SelectedFrame;
            var chain  = _selectedState.SelectedChain;

            if (shape is not null)
            {
                var ownerFrame = _objectFinder.GetAnimationFrameContaining(shape);
                if (ownerFrame is not null) MoveShape(shape, ownerFrame, delta);
            }
            else if (SelectionSpansSeveralContainers())
            {
                ReorderMixedSelection(delta);
            }
            else if (frame is not null && chain is not null)
            {
                // Reorder the whole multi-selection together (gaps preserved); fall back to
                // the single-frame move when only the primary frame is selected.
                var frames = _selectedState.SelectedFrames
                    .Where(chain.Frames.Contains)
                    .ToList();
                if (frames.Count > 1)
                    MoveFramesRelative(frames, chain, delta);
                else
                    MoveFrame(frame, chain, delta);
            }
            else if (chain is not null)
            {
                // Reorder the whole multi-selection together (gaps preserved); fall back to
                // the single-chain move when only the primary chain is selected.
                var chains = _selectedState.SelectedChains;
                if (chains.Count > 1)
                    MoveChainsRelative(chains, delta);
                else
                    MoveChain(chain, delta);
            }
        }

        /// <summary>
        /// True when a reorder has to move things in more than one container: whole animations
        /// together with frames, or frames spread over several animations.
        /// </summary>
        private bool SelectionSpansSeveralContainers()
        {
            var nodes = _selectedState.SelectedNodes;
            if (nodes.Any(n => n is AnimationChainSave) && nodes.Any(n => n is AnimationFrameSave))
                return true;
            return _selectedState.SelectedFrames
                .Select(f => _objectFinder.GetAnimationChainContaining(f))
                .Distinct()
                .Count() > 1;
        }

        /// <summary>
        /// Reorders animations and frames from several containers in one undo step: the
        /// animations move as a block in the list, and each animation's selected frames move as a
        /// block within it. Frames of a locked animation stay put; a locked animation itself still moves.
        /// </summary>
        private void ReorderMixedSelection(int delta)
        {
            var commands = new List<IUndoableCommand>();
            if (BuildMoveChainsRelative(_selectedState.SelectedChains, delta) is { } chainMove)
                commands.Add(chainMove);
            foreach (var group in _selectedState.SelectedFrames
                .Select(f => (Frame: f, Chain: _objectFinder.GetAnimationChainContaining(f)))
                .Where(x => x.Chain is not null)
                .GroupBy(x => x.Chain!))
            {
                if (BuildMoveFramesRelative(group.Select(x => x.Frame).ToList(), group.Key, delta) is { } frameMove)
                    commands.Add(frameMove);
            }

            if (commands.Count == 1)
                _undoManager.Execute(commands[0]);
            else if (commands.Count > 1)
                _undoManager.Execute(new CompositeCommand(commands, delta > 0 ? "Move Selection Down" : "Move Selection Up", _events));
        }

        public void SetFrameFlip(
            IReadOnlyList<AnimationFrameSave> frames, bool? flipHorizontal, bool? flipVertical, bool? flipDiagonal = null)
        {
            if (IsAchxOnlyEditBlocked()) return;
            // Absolute set, not toggle: only the frames whose flag actually differs from the target
            // get flipped (and their offset/shapes mirrored), so a frame already at the target state
            // is untouched. Reuses FlipCommand's toggle for exactly those frames, grouped into one
            // undo step via CompositeCommand.
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            var commands = new List<IUndoableCommand>();

            if (flipHorizontal.HasValue)
            {
                var toFlip = unlockedFrames.Where(f => f.FlipHorizontal != flipHorizontal.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Horizontal, this, _events, RefreshWireframe));
            }
            if (flipVertical.HasValue)
            {
                var toFlip = unlockedFrames.Where(f => f.FlipVertical != flipVertical.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Vertical, this, _events, RefreshWireframe));
            }
            if (flipDiagonal.HasValue)
            {
                var toFlip = unlockedFrames.Where(f => f.FlipDiagonal != flipDiagonal.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Diagonal, this, _events, RefreshWireframe));
            }

            if (commands.Count == 0) return;
            _undoManager.Execute(new CompositeCommand(commands, "Set Flip", _events));
        }

        public void FlipChainHorizontally(AnimationChainSave chain)
        {
            if (IsChainLocked(chain) || IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), FlipAxis.Horizontal, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void FlipChainVertically(AnimationChainSave chain)
        {
            if (IsChainLocked(chain) || IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), FlipAxis.Vertical, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void FlipChainDiagonally(AnimationChainSave chain)
        {
            if (IsChainLocked(chain) || IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), FlipAxis.Diagonal, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void InvertFrameOrder(AnimationChainSave chain)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => chain.Frames.Reverse(),
                this, _events, () => RefreshTreeNode(chain),
                $"Invert Frame Order in '{chain.Name}'"));
        }

        public void SetAllFrameLengths(AnimationChainSave chain, float frameLength)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => { foreach (var frame in chain.Frames) frame.FrameLength = frameLength; },
                this, _events, refreshWireframe: false,
                "Set All Frame Lengths"));
        }

        public AnimationChainSave? DuplicateChain(
            AnimationChainSave source,
            ChainFlip flip  = ChainFlip.None,
            string? newName = null)
        {
            var copies = DuplicateChains(new[] { source }, flip);
            if (copies.Count == 0) return null;

            var copy = copies[0];
            if (newName is not null) copy.Name = newName;
            return copy;
        }

        // Clones a chain and, when requested, flips every frame and shape along the given axes
        // through FlipCommand.FlipFrame, the same code an in-place flip runs. Shared by
        // DuplicateChain (single) and DuplicateChains (batch) so right-click Duplicate's
        // Original/Flip submenu items behave identically whether one or many chains are selected.
        private static AnimationChainSave CloneChainWithFlip(AnimationChainSave source, ChainFlip flip)
        {
            var copy = AnimationCloneHelper.CloneChain(source);
            foreach (var frame in copy.Frames)
            {
                if (flip.HasFlag(ChainFlip.Horizontal)) FlipCommand.FlipFrame(frame, FlipAxis.Horizontal);
                if (flip.HasFlag(ChainFlip.Vertical))   FlipCommand.FlipFrame(frame, FlipAxis.Vertical);
                if (flip.HasFlag(ChainFlip.Diagonal))   FlipCommand.FlipFrame(frame, FlipAxis.Diagonal);
            }
            return copy;
        }

        public AnimationFrameSave? DuplicateFrame(AnimationFrameSave source, AnimationChainSave chain)
        {
            if (!chain.Frames.Contains(source) || IsChainLocked(chain)) return null;
            var copies = DuplicateFramesBatch(new[] { source });
            return copies.Count > 0 ? copies[0] : null;
        }

        /// <inheritdoc cref="IAppCommands.DuplicateShape"/>
        public object? DuplicateShape(object source)
        {
            var copies = DuplicateShapesBatch(new[] { source });
            return copies.Count > 0 ? copies[0] : null;
        }

        /// <inheritdoc cref="IAppCommands.DuplicateSelection"/>
        public void DuplicateSelection(CopySelectionPayload payload)
        {
            switch (payload.Kind)
            {
                case CopySelectionKind.Chain:
                    DuplicateChains(payload.Chains);
                    break;
                case CopySelectionKind.Frame:
                    DuplicateFramesBatch(payload.Frames);
                    break;
                case CopySelectionKind.Shape:
                    DuplicateShapesBatch(payload.Shapes);
                    break;
            }
        }

        /// <inheritdoc cref="IAppCommands.DuplicateChains"/>
        public IReadOnlyList<AnimationChainSave> DuplicateChains(
            IReadOnlyList<AnimationChainSave> sources, ChainFlip flip = ChainFlip.None)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null || sources.Count == 0) return Array.Empty<AnimationChainSave>();
            if (IsAchxOnlyEditBlocked())
                flip = ChainFlip.None;

            var ordered = sources
                .OrderBy(c => acls.AnimationChains.IndexOf(c))
                .ToList();
            var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
            var items = new List<(AnimationChainSave Source, AnimationChainSave Copy)>();
            foreach (var source in ordered)
            {
                var copy = CloneChainWithFlip(source, flip);
                var invertedName = ChainFlipNaming.Rename(source.Name, flip, source.Frames);
                // Duplicates increment the trailing number (Walk -> Walk2, Walk2 -> Walk3, Id0 -> Id1)
                // so repeated duplicates never stack a suffix. Only an unnumbered name skips 1.
                bool numbered = invertedName.Length > 0 && char.IsAsciiDigit(invertedName[^1]);
                copy.Name = StringFunctions.MakeStringUnique(invertedName, existingNames, numbered ? 1 : 2);
                existingNames.Add(copy.Name);
                items.Add((source, copy));
            }

            _undoManager.Execute(new DuplicateChainsCommand(acls, items, this, _events, _selectedState));
            return items.Select(i => i.Copy).ToList();
        }

        private IReadOnlyList<AnimationFrameSave> DuplicateFramesBatch(IReadOnlyList<AnimationFrameSave> sources)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null || sources.Count == 0) return Array.Empty<AnimationFrameSave>();

            var ops = new List<DuplicateFrameInsertOp>();
            var allCopies = new List<AnimationFrameSave>();

            var grouped = sources
                .Select(f => (Frame: f, Chain: _objectFinder.GetAnimationChainContaining(f)))
                .Where(x => x.Chain is not null && !IsChainLocked(x.Chain))
                .GroupBy(x => x.Chain!);

            foreach (var group in grouped)
            {
                var chain = group.Key;
                var frames = group.Select(x => x.Frame)
                    .OrderBy(f => chain.Frames.IndexOf(f))
                    .ToList();
                var indices = frames.Select(f => chain.Frames.IndexOf(f)).ToList();
                bool contiguous = frames.Count > 1
                    && indices.Zip(indices.Skip(1), (a, b) => b == a + 1).All(x => x);

                if (contiguous)
                {
                    var clones = frames.Select(AnimationCloneHelper.CloneFrame).ToArray();
                    allCopies.AddRange(clones);
                    ops.Add(new DuplicateFrameBlockOp(chain, clones, indices[^1] + 1));
                }
                else
                {
                    foreach (var frame in frames.OrderByDescending(f => chain.Frames.IndexOf(f)))
                    {
                        var copy = AnimationCloneHelper.CloneFrame(frame);
                        allCopies.Add(copy);
                        ops.Add(new DuplicateFrameAdjacentOp(chain, frame, copy));
                    }
                }
            }

            if (ops.Count == 0) return Array.Empty<AnimationFrameSave>();
            _undoManager.Execute(new DuplicateFramesCommand(ops, this, _events, _selectedState));
            return allCopies;
        }

        private IReadOnlyList<object> DuplicateShapesBatch(IReadOnlyList<object> sources)
        {
            if (sources.Count == 0) return Array.Empty<object>();
            var frame = sources[0] is ShapeSave first ? _objectFinder.GetAnimationFrameContaining(first) : null;
            if (frame is null || IsFrameLocked(frame)) return Array.Empty<object>();

            frame.ShapesSave ??= new ShapesSave();
            var existingNames = GetShapeNames(frame);
            var copies = new List<object>();
            foreach (var source in sources)
            {
                if (AnimationCloneHelper.CloneShape(source) is not ShapeSave copy) continue;
                copy.Name = StringFunctions.MakeStringUnique(copy.Name, existingNames, 2);
                existingNames.Add(copy.Name);
                copies.Add(copy);
            }
            if (copies.Count == 0) return Array.Empty<object>();
            _undoManager.Execute(new DuplicateShapesCommand(frame, copies, this, _events, _selectedState));
            return copies;
        }

        private static List<string> GetShapeNames(AnimationFrameSave frame) =>
            frame.ShapesSave?.Shapes.OfType<ShapeSave>().Select(s => s.Name).ToList() ?? [];

        public void SortAnimationsAlphabetically()
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                acls.AnimationChains,
                () =>
                {
                    var sorted = acls.AnimationChains.OrderBy(c => c.Name).ToList();
                    acls.AnimationChains.Clear();
                    foreach (var c in sorted)
                        acls.AnimationChains.Add(c);
                },
                this, _events, RefreshTreeView,
                "Sort Animations"));
        }

        // ── A16: Adjust Offsets ───────────────────────────────────────────────

        /// <summary>
        /// Justify (Bottom) — sets each frame's RelativeY so the bottom pixel edge
        /// aligns to y=0, divided by <paramref name="offsetMultiplier"/>.
        /// A frame whose region is not set is left alone.
        /// </summary>
        /// <param name="chain">Chain whose frames will be adjusted.</param>
        /// <param name="offsetMultiplier">Preview offset multiplier (PL12), default 1.0.</param>
        public void AdjustOffsetsJustifyBottom(
            AnimationChainSave chain,
            float offsetMultiplier = 1f)
        {
            if (IsChainLocked(chain) || IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () =>
                {
                    foreach (var frame in chain.Frames)
                    {
                        if (!FrameRegion.IsUnset(frame, GetTextureSize))
                            AdjustOffsetCalculator.ApplyJustifyBottom([frame], offsetMultiplier);
                    }
                },
                this, _events, refreshWireframe: true,
                "Justify Bottom"));
        }

        /// <summary>
        /// Adjust All — shifts or overwrites RelativeX/Y of every frame in
        /// <paramref name="chain"/> by the given amounts.
        /// </summary>
        public void AdjustOffsetsAdjustAll(
            AnimationChainSave chain,
            float? deltaX,
            float? deltaY,
            bool relative)
        {
            if (IsChainLocked(chain) || IsAchxOnlyEditBlocked()) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => AdjustOffsetCalculator.ApplyAdjustAll(chain.Frames, deltaX, deltaY, relative),
                this, _events, refreshWireframe: true,
                "Adjust Offsets"));
        }

        // ── A17: Scale Frame Times ────────────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.ScaleFrameTimesProportional"/>
        public void ScaleFrameTimesProportional(
            AnimationChainSave chain,
            float targetTotalDuration,
            IReadOnlyList<float>? fromLengths = null)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () =>
                {
                    for (int i = 0; i < fromLengths?.Count; i++)
                        chain.Frames[i].FrameLength = fromLengths[i];
                    FrameTimeScaler.ApplyKeepProportional(chain.Frames, targetTotalDuration);
                },
                this, _events, refreshWireframe: false,
                "Scale Frame Times", coalesceKind: "ScaleFrameTimes"));
        }

        /// <summary>
        /// Sets every frame in <paramref name="chain"/> to the same duration:
        /// <paramref name="targetTotalDuration"/> / frame count.
        /// </summary>
        public void ScaleFrameTimesSetAllSame(
            AnimationChainSave chain,
            float targetTotalDuration)
        {
            if (IsChainLocked(chain)) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => FrameTimeScaler.ApplySetAllSame(chain.Frames, targetTotalDuration),
                this, _events, refreshWireframe: false,
                "Scale Frame Times", coalesceKind: "ScaleFrameTimes"));
        }

        // ── F12: Add Multiple Frames ──────────────────────────────────────────

        /// <summary>
        /// Adds <paramref name="count"/> new frames to <paramref name="chain"/>,
        /// optionally advancing one cell of the sheet per frame.
        /// Returns <c>true</c> if any frame's advanced region exceeded texture
        /// bounds (so the UI can warn the user).
        /// </summary>
        public bool AddMultipleFrames(
            AnimationChainSave chain,
            int count,
            bool incrementUV)
        {
            if (IsChainLocked(chain)) return false;

            // With no frame of a tsx's image to continue from, the batch starts at tile 0.
            var lastFrame = chain.Frames.Count > 0 ? chain.Frames[^1] : null;
            var firstTile = count > 0 && (lastFrame is null || IsTextureBlocked(lastFrame.TextureName)) ? FirstTsxTileFrame() : null;
            lastFrame = firstTile ?? lastFrame;
            var result    = BatchFrameBuilder.BuildBatch(
                lastFrame, firstTile is null ? count : count - 1, incrementUV, CurrentFrameLengthUnit,
                lastFrame is null ? null : GetTextureSize(lastFrame.TextureName), _pm.TsxTileGrid);

            var added = (firstTile is null ? result.Frames : result.Frames.Prepend(firstTile)).ToArray();
            if (added.Length > 0)
            {
                _undoManager.Execute(new AddFramesCommand(added, chain, this, _events, _selectedState));
            }

            return result.ExceededTextureBounds;
        }

        // ── IO11: File > New ──────────────────────────────────────────────────

        /// <summary>
        /// Resets the editor to an empty, unsaved state:
        /// creates a fresh <see cref="AnimationChainListSave"/>, clears the file name,
        /// clears all selections, and notifies the UI.
        /// </summary>
        public void NewFile()
        {
            // ResetToBlankDocument covers AnimationChainListSave/FileName/OnDiskCoordinateType
            // plus every native-tsx/texture-size/ReferencedPngs tracking field a reused
            // ProjectManager instance could otherwise leak from the previously-active tab (#1147).
            _pm.ResetToBlankDocument();
            _selectedState.SelectedChain = null;
            _selectedState.SelectedFrame = null;
            _undoManager.Clear();
            RefreshTreeViewRequested?.Invoke();
            _events.RaiseAnimationChainsChanged();
        }

        /// <inheritdoc cref="IAppCommands.CloseProject"/>
        public void CloseProject()
        {
            // See the matching comment in NewFile -- ResetToBlankDocument covers everything a
            // closed project needs cleared except ProjectFolderPath, which is session-wide (see
            // its own doc comment) and reset separately here.
            _pm.ResetToBlankDocument();
            _pm.ProjectFolderPath = null;
            _selectedState.Reset();
            _undoManager.Clear();
            // No content survives a close, so remove any crash-recovery file rather than
            // writing a fresh one for an empty project (unlike NewFile, which still fires
            // AnimationChainsChanged and lets the autosave path write one).
            _ioManager.DeleteRecoveryFiles();
            RefreshTreeViewRequested?.Invoke();
        }

        /// <inheritdoc cref="IAppCommands.HandleApplicationClosing"/>
        public void HandleApplicationClosing() => _ioManager.DeleteRecoveryFiles();

        // ── WF09: Create frame from magic-wand pixel bounds ───────────────────

        /// <summary>
        /// Creates a new <see cref="AnimationFrameSave"/> showing the pixel bounding box returned by the
        /// flood-fill (magic wand) tool, then appends it to <paramref name="chain"/> and selects it.
        /// </summary>
        /// <param name="chain">Chain to add the frame to.</param>
        /// <param name="textureName">Relative texture path (stored as-is in the frame).</param>
        /// <param name="minX">Left pixel bound of the region (inclusive).</param>
        /// <param name="minY">Top pixel bound of the region (inclusive).</param>
        /// <param name="maxX">Right pixel bound of the region (exclusive).</param>
        /// <param name="maxY">Bottom pixel bound of the region (exclusive).</param>
        public void AddFrameFromPixelBounds(
            AnimationChainSave chain,
            string textureName,
            int minX, int minY, int maxX, int maxY)
        {
            CreateAndAddFrame(chain, textureName, new FrameRegion(minX, minY, maxX, maxY));
        }

        // ── Texture assignment (WF10b — write direction) ─────────────────────────

        /// <summary>
        /// Sets the texture name on <paramref name="frame"/> and fires change events so
        /// the tree view and preview panel refresh. This is the write-direction complement
        /// to <see cref="TextureListBuilder.GetAvailableTextures"/>.
        /// </summary>
        /// <param name="frame">The frame whose texture should change. No-ops when null.</param>
        /// <param name="textureName">Relative texture path to assign (may be null to clear).</param>
        public void SetFrameTextureName(AnimationFrameSave frame, string? textureName)
        {
            if (frame == null || IsFrameLocked(frame) || IsTextureBlocked(textureName)) return;
            _undoManager.Execute(new SetFrameTextureNameCommand(frame, frame.TextureName, textureName, GetTextureSize, this, _events));
        }

        public void AddFrameEvent(AnimationFrameSave frame, string name, string? data = null)
        {
            if (!CanEditFrameEvents(frame)) return;
            var after = frame.Events.Append(new AnimationFrameEvent { Name = name, Data = string.IsNullOrWhiteSpace(data) ? null : data });
            _undoManager.Execute(new SetFrameEventsCommand(frame, after, $"Add Event '{name}'", this, _events));
        }

        public void SetFrameEvent(AnimationFrameSave frame, int index, string name, string? data)
        {
            if (!CanEditFrameEvents(frame) || (uint)index >= (uint)frame.Events.Count) return;
            var after = frame.Events.Select(e => e.Clone()).ToList();
            after[index] = new AnimationFrameEvent
            {
                Name = name,
                Data = string.IsNullOrWhiteSpace(data) ? null : data,
            };
            _undoManager.Execute(new SetFrameEventsCommand(frame, after, $"Edit Event '{name}'", this, _events));
        }

        public void RemoveFrameEvent(AnimationFrameSave frame, int index)
        {
            if (!CanEditFrameEvents(frame) || (uint)index >= (uint)frame.Events.Count) return;
            var removedName = frame.Events[index].Name;
            var after = frame.Events.Where((_, i) => i != index);
            _undoManager.Execute(new SetFrameEventsCommand(frame, after, $"Remove Event '{removedName}'", this, _events));
        }

        private bool CanEditFrameEvents(AnimationFrameSave? frame) =>
            frame != null && !IsFrameLocked(frame) && !IsAchxOnlyEditBlocked();

        public void SetFrameTextureName(IReadOnlyList<AnimationFrameSave> frames, string? textureName)
        {
            if (IsTextureBlocked(textureName)) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToArray();
            if (unlockedFrames.Length == 0) return;
            var cmds = unlockedFrames
                .Select(f => (IUndoableCommand)new SetFrameTextureNameCommand(f, f.TextureName, textureName, GetTextureSize, this, _events))
                .ToArray();
            _undoManager.Execute(new CompositeCommand(cmds, "Set Frame Texture", _events));
        }

        public void SetAllFramesTextureName(AnimationChainSave chain, string? textureName)
        {
            if (chain.Frames.Count == 0 || IsChainLocked(chain) || IsTextureBlocked(textureName)) return;
            var cmds = chain.Frames
                .Select(f => (IUndoableCommand)new SetFrameTextureNameCommand(f, f.TextureName, textureName, GetTextureSize, this, _events))
                .ToArray();
            _undoManager.Execute(new CompositeCommand(cmds, "Set All Frame Textures", _events));
        }

        public void SetFrameLength(IReadOnlyList<AnimationFrameSave> frames, NumericEdit newLength)
        {
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            var desc = newLength.IsRelative ? "Set Length" : $"Set Length: {CurrentFrameLengthUnit.FormatCompact(newLength.Apply(0m))}";
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () => { foreach (var f in unlockedFrames) f.FrameLength = newLength.Apply(f.FrameLength); },
                this, _events, false, desc, coalesceKind: "Length"));
        }

        public void SetFrameRelative(IReadOnlyList<AnimationFrameSave> frames, NumericEdit? newRelX, NumericEdit? newRelY)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () =>
                {
                    foreach (var f in unlockedFrames)
                    {
                        if (newRelX.HasValue) f.RelativeX = newRelX.Value.Apply(f.RelativeX);
                        if (newRelY.HasValue) f.RelativeY = newRelY.Value.Apply(f.RelativeY);
                    }
                },
                this, _events, true, "Set Offset", coalesceKind: "Relative"));
        }

        public void SetFrameColor(IReadOnlyList<AnimationFrameSave> frames, ChannelEdit red, ChannelEdit green, ChannelEdit blue)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            // Resolve every target before mutating: a relative edit on an unset channel starts from
            // the inherited value, which an earlier selected frame's edit would otherwise change.
            var targets = unlockedFrames.Select(f =>
            {
                var inherited = ResolveEffectiveColor(f);
                int channelDefault = EffectiveFrameColor.ChannelDefault(inherited.Operation);
                return (Frame: f,
                    Red: red.Apply(f.Red, inherited.Red ?? channelDefault, -255, 255),
                    Green: green.Apply(f.Green, inherited.Green ?? channelDefault, -255, 255),
                    Blue: blue.Apply(f.Blue, inherited.Blue ?? channelDefault, -255, 255));
            }).ToList();
            // Color tints the preview and the timeline/tree thumbnails but not the wireframe, so no
            // wireframe refresh is needed. The AnimationChainsChanged raised here rebuilds those.
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () => { foreach (var t in targets) { t.Frame.Red = t.Red; t.Frame.Green = t.Green; t.Frame.Blue = t.Blue; } },
                this, _events, false, "Set Frame Color", coalesceKind: "Color"));
        }

        private ResolvedFrameColor ResolveEffectiveColor(AnimationFrameSave frame)
        {
            var chain = _objectFinder.GetAnimationChainContaining(frame);
            int index = chain?.Frames.IndexOf(frame) ?? -1;
            return index >= 0 ? EffectiveFrameColor.Resolve(chain!.Frames, index) : default;
        }

        public void SetFrameColorOperation(IReadOnlyList<AnimationFrameSave> frames, ColorOperation? operation)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            // Mode drives how the preview + timeline/tree thumbnails tint; it doesn't touch the
            // wireframe, so no wireframe refresh is needed.
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () => { foreach (var f in unlockedFrames) f.ColorOperation = operation; },
                this, _events, false, "Set Frame Color Mode"));
        }

        public void SetFrameColorValues(
            IReadOnlyList<AnimationFrameSave> frames, int? red, int? green, int? blue, int? alpha, ColorOperation? operation)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () =>
                {
                    foreach (var f in unlockedFrames)
                    {
                        if (red.HasValue) f.Red = red;
                        if (green.HasValue) f.Green = green;
                        if (blue.HasValue) f.Blue = blue;
                        if (alpha.HasValue) f.Alpha = alpha;
                        if (operation.HasValue) f.ColorOperation = operation;
                    }
                },
                this, _events, false, "Set Frame Color"));
        }

        public void ClearFrameColorValues(
            IReadOnlyList<AnimationFrameSave> frames, bool red, bool green, bool blue, bool alpha, bool operation)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () =>
                {
                    foreach (var f in unlockedFrames)
                    {
                        if (red) f.Red = null;
                        if (green) f.Green = null;
                        if (blue) f.Blue = null;
                        if (alpha) f.Alpha = null;
                        if (operation) f.ColorOperation = null;
                    }
                },
                this, _events, false, "Clear Frame Color"));
        }
        public void SetTimeMeasurementUnit(TimeMeasurementUnit unit)
        {
            if (_pm.AnimationChainListSave is not { } list) return;
            _undoManager.Execute(new SetTimeUnitCommand(list, unit, this, _events));
        }

        public void SetFrameAlpha(IReadOnlyList<AnimationFrameSave> frames, ChannelEdit alpha)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;
            var targets = unlockedFrames
                .Select(f => (Frame: f, Alpha: alpha.Apply(f.Alpha, ResolveEffectiveColor(f).Alpha ?? 255, 0, 255)))
                .ToList();
            // Alpha is straight transparency; it fades the preview + timeline/tree thumbnails but not
            // the wireframe, so no wireframe refresh is needed.
            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames, () => { foreach (var t in targets) t.Frame.Alpha = t.Alpha; },
                this, _events, false, "Set Frame Alpha", coalesceKind: "Alpha"));
        }

        public void SetFrameKeyframe(IReadOnlyList<AnimationFrameSave> frames, KeyframeGroup group, bool isKeyframe)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var targets = frames
                .Where(f => !IsFrameLocked(f) && !KeyframeInspectorState.IsFirstFrame(f, _objectFinder.GetAnimationChainContaining(f)))
                .ToList();
            if (targets.Count == 0) return;
            string name = group == KeyframeGroup.Offset ? "Offset" : "Color";
            _undoManager.Execute(new BulkFrameEditCommand(
                targets, () => { foreach (var f in targets) KeyframeInspectorState.SetSkip(group, f, !isKeyframe); },
                this, _events, false, $"{(isKeyframe ? "Enable" : "Disable")} {name} Keyframe"));
        }

        public void SetFrameKeyframeCurve(IReadOnlyList<AnimationFrameSave> frames, KeyframeGroup group, KeyframeCurveChoice curve)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var targets = frames
                .Where(f => !IsFrameLocked(f)
                    && KeyframeInspectorState.IsEffectivelyKeyframe(group, f, _objectFinder.GetAnimationChainContaining(f)))
                .ToList();
            if (targets.Count == 0) return;
            var (type, easing) = KeyframeCurvePicker.ToStored(curve);
            string name = group == KeyframeGroup.Offset ? "Offset" : "Color";
            _undoManager.Execute(new BulkFrameEditCommand(
                targets, () => { foreach (var f in targets) KeyframeInspectorState.SetCurve(group, f, type, easing); },
                this, _events, false, $"Set {name} Curve: {KeyframeCurvePicker.DisplayName(curve)}"));
        }

        public void SetShapeKeyframe(IReadOnlyList<ShapeSave> shapes, ShapeKeyframeGroup group, bool isKeyframe)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var targets = shapes
                .Where(s => !IsShapeLocked(null, s)
                    && KeyframeInspectorState.HasGroup(group, s)
                    && !KeyframeInspectorState.IsRunStart(s, ChainHolding(s)))
                .ToList();
            if (targets.Count == 0) return;
            _undoManager.Execute(new BulkShapePropsCommand(
                targets.Cast<object>().ToList(),
                () => { foreach (var s in targets) KeyframeInspectorState.SetSkip(group, s, !isKeyframe); },
                this, _events, _objectFinder,
                $"{(isKeyframe ? "Enable" : "Disable")} Shape {group} Keyframe", coalesce: false));
        }

        public void SetShapeKeyframeCurve(IReadOnlyList<ShapeSave> shapes, ShapeKeyframeGroup group, KeyframeCurveChoice curve)
        {
            if (IsAchxOnlyEditBlocked()) return;
            var targets = shapes
                .Where(s => !IsShapeLocked(null, s)
                    && KeyframeInspectorState.HasGroup(group, s)
                    && KeyframeInspectorState.IsEffectivelyKeyframe(group, s, ChainHolding(s)))
                .ToList();
            if (targets.Count == 0) return;
            var (type, easing) = KeyframeCurvePicker.ToStored(curve);
            _undoManager.Execute(new BulkShapePropsCommand(
                targets.Cast<object>().ToList(),
                () => { foreach (var s in targets) KeyframeInspectorState.SetCurve(group, s, type, easing); },
                this, _events, _objectFinder,
                $"Set Shape {group} Curve: {KeyframeCurvePicker.DisplayName(curve)}", coalesce: false));
        }

        private AnimationChainSave? ChainHolding(ShapeSave shape) =>
            _objectFinder.GetAnimationFrameContaining(shape) is { } frame
                ? _objectFinder.GetAnimationChainContaining(frame)
                : null;

        public void SetFramePixelRegion(IReadOnlyList<AnimationFrameSave> frames,
            NumericEdit? pixelX, NumericEdit? pixelY, NumericEdit? pixelW, NumericEdit? pixelH)
        {
            var unlockedFrames = frames.Where(f => !IsFrameLocked(f)).ToList();
            if (unlockedFrames.Count == 0) return;

            // A native tsx chain's frames must all share one whole-tile footprint (see
            // FrameFootprintSync), so a width/height typed for one frame is the whole chain's new
            // size: every sibling takes it too, keeping its own X/Y. Same rule the wireframe's
            // handle drag applies; a move (X/Y only) changes no footprint and propagates nothing.
            var siblings = new List<AnimationFrameSave>();
            if (_pm.IsNativeTsxProject && (pixelW.HasValue || pixelH.HasValue))
            {
                var edited = new HashSet<AnimationFrameSave>(unlockedFrames, ReferenceEqualityComparer.Instance);
                siblings = unlockedFrames
                    .Select(_objectFinder.GetAnimationChainContaining)
                    .OfType<AnimationChainSave>()
                    .Distinct<AnimationChainSave>(ReferenceEqualityComparer.Instance)
                    .SelectMany(c => c.Frames)
                    .Where(f => !edited.Contains(f))
                    .ToList();
            }

            _undoManager.Execute(new BulkFrameEditCommand(
                unlockedFrames.Concat(siblings).ToList(), () =>
                {
                    foreach (var f in unlockedFrames)
                    {
                        // Order matters: SetX/SetY preserve each frame's own current width/height, so
                        // they must run before SetWidth/SetHeight overwrite Right/Bottom using the
                        // (possibly just-moved) Left/Top.
                        if (pixelX.HasValue) PixelFrameEditor.SetX(f, pixelX.Value.Apply(FrameDisplayValues.GetPixelX(f)));
                        if (pixelY.HasValue) PixelFrameEditor.SetY(f, pixelY.Value.Apply(FrameDisplayValues.GetPixelY(f)));
                        if (pixelW.HasValue) PixelFrameEditor.SetWidth(f, pixelW.Value.Apply(FrameDisplayValues.GetPixelWidth(f)));
                        if (pixelH.HasValue) PixelFrameEditor.SetHeight(f, pixelH.Value.Apply(FrameDisplayValues.GetPixelHeight(f)));
                    }
                    foreach (var f in siblings)
                    {
                        if (pixelW.HasValue) PixelFrameEditor.SetWidth(f, pixelW.Value.Apply(FrameDisplayValues.GetPixelWidth(f)));
                        if (pixelH.HasValue) PixelFrameEditor.SetHeight(f, pixelH.Value.Apply(FrameDisplayValues.GetPixelHeight(f)));
                    }
                },
                this, _events, true, "Set Region", coalesceKind: "PixelRegion"));
        }

        public void SetRectProps(AnimationFrameSave? frame, AARectSave rect,
            string name, float x, float y, float scaleX, float scaleY)
        {
            if (IsShapeLocked(frame, rect)) return;
            _undoManager.Execute(SetShapePropsCommand.ForRect(frame, rect, name, x, y, scaleX, scaleY, this, _events));
        }

        public void SetCircleProps(AnimationFrameSave? frame, CircleSave circ,
            string name, float x, float y, float radius)
        {
            if (IsShapeLocked(frame, circ)) return;
            _undoManager.Execute(SetShapePropsCommand.ForCircle(frame, circ, name, x, y, radius, this, _events));
        }

        public void SetRectPropsBulk(IReadOnlyList<AARectSave> rects,
            string? name, NumericEdit? x, NumericEdit? y, NumericEdit? scaleX, NumericEdit? scaleY)
        {
            rects = rects.Where(r => !IsShapeLocked(null, r)).ToList();
            if (rects.Count == 0) return;
            _undoManager.Execute(new BulkShapePropsCommand(
                rects.Cast<object>().ToList(),
                () =>
                {
                    foreach (var r in rects)
                    {
                        if (name != null) r.Name = name;
                        if (x.HasValue) r.X = x.Value.Apply(r.X);
                        if (y.HasValue) r.Y = y.Value.Apply(r.Y);
                        if (scaleX.HasValue) r.ScaleX = scaleX.Value.Apply(r.ScaleX);
                        if (scaleY.HasValue) r.ScaleY = scaleY.Value.Apply(r.ScaleY);
                    }
                },
                this, _events, _objectFinder, "Edit Rectangles"));
        }

        public void SetCirclePropsBulk(IReadOnlyList<CircleSave> circles,
            string? name, NumericEdit? x, NumericEdit? y, NumericEdit? radius)
        {
            circles = circles.Where(c => !IsShapeLocked(null, c)).ToList();
            if (circles.Count == 0) return;
            _undoManager.Execute(new BulkShapePropsCommand(
                circles.Cast<object>().ToList(),
                () =>
                {
                    foreach (var c in circles)
                    {
                        if (name != null) c.Name = name;
                        if (x.HasValue) c.X = x.Value.Apply(c.X);
                        if (y.HasValue) c.Y = y.Value.Apply(c.Y);
                        if (radius.HasValue) c.Radius = radius.Value.Apply(c.Radius);
                    }
                },
                this, _events, _objectFinder, "Edit Circles"));
        }

        public void SetShapePropsBulk(IReadOnlyList<ShapeSave> shapes,
            string? name, NumericEdit? x, NumericEdit? y)
        {
            shapes = shapes.Where(s => !IsShapeLocked(null, s)).ToList();
            if (shapes.Count == 0) return;
            _undoManager.Execute(new BulkShapePropsCommand(
                shapes.Cast<object>().ToList(),
                () =>
                {
                    foreach (var s in shapes)
                    {
                        if (name != null) s.Name = name;
                        if (x.HasValue) s.X = x.Value.Apply(s.X);
                        if (y.HasValue) s.Y = y.Value.Apply(s.Y);
                    }
                },
                this, _events, _objectFinder, "Edit Shapes"));
        }

        public void SetPolygonPropsBulk(IReadOnlyList<PolygonSave> polygons,
            string? name, NumericEdit? x, NumericEdit? y)
        {
            polygons = polygons.Where(p => !IsShapeLocked(null, p)).ToList();
            if (polygons.Count == 0) return;
            _undoManager.Execute(new BulkShapePropsCommand(
                polygons.Cast<object>().ToList(),
                () =>
                {
                    foreach (var p in polygons)
                    {
                        if (name != null) p.Name = name;
                        if (x.HasValue) p.X = x.Value.Apply(p.X);
                        if (y.HasValue) p.Y = y.Value.Apply(p.Y);
                    }
                },
                this, _events, _objectFinder, "Edit Polygons"));
        }

        public void SetPolygonProps(AnimationFrameSave? frame, PolygonSave polygon, string name, float x, float y)
        {
            if (IsShapeLocked(frame, polygon)) return;
            _undoManager.Execute(SetShapePropsCommand.ForPolygon(frame, polygon, name, x, y, this, _events));
        }

        /// <inheritdoc cref="IAppCommands.SetPolygonGeometry"/>
        public bool SetPolygonGeometry(PolygonSave polygon, float x, float y, IReadOnlyList<Vector2Save>? points)
        {
            var frame = _objectFinder.GetAnimationFrameContaining(polygon);
            if (frame is not null && IsFrameLocked(frame)) return false;

            var steps = new List<IUndoableCommand>();
            if (polygon.X != x || polygon.Y != y)
                steps.Add(SetShapePropsCommand.ForPolygon(frame, polygon, polygon.Name, x, y, this, _events));
            if (points is not null)
            {
                var after = points.Select(p => new Vector2Save { X = p.X, Y = p.Y }).ToList();
                var entry = new SetPolygonPointsCommand.Entry(frame, polygon, PolygonVertices.CopyPoints(polygon), after);
                steps.Add(new SetPolygonPointsCommand(new[] { entry }, this, _events, $"Edit Points of {ShapeUndoLabel.Format(polygon)}"));
            }

            if (steps.Count == 1) _undoManager.Execute(steps[0]);
            else if (steps.Count > 1) _undoManager.Execute(new CompositeCommand(steps, $"Edit {ShapeUndoLabel.Format(polygon)}", _events));
            return true;
        }

        /// <inheritdoc cref="IAppCommands.MovePolygonVertex"/>
        public void MovePolygonVertex(PolygonSave polygon, int index, float x, float y)
        {
            var (oldX, oldY) = PolygonVertices.Get(polygon, index);
            EditPolygonPoints(polygon, VertexOp.Move, index,
                p => PolygonVertices.Set(p, index, x, y),
                p => OffsetVertex(p, index, x - oldX, y - oldY),
                coalesceGroup: $"PolygonVertex:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(polygon)}:{index}");
        }

        /// <inheritdoc cref="IAppCommands.InsertPolygonVertex"/>
        public void InsertPolygonVertex(PolygonSave polygon, int index, float x, float y)
        {
            var (mx, my) = InsertionMidpoint(polygon, index);
            EditPolygonPoints(polygon, VertexOp.Insert, index,
                p => PolygonVertices.Insert(p, index, x, y),
                p => InsertAtMidpoint(p, index, x - mx, y - my));
        }

        /// <inheritdoc cref="IAppCommands.DeletePolygonVertex"/>
        public bool DeletePolygonVertex(PolygonSave polygon, int index)
        {
            if (PolygonVertices.Count(polygon) <= 3) return false;
            return EditPolygonPoints(polygon, VertexOp.Delete, index,
                p => PolygonVertices.RemoveAt(p, index),
                p => PolygonVertices.RemoveAt(p, index));
        }

        /// <inheritdoc cref="IAppCommands.CommitPolygonPoints"/>
        public void CommitPolygonPoints(PolygonSave polygon, IReadOnlyList<Vector2Save> pointsBefore, PolygonVertexEdit edit)
        {
            var before = new PolygonSave { Points = pointsBefore.Select(p => new Vector2Save { X = p.X, Y = p.Y }).ToList() };
            var after = PolygonVertices.CopyPoints(polygon);
            // The live drag already moved the points. Put them back so the edit replays through one
            // command from the "before" snapshot; a locked chain simply keeps its original outline.
            polygon.Points.Clear();
            polygon.Points.AddRange(PolygonVertices.CopyPoints(before));

            int i = edit.Index;
            var (ax, ay) = PolygonVertices.Get(new PolygonSave { Points = after }, i);
            Action<PolygonSave> replay = p => { p.Points.Clear(); p.Points.AddRange(after); };
            if (edit.Inserted)
            {
                var (mx, my) = InsertionMidpoint(before, i);
                EditPolygonPoints(polygon, VertexOp.Insert, i, replay, p => InsertAtMidpoint(p, i, ax - mx, ay - my));
            }
            else
            {
                var (bx, by) = PolygonVertices.Get(before, i);
                EditPolygonPoints(polygon, VertexOp.Move, i, replay, p => OffsetVertex(p, i, ax - bx, ay - by));
            }
        }

        /// <inheritdoc cref="IAppCommands.FlipPolygonHorizontally"/>
        public void FlipPolygonHorizontally(PolygonSave polygon) => FlipPolygons(polygon, horizontal: true);

        /// <inheritdoc cref="IAppCommands.FlipPolygonVertically"/>
        public void FlipPolygonVertically(PolygonSave polygon) => FlipPolygons(polygon, horizontal: false);

        // Mirrors `polygon` and, when it is part of a polygon multi-selection, every other selected
        // unlocked polygon, each about its own bounds center. Unlike a vertex edit this needs no
        // matching vertex count, so nothing is skipped.
        private void FlipPolygons(PolygonSave polygon, bool horizontal)
        {
            var selected = _selectedState.SelectedPolygons;
            var targets = selected.Contains(polygon) ? selected.ToList() : new List<PolygonSave> { polygon };
            var entries = targets
                .Where(p => !IsShapeLocked(null, p) && p.Points.Count > 0)
                .Select(p => Edited(_objectFinder.GetAnimationFrameContaining(p), p, scratch => MirrorPoints(scratch, horizontal)))
                .ToList();
            if (entries.Count == 0) return;

            string target = entries.Count == 1 ? ShapeUndoLabel.Format(entries[0].Polygon) : $"{entries.Count} Polygons";
            _undoManager.Execute(new SetPolygonPointsCommand(
                entries, this, _events, $"Flip {(horizontal ? "Horizontal" : "Vertical")} {target}"));
        }

        private static void MirrorPoints(PolygonSave polygon, bool horizontal)
        {
            var points = polygon.Points;
            if (horizontal)
            {
                float sum = points.Min(p => p.X) + points.Max(p => p.X);
                foreach (var p in points) p.X = sum - p.X;
            }
            else
            {
                float sum = points.Min(p => p.Y) + points.Max(p => p.Y);
                foreach (var p in points) p.Y = sum - p.Y;
            }
        }

        /// <inheritdoc cref="IAppCommands.InfoRaised"/>
        public event Action<string>? InfoRaised;

        private enum VertexOp { Move, Insert, Delete }

        private static void OffsetVertex(PolygonSave polygon, int index, float dx, float dy)
        {
            var (x, y) = PolygonVertices.Get(polygon, index);
            PolygonVertices.Set(polygon, index, x + dx, y + dy);
        }

        // Midpoint of the edge a vertex inserted at `index` splits: from the vertex before it to the
        // one it pushes along, wrapping to the first vertex when appending.
        private static (float X, float Y) InsertionMidpoint(PolygonSave polygon, int index)
        {
            int count = PolygonVertices.Count(polygon);
            var (ax, ay) = PolygonVertices.Get(polygon, (index - 1 + count) % count);
            var (bx, by) = PolygonVertices.Get(polygon, index % count);
            return ((ax + bx) / 2f, (ay + by) / 2f);
        }

        private static void InsertAtMidpoint(PolygonSave polygon, int index, float dx, float dy)
        {
            var (mx, my) = InsertionMidpoint(polygon, index);
            PolygonVertices.Insert(polygon, index, mx + dx, my + dy);
        }

        // Applies `edit` to `polygon` and, when it is part of a polygon multi-selection, `peerEdit` to
        // every other selected, unlocked polygon with the same vertex count (issue #1255). Edits run
        // on scratch copies so one command owns every snapshot and undoes them together.
        private bool EditPolygonPoints(PolygonSave polygon, VertexOp op, int index,
            Action<PolygonSave> edit, Action<PolygonSave> peerEdit, string? coalesceGroup = null)
        {
            var frame = _objectFinder.GetAnimationFrameContaining(polygon);
            if (frame is not null && IsFrameLocked(frame)) return false;

            var entries = new List<SetPolygonPointsCommand.Entry> { Edited(frame, polygon, edit) };
            var selected = _selectedState.SelectedPolygons;
            var peers = selected.Contains(polygon)
                ? selected.Where(p => !ReferenceEquals(p, polygon) && !IsShapeLocked(null, p)).ToList()
                : new List<PolygonSave>();
            int count = PolygonVertices.Count(polygon);
            foreach (var peer in peers.Where(p => PolygonVertices.Count(p) == count))
                entries.Add(Edited(_objectFinder.GetAnimationFrameContaining(peer), peer, peerEdit));

            string target = entries.Count == 1 ? ShapeUndoLabel.Format(polygon) : $"{entries.Count} Polygons";
            string description = op switch
            {
                VertexOp.Insert => $"Add Vertex to {target}",
                VertexOp.Delete => $"Delete Vertex from {target}",
                _ => $"Move Vertex {index + 1} of {target}",
            };
            _undoManager.Execute(new SetPolygonPointsCommand(entries, this, _events, description, coalesceGroup));

            int skipped = peers.Count + 1 - entries.Count;
            if (skipped > 0)
            {
                string verb = op switch { VertexOp.Insert => "Added", VertexOp.Delete => "Deleted", _ => "Moved" };
                InfoRaised?.Invoke($"{verb} vertex on {entries.Count} of {peers.Count + 1} polygons, " +
                    $"{skipped} skipped: different vertex count");
            }
            return true;
        }

        private static SetPolygonPointsCommand.Entry Edited(AnimationFrameSave? frame, PolygonSave polygon, Action<PolygonSave> edit)
        {
            var scratch = new PolygonSave { Points = PolygonVertices.CopyPoints(polygon) };
            edit(scratch);
            return new(frame, polygon, PolygonVertices.CopyPoints(polygon), scratch.Points);
        }

        /// <inheritdoc cref="IAppCommands.SealPendingEdits"/>
        public void SealPendingEdits() => _undoManager.SealCoalescing();

        /// <inheritdoc cref="IAppCommands.DiscardPendingEdits"/>
        public void DiscardPendingEdits()
        {
            if (_undoManager.HasOpenCoalescedEdit)
                _undoManager.Undo();
        }

        /// <inheritdoc cref="IAppCommands.HasSameFrameNameCollision"/>
        public bool HasSameFrameNameCollision(IReadOnlyList<object> shapes) =>
            ShapeFrameLookup.HasSameFrameCollision(_pm.AnimationChainListSave, shapes);

        // ── Paste (clipboard → project) ───────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.PasteChains"/>
        public void PasteChains(IReadOnlyList<AnimationChainSave> chains, int? insertIndex = null)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return;
            _undoManager.Execute(new PasteChainsCommand(acls, chains, this, _events, _selectedState, insertIndex));
        }

        /// <inheritdoc cref="IAppCommands.PasteFrames"/>
        public bool PasteFrames(AnimationChainSave chain, IReadOnlyList<AnimationFrameSave> frames,
            int? insertIndex = null)
        {
            if (IsChainLocked(chain)) return false;
            var clones = frames.Select(AnimationCloneHelper.CloneFrame).ToArray();
            _undoManager.Execute(new AddFramesCommand(clones, chain, this, _events, _selectedState, insertIndex));
            return true;
        }

        /// <inheritdoc cref="IAppCommands.PasteShapes(AnimationFrameSave, IReadOnlyList{object})"/>
        public bool PasteShapes(AnimationFrameSave frame, IReadOnlyList<object> shapes)
        {
            if (IsAchxOnlyEditBlocked()) return false;
            var clones = BuildShapeClones(frame, shapes);
            if (clones.Count == 0) return false;
            _undoManager.Execute(new PasteShapesCommand(frame, clones, this, _events, _selectedState));
            return true;
        }

        /// <inheritdoc cref="IAppCommands.PasteShapes(IReadOnlyList{AnimationFrameSave}, IReadOnlyList{object})"/>
        public void PasteShapes(IReadOnlyList<AnimationFrameSave> frames, IReadOnlyList<object> shapes)
        {
            if (frames.Count == 0 || IsAchxOnlyEditBlocked()) return;
            if (frames.Count == 1)
            {
                PasteShapes(frames[0], shapes);
                return;
            }

            // One command across all frames (not a composite of per-frame commands) so the
            // selection ends up as every pasted shape, not just the last frame's.
            var groups = new List<(AnimationFrameSave Frame, IReadOnlyList<object> Shapes)>();
            foreach (var frame in frames)
            {
                var clones = BuildShapeClones(frame, shapes);
                if (clones.Count > 0)
                    groups.Add((frame, clones));
            }
            if (groups.Count == 0) return;

            int shapeCount = shapes.Count;
            string desc = shapeCount == 1
                ? $"Paste {ShapeUndoLabel.Format(shapes[0])} into {groups.Count} Frames"
                : $"Paste {shapeCount} Shapes into {groups.Count} Frames";
            _undoManager.Execute(new PasteShapesCommand(groups, desc, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.PasteChainsCut"/>
        public void PasteChainsCut(IReadOnlyList<AnimationChainSave> chains, int? insertIndex = null)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null || chains.Count == 0) return;

            var anchors = _selectedState.SelectedChains is { Count: > 0 } selected ? selected : chains;
            int index = insertIndex ?? ChainPasteLogic.ResolveBlockInsertIndexAfterAnchors(acls, anchors);
            if (BuildMoveChainsToIndex(chains, index, "Cut") is { } move)
                _undoManager.Execute(new SelectMovedChainsCommand(move, chains, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.PasteFramesCut"/>
        public void PasteFramesCut(AnimationChainSave targetChain,
            IReadOnlyList<AnimationFrameSave> frames, int? insertIndex)
        {
            var sources = frames
                .Select(f => (Frame: f, Chain: _objectFinder.GetAnimationChainContaining(f)))
                .Where(x => x.Chain is not null)
                .ToList();
            if (sources.Count == 0 || IsChainLocked(targetChain)) return;

            // A locked chain keeps its frames, so the paste gets copies of them.
            var block = sources
                .Select(x => IsChainLocked(x.Chain!) ? AnimationCloneHelper.CloneFrame(x.Frame) : x.Frame)
                .ToArray();
            var sourceChains = sources.Select(x => x.Chain!).Where(c => !IsChainLocked(c)).ToList();
            string description = sources.Count == 1
                ? $"Cut Frame in '{sources[0].Chain!.Name}'"
                : $"Cut {sources.Count} Frames";
            _undoManager.Execute(new MoveFramesCommand(
                block, sourceChains, targetChain, insertIndex ?? targetChain.Frames.Count, description, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.PasteShapesCut"/>
        public void PasteShapesCut(AnimationFrameSave targetFrame, IReadOnlyList<object> shapes)
        {
            var sources = shapes.OfType<ShapeSave>()
                .Select(s => (Shape: s, Frame: _objectFinder.GetAnimationFrameContaining(s)))
                .Where(x => x.Frame is not null)
                .ToList();
            if (sources.Count == 0 || IsFrameLocked(targetFrame) || IsAchxOnlyEditBlocked()) return;

            // A locked chain keeps its shapes, so the paste gets copies of them.
            var block = sources
                .Select(x => IsFrameLocked(x.Frame!) ? x.Shape.Clone() : x.Shape)
                .ToArray();
            _undoManager.Execute(new MoveShapesToFrameCommand(
                block, sources.Select(x => x.Frame!).Where(f => !IsFrameLocked(f)), targetFrame, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.CutFromBackgroundTab"/>
        public bool CutFromBackgroundTab(TabEntry tab, IPendingCutState cut)
        {
            var acls = tab.CachedEditorModel!;
            var deletes = new List<IDeleteCommand>();
            string desc;
            switch (cut.Kind)
            {
                case CopySelectionKind.Chain:
                    deletes.Add(new DeleteChainsCommand(cut.Chains, acls, this, _events, _selectedState));
                    desc = cut.Chains.Count == 1 ? $"Cut Animation '{cut.Chains[0].Name}'" : $"Cut {cut.Chains.Count} Animations";
                    break;

                case CopySelectionKind.Frame:
                    var frames = cut.Frames.Select(f => (Frame: f, Chain: PendingCutState.ChainContaining(acls, f)!)).ToList();
                    foreach (var group in frames.Where(x => !IsChainLocked(x.Chain)).GroupBy(x => x.Chain))
                        deletes.Add(new DeleteFramesCommand(group.Select(x => x.Frame).ToList(), group.Key, this, _events, _selectedState));
                    desc = frames.Count == 1 ? $"Cut Frame in '{frames[0].Chain.Name}'" : $"Cut {frames.Count} Frames";
                    break;

                default:
                    foreach (var shape in cut.Shapes.OfType<ShapeSave>())
                        if (PendingCutState.FrameContaining(acls, shape) is { } frame && !IsChainLocked(PendingCutState.ChainContaining(acls, frame)!))
                            deletes.Add(new DeleteShapeCommand(shape, frame, this, _events, _selectedState));
                    desc = cut.Shapes.Count == 1 ? $"Cut {ShapeUndoLabel.Format(cut.Shapes[0])}" : $"Cut {cut.Shapes.Count} Shapes";
                    break;
            }
            if (CompositeCommand.Removed(deletes, desc, _events) is not { } command) return false;

            tab.UndoSnapshot = new UndoSnapshot(
                [.. tab.UndoSnapshot?.UndoStack ?? [], new UndoEntry(command, tab.CachedTsxState)], []);
            return true;
        }

        private List<object> BuildShapeClones(AnimationFrameSave frame, IReadOnlyList<object> shapes)
        {
            if (IsFrameLocked(frame)) return new List<object>();

            var existingNames = GetShapeNames(frame);
            var clones = new List<object>();
            foreach (var shape in shapes)
            {
                if (AnimationCloneHelper.CloneShape(shape) is not ShapeSave copy) continue;
                copy.Name = StringFunctions.MakeStringUnique(copy.Name, existingNames, 2);
                existingNames.Add(copy.Name);
                clones.Add(copy);
            }
            return clones;
        }

        // ── Hot Reload ────────────────────────────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.WireHotReloadWatcher"/>
        public void WireHotReloadWatcher()
        {
            HotReloadWatcher.AchxChangedOnDisk += path =>
                DoOnUiThread(() => OnAchxChangedOnDisk(path));

            HotReloadWatcher.PngChangedOnDisk += path =>
                DoOnUiThread(() => ReloadPngFromDisk(path));

            HotReloadWatcher.AchxDeletedOnDisk += path =>
                DoOnUiThread(() => _events.RaiseAchxDeletedOnDisk(path));
        }

        private void OnAchxChangedOnDisk(string path)
        {
            // A report can outlive its document: queued just before a tab switch, or for the file open before an
            // Untitled, .seqj or .png tab. The tab it belongs to compares its file when it is switched back to.
            if (IsDocumentTabInFront?.Invoke() == false
                || string.IsNullOrEmpty(_pm.FileName) || new FilePath(_pm.FileName) != new FilePath(path)) return;

            // The document holds edits its file lacks; only File > Reload from Disk drops them.
            if (_undoManager.SaveState == SaveState.Failed)
            {
                RefuseIfChangedOnDisk(path, _diskState);
                return;
            }
            ReloadAchxFromDisk(path);
        }

        /// <inheritdoc cref="IAppCommands.ReloadAchxFromDisk"/>
        public void ReloadAchxFromDisk(string path)
        {
            // Capture selection state before reload
            string? selectedChainName = _selectedState.SelectedChain?.Name;
            int? selectedFrameIndex = _selectedState.SelectedFrame != null
                ? _selectedState.SelectedChain?.Frames.IndexOf(_selectedState.SelectedFrame)
                : null;

            // Taken even when the reload fails: the save pause covers that content, and only a
            // further change should reload the tab.
            _diskState = ReadDiskState(path);
            try
            {
                // SyncHotReloadWatcher watches whatever IProjectManager.FileName currently is,
                // tsx or achx/achj alike, with no extension check -- so this must route a tsx
                // path to LoadTsxProject. LoadAnimationChain's hand-rolled XML parser doesn't
                // validate the root element name, so it would silently "succeed" against a
                // tsx's <tileset> root with zero chains instead of throwing.
                var filePath = new AnimationEditor.Core.Paths.FilePath(path);
                if (filePath.Extension == "tsx")
                    _pm.LoadTsxProject(filePath);
                else
                    _pm.LoadAnimationChain(filePath);
            }
            catch (Exception ex)
            {
                _staleOnDiskReasons[new AnimationEditor.Core.Paths.FilePath(path).FullPath] = ex.Message;
                HotReloadFailed?.Invoke(path, ex.Message);
                return;
            }
            _staleOnDiskReasons.Remove(new AnimationEditor.Core.Paths.FilePath(path).FullPath);

            _undoManager.Clear();
            _undoManager.MarkSaved();

            RestoreSelection(selectedChainName, selectedFrameIndex);

            // Refresh tree without collapsing (preserves expanded nodes)
            RefreshTreeViewRequested?.Invoke();

            // Update PNG watch list for new references
            var achxDir = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var newPngs = GetReferencedAbsolutePngPaths(path, achxDir);
            HotReloadWatcher.UpdatePngList(newPngs);

            _ioManager.LoadAndApplyCompanionFileFor(path);
            RefreshWireframeRequested?.Invoke();
            RefreshAnimationFrameDisplayRequested?.Invoke();

            _events.RaiseAchxReloadedFromDisk(path);
            EditorProjectModelChanged?.Invoke(path);
        }

        /// <inheritdoc cref="IAppCommands.ReloadPngFromDisk"/>
        public void ReloadPngFromDisk(string absolutePngPath) =>
            _events.RaisePngChangedOnDisk(absolutePngPath);

        /// <inheritdoc cref="IAppCommands.SyncHotReloadWatcher"/>
        public void SyncHotReloadWatcher()
        {
            var achxPath = _pm.FileName ?? string.Empty;
            var achxDir  = !string.IsNullOrEmpty(achxPath)
                ? System.IO.Path.GetDirectoryName(achxPath) ?? string.Empty
                : string.Empty;
            HotReloadWatcher.StartWatching(achxPath, GetReferencedAbsolutePngPaths(achxPath, achxDir));
            // It takes the file as it is now for what the document holds.
            if (_pm.TsxLacksOutsideChange)
                HotReloadWatcher.ReportChange(achxPath);
        }

        private IEnumerable<string> GetReferencedAbsolutePngPaths(string _, string achxDir)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A tsx's single <image> is shown on open even when no frame references it, so it
            // must be watched independent of frames (#1365).
            if (_pm.IsNativeTsxProject)
                foreach (var png in _pm.ReferencedPngs)
                    if (seen.Add(png.FullPath)) yield return png.FullPath;

            foreach (var chain in acls.AnimationChains)
            foreach (var frame in chain.Frames)
            {
                if (string.IsNullOrEmpty(frame.TextureName)) continue;
                var abs = System.IO.Path.IsPathRooted(frame.TextureName)
                    ? frame.TextureName
                    : System.IO.Path.Combine(achxDir, frame.TextureName);
                if (seen.Add(abs)) yield return abs;
            }
        }
    }
}
