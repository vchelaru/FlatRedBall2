using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
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
        private PolygonSave? _selectedPolygon;
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
                // A chain picked outside the tree (e.g. a texture dropped onto it) replaces the
                // multi-select bag; commands that set SelectedNodes first keep theirs.
                if (value != null && !_selectedNodes.Contains(value))
                    _selectedNodes = new List<object>();
                _selectedFrame = null;
                _selectedRectangle = null;
                _selectedCircle = null;
                _selectedPolygon = null;
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
                    // A frame picked outside the tree (e.g. double-clicking its wireframe box
                    // while several chains are selected) replaces the multi-select bag; otherwise
                    // SelectedChains stays stale and the tree keeps every chain highlighted. Tree
                    // routing always puts the frame in the bag first, so it keeps its bag.
                    if (!_selectedNodes.Contains(value))
                        _selectedNodes = new List<object>();
                }
                _selectedRectangle = null;
                _selectedCircle = null;
                _selectedPolygon = null;
                SelectionChanged?.Invoke();
            }
        }

        public AARectSave? SelectedRectangle
        {
            get => _selectedRectangle;
            set
            {
                _selectedRectangle = value;
                if (value != null) { _selectedCircle = null; _selectedPolygon = null; }
                SelectionChanged?.Invoke();
            }
        }

        public CircleSave? SelectedCircle
        {
            get => _selectedCircle;
            set
            {
                _selectedCircle = value;
                if (value != null) { _selectedRectangle = null; _selectedPolygon = null; }
                SelectionChanged?.Invoke();
            }
        }

        public PolygonSave? SelectedPolygon
        {
            get => _selectedPolygon;
            set
            {
                _selectedPolygon = value;
                if (value != null) { _selectedRectangle = null; _selectedCircle = null; }
                SelectionChanged?.Invoke();
            }
        }

        public object? SelectedShape => (object?)_selectedRectangle ?? (object?)_selectedCircle ?? _selectedPolygon;

        /// <inheritdoc/>
        public void SelectShape(object? shape)
        {
            switch (shape)
            {
                case AARectSave r: SelectedRectangle = r; break;
                case CircleSave c: SelectedCircle = c; break;
                case PolygonSave p: SelectedPolygon = p; break;
                default:
                    _selectedRectangle = null;
                    _selectedCircle = null;
                    _selectedPolygon = null;
                    SelectionChanged?.Invoke();
                    break;
            }
        }

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

        public List<AnimationChainSave> PreviewChains
        {
            get
            {
                var chains = new List<AnimationChainSave>();
                foreach (var node in _selectedNodes)
                {
                    var chain = node as AnimationChainSave
                        ?? (node is AnimationFrameSave frame ? FindChainForFrame(frame) : null)
                        ?? (node is ShapeSave shape ? FindChainForShape(shape) : null);
                    if (chain is not null && !chains.Contains(chain))
                        chains.Add(chain);
                }
                return chains;
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

        public List<PolygonSave> SelectedPolygons
        {
            get
            {
                var polygons = _selectedNodes.OfType<PolygonSave>().ToList();
                if (polygons.Count == 0 && _selectedPolygon != null)
                    polygons.Add(_selectedPolygon);
                return polygons;
            }
        }

        public List<object> SelectedShapes
        {
            get
            {
                var shapes = _selectedNodes.Where(n => n is ShapeSave).ToList();
                if (shapes.Count == 0 && SelectedShape is { } single)
                    shapes.Add(single);
                return shapes;
            }
        }

        /// <summary>
        /// Multi-selection bag. Can hold AnimationChainSave, AnimationFrameSave,
        /// AARectSave, CircleSave, or PolygonSave objects.
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

        private AnimationChainSave? FindChainForShape(ShapeSave shape)
        {
            if (AnimationChainListSave == null) return null;
            foreach (var chain in AnimationChainListSave.AnimationChains)
                foreach (var frame in chain.Frames)
                    if (frame.ShapesSave?.Shapes.Contains(shape) == true)
                        return chain;
            return null;
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
            _selectedPolygon = null;
            _selectedNodes = new List<object>();
            SelectionChanged?.Invoke();
        }
    }
}
