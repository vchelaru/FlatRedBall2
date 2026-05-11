using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
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
        event Action<AnimationChainSave> RefreshChainNodeRequested;
        event Action<AnimationFrameSave> RefreshFrameNodeRequested;
        event Action RefreshAnimationFrameDisplayRequested;
        event Action RefreshWireframeRequested;
        event Action<string>? SaveAsCompleted;

        // ── Methods ───────────────────────────────────────────────────────────

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
        void MatchRectangleToFrame(AxisAlignedRectangleSave rectangle, AnimationFrameSave animationFrame);
        void MatchCircleToFrame(CircleSave circle, AnimationFrameSave animationFrame);
        void DeleteCircle(CircleSave circle, AnimationFrameSave owner);
        void DeleteAxisAlignedRectangle(AxisAlignedRectangleSave rectangle, AnimationFrameSave owner);
        Task AskToDeleteRectangles(List<AxisAlignedRectangleSave> rectangles);
        Task AskToDeleteCircles(List<CircleSave> circles);
        Task AskToDeleteAnimationChains(List<AnimationChainSave> animationChains);
        Task AskToDeleteFrames(List<AnimationFrameSave> frames);
        Task AddAnimationChain();
        bool RenameChain(AnimationChainSave chain, string newName);
        void RenameFrame(AnimationFrameSave frame, string newTextureName);
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
    }
}
