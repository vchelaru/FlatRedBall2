using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core
{
    public class SelectionSnapshot
    {
        public AnimationChainSave? AnimationChainSave;
        public AnimationFrameSave? AnimationFrameSave;
    }

    public class SelectedState : ISelectedState
    {
        private readonly IProjectManager _pm;

        public SelectedState(IProjectManager pm)
        {
            _pm = pm;
        }
        private AnimationChainSave? _selectedChain;
        private AnimationFrameSave? _selectedFrame;
        private AARectSave? _selectedRectangle;
        private CircleSave? _selectedCircle;
        private List<object> _selectedNodes = new List<object>();

        private SelectionSnapshot mSnapshot = new SelectionSnapshot();

        public event Action? SelectionChanged;

        public AnimationChainListSave? AnimationChainListSave =>
            _pm.AnimationChainListSave;

        public AnimationChainSave? SelectedChain
        {
            get => _selectedChain;
            set
            {
                _selectedChain = value;
                _selectedFrame = null;
                _selectedRectangle = null;
                _selectedCircle = null;
                SelectionChanged?.Invoke();
            }
        }

        public AnimationFrameSave? SelectedFrame
        {
            get => _selectedFrame;
            set
            {
                _selectedFrame = value;
                if (value != null)
                {
                    // Automatically set the parent chain
                    _selectedChain = FindChainForFrame(value);
                }
                _selectedRectangle = null;
                _selectedCircle = null;
                SelectionChanged?.Invoke();
            }
        }

        public AARectSave? SelectedRectangle
        {
            get => _selectedRectangle;
            set
            {
                _selectedRectangle = value;
                if (value != null) _selectedCircle = null;
                SelectionChanged?.Invoke();
            }
        }

        public CircleSave? SelectedCircle
        {
            get => _selectedCircle;
            set
            {
                _selectedCircle = value;
                if (value != null) _selectedRectangle = null;
                SelectionChanged?.Invoke();
            }
        }

        public object? SelectedShape => (object?)_selectedRectangle ?? _selectedCircle;

        public List<AnimationChainSave> SelectedChains =>
            _selectedNodes.OfType<AnimationChainSave>().ToList();

        public List<AnimationFrameSave> SelectedFrames
        {
            get
            {
                var frames = _selectedNodes.OfType<AnimationFrameSave>().ToList();
                if (frames.Count == 0 && _selectedFrame != null)
                    frames.Add(_selectedFrame);
                return frames;
            }
        }

        public List<AARectSave> SelectedRectangles
        {
            get
            {
                var rects = _selectedNodes.OfType<AARectSave>().ToList();
                if (rects.Count == 0 && _selectedRectangle != null)
                    rects.Add(_selectedRectangle);
                return rects;
            }
        }

        public List<CircleSave> SelectedCircles
        {
            get
            {
                var circles = _selectedNodes.OfType<CircleSave>().ToList();
                if (circles.Count == 0 && _selectedCircle != null)
                    circles.Add(_selectedCircle);
                return circles;
            }
        }

        /// <summary>
        /// Multi-selection bag. Can hold AnimationChainSave, AnimationFrameSave,
        /// AARectSave, or CircleSave objects.
        /// </summary>
        public List<object> SelectedNodes
        {
            get => _selectedNodes;
            set
            {
                var newList = value ?? new List<object>();
                if (_selectedNodes.Count == newList.Count
                    && _selectedNodes.SequenceEqual(newList))
                    return;
                _selectedNodes = newList;
                SelectionChanged?.Invoke();
            }
        }

        public string? SelectedTextureName
        {
            get
            {
                if (_selectedFrame != null)
                    return _selectedFrame.TextureName;
                if (_selectedChain?.Frames.Count > 0)
                    return _selectedChain.Frames[0].TextureName;
                return null;
            }
        }

        public TileMapInformation? SelectedTileMapInformation
        {
            get
            {
                var fileName = _selectedFrame?.TextureName
                    ?? (_selectedChain?.Frames.Count > 0 ? _selectedChain.Frames[0].TextureName : null);

                if (!string.IsNullOrEmpty(fileName))
                    return _pm.TileMapInformationList.GetTileMapInformation(fileName);

                return null;
            }
        }

        public SelectionSnapshot Snapshot
        {
            get => mSnapshot;
            set => mSnapshot = value;
        }

        private AnimationChainSave? FindChainForFrame(AnimationFrameSave frame)
        {
            if (AnimationChainListSave == null) return null;
            foreach (var chain in AnimationChainListSave.AnimationChains)
            {
                if (chain.Frames.Contains(frame))
                    return chain;
            }
            return null;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _selectedChain = null;
            _selectedFrame = null;
            _selectedRectangle = null;
            _selectedCircle = null;
            _selectedNodes = new List<object>();
            SelectionChanged?.Invoke();
        }
    }
}
