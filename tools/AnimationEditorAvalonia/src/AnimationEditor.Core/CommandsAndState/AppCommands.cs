using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
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
        public event Action? RebuildTreeViewRequested;

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

        public event Action<string>? FramesDeleted;

        /// <summary>
        /// Fired after <see cref="SaveCurrentAnimationChainListAsync"/> successfully saves a file.
        /// The argument is the full path of the saved file.
        /// </summary>
        public event Action<string>? SaveAsCompleted;

        /// <inheritdoc cref="IAppCommands.LoadFailed"/>
        public event Action<string, Exception>? LoadFailed;

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
            try { LoadAnimationChain(path); }
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

            _undoManager.Clear();
            _undoManager.MarkSaved();
            _selectedState.Reset();
            _selectedState.SelectedChain = _pm.AnimationChainListSave?.AnimationChains.FirstOrDefault();
            // Rebuild (not refresh): a freshly-opened file should present a collapsed,
            // scannable overview rather than every chain's frames expanded.
            RebuildTreeViewRequested?.Invoke();
            _ioManager.LoadAndApplyCompanionFileFor(fileName);
            RefreshWireframeRequested?.Invoke();
            RefreshAnimationFrameDisplayRequested?.Invoke();
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
                try
                {
                    _pm.SaveAnimationChainList(target);
                    _undoManager.MarkSaved();
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
            _ioManager.DeleteRecoveryFile();
            SaveAsCompleted?.Invoke(path);
            _events.RaiseCurrentFileChanged(path);
        }

        public void DeleteAnimationChains(List<AnimationChainSave> animationChains)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls == null) return;

            _undoManager.Execute(new DeleteChainsCommand(animationChains, acls, this, _events));
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
            _undoManager.Execute(new AddAxisAlignedRectangleCommand(rectangleSave, frame, this, _events));
            _selectedState.SelectedRectangle = rectangleSave;
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
            _undoManager.Execute(new AddCircleCommand(circleSave, frame, this, _events));
            _selectedState.SelectedCircle = circleSave;
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
            _undoManager.Execute(new DeleteCircleCommand(circle, owner, this, _events));
        }

        public void DeleteAxisAlignedRectangle(AARectSave rectangle, AnimationFrameSave owner)
        {
            _undoManager.Execute(new DeleteAxisAlignedRectangleCommand(rectangle, owner, this, _events));
        }

        public async Task AskToDeleteRectangles(List<AARectSave> rectangles)
        {
            var message = "Delete the following rectangle(s)?\n\n" +
                string.Join("\n", rectangles.Select(r => r.Name));

            if (await ConfirmAsync(message, "Delete?"))
            {
                // One composite entry so deleting several shapes is a single undo step.
                var commands = new List<IUndoableCommand>();
                foreach (var rectangle in rectangles.ToArray())
                {
                    var frame = _objectFinder.GetAnimationFrameContaining(rectangle);
                    if (frame != null)
                        commands.Add(new DeleteAxisAlignedRectangleCommand(rectangle, frame, this, _events));
                }
                if (commands.Count > 0)
                {
                    var desc = rectangles.Count == 1
                        ? $"Delete Rectangle '{rectangles[0].Name}'"
                        : $"Delete {rectangles.Count} Rectangles";
                    _undoManager.Execute(new CompositeCommand(commands, desc));
                }
            }
        }

        public async Task AskToDeleteCircles(List<CircleSave> circles)
        {
            var message = "Delete the following circle(s)?\n\n" +
                string.Join("\n", circles.Select(c => c.Name));

            if (await ConfirmAsync(message, "Delete?"))
            {
                // One composite entry so deleting several shapes is a single undo step.
                var commands = new List<IUndoableCommand>();
                foreach (var circle in circles.ToArray())
                {
                    var frame = _objectFinder.GetAnimationFrameContaining(circle);
                    if (frame != null)
                        commands.Add(new DeleteCircleCommand(circle, frame, this, _events));
                }
                if (commands.Count > 0)
                {
                    var desc = circles.Count == 1
                        ? $"Delete Circle '{circles[0].Name}'"
                        : $"Delete {circles.Count} Circles";
                    _undoManager.Execute(new CompositeCommand(commands, desc));
                }
            }
        }

        public async Task AskToDeleteAnimationChains(List<AnimationChainSave> animationChains)
        {
            var message = "Delete the following animation(s)?\n\n" +
                string.Join("\n", animationChains.Select(c => c.Name));

            if (await ConfirmAsync(message, "Delete?"))
            {
                DeleteAnimationChains(animationChains);
            }
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
                _undoManager.Execute(new DeleteFramesCommand(frames, chain, this, _events));
                if (validFrames.Count > 0)
                    FramesDeleted?.Invoke(label);
            }

            RefreshWireframeRequested?.Invoke();
            _events.RaiseAnimationChainsChanged();
        }

        private List<string> GetSelectedFrameShapeNames()
        {
            var frame = _selectedState.SelectedFrame;
            if (frame?.ShapesSave == null) return new List<string>();

            return frame.ShapesSave!.AARectSaves
                .Select(r => r.Name)
                .Concat(frame.ShapesSave!.CircleSaves.Select(c => c.Name))
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
            _undoManager.Execute(new AddChainCommand(chain, acls, this, _events));
            _selectedState.SelectedChain = chain;
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
            var frame = new AnimationFrameSave
            {
                TextureName  = textureName ?? string.Empty,
                LeftCoordinate   = 0f,
                RightCoordinate  = 1f,
                TopCoordinate    = 0f,
                BottomCoordinate = 1f,
                FrameLength      = 0.1f,
                ShapesSave = new FlatRedBall2.Animation.Content.ShapesSave()
            };
            _undoManager.Execute(new AddFrameCommand(frame, chain, this, _events));
            _selectedState.SelectedFrame = frame;
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
                delta > 0 ? "Move Animation Down" : "Move Animation Up"));
        }

        public void MoveChainToTop(AnimationChainSave chain)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.Remove(chain); chains.Insert(0, chain); },
                this, _events, RefreshTreeView,
                "Move Animation to Top"));
        }

        public void MoveChainToBottom(AnimationChainSave chain)
        {
            var chains = _pm.AnimationChainListSave?.AnimationChains;
            if (chains is null) return;
            _undoManager.Execute(new ReorderCommand<AnimationChainSave>(
                chains,
                () => { chains.Remove(chain); chains.Add(chain); },
                this, _events, RefreshTreeView,
                "Move Animation to Bottom"));
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
                delta > 0 ? "Move Frame Down" : "Move Frame Up"));
        }

        public void MoveFrameToTop(AnimationFrameSave frame, AnimationChainSave chain)
        {
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.Remove(frame); chain.Frames.Insert(0, frame); },
                this, _events, () => RefreshTreeNode(chain),
                "Move Frame to Top"));
        }

        public void MoveFrameToBottom(AnimationFrameSave frame, AnimationChainSave chain)
        {
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => { chain.Frames.Remove(frame); chain.Frames.Add(frame); },
                this, _events, () => RefreshTreeNode(chain),
                "Move Frame to Bottom"));
        }

        /// <summary>
        /// Moves the currently-selected frame or chain up (<paramref name="delta"/> = -1)
        /// or down (<paramref name="delta"/> = +1) in the tree.
        /// Frame selection takes priority: if a frame is selected its parent chain is used.
        /// No-op when nothing is selected or when the item is already at the boundary.
        /// </summary>
        public void HandleReorder(int delta)
        {
            var frame = _selectedState.SelectedFrame;
            var chain = _selectedState.SelectedChain;

            if (frame is not null && chain is not null)
                MoveFrame(frame, chain, delta);
            else if (chain is not null)
                MoveChain(chain, delta);
        }

        public void FlipFrameHorizontally(AnimationFrameSave frame)
        {
            _undoManager.Execute(new FlipCommand(
                new[] { frame }, horizontal: true, this, _events, RefreshWireframe));
        }

        public void FlipFrameVertically(AnimationFrameSave frame)
        {
            _undoManager.Execute(new FlipCommand(
                new[] { frame }, horizontal: false, this, _events, RefreshWireframe));
        }

        public void FlipChainHorizontally(AnimationChainSave chain)
        {
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), horizontal: true, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void FlipChainVertically(AnimationChainSave chain)
        {
            _undoManager.Execute(new FlipCommand(
                chain.Frames.ToArray(), horizontal: false, this, _events,
                () => { RefreshTreeNode(chain); RefreshWireframe(); }));
        }

        public void InvertFrameOrder(AnimationChainSave chain)
        {
            _undoManager.Execute(new ReorderCommand<AnimationFrameSave>(
                chain.Frames,
                () => chain.Frames.Reverse(),
                this, _events, () => RefreshTreeNode(chain),
                "Invert Frame Order"));
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
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return null;

            var copyName = newName ?? StringFunctions.MakeStringUnique(
                source.Name + "Copy",
                acls.AnimationChains.Select(c => c.Name).ToList());

            var copy = new AnimationChainSave { Name = copyName };
            foreach (var frame in source.Frames)
            {
                var fCopy = new AnimationFrameSave
                {
                    TextureName      = frame.TextureName,
                    LeftCoordinate   = frame.LeftCoordinate,
                    RightCoordinate  = frame.RightCoordinate,
                    TopCoordinate    = frame.TopCoordinate,
                    BottomCoordinate = frame.BottomCoordinate,
                    FrameLength      = frame.FrameLength,
                    FlipHorizontal   = flipH ? !frame.FlipHorizontal : frame.FlipHorizontal,
                    FlipVertical     = flipV ? !frame.FlipVertical   : frame.FlipVertical,
                    RelativeX        = frame.RelativeX,
                    RelativeY        = frame.RelativeY,
                    ShapesSave = new FlatRedBall2.Animation.Content.ShapesSave()
                };
                if (frame.ShapesSave != null)
                {
                    foreach (var r in frame.ShapesSave!.AARectSaves)
                        fCopy.ShapesSave!.AARectSaves.Add(
                            new AARectSave
                            { Name = r.Name, X = r.X, Y = r.Y, ScaleX = r.ScaleX, ScaleY = r.ScaleY });
                    foreach (var c in frame.ShapesSave!.CircleSaves)
                        fCopy.ShapesSave!.CircleSaves.Add(
                            new CircleSave
                            { Name = c.Name, X = c.X, Y = c.Y, Radius = c.Radius });
                }
                copy.Frames.Add(fCopy);
            }

            _undoManager.Execute(new AddChainCommand(copy, acls, this, _events));
            _selectedState.SelectedChain = copy;
            return copy;
        }

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
                _undoManager.Execute(new AddFramesCommand(added, chain, this, _events));
                _selectedState.SelectedFrame = added[^1];
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

            _undoManager.Execute(new AddFrameCommand(frame, chain, this, _events));
            _selectedState.SelectedFrame = frame;
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

        // ── Paste (clipboard → project) ───────────────────────────────────────

        /// <inheritdoc cref="IAppCommands.PasteChains"/>
        public void PasteChains(IReadOnlyList<AnimationChainSave> chains)
        {
            var acls = _pm.AnimationChainListSave;
            if (acls is null) return;
            _undoManager.Execute(new PasteChainsCommand(acls, chains, this, _events));
        }

        /// <inheritdoc cref="IAppCommands.PasteFrames"/>
        public void PasteFrames(AnimationChainSave chain, IReadOnlyList<AnimationFrameSave> frames) =>
            _undoManager.Execute(new AddFramesCommand(frames.ToArray(), chain, this, _events));

        /// <inheritdoc cref="IAppCommands.PasteRectangle"/>
        public void PasteRectangle(AnimationFrameSave frame, AARectSave rectangle) =>
            _undoManager.Execute(new AddAxisAlignedRectangleCommand(rectangle, frame, this, _events));

        /// <inheritdoc cref="IAppCommands.PasteCircle"/>
        public void PasteCircle(AnimationFrameSave frame, CircleSave circle) =>
            _undoManager.Execute(new AddCircleCommand(circle, frame, this, _events));
    }
}
