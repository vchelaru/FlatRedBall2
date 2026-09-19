using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Deep-copies the parts of an <see cref="AnimationChainListSave"/> that
/// <see cref="AchjToTiledAnimationMapper"/> reads (coordinate type, time unit, chain names, frame
/// rects/flips), so a background Tiled tileset sync (<see cref="TiledTilesetSyncRunner"/>) can run
/// off the UI thread without racing further edits to the live, still-open project model. Shapes,
/// colors, and other frame data the mapper doesn't consume are intentionally omitted.
/// </summary>
internal static class AchjSyncSnapshot
{
    public static AnimationChainListSave Clone(AnimationChainListSave source)
    {
        var clone = new AnimationChainListSave
        {
            CoordinateType = source.CoordinateType,
            TimeMeasurementUnit = source.TimeMeasurementUnit,
        };

        foreach (var chain in source.AnimationChains)
        {
            var chainClone = new AnimationChainSave { Name = chain.Name };
            foreach (var frame in chain.Frames)
            {
                chainClone.Frames.Add(new AnimationFrameSave
                {
                    TextureName = frame.TextureName,
                    FrameLength = frame.FrameLength,
                    LeftCoordinate = frame.LeftCoordinate,
                    RightCoordinate = frame.RightCoordinate,
                    TopCoordinate = frame.TopCoordinate,
                    BottomCoordinate = frame.BottomCoordinate,
                    FlipHorizontal = frame.FlipHorizontal,
                    FlipVertical = frame.FlipVertical,
                    FlipDiagonal = frame.FlipDiagonal,
                });
            }
            clone.AnimationChains.Add(chainClone);
        }

        return clone;
    }
}
