using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteFramesCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<AnimationFrameSave> _frames;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;

        // Captured by Do(): the frames actually removed, paired with where they were.
        private (AnimationFrameSave Frame, int OriginalIndex)[] _removed = [];

        public string Description { get; }

        public DeleteFramesCommand(
            IReadOnlyList<AnimationFrameSave> frames,
            AnimationChainSave chain,
            IAppCommands commands,
            IApplicationEvents events,
            ISelectedState selectedState)
        {
            _frames = frames;
            _chain = chain;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            Description = frames.Count == 1 ? "Delete Frame" : $"Delete {frames.Count} Frames";
        }

        public bool Do()
        {
            // Capture every original index BEFORE removing anything — removing one
            // frame would shift the indices of those still to be captured.
            var removed = new List<(AnimationFrameSave, int)>();
            foreach (var frame in _frames)
            {
                int idx = _chain.Frames.IndexOf(frame);
                if (idx >= 0) removed.Add((frame, idx));
            }
            _removed = removed.ToArray();

            if (_removed.Length == 0) return false;

            foreach (var (frame, _) in _removed)
                _chain.Frames.Remove(frame);

            ClearSelectionForRemovedFrames();
            _commands.RefreshTreeNode(_chain);
            _commands.RefreshWireframe();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            foreach (var (frame, idx) in _removed)
            {
                int safeIdx = Math.Min(idx, _chain.Frames.Count);
                _chain.Frames.Insert(safeIdx, frame);
            }
            _commands.RefreshTreeNode(_chain);
            _commands.RefreshWireframe();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedFrame = _removed[0].Frame;
        }

        public void Redo()
        {
            foreach (var (frame, _) in _removed)
                _chain.Frames.Remove(frame);
            ClearSelectionForRemovedFrames();
            _commands.RefreshTreeNode(_chain);
            _commands.RefreshWireframe();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        /// <summary>
        /// Drops every just-removed frame from the selection. Without this the
        /// preview keeps rendering an orphaned frame's sprite and shapes, because
        /// <see cref="ISelectedState.SelectedFrame"/> still points at a frame that
        /// is no longer in the chain. See issue #284.
        /// </summary>
        private void ClearSelectionForRemovedFrames()
        {
            var removedFrames = _removed.Select(r => r.Frame).ToHashSet();

            // Drop deleted frames from the multi-selection bag.
            var nodes = _selectedState.SelectedNodes;
            if (nodes.Any(n => n is AnimationFrameSave f && removedFrames.Contains(f)))
            {
                _selectedState.SelectedNodes = nodes
                    .Where(n => n is not AnimationFrameSave f || !removedFrames.Contains(f))
                    .ToList();
            }

            // Clear the primary selection if it pointed at a deleted frame. Setting
            // SelectedFrame = null also clears any rectangle/circle selected on it.
            if (_selectedState.SelectedFrame is { } selected && removedFrames.Contains(selected))
                _selectedState.SelectedFrame = null;
        }
    }
}
