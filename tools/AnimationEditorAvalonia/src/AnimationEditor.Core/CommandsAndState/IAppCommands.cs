using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IAppCommands
    {
        // ── Delegates wired by the app layer ──────────────────────────────────

        Action<Action> DoOnUiThread { get; set; }
        Func<string, string, Task<bool>> ConfirmAsync { get; set; }
        Func<string, string, string, Task<string?>> PromptStringAsync { get; set; }
        IFileDialogService FileDialogService { get; set; }

        // ── Events ────────────────────────────────────────────────────────────

        event Action RefreshTreeViewRequested;

        /// <summary>
        /// Request a full tree-view rebuild from scratch with every chain collapsed.
        /// Raised on .achx load (File &gt; Open, recent files, drag-drop, startup reopen).
        /// Differs from <see cref="RefreshTreeViewRequested"/>, which diff-updates the
        /// existing tree and preserves each chain's collapse state across edits.
        /// </summary>
        event Action RebuildTreeViewRequested;

        event Action<AnimationChainSave> RefreshChainNodeRequested;
        event Action<AnimationFrameSave> RefreshFrameNodeRequested;
        event Action RefreshAnimationFrameDisplayRequested;
        event Action RefreshWireframeRequested;
        event Action<string>? SaveAsCompleted;

        /// <summary>
        /// Raised after <see cref="ExportToPixiJsAsync"/> writes a PixiJS spritesheet JSON. The first
        /// argument is the export path; the second is a (possibly empty) list of non-fatal warnings
        /// (e.g. dropped per-frame duration, multiple source textures) for the app layer to surface.
        /// </summary>
        event Action<string, IReadOnlyList<string>>? PixiJsExportCompleted;

        /// <summary>
        /// Raised after a frame, shape, or animation chain is deleted, carrying a short
        /// label for the deleted item(s). The app layer shows an undo toast in response.
        /// </summary>
        event Action<string>? ItemsDeleted;

        /// <summary>
        /// Fired when <see cref="LoadAnimationChain"/> fails — file not found, corrupt XML,
        /// or any other engine-side throw. The first argument is the attempted file path;
        /// the second is the exception. <c>RefreshTreeViewRequested</c> is NOT fired when
        /// this event fires; project state is left unchanged.
        /// </summary>
        event Action<string, Exception>? LoadFailed;

        /// <summary>
        /// Fired when <see cref="ReloadAchxFromDisk"/> detects a mangled file (bad XML,
        /// Git conflict markers, etc.). The first argument is the file path; the second is
        /// a user-readable reason. Project state is left unchanged and the undo stack is
        /// not cleared. Use this to surface a toast rather than a blocking error dialog.
        /// </summary>
        event Action<string, string>? HotReloadFailed;

        // ── Methods ───────────────────────────────────────────────────────────

        /// <summary>
        /// Full open workflow: loads the .achx, fires <c>AchxLoaded</c> (post-load),
        /// then fires <c>CurrentFileChanged</c> and <c>AvailableTexturesChanged</c>
        /// so the UI can update its title, recent-files list, and texture combo.
        /// UV-format files require all referenced textures to be resolvable; missing
        /// textures fire <see cref="LoadFailed"/> and abort the load. UV files with all
        /// textures present prompt via <see cref="AppCommands.ConfirmAsync"/> before converting.
        /// </summary>
        Task OpenAchxWorkflowAsync(string path);
        void LoadAnimationChain(string fileName);

        /// <summary>
        /// Stores the current project model on <paramref name="tab"/> for later tab switches.
        /// </summary>
        void CaptureTabEditorState(TabEntry tab);

        /// <summary>
        /// Swaps the editor to <paramref name="tab"/>'s cached in-memory model when the cache
        /// is fresh. Does not clear or restore undo — the app layer handles that.
        /// </summary>
        /// <returns><c>true</c> when the cache was applied; <c>false</c> when a disk load is needed.</returns>
        bool TryActivateTabFromCache(TabEntry tab);

        /// <summary>
        /// Activates <paramref name="tab"/>'s content from cache when possible; otherwise runs
        /// <see cref="OpenAchxWorkflowAsync"/>. Does not restore undo.
        /// </summary>
        Task ActivateTabContentAsync(TabEntry tab);

        /// <summary>
        /// Raised after the in-memory project model is loaded or saved from disk so the app
        /// layer can refresh per-tab caches. The argument is the affected <c>.achx</c> path,
        /// or <c>null</c> for untitled projects.
        /// </summary>
        event Action<string?>? EditorProjectModelChanged;

        void RefreshTreeNode(AnimationChainSave animationChain);
        void RefreshTreeNode(AnimationFrameSave animationFrame);
        void RefreshAnimationFrameDisplay();
        void RefreshWireframe();
        void RefreshTreeView();
        void SaveCurrentAnimationChainList(string? fileName = null);
        Task SaveCurrentAnimationChainListAsync();
        Task ExportToPixiJsAsync();
        void DeleteAnimationChains(List<AnimationChainSave> animationChains);
        void AddAxisAlignedRectangle(AnimationFrameSave frame);
        void AddCircle(AnimationFrameSave frame);
        void MatchRectangleToFrame(AARectSave rectangle, AnimationFrameSave animationFrame);
        void MatchCircleToFrame(CircleSave circle, AnimationFrameSave animationFrame);
        void DeleteCircle(CircleSave circle, AnimationFrameSave owner);
        void DeleteAxisAlignedRectangle(AARectSave rectangle, AnimationFrameSave owner);
        void DeleteShapes(AnimationFrameSave frame, List<AARectSave> rectangles, List<CircleSave> circles);
        void DeleteFrames(List<AnimationFrameSave> frames);
        Task AddAnimationChain();
        AnimationChainSave? AddAnimationChainWithName(string name);
        bool RenameChain(AnimationChainSave chain, string newName);
        void AddFrame(AnimationChainSave chain, string? textureName = null);
        void MoveChain(AnimationChainSave chain, int delta);
        void MoveChainToTop(AnimationChainSave chain);
        void MoveChainToBottom(AnimationChainSave chain);
        void MoveFrame(AnimationFrameSave frame, AnimationChainSave chain, int delta);
        void MoveFrameToTop(AnimationFrameSave frame, AnimationChainSave chain);
        void MoveFrameToBottom(AnimationFrameSave frame, AnimationChainSave chain);

        /// <summary>
        /// Moves <paramref name="frames"/> (the same instances, not clones) from
        /// <paramref name="sourceChain"/> to <paramref name="targetChain"/> at
        /// <paramref name="insertIndex"/> as one undo step. Drives drag-and-drop reorder
        /// within a chain (source == target) and cross-animation moves. The frames are
        /// sorted by source index and inserted as a contiguous block; <paramref name="insertIndex"/>
        /// is interpreted against the target's current frame list before source removal.
        /// </summary>
        void MoveFrames(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave sourceChain, AnimationChainSave targetChain, int insertIndex);
        void MoveShape(object shape, AnimationFrameSave frame, int delta);
        void MoveShapeToTop(object shape, AnimationFrameSave frame);
        void MoveShapeToBottom(object shape, AnimationFrameSave frame);
        void HandleReorder(int delta);
        void FlipFrameHorizontally(AnimationFrameSave frame);
        void FlipFrameVertically(AnimationFrameSave frame);
        void FlipChainHorizontally(AnimationChainSave chain);
        void FlipChainVertically(AnimationChainSave chain);
        void InvertFrameOrder(AnimationChainSave chain);
        void SetAllFrameLengths(AnimationChainSave chain, float frameLength);
        AnimationChainSave? DuplicateChain(AnimationChainSave source, bool flipH = false, bool flipV = false, string? newName = null);

        /// <summary>
        /// Deep-copies <paramref name="source"/> and inserts the copy immediately after it
        /// in <paramref name="chain"/>. All UV coordinates, timing, flip flags, relative offsets,
        /// and attached shapes are copied. Undoable.
        /// </summary>
        AnimationFrameSave? DuplicateFrame(AnimationFrameSave source, AnimationChainSave chain);

        /// <summary>
        /// Deep-copies a shape (<see cref="AARectSave"/> or <see cref="CircleSave"/>) into the
        /// frame that contains it, with a unique name, and selects the copy. Returns the copy,
        /// or <c>null</c> if the shape isn't in any frame or its kind isn't duplicable. Undoable.
        /// </summary>
        object? DuplicateShape(object source);
        void SortAnimationsAlphabetically();
        void AdjustOffsetsJustifyBottom(AnimationChainSave chain, Func<AnimationFrameSave, float?> getTextureHeight, float offsetMultiplier = 1f);
        void AdjustOffsetsAdjustAll(AnimationChainSave chain, float? deltaX, float? deltaY, bool relative);
        void ScaleFrameTimesProportional(AnimationChainSave chain, float targetTotalDuration);
        void ScaleFrameTimesSetAllSame(AnimationChainSave chain, float targetTotalDuration);
        bool AddMultipleFrames(AnimationChainSave chain, int count, bool incrementUV);
        List<AnimationFrameSave> AdjustUVAfterResize(string absoluteTextureFilePath, int oldWidth, int oldHeight, int newWidth, int newHeight);
        void NewFile();
        void AddFrameFromPixelBounds(AnimationChainSave chain, string textureName, int minX, int minY, int maxX, int maxY, int bitmapWidth, int bitmapHeight);
        void SetFrameTextureName(AnimationFrameSave frame, string? textureName);

        /// <summary>
        /// Assigns <paramref name="textureName"/> to every frame in <paramref name="chain"/>
        /// as a single undoable operation. No-op when the chain has no frames.
        /// </summary>
        void SetAllFramesTextureName(AnimationChainSave chain, string? textureName);

        void SetFrameLength(AnimationFrameSave frame, float newLength);
        void SetFrameRelative(AnimationFrameSave frame, float newRelX, float newRelY);
        void SetFrameColor(AnimationFrameSave frame, int? red, int? green, int? blue);
        void SetFrameColorOperation(AnimationFrameSave frame, ColorOperation? operation);
        void SetFrameAlpha(AnimationFrameSave frame, int? alpha);
        void SetFramePixelRegion(AnimationFrameSave frame, int pixelX, int pixelY, int pixelW, int pixelH, int bmpW, int bmpH);
        void SetRectProps(AnimationFrameSave? frame, AARectSave rect, string name, float x, float y, float scaleX, float scaleY);
        void SetCircleProps(AnimationFrameSave? frame, CircleSave circ, string name, float x, float y, float radius);

        // ── Hot Reload ────────────────────────────────────────────────────────────

        /// <summary>
        /// Hot-reload watcher. Wired to a real <see cref="HotReloadWatcher"/> by the app layer;
        /// defaults to <see cref="NullHotReloadWatcher"/> so tests don't need to stub it.
        /// </summary>
        IHotReloadWatcher HotReloadWatcher { get; set; }

        /// <summary>
        /// Subscribe to the hot-reload watcher after it's injected by the app layer.
        /// </summary>
        void WireHotReloadWatcher();

        /// <summary>
        /// Hot-reload: reloads the .achx from disk, preserving selection state.
        /// Clears the undo stack. Fires <see cref="IApplicationEvents.AchxReloadedFromDisk"/>.
        /// </summary>
        void ReloadAchxFromDisk(string path);

        /// <summary>
        /// Hot-reload: notifies consumers that a PNG has changed on disk.
        /// </summary>
        void ReloadPngFromDisk(string absolutePngPath);

        /// <summary>
        /// Synchronizes the hot-reload watcher with the current project state.
        /// Starts (or restarts) watching PNG directories and (if saved) the .achx file.
        /// Safe to call for unsaved projects — uses <see cref="string.Empty"/> as achxPath.
        /// Call after any operation that adds or changes PNG references (drag-drop, Save As).
        /// </summary>
        void SyncHotReloadWatcher();

        /// <summary>Pastes clipboard chains into the project: renames each to be unique and inserts
        /// the block below its source rows (see <see cref="IO.ChainPasteLogic"/>). Undoable.
        /// </summary>
        void PasteChains(IReadOnlyList<AnimationChainSave> chains);

        /// <summary>Adds clipboard frames to <paramref name="chain"/>. Undoable.
        /// <paramref name="insertIndex"/> <c>null</c> appends; a value inserts the frames
        /// there in order (paste after the selected frame).</summary>
        void PasteFrames(AnimationChainSave chain, IReadOnlyList<AnimationFrameSave> frames,
            int? insertIndex = null);

        /// <summary>Adds a clipboard rectangle to <paramref name="frame"/>. Undoable.</summary>
        void PasteRectangle(AnimationFrameSave frame, AARectSave rectangle);

        /// <summary>Adds a clipboard circle to <paramref name="frame"/>. Undoable.</summary>
        void PasteCircle(AnimationFrameSave frame, CircleSave circle);

        /// <summary>Adds multiple clipboard shapes to <paramref name="frame"/> in one undo step.</summary>
        void PasteShapes(AnimationFrameSave frame, IReadOnlyList<AARectSave> rectangles,
            IReadOnlyList<CircleSave> circles);

        /// <summary>Paste chains then remove <paramref name="sourcesToRemove"/> in one undo step.</summary>
        void PasteChainsCut(IReadOnlyList<AnimationChainSave> chains,
            IReadOnlyList<AnimationChainSave> sourcesToRemove);

        /// <summary>Paste frames then remove <paramref name="sourcesToRemove"/> in one undo step.</summary>
        void PasteFramesCut(AnimationChainSave targetChain, IReadOnlyList<AnimationFrameSave> frames,
            int? insertIndex, IReadOnlyList<AnimationFrameSave> sourcesToRemove);

        /// <summary>Paste shapes then remove <paramref name="sourcesToRemove"/> in one undo step.</summary>
        void PasteShapesCut(AnimationFrameSave targetFrame,
            IReadOnlyList<AARectSave> rectangles, IReadOnlyList<CircleSave> circles,
            IReadOnlyList<object> sourcesToRemove, AnimationFrameSave sourceFrame);

        /// <summary>Duplicates the homogeneous multi-selection as one undo step.</summary>
        void DuplicateSelection(CopySelectionPayload payload);
    }
}
