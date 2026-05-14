using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
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
        /// Fired when <see cref="LoadAnimationChain"/> fails — file not found, corrupt XML,
        /// or any other engine-side throw. The first argument is the attempted file path;
        /// the second is the exception. <c>RefreshTreeViewRequested</c> is NOT fired when
        /// this event fires; project state is left unchanged.
        /// </summary>
        event Action<string, Exception>? LoadFailed;

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
        void RefreshTreeNode(AnimationChainSave animationChain);
        void RefreshTreeNode(AnimationFrameSave animationFrame);
        void RefreshAnimationFrameDisplay();
        void RefreshWireframe();
        void RefreshTreeView();
        void SaveCurrentAnimationChainList(string? fileName = null);
        Task SaveCurrentAnimationChainListAsync();
        void DeleteAnimationChains(List<AnimationChainSave> animationChains);
        void AddAxisAlignedRectangle(AnimationFrameSave frame);
        void AddCircle(AnimationFrameSave frame);
        void MatchRectangleToFrame(AARectSave rectangle, AnimationFrameSave animationFrame);
        void MatchCircleToFrame(CircleSave circle, AnimationFrameSave animationFrame);
        void DeleteCircle(CircleSave circle, AnimationFrameSave owner);
        void DeleteAxisAlignedRectangle(AARectSave rectangle, AnimationFrameSave owner);
        Task AskToDeleteRectangles(List<AARectSave> rectangles);
        Task AskToDeleteCircles(List<CircleSave> circles);
        Task AskToDeleteAnimationChains(List<AnimationChainSave> animationChains);
        Task AskToDeleteFrames(List<AnimationFrameSave> frames);
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
        void HandleReorder(int delta);
        void FlipFrameHorizontally(AnimationFrameSave frame);
        void FlipFrameVertically(AnimationFrameSave frame);
        void FlipChainHorizontally(AnimationChainSave chain);
        void FlipChainVertically(AnimationChainSave chain);
        void InvertFrameOrder(AnimationChainSave chain);
        void SetAllFrameLengths(AnimationChainSave chain, float frameLength);
        AnimationChainSave? DuplicateChain(AnimationChainSave source, bool flipH = false, bool flipV = false, string? newName = null);
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
        /// Pastes clipboard chains into the project: renames each to be unique and inserts
        /// the block below its source rows (see <see cref="IO.ChainPasteLogic"/>). Undoable.
        /// </summary>
        void PasteChains(IReadOnlyList<AnimationChainSave> chains);

        /// <summary>Appends clipboard frames to <paramref name="chain"/>. Undoable.</summary>
        void PasteFrames(AnimationChainSave chain, IReadOnlyList<AnimationFrameSave> frames);

        /// <summary>Adds a clipboard rectangle to <paramref name="frame"/>. Undoable.</summary>
        void PasteRectangle(AnimationFrameSave frame, AARectSave rectangle);

        /// <summary>Adds a clipboard circle to <paramref name="frame"/>. Undoable.</summary>
        void PasteCircle(AnimationFrameSave frame, CircleSave circle);
    }
}
