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
        /// Request a full tree-view rebuild from scratch. Raised on .achx load (File &gt; Open,
        /// recent files, drag-drop, startup reopen, tab switch). The argument is the set of
        /// chain names to expand — the saved companion-file expand state when one exists, or
        /// empty for a genuinely new file, so the tree is built already in its final state
        /// instead of collapsing first and correcting itself once settings load (which flickers
        /// on tab switch since the two steps land in separate dispatcher jobs). Differs from
        /// <see cref="RefreshTreeViewRequested"/>, which diff-updates the existing tree and
        /// preserves each chain's collapse state across edits.
        /// </summary>
        event Action<IReadOnlyList<string>> RebuildTreeViewRequested;

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
        /// Stores the current project model and chain/frame selection on <paramref name="tab"/>
        /// for later tab switches.
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
        /// Restores chain/frame selection from <paramref name="tab"/>'s cached selection fields
        /// onto the current project model. Call after applying a cached or untitled model that
        /// does not go through <see cref="TryActivateTabFromCache"/>.
        /// </summary>
        void RestoreTabSelection(TabEntry tab);

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

        /// <summary>
        /// Moves <paramref name="chain"/> to an absolute slot in the chain list as one undo
        /// step — the drag-and-drop reorder entry point (parallel to <see cref="MoveFrames"/>
        /// for frames). <paramref name="insertIndex"/> is interpreted against the current list
        /// <em>before</em> the chain is removed, so it is adjusted internally for the removal;
        /// an index landing on the chain's own slot is a no-op with no undo entry.
        /// </summary>
        void MoveChainToIndex(AnimationChainSave chain, int insertIndex);

        /// <summary>
        /// Moves <paramref name="chains"/> to an absolute slot in the chain list as one undo
        /// step — the multi-selection drag-and-drop reorder entry point (parallel to
        /// <see cref="MoveFrames"/> for frames). <paramref name="insertIndex"/> is interpreted
        /// against the current list <em>before</em> the chains are removed, so it is adjusted
        /// internally for the removal. The chains are sorted by their current index and become
        /// a contiguous, gap-squashed block at the destination.
        /// </summary>
        void MoveChainsToIndex(IReadOnlyList<AnimationChainSave> chains, int insertIndex);
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

        /// <summary>
        /// Shifts <paramref name="frames"/> within <paramref name="chain"/> by
        /// <paramref name="delta"/> slots (−1 = up, +1 = down) as one rigid group, preserving
        /// the gaps between non-contiguous frames, as a single undo step. The whole group is
        /// clamped at the chain boundaries: if the shift would push any selected frame past an
        /// end, nothing moves. Drives the Alt+Arrow keyboard reorder — unlike the drag-drop
        /// <see cref="MoveFrames"/> path, it does not collapse the selection into a contiguous block.
        /// </summary>
        void MoveFramesRelative(IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave chain, int delta);

        /// <summary>
        /// Shifts <paramref name="chains"/> within the chain list by <paramref name="delta"/>
        /// slots (−1 = up, +1 = down) as one rigid group, preserving the gaps between
        /// non-contiguous chains, as a single undo step. The whole group is clamped at the
        /// list boundaries: if the shift would push any selected chain past an end, nothing
        /// moves. Drives the Alt+Arrow keyboard reorder when multiple chains are selected —
        /// mirrors <see cref="MoveFramesRelative"/> for frames.
        /// </summary>
        void MoveChainsRelative(IReadOnlyList<AnimationChainSave> chains, int delta);
        void MoveShape(object shape, AnimationFrameSave frame, int delta);
        void MoveShapeToTop(object shape, AnimationFrameSave frame);
        void MoveShapeToBottom(object shape, AnimationFrameSave frame);
        void HandleReorder(int delta);
        /// <summary>
        /// Sets the horizontal/vertical/diagonal flip flags on every frame in <paramref name="frames"/>
        /// as one undo step. Each axis is absolute (not a toggle): pass <c>null</c> for an axis to leave it
        /// untouched (used when the inspector checkbox is showing a mixed/indeterminate multi-selection
        /// state and the user didn't interact with it). Only frames whose flag actually changes are
        /// mirrored/transposed (offset and attached shapes updated to match) — a frame already at the
        /// target state is a no-op for that axis.
        /// </summary>
        void SetFrameFlip(
            IReadOnlyList<AnimationFrameSave> frames, bool? flipHorizontal, bool? flipVertical, bool? flipDiagonal = null);
        void FlipChainHorizontally(AnimationChainSave chain);
        void FlipChainVertically(AnimationChainSave chain);
        void InvertFrameOrder(AnimationChainSave chain);
        void SetAllFrameLengths(AnimationChainSave chain, float frameLength);
        AnimationChainSave? DuplicateChain(AnimationChainSave source, bool flipH = false, bool flipV = false, string? newName = null);

        /// <summary>
        /// Deep-copies every chain in <paramref name="sources"/> and inserts the copies as one
        /// contiguous block after the last source, in a single undo step. When <paramref name="flipH"/>
        /// or <paramref name="flipV"/> is set, every copied frame and shape is mirrored along that axis.
        /// Backs both <see cref="DuplicateSelection"/> (no flip) and the tree context menu's
        /// Duplicate → Flip Horizontal/Vertical submenu items (which apply to the whole selection,
        /// not just the right-clicked chain).
        /// </summary>
        IReadOnlyList<AnimationChainSave> DuplicateChains(
            IReadOnlyList<AnimationChainSave> sources, bool flipH = false, bool flipV = false);

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

        void SetFrameLength(IReadOnlyList<AnimationFrameSave> frames, float newLength);

        /// <summary>
        /// Sets RelativeX/Y on every frame in <paramref name="frames"/>. Either axis may be
        /// <c>null</c> to leave it untouched per-frame (its own existing value survives) — used when
        /// the inspector field is showing "(mixed)" and the user only edited the other axis. Unlike
        /// color channels, there is no legitimate "clear to null" target for this field, so <c>null</c>
        /// unambiguously means "don't touch".
        /// </summary>
        void SetFrameRelative(IReadOnlyList<AnimationFrameSave> frames, float? newRelX, float? newRelY);
        void SetFrameColor(IReadOnlyList<AnimationFrameSave> frames, int? red, int? green, int? blue);
        void SetFrameColorOperation(IReadOnlyList<AnimationFrameSave> frames, ColorOperation? operation);
        void SetFrameAlpha(IReadOnlyList<AnimationFrameSave> frames, int? alpha);

        /// <summary>
        /// Sets the pixel region (X/Y/W/H) on every frame in <paramref name="frames"/>. Any of the
        /// four may be <c>null</c> to leave that component untouched per-frame — used when the
        /// inspector field is showing "(mixed)" and the user only edited one of the four. See
        /// <see cref="SetFrameRelative"/> for why <c>null</c> is unambiguous here.
        /// </summary>
        void SetFramePixelRegion(IReadOnlyList<AnimationFrameSave> frames, int? pixelX, int? pixelY, int? pixelW, int? pixelH, int bmpW, int bmpH);
        void SetRectProps(AnimationFrameSave? frame, AARectSave rect, string name, float x, float y, float scaleX, float scaleY);
        void SetCircleProps(AnimationFrameSave? frame, CircleSave circ, string name, float x, float y, float radius);

        /// <summary>
        /// Sets Name/X/Y/ScaleX/ScaleY on every rectangle in <paramref name="rects"/> as a single
        /// undoable operation — the multi-select counterpart to <see cref="SetRectProps"/>. Rects may
        /// belong to different frames. <paramref name="name"/> and each numeric parameter may be
        /// <c>null</c> to leave that field untouched per-rect (its own existing value survives) —
        /// used when the inspector field is showing "(mixed)", or (for name) when more than one
        /// rect is selected and applying one literal name to every rect would clobber their
        /// distinct names. See <see cref="SetFrameRelative"/> for why <c>null</c> is unambiguous here.
        /// </summary>
        void SetRectPropsBulk(IReadOnlyList<AARectSave> rects, string? name, float? x, float? y, float? scaleX, float? scaleY);

        /// <summary>
        /// Sets Name/X/Y/Radius on every circle in <paramref name="circles"/> as a single undoable
        /// operation — the multi-select counterpart to <see cref="SetCircleProps"/>. See
        /// <see cref="SetRectPropsBulk"/> for the null-means-"don't touch" semantics.
        /// </summary>
        void SetCirclePropsBulk(IReadOnlyList<CircleSave> circles, string? name, float? x, float? y, float? radius);

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
