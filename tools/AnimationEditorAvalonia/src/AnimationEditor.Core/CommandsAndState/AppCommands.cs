using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Fired after <see cref="SaveCurrentAnimationChainListAsync"/> successfully saves a file.
        /// The argument is the full path of the saved file.
        /// </summary>
        public event Action<string>? SaveAsCompleted;

        /// <inheritdoc cref="IAppCommands.PixiJsExportCompleted"/>
        public event Action<string, IReadOnlyList<string>>? PixiJsExportCompleted;

        /// <inheritdoc cref="IAppCommands.LoadFailed"/>
        public event Action<string, Exception>? LoadFailed;

        /// <inheritdoc cref="IAppCommands.HotReloadFailed"/>
        public event Action<string, string>? HotReloadFailed;

        /// <inheritdoc cref="IAppCommands.EditorProjectModelChanged"/>
        public event Action<string?>? EditorProjectModelChanged;

        // ── Open workflow ─────────────────────────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.OpenAchxWorkflowAsync"/>
        public async Task OpenAchxWorkflowAsync(string path)
        {
            // Quick-parse to read CoordinateType without committing to a full load.
            AnimationChainListSave preview;
            try { preview = AnimationChainListSave.FromFile(path); }
            catch (Exception ex) { LoadFailed?.Invoke(path, ex); return; }

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
                        $"Cannot open '{System.IO.Path.GetFileName(path)}' — the following texture(s) could not be found or decoded: {names}. " +
                        "All textures must be present to convert UV coordinates to pixel coordinates."));
                return;
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

            if (outcome == IO.UvLoadOutcome.RefuseUserDeclined) return;

            bool failed = false;
            void OnFail(string _, Exception __) => failed = true;
            LoadFailed += OnFail;
            try { LoadAnimationChainFromParsed(path, preview); }
            finally { LoadFailed -= OnFail; }

            if (failed) return;

            if (outcome == IO.UvLoadOutcome.ConvertAndLoad)
                _pm.OnDiskCoordinateType = FlatRedBall2.Animation.Content.TextureCoordinateType.Pixel;

            _events.CallAchxLoaded(path);
            _events.RaiseCurrentFileChanged(path);
            _events.RaiseAvailableTexturesChanged();
        }

        // -------------------------------------------------------------------------

        public void LoadAnimationChain(string fileName)
        {
            try
            {
                _pm.LoadAnimationChain(new AnimationEditor.Core.Paths.FilePath(fileName));
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(fileName, ex);
                return;
            }

            FinishLoadIntoEditor(fileName);
        }

        private void LoadAnimationChainFromParsed(string fileName, AnimationChainListSave preParsed)
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

            FinishLoadIntoEditor(fileName);
        }

        private void FinishLoadIntoEditor(string fileName)
        {
            _undoManager.Clear();
            _undoManager.MarkSaved();
            _selectedState.Reset();
            _selectedState.SelectedChain = _pm.AnimationChainListSave?.AnimationChains.FirstOrDefault();
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

            // Start watching the loaded file and its referenced PNGs
            var achxDir = System.IO.Path.GetDirectoryName(fileName) ?? string.Empty;
            var pngPaths = GetReferencedAbsolutePngPaths(fileName, achxDir);
            HotReloadWatcher.StartWatching(fileName, pngPaths);

            EditorProjectModelChanged?.Invoke(fileName);
        }

        /// <inheritdoc cref="IAppCommands.CaptureTabEditorState"/>
        public void CaptureTabEditorState(TabEntry tab)
        {
            TabEditorCache.CaptureFromProject(tab, _pm);
            tab.CachedSelectedChainName = _selectedState.SelectedChain?.Name;
            tab.CachedSelectedFrameIndex = _selectedState.SelectedFrame != null
                ? _selectedState.SelectedChain?.Frames.IndexOf(_selectedState.SelectedFrame)
                : null;
        }

        /// <inheritdoc cref="IAppCommands.TryActivateTabFromCache"/>
        public bool TryActivateTabFromCache(TabEntry tab)
        {
            if (!TabEditorCache.HasFreshCache(tab))
            {
                TabEditorCache.Invalidate(tab);
                return false;
            }

            TabEditorCache.ApplyToProject(tab, _pm);
            RestoreTabSelection(tab);
            // See the comment in FinishLoadIntoEditor: build already-expanded from the
            // companion file so reactivating a tab doesn't flicker collapsed-then-expanded.
            RebuildTreeViewRequested?.Invoke((IReadOnlyList<string>?)_ioManager.TryLoadCompanionSettings(tab.Path.FullPath)?.ExpandedNodes ?? Array.Empty<string>());
            _ioManager.LoadAndApplyCompanionFileFor(tab.Path.FullPath);
            RefreshWireframeRequested?.Invoke();
            RefreshAnimationFrameDisplayRequested?.Invoke();
            SyncHotReloadWatcher();

            _events.RaiseCurrentFileChanged(tab.Path.FullPath);
            _events.RaiseAvailableTexturesChanged();
            EditorProjectModelChanged?.Invoke(tab.Path.FullPath);
            return true;
        }

        /// <inheritdoc cref="IAppCommands.ActivateTabContentAsync"/>
        public async Task ActivateTabContentAsync(TabEntry tab)
        {
            if (TryActivateTabFromCache(tab))
                return;

            // FinishLoadIntoEditor resets selection to the first chain; keep the prior
            // per-tab selection so we can restore it after the disk load.
            string? chainName = tab.CachedSelectedChainName;
            int? frameIndex = tab.CachedSelectedFrameIndex;

            await OpenAchxWorkflowAsync(tab.Path.FullPath);

            tab.CachedSelectedChainName = chainName;
            tab.CachedSelectedFrameIndex = frameIndex;
            RestoreTabSelection(tab);
            CaptureTabEditorState(tab);
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

        public void SaveCurrentAnimationChainList(string? fileName = null)
        {
            var target = fileName ?? _pm.FileName;
            if (!string.IsNullOrEmpty(target))
            {
                HotReloadWatcher.RecordOwnSave(target);
                try
                {
                    _pm.SaveAnimationChainList(target);
                    _undoManager.MarkSaved();
                    EditorProjectModelChanged?.Invoke(target);
                }
                catch
                {
                    _undoManager.MarkSaveFailed();
                }
            }
            else
            {
                _ioManager.WriteRecoveryFile(_pm.AnimationChainListSave);
            }
        }

        /// <summary>
        /// Show a Save-As file picker via <see cref="FileDialogService"/>, save the
        /// current animation chain list to the chosen path, update
        /// <see cref="ProjectManager.FileName"/>, and fire <see cref="SaveAsCompleted"/>
        /// and <see cref="IApplicationEvents.CurrentFileChanged"/>.
        /// Does nothing if the user cancels the dialog.
        /// </summary>
        public async Task SaveCurrentAnimationChainListAsync()
        {
            var path = await FileDialogService.PickSaveFileAsync(
                "Save Animation Chain", "achx", "Animation Chain (*.achx)");

            if (string.IsNullOrEmpty(path)) return;

            SaveCurrentAnimationChainList(path);
            _pm.FileName = path;
            SyncHotReloadWatcher();  // pick up the saved achx path + any PNG dirs
            _ioManager.DeleteRecoveryFile();
            SaveAsCompleted?.Invoke(path);
            _events.RaiseCurrentFileChanged(path);
        }

        /// <summary>
        /// Show a file picker and export the current animation chain list as a PixiJS spritesheet
        /// JSON (<c>SpriteSheetJson</c>). Does nothing if there is no project or the user cancels.
        /// Fires <see cref="PixiJsExportCompleted"/> with the path and any non-fatal warnings
        /// (dropped per-frame duration, multiple source textures) on success.
        /// </summary>
        public async Task ExportToPixiJsAsync()
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            var path = await FileDialogService.PickSaveFileAsync(
                "Export to PixiJS", "json", "PixiJS Spritesheet (*.json)");
            if (string.IsNullOrEmpty(path)) return;

            var result = Export.PixiJsSpriteSheetExporter.Export(acls, _pm.GetTextureSizeInPixels);
            System.IO.File.WriteAllText(path, result.Json);

            // PixiJS resolves meta.image relative to the JSON, so when exporting elsewhere the
            // referenced textures must travel with it. Copy each relative texture (preserving any
            // subdirectory) from the .achx's directory into the export directory.
            var exportDir = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var sourceDir = string.IsNullOrEmpty(_pm.FileName)
                ? string.Empty
                : System.IO.Path.GetDirectoryName(_pm.FileName) ?? string.Empty;
            var copyWarnings = CopyReferencedTextures(result.ReferencedTextures, sourceDir, exportDir);

            PixiJsExportCompleted?.Invoke(path, result.Warnings.Concat(copyWarnings).ToList());
        }

        /// <summary>
        /// Copies each relative texture in <paramref name="textureNames"/> from
        /// <paramref name="sourceDir"/> to <paramref name="exportDir"/>, preserving any
        /// subdirectory. No-op when the directories are the same or the source is unknown
        /// (unsaved project). Rooted (absolute) texture names are left in place. Returns a
        /// warning per texture that could not be found at the source.
        /// </summary>
        private static IReadOnlyList<string> CopyReferencedTextures(
            IReadOnlyList<string> textureNames, string sourceDir, string exportDir)
        {
            var warnings = new List<string>();

            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(exportDir)) return warnings;
            if (string.Equals(System.IO.Path.GetFullPath(sourceDir),
                              System.IO.Path.GetFullPath(exportDir),
                              StringComparison.OrdinalIgnoreCase))
                return warnings;

            foreach (var name in textureNames)
            {
                if (string.IsNullOrEmpty(name) || System.IO.Path.IsPathRooted(name)) continue;

                var src = System.IO.Path.Combine(sourceDir, name);
                if (!System.IO.File.Exists(src))
                {
                    warnings.Add($"Texture '{name}' was not found next to the .achx, so it was not copied.");
                    continue;
                }

                var dest = System.IO.Path.Combine(exportDir, name);
                var destDir = System.IO.Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) System.IO.Directory.CreateDirectory(destDir);
                System.IO.File.Copy(src, dest, overwrite: true);
            }

            return warnings;
        }

        public void DeleteAnimationChains(List<AnimationChainSave> animationChains)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            // Capture which chains will actually be removed (so the toast count is honest)
            // before the command runs and removes them.
            var valid = animationChains.Where(c => acls.AnimationChains.Contains(c)).ToList();
            _undoManager.Execute(new DeleteChainsCommand(animationChains, acls, this, _events, _selectedState));
            if (valid.Count > 0)
            {
                string label = valid.Count == 1 ? valid[0].Name : $"{valid.Count} animations";
                ItemsDeleted?.Invoke(label);
            }
        }

        public void AddAxisAlignedRectangle(AnimationFrameSave frame)
        {
            var rectangleSave = new AARectSave
            {
                ScaleX = 8,
                ScaleY = 8,
                Name = StringFunctions.MakeStringUnique("AxisAlignedRectangleInstance",
                    GetSelectedFrameShapeNames())
            };

            ApplyRectangleMatch(rectangleSave, frame);
            _undoManager.Execute(new AddAxisAlignedRectangleCommand(rectangleSave, frame, this, _events, _selectedState));
        }

        public void AddCircle(AnimationFrameSave frame)
        {
            var circleSave = new CircleSave
            {
                Radius = 8,
                Name = StringFunctions.MakeStringUnique("CircleInstance",
                    GetSelectedFrameShapeNames())
            };

            ApplyCircleMatch(circleSave, frame);
            _undoManager.Execute(new AddCircleCommand(circleSave, frame, this, _events, _selectedState));
        }

        /// <summary>
        /// Moves <paramref name="rectangle"/> to <paramref name="animationFrame"/>'s offset
        /// (the "Match Frame Size" command). Records an undo entry; callers refresh the UI.
        /// </summary>
        public void MatchRectangleToFrame(AARectSave rectangle, AnimationFrameSave animationFrame)
        {
            _undoManager.Execute(new MoveShapeCommand(
                animationFrame, rectangle, rectangle.X, rectangle.Y,
                animationFrame.RelativeX, animationFrame.RelativeY, this, _events));
        }

        /// <summary>
        /// Moves <paramref name="circle"/> to <paramref name="animationFrame"/>'s offset.
        /// Records an undo entry; callers refresh the UI.
        /// </summary>
        public void MatchCircleToFrame(CircleSave circle, AnimationFrameSave animationFrame)
        {
            _undoManager.Execute(new MoveShapeCommand(
                animationFrame, circle, circle.X, circle.Y,
                animationFrame.RelativeX, animationFrame.RelativeY, this, _events));
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
            _undoManager.Execute(new DeleteCircleCommand(circle, owner, this, _events, _selectedState));
        }

        public void DeleteAxisAlignedRectangle(AARectSave rectangle, AnimationFrameSave owner)
        {
            _undoManager.Execute(new DeleteAxisAlignedRectangleCommand(rectangle, owner, this, _events, _selectedState));
        }

        public void DeleteShapes(AnimationFrameSave frame, List<AARectSave> rectangles, List<CircleSave> circles)
        {
            var commands = new List<IUndoableCommand>();
            foreach (var rect in rectangles.ToArray())
                commands.Add(new DeleteAxisAlignedRectangleCommand(rect, frame, this, _events, _selectedState));
            foreach (var circle in circles.ToArray())
                commands.Add(new DeleteCircleCommand(circle, frame, this, _events, _selectedState));
            if (commands.Count == 0) return;

            int total = commands.Count;
            // Single shape reuses its own command's "Delete Rectangle 'X'" / "Delete Circle 'X'"
            // text so the History panel reads naturally instead of "Composite Action".
            string desc = total == 1 ? commands[0].Description : $"Delete {total} Shapes";
            _undoManager.Execute(new CompositeCommand(commands, desc));

            string label = total == 1
                ? (rectangles.Count == 1 ? rectangles[0].Name : circles[0].Name)
                : $"{total} shapes";
            ItemsDeleted?.Invoke(label);
        }

        public void DeleteFrames(List<AnimationFrameSave> frames)
        {
            var chain = _selectedState.SelectedChain;
            if (chain != null)
            {
                var validFrames = frames.Where(f => chain.Frames.Contains(f)).ToList();
                string label = validFrames.Count == 1
                    ? $"Frame {chain.Frames.IndexOf(validFrames[0]) + 1}"
                    : $"{validFrames.Count} frames";
                _undoManager.Execute(new DeleteFramesCommand(frames, chain, this, _events, _selectedState));
                if (validFrames.Count > 0)
                    ItemsDeleted?.Invoke(label);
            }

            RefreshWireframeRequested?.Invoke();
            _events.RaiseAnimationChainsChanged();
        }

        private List<string> GetSelectedFrameShapeNames()
        {
            var frame = _selectedState.SelectedFrame;
            if (frame?.ShapesSave == null) return new List<string>();

            return frame.ShapesSave!.Shapes
                .Select(s => s switch { AARectSave r => r.Name, CircleSave c => c.Name, _ => null })
                .OfType<string>()
                .ToList();
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
        /// already uses <paramref name="newName"/>; returns <c>true</c> on success.
        /// </summary>
        public bool RenameChain(AnimationChainSave chain, string newName)
        {
            if (chain.Name == newName) return true;

            var acls = _pm.AnimationChainListSave;
            if (acls is not null &&
                acls.AnimationChains.Any(c => !ReferenceEquals(c, chain) && c.Name == newName))
                return false;

            _undoManager.Execute(new RenameChainCommand(chain, chain.Name, newName, this, _events));
            return true;
        }

        public void AddFrame(AnimationChainSave chain, string? textureName = null)
        {
            // When no texture is passed, inherit both the texture and the sub-region from a
            // source frame so the new frame lands on the same sheet cell the user is working in,
            // rather than snapping back to the whole texture. An explicit texture (e.g. drag-drop
            // of a different image) keeps the default full-texture region.
            var source = textureName is null
                ? ResolveInheritedFrame(chain, _pm.AnimationChainListSave)
                : null;

            var frame = new AnimationFrameSave
            {
                TextureName  = textureName ?? source?.TextureName ?? string.Empty,
                LeftCoordinate   = source?.LeftCoordinate   ?? 0f,
                RightCoordinate  = source?.RightCoordinate  ?? 1f,
                TopCoordinate    = source?.TopCoordinate    ?? 0f,
                BottomCoordinate = source?.BottomCoordinate ?? 1f,
                FrameLength      = 0.1f,
                ShapesSave = new FlatRedBall2.Animation.Content.ShapesSave()
            };
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

            if (chainList is not null)
            {
                foreach (var otherChain in chainList.AnimationChains)
                    foreach (var frame in otherChain.Frames)
                        if (!string.IsNullOrEmpty(frame.TextureName))
                            return frame;
            }

            return null;
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
            var list = _pm.AnimationChainListSave?.AnimationChains;
            if (list is null || chains.Count == 0) return;

            var indices = chains
                .Select(c => list.IndexOf(c))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return;

            var moved = indices.Select(i => list[i]).ToArray();

            // insertIndex is measured against the list before removal; removing the moved
            // chains shifts the landing spot left by however many of them sat ahead of it
            // (mirrors MoveFramesCommand).
            int insertAt = insertIndex - indices.Count(i => i < insertIndex);

            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
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
                    ? $"Move Animation '{moved[0].Name}'"
                    : $"Move {moved.Length} Animations"));
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
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.Remove(frame); chain.Frames.Insert(0, frame); },
                this, _events, () => RefreshTreeNode(chain),
                $"Move Frame in '{chain.Name}' to Top"));
        }

        public void MoveFrameToBottom(AnimationFrameSave frame, AnimationChainSave chain)
        {
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
            if (frames.Count == 0) return;
            _undoManager.Execute(new MoveFramesCommand(
                frames, sourceChain, targetChain, insertIndex, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.MoveFramesRelative"/>
        public void MoveFramesRelative(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave chain, int delta)
        {
            if (delta == 0 || frames.Count == 0) return;

            var indices = frames
                .Select(f => chain.Frames.IndexOf(f))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return;

            // Rigid group: if shifting either edge by delta would cross a boundary, no-op.
            if (indices[0] + delta < 0) return;
            if (indices[^1] + delta > chain.Frames.Count - 1) return;

            var selected = new HashSet<int>(indices);

            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
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
                    delta > 0 ? "Down" : "Up", frameContextName: chain.Name)));
        }

        /// <inheritdoc cref="IAppCommands.MoveChainsRelative"/>
        public void MoveChainsRelative(IReadOnlyList<AnimationChainSave> chains, int delta)
        {
            var list = _pm.AnimationChainListSave?.AnimationChains;
            if (list is null || delta == 0 || chains.Count == 0) return;

            var indices = chains
                .Select(c => list.IndexOf(c))
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return;

            // Rigid group: if shifting either edge by delta would cross a boundary, no-op.
            if (indices[0] + delta < 0) return;
            if (indices[^1] + delta > list.Count - 1) return;

            var selected = new HashSet<int>(indices);

            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
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
                    itemName: indices.Count == 1 ? list[indices[0]].Name : null)));
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
            var shapes = frame.ShapesSave?.Shapes;
            if (shapes is null) return;
            int idx    = shapes.IndexOf(shape);
            if (idx < 0) return;
            int newIdx = Math.Clamp(idx + delta, 0, shapes.Count - 1);
            if (newIdx == idx) return;
            _undoManager.Execute(new ReorderCommand<object>(
                shapes,
                () => { shapes.RemoveAt(idx); shapes.Insert(newIdx, shape); },
                this, _events, () => RefreshTreeNode(frame),
                delta > 0
                    ? $"Move {ShapeReorderLabel(shape)} Down"
                    : $"Move {ShapeReorderLabel(shape)} Up"));
        }

        public void MoveShapeToTop(object shape, AnimationFrameSave frame)
        {
            var shapes = frame.ShapesSave?.Shapes;
            if (shapes is null || !shapes.Contains(shape)) return;
            _undoManager.Execute(new ReorderCommand<object>(
                shapes,
                () => { shapes.Remove(shape); shapes.Insert(0, shape); },
                this, _events, () => RefreshTreeNode(frame),
                $"Move {ShapeReorderLabel(shape)} to Top"));
        }

        public void MoveShapeToBottom(object shape, AnimationFrameSave frame)
        {
            var shapes = frame.ShapesSave?.Shapes;
            if (shapes is null || !shapes.Contains(shape)) return;
            _undoManager.Execute(new ReorderCommand<object>(
                shapes,
                () => { shapes.Remove(shape); shapes.Add(shape); },
                this, _events, () => RefreshTreeNode(frame),
                $"Move {ShapeReorderLabel(shape)} to Bottom"));
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
            var rect   = _selectedState.SelectedRectangle;
            var circle = _selectedState.SelectedCircle;
            var frame  = _selectedState.SelectedFrame;
            var chain  = _selectedState.SelectedChain;

            if (rect is not null)
            {
                var ownerFrame = _objectFinder.GetAnimationFrameContaining(rect);
                if (ownerFrame is not null) MoveShape(rect, ownerFrame, delta);
            }
            else if (circle is not null)
            {
                var ownerFrame = _objectFinder.GetAnimationFrameContaining(circle);
                if (ownerFrame is not null) MoveShape(circle, ownerFrame, delta);
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

        public void SetFrameFlip(
            IReadOnlyList<AnimationFrameSave> frames, bool? flipHorizontal, bool? flipVertical, bool? flipDiagonal = null)
        {
            // Absolute set, not toggle: only the frames whose flag actually differs from the target
            // get flipped (and their offset/shapes mirrored), so a frame already at the target state
            // is untouched. Reuses FlipCommand's toggle for exactly those frames, grouped into one
            // undo step via CompositeCommand.
            var commands = new List<IUndoableCommand>();

            if (flipHorizontal.HasValue)
            {
                var toFlip = frames.Where(f => f.FlipHorizontal != flipHorizontal.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Horizontal, this, _events, RefreshWireframe));
            }
            if (flipVertical.HasValue)
            {
                var toFlip = frames.Where(f => f.FlipVertical != flipVertical.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Vertical, this, _events, RefreshWireframe));
            }
            if (flipDiagonal.HasValue)
            {
                var toFlip = frames.Where(f => f.FlipDiagonal != flipDiagonal.Value).ToArray();
                if (toFlip.Length > 0)
                    commands.Add(new FlipCommand(toFlip, FlipAxis.Diagonal, this, _events, RefreshWireframe));
            }

            if (commands.Count == 0) return;
            _undoManager.Execute(new CompositeCommand(commands, "Set Flip"));
        }

        public void FlipChainHorizontally(AnimationChainSave chain)
        {
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), FlipAxis.Horizontal, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void FlipChainVertically(AnimationChainSave chain)
        {
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), FlipAxis.Vertical, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void InvertFrameOrder(AnimationChainSave chain)
        {
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => chain.Frames.Reverse(),
                this, _events, () => RefreshTreeNode(chain),
                $"Invert Frame Order in '{chain.Name}'"));
        }

        public void SetAllFrameLengths(AnimationChainSave chain, float frameLength)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => { foreach (var frame in chain.Frames) frame.FrameLength = frameLength; },
                this, _events, refreshWireframe: false,
                "Set All Frame Lengths"));
        }

        public AnimationChainSave? DuplicateChain(
            AnimationChainSave source,
            bool   flipH    = false,
            bool   flipV    = false,
            string? newName = null)
        {
            var copies = DuplicateChains(new[] { source }, flipH, flipV);
            if (copies.Count == 0) return null;

            var copy = copies[0];
            if (newName is not null) copy.Name = newName;
            return copy;
        }

        // Clones a chain and, when requested, mirrors every frame and shape horizontally
        // and/or vertically. Shared by DuplicateChain (single) and DuplicateChains (batch)
        // so right-click Duplicate's Original/Flip Horizontal/Flip Vertical submenu items
        // behave identically whether one or many chains are selected.
        private static AnimationChainSave CloneChainWithFlip(AnimationChainSave source, bool flipH, bool flipV)
        {
            var copy = new AnimationChainSave { Name = source.Name };
            foreach (var frame in source.Frames)
            {
                var fCopy = AnimationCloneHelper.CloneFrame(frame);
                fCopy.FlipHorizontal = flipH ? !frame.FlipHorizontal : frame.FlipHorizontal;
                fCopy.FlipVertical   = flipV ? !frame.FlipVertical   : frame.FlipVertical;
                // Mirror the sprite offset about the entity origin so an off-center frame stays
                // aligned with its (also-mirrored) shapes after the flip.
                fCopy.RelativeX = flipH ? -frame.RelativeX : frame.RelativeX;
                fCopy.RelativeY = flipV ? -frame.RelativeY : frame.RelativeY;
                if (fCopy.ShapesSave is not null)
                    foreach (var shape in fCopy.ShapesSave.Shapes)
                        ShapeFlip.Mirror(shape, flipH, flipV);
                copy.Frames.Add(fCopy);
            }
            return copy;
        }

        public AnimationFrameSave? DuplicateFrame(AnimationFrameSave source, AnimationChainSave chain)
        {
            if (!chain.Frames.Contains(source)) return null;
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
            IReadOnlyList<AnimationChainSave> sources, bool flipH = false, bool flipV = false)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null || sources.Count == 0) return Array.Empty<AnimationChainSave>();

            var ordered = sources
                .OrderBy(c => acls.AnimationChains.IndexOf(c))
                .ToList();
            var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
            var items = new List<(AnimationChainSave Source, AnimationChainSave Copy)>();
            foreach (var source in ordered)
            {
                var copy = CloneChainWithFlip(source, flipH, flipV);
                copy.Name = StringFunctions.MakeStringUnique(source.Name + "Copy", existingNames, 2);
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
                .Where(x => x.Chain is not null)
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
            var frame = sources[0] switch
            {
                AARectSave r => _objectFinder.GetAnimationFrameContaining(r),
                CircleSave c => _objectFinder.GetAnimationFrameContaining(c),
                _ => null,
            };
            if (frame is null) return Array.Empty<object>();

            frame.ShapesSave ??= new ShapesSave();
            var existingNames = GetShapeNames(frame);
            var copies = new List<object>();
            foreach (var source in sources)
            {
                if (AnimationCloneHelper.CloneShape(source) is not { } copy) continue;
                switch (copy)
                {
                    case AARectSave r:
                        r.Name = StringFunctions.MakeStringUnique(r.Name, existingNames, 2);
                        existingNames.Add(r.Name);
                        copies.Add(r);
                        break;
                    case CircleSave c:
                        c.Name = StringFunctions.MakeStringUnique(c.Name, existingNames, 2);
                        existingNames.Add(c.Name);
                        copies.Add(c);
                        break;
                }
            }
            if (copies.Count == 0) return Array.Empty<object>();
            _undoManager.Execute(new DuplicateShapesCommand(frame, copies, this, _events, _selectedState));
            return copies;
        }

        private static List<string> GetShapeNames(AnimationFrameSave frame) =>
            frame.ShapesSave!.AARectSaves.Select(r => r.Name)
                .Concat(frame.ShapesSave.CircleSaves.Select(c => c.Name))
                .ToList();

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
        /// Requires the texture height for each frame; callers supply a resolver
        /// delegate so this method stays free of rendering code.
        /// </summary>
        /// <param name="chain">Chain whose frames will be adjusted.</param>
        /// <param name="getTextureHeight">
        ///     Returns the texture height in pixels for a given frame,
        ///     or <c>null</c> when no texture is loaded.
        /// </param>
        /// <param name="offsetMultiplier">Preview offset multiplier (PL12), default 1.0.</param>
        public void AdjustOffsetsJustifyBottom(
            AnimationChainSave chain,
            Func<AnimationFrameSave, float?> getTextureHeight,
            float offsetMultiplier = 1f)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () =>
                {
                    foreach (var frame in chain.Frames)
                    {
                        var height = getTextureHeight(frame);
                        if (height.HasValue)
                            AdjustOffsetCalculator.ApplyJustifyBottom([frame], height.Value, offsetMultiplier);
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
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => AdjustOffsetCalculator.ApplyAdjustAll(chain.Frames, deltaX, deltaY, relative),
                this, _events, refreshWireframe: true,
                "Adjust Offsets"));
        }

        // ── A17: Scale Frame Times ────────────────────────────────────────────

        /// <summary>
        /// Scales all frame lengths so the total duration of <paramref name="chain"/>
        /// equals <paramref name="targetTotalDuration"/>, keeping ratios proportional.
        /// </summary>
        public void ScaleFrameTimesProportional(
            AnimationChainSave chain,
            float targetTotalDuration)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => FrameTimeScaler.ApplyKeepProportional(chain.Frames, targetTotalDuration),
                this, _events, refreshWireframe: false,
                "Scale Frame Times"));
        }

        /// <summary>
        /// Sets every frame in <paramref name="chain"/> to the same duration:
        /// <paramref name="targetTotalDuration"/> / frame count.
        /// </summary>
        public void ScaleFrameTimesSetAllSame(
            AnimationChainSave chain,
            float targetTotalDuration)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                chain.Frames,
                () => FrameTimeScaler.ApplySetAllSame(chain.Frames, targetTotalDuration),
                this, _events, refreshWireframe: false,
                "Scale Frame Times"));
        }

        // ── F12: Add Multiple Frames ──────────────────────────────────────────

        /// <summary>
        /// Adds <paramref name="count"/> new frames to <paramref name="chain"/>,
        /// optionally auto-incrementing UV cells.
        /// Returns <c>true</c> if any frame's auto-incremented UV exceeded texture
        /// bounds (so the UI can warn the user).
        /// </summary>
        public bool AddMultipleFrames(
            AnimationChainSave chain,
            int count,
            bool incrementUV)
        {
            var lastFrame = chain.Frames.Count > 0 ? chain.Frames[^1] : null;
            var result    = BatchFrameBuilder.BuildBatch(lastFrame, count, incrementUV);

            var added = result.Frames.ToArray();
            if (added.Length > 0)
            {
                _undoManager.Execute(new AddFramesCommand(added, chain, this, _events, _selectedState));
            }

            return result.ExceededTextureBounds;
        }

        // ── IO15: Adjust UV after texture resize ──────────────────────────────

        /// <summary>
        /// Adjusts UV coordinates of every frame in the current ACLS that references
        /// <paramref name="absoluteTextureFilePath"/> after it was resized from
        /// (<paramref name="oldWidth"/> × <paramref name="oldHeight"/>) to
        /// (<paramref name="newWidth"/> × <paramref name="newHeight"/>).
        /// Returns the list of frames that were modified.
        /// </summary>
        public List<AnimationFrameSave> AdjustUVAfterResize(
            string absoluteTextureFilePath,
            int oldWidth, int oldHeight,
            int newWidth, int newHeight)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return [];

            var aclsDir = string.IsNullOrEmpty(_pm.FileName)
                ? string.Empty
                : System.IO.Path.GetDirectoryName(_pm.FileName) ?? string.Empty;

            // Snapshot every frame; the command compares before/after and only records
            // when something actually changed, so an all-no-op resize leaves no entry.
            var allFrames = acls.AnimationChains.SelectMany(c => c.Frames).ToList();
            List<AnimationFrameSave> modified = [];

            _undoManager.Execute(new BulkFrameEditCommand(
                allFrames,
                () => modified = TextureResizeAdjuster.AdjustAll(
                    acls, aclsDir, absoluteTextureFilePath,
                    oldWidth, oldHeight, newWidth, newHeight),
                this, _events, refreshWireframe: true,
                "Adjust UV After Resize"));

            return modified;
        }

        // ── IO11: File > New ──────────────────────────────────────────────────

        /// <summary>
        /// Resets the editor to an empty, unsaved state:
        /// creates a fresh <see cref="AnimationChainListSave"/>, clears the file name,
        /// clears all selections, and notifies the UI.
        /// </summary>
        public void NewFile()
        {
            _pm.AnimationChainListSave = new AnimationChainListSave();
            _pm.FileName = string.Empty;
            _pm.OnDiskCoordinateType = FlatRedBall2.Animation.Content.TextureCoordinateType.Pixel;
            _selectedState.SelectedChain = null;
            _selectedState.SelectedFrame = null;
            _undoManager.Clear();
            RefreshTreeViewRequested?.Invoke();
            _events.RaiseAnimationChainsChanged();
        }

        // ── WF09: Create frame from magic-wand pixel bounds ───────────────────

        /// <summary>
        /// Creates a new <see cref="AnimationFrameSave"/> whose UV coordinates correspond
        /// to the pixel bounding box returned by the flood-fill (magic wand) tool, then
        /// appends it to <paramref name="chain"/> and selects it.
        /// </summary>
        /// <param name="chain">Chain to add the frame to.</param>
        /// <param name="textureName">Relative texture path (stored as-is in the frame).</param>
        /// <param name="minX">Left pixel bound of the region (inclusive).</param>
        /// <param name="minY">Top pixel bound of the region (inclusive).</param>
        /// <param name="maxX">Right pixel bound of the region (exclusive).</param>
        /// <param name="maxY">Bottom pixel bound of the region (exclusive).</param>
        /// <param name="bitmapWidth">Full width of the texture in pixels.</param>
        /// <param name="bitmapHeight">Full height of the texture in pixels.</param>
        public void AddFrameFromPixelBounds(
            AnimationChainSave chain,
            string textureName,
            int minX, int minY, int maxX, int maxY,
            int bitmapWidth, int bitmapHeight)
        {
            var frame = new AnimationFrameSave
            {
                TextureName         = textureName,
                LeftCoordinate      = minX / (float)bitmapWidth,
                RightCoordinate     = maxX / (float)bitmapWidth,
                TopCoordinate       = minY / (float)bitmapHeight,
                BottomCoordinate    = maxY / (float)bitmapHeight,
                FrameLength         = 0.1f,
                ShapesSave = new FlatRedBall2.Animation.Content.ShapesSave()
            };

            _undoManager.Execute(new AddFrameCommand(frame, chain, this, _events, _selectedState));
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
            if (frame == null) return;
            _undoManager.Execute(new SetFrameTextureNameCommand(frame, frame.TextureName, textureName, this, _events));
        }

        public void SetAllFramesTextureName(AnimationChainSave chain, string? textureName)
        {
            if (chain.Frames.Count == 0) return;
            var cmds = chain.Frames
                .Select(f => (IUndoableCommand)new SetFrameTextureNameCommand(f, f.TextureName, textureName, this, _events))
                .ToArray();
            _undoManager.Execute(new CompositeCommand(cmds, "Set All Frame Textures"));
        }

        public void SetFrameLength(IReadOnlyList<AnimationFrameSave> frames, float newLength)
        {
            var desc = $"Set Length: {newLength:0.###}s";
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () => { foreach (var f in frames) f.FrameLength = newLength; },
                this, _events, false, desc));
        }

        public void SetFrameRelative(IReadOnlyList<AnimationFrameSave> frames, float? newRelX, float? newRelY)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () =>
                {
                    foreach (var f in frames)
                    {
                        if (newRelX.HasValue) f.RelativeX = newRelX.Value;
                        if (newRelY.HasValue) f.RelativeY = newRelY.Value;
                    }
                },
                this, _events, true, "Set Offset"));
        }

        public void SetFrameColor(IReadOnlyList<AnimationFrameSave> frames, int? red, int? green, int? blue)
        {
            // Color tints the preview and the timeline/tree thumbnails but not the wireframe, so no
            // wireframe refresh is needed. The AnimationChainsChanged raised here rebuilds those.
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () => { foreach (var f in frames) { f.Red = red; f.Green = green; f.Blue = blue; } },
                this, _events, false, "Set Frame Color"));
        }

        public void SetFrameColorOperation(IReadOnlyList<AnimationFrameSave> frames, ColorOperation? operation)
        {
            // Mode drives how the preview + timeline/tree thumbnails tint; it doesn't touch the
            // wireframe, so no wireframe refresh is needed.
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () => { foreach (var f in frames) f.ColorOperation = operation; },
                this, _events, false, "Set Frame Color Mode"));
        }

        public void SetFrameAlpha(IReadOnlyList<AnimationFrameSave> frames, int? alpha)
        {
            // Alpha is straight transparency; it fades the preview + timeline/tree thumbnails but not
            // the wireframe, so no wireframe refresh is needed.
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () => { foreach (var f in frames) f.Alpha = alpha; },
                this, _events, false, "Set Frame Alpha"));
        }

        public void SetFramePixelRegion(IReadOnlyList<AnimationFrameSave> frames,
            int? pixelX, int? pixelY, int? pixelW, int? pixelH, int bmpW, int bmpH)
        {
            _undoManager.Execute(new BulkFrameEditCommand(
                frames, () =>
                {
                    foreach (var f in frames)
                    {
                        // Order matters: SetX/SetY preserve each frame's own current width/height, so
                        // they must run before SetWidth/SetHeight overwrite Right/Bottom using the
                        // (possibly just-moved) Left/Top.
                        if (pixelX.HasValue) PixelFrameEditor.SetX(f, pixelX.Value, bmpW);
                        if (pixelY.HasValue) PixelFrameEditor.SetY(f, pixelY.Value, bmpH);
                        if (pixelW.HasValue) PixelFrameEditor.SetWidth(f, pixelW.Value, bmpW);
                        if (pixelH.HasValue) PixelFrameEditor.SetHeight(f, pixelH.Value, bmpH);
                    }
                },
                this, _events, true, "Set Region"));
        }

        public void SetRectProps(AnimationFrameSave? frame, AARectSave rect,
            string name, float x, float y, float scaleX, float scaleY) =>
            _undoManager.Execute(SetShapePropsCommand.ForRect(frame, rect, name, x, y, scaleX, scaleY, this, _events));

        public void SetCircleProps(AnimationFrameSave? frame, CircleSave circ,
            string name, float x, float y, float radius) =>
            _undoManager.Execute(SetShapePropsCommand.ForCircle(frame, circ, name, x, y, radius, this, _events));

        // ── Paste (clipboard → project) ───────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.PasteChains"/>
        public void PasteChains(IReadOnlyList<AnimationChainSave> chains)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return;
            _undoManager.Execute(new PasteChainsCommand(acls, chains, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.PasteFrames"/>
        public void PasteFrames(AnimationChainSave chain, IReadOnlyList<AnimationFrameSave> frames,
            int? insertIndex = null)
        {
            var clones = frames.Select(AnimationCloneHelper.CloneFrame).ToArray();
            _undoManager.Execute(new AddFramesCommand(clones, chain, this, _events, _selectedState, insertIndex));
        }

        /// <inheritdoc cref="IAppCommands.PasteCircle"/>
        public void PasteCircle(AnimationFrameSave frame, CircleSave circle) =>
            PasteShapes(frame, Array.Empty<AARectSave>(), new[] { circle });

        /// <inheritdoc cref="IAppCommands.PasteRectangle"/>
        public void PasteRectangle(AnimationFrameSave frame, AARectSave rectangle) =>
            PasteShapes(frame, new[] { rectangle }, Array.Empty<CircleSave>());

        /// <inheritdoc cref="IAppCommands.PasteShapes"/>
        public void PasteShapes(AnimationFrameSave frame, IReadOnlyList<AARectSave> rectangles,
            IReadOnlyList<CircleSave> circles)
        {
            var clones = BuildShapeClones(frame, rectangles, circles);
            if (clones.Count == 0) return;
            _undoManager.Execute(new PasteShapesCommand(frame, clones, this, _events, _selectedState));
        }

        /// <inheritdoc cref="IAppCommands.PasteChainsCut"/>
        public void PasteChainsCut(IReadOnlyList<AnimationChainSave> chains,
            IReadOnlyList<AnimationChainSave> sourcesToRemove)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null || chains.Count == 0) return;

            var cmds = new List<IUndoableCommand>
            {
                new PasteChainsCommand(acls, chains, this, _events, _selectedState),
                new DeleteChainsCommand(sourcesToRemove, acls, this, _events, _selectedState),
            };
            string desc = chains.Count == 1
                ? $"Cut Animation '{(sourcesToRemove.Count > 0 ? sourcesToRemove[0].Name : chains[0].Name)}'"
                : $"Cut {chains.Count} Animations";
            _undoManager.Execute(new CompositeCommand(cmds, desc));
        }

        /// <inheritdoc cref="IAppCommands.PasteFramesCut"/>
        public void PasteFramesCut(AnimationChainSave targetChain,
            IReadOnlyList<AnimationFrameSave> frames, int? insertIndex,
            IReadOnlyList<AnimationFrameSave> sourcesToRemove)
        {
            if (frames.Count == 0) return;
            var clones = frames.Select(AnimationCloneHelper.CloneFrame).ToArray();
            var cmds = new List<IUndoableCommand>
            {
                new AddFramesCommand(clones, targetChain, this, _events, _selectedState, insertIndex),
            };
            foreach (var group in sourcesToRemove
                         .Select(f => (Frame: f, Chain: _objectFinder.GetAnimationChainContaining(f)))
                         .Where(x => x.Chain is not null)
                         .GroupBy(x => x.Chain!))
            {
                cmds.Add(new DeleteFramesCommand(
                    group.Select(x => x.Frame).ToList(), group.Key, this, _events, _selectedState));
            }
            string desc;
            if (frames.Count == 1)
            {
                var sourceChain = sourcesToRemove.Count > 0
                    ? _objectFinder.GetAnimationChainContaining(sourcesToRemove[0])
                    : null;
                desc = $"Cut Frame in '{(sourceChain ?? targetChain).Name}'";
            }
            else
            {
                desc = $"Cut {frames.Count} Frames";
            }
            _undoManager.Execute(new CompositeCommand(cmds, desc));
        }

        /// <inheritdoc cref="IAppCommands.PasteShapesCut"/>
        public void PasteShapesCut(AnimationFrameSave targetFrame,
            IReadOnlyList<AARectSave> rectangles, IReadOnlyList<CircleSave> circles,
            IReadOnlyList<object> sourcesToRemove, AnimationFrameSave sourceFrame)
        {
            var clones = BuildShapeClones(targetFrame, rectangles, circles);
            if (clones.Count == 0) return;

            var deleteCmds = new List<IUndoableCommand>();
            foreach (var shape in sourcesToRemove)
            {
                switch (shape)
                {
                    case AARectSave r:
                        deleteCmds.Add(new DeleteAxisAlignedRectangleCommand(
                            r, sourceFrame, this, _events, _selectedState));
                        break;
                    case CircleSave c:
                        deleteCmds.Add(new DeleteCircleCommand(
                            c, sourceFrame, this, _events, _selectedState));
                        break;
                }
            }
            if (deleteCmds.Count == 0) return;

            var cmds = new List<IUndoableCommand>
            {
                new PasteShapesCommand(targetFrame, clones, this, _events, _selectedState),
            };
            cmds.AddRange(deleteCmds);
            string desc = clones.Count == 1
                ? $"Cut {ShapeUndoLabel.Format(sourcesToRemove.Count > 0 ? sourcesToRemove[0] : clones[0])}"
                : $"Cut {clones.Count} Shapes";
            _undoManager.Execute(new CompositeCommand(cmds, desc));
        }

        private List<object> BuildShapeClones(AnimationFrameSave frame,
            IReadOnlyList<AARectSave> rectangles, IReadOnlyList<CircleSave> circles)
        {
            frame.ShapesSave ??= new ShapesSave();
            var existingNames = GetShapeNames(frame);
            var clones = new List<object>();
            foreach (var rect in rectangles)
            {
                if (AnimationCloneHelper.CloneShape(rect) is not AARectSave copy) continue;
                copy.Name = StringFunctions.MakeStringUnique(copy.Name, existingNames, 2);
                existingNames.Add(copy.Name);
                clones.Add(copy);
            }
            foreach (var circle in circles)
            {
                if (AnimationCloneHelper.CloneShape(circle) is not CircleSave copy) continue;
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
                DoOnUiThread(() => ReloadAchxFromDisk(path));

            HotReloadWatcher.PngChangedOnDisk += path =>
                DoOnUiThread(() => ReloadPngFromDisk(path));

            HotReloadWatcher.AchxDeletedOnDisk += path =>
                DoOnUiThread(() => _events.RaiseAchxDeletedOnDisk(path));
        }

        /// <inheritdoc cref="IAppCommands.ReloadAchxFromDisk"/>
        public void ReloadAchxFromDisk(string path)
        {
            // Capture selection state before reload
            string? selectedChainName = _selectedState.SelectedChain?.Name;
            int? selectedFrameIndex = _selectedState.SelectedFrame != null
                ? _selectedState.SelectedChain?.Frames.IndexOf(_selectedState.SelectedFrame)
                : null;

            try
            {
                _pm.LoadAnimationChain(new AnimationEditor.Core.Paths.FilePath(path));
            }
            catch (Exception ex)
            {
                HotReloadFailed?.Invoke(path, ex.Message);
                return;
            }

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
        }

        private IEnumerable<string> GetReferencedAbsolutePngPaths(string _, string achxDir)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
