using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core
{
    public interface ISelectedState
    {
        event Action SelectionChanged;

        AnimationChainListSave? AnimationChainListSave { get; }
        AnimationChainSave? SelectedChain { get; set; }
        AnimationFrameSave? SelectedFrame { get; set; }
        AARectSave? SelectedRectangle { get; set; }
        CircleSave? SelectedCircle { get; set; }
        PolygonSave? SelectedPolygon { get; set; }
        object? SelectedShape { get; }

        /// <summary>
        /// Sets whichever of <see cref="SelectedRectangle"/>, <see cref="SelectedCircle"/> or
        /// <see cref="SelectedPolygon"/> matches <paramref name="shape"/>'s type, clearing the other
        /// two; <c>null</c> (or a non-shape) clears all three.
        /// </summary>
        void SelectShape(object? shape);
        List<AnimationChainSave> SelectedChains { get; }
        List<AnimationFrameSave> SelectedFrames { get; }

        /// <summary>
        /// The distinct chains the preview should show, in multi-selection order: every selected
        /// chain plus the owner of every selected frame. A frame selected in each of two chains
        /// therefore previews both chains, each pinned to its frame.
        /// </summary>
        List<AnimationChainSave> PreviewChains { get; }
        List<AARectSave> SelectedRectangles { get; }
        List<CircleSave> SelectedCircles { get; }
        List<PolygonSave> SelectedPolygons { get; }

        /// <summary>
        /// Every selected shape (rectangles, circles and polygons) in multi-selection order, or the
        /// single <see cref="SelectedShape"/> when the multi-selection holds no shapes.
        /// </summary>
        List<object> SelectedShapes { get; }
        List<object> SelectedNodes { get; set; }
        string? SelectedTextureName { get; }
        TileMapInformation? SelectedTileMapInformation { get; }
        SelectionSnapshot Snapshot { get; set; }

        /// <summary>
        /// Clears all selection state (chain, frame, shapes, multi-select) and fires
        /// <see cref="SelectionChanged"/> once. Call this when the project is reset or
        /// a new file is loaded so the wireframe and preview stop showing stale content.
        /// </summary>
        void Reset();
    }
}
