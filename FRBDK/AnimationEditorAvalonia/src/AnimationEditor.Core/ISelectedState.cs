using AnimationEditor.Core.Data;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
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
        AxisAlignedRectangleSave? SelectedRectangle { get; set; }
        CircleSave? SelectedCircle { get; set; }
        object? SelectedShape { get; }
        List<AnimationChainSave> SelectedChains { get; set; }
        List<AnimationFrameSave> SelectedFrames { get; }
        List<AxisAlignedRectangleSave> SelectedRectangles { get; }
        List<CircleSave> SelectedCircles { get; }
        List<object> SelectedNodes { get; set; }
        string? SelectedTextureName { get; }
        TileMapInformation? SelectedTileMapInformation { get; }
        SelectionSnapshot Snapshot { get; set; }
    }
}
