using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Core.Keyframes;

namespace AnimationEditor.Core.ViewModels;

/// <summary>The two keyframe lanes under the timeline cells and the labels they carry; a <c>null</c> lane is hidden.</summary>
public sealed record TimelineLanePlan(KeyframeLane? First, string FirstLabel, KeyframeLane? Second, string SecondLabel)
{
    /// <summary>No lanes.</summary>
    public static readonly TimelineLanePlan None = new(null, "", null, "");
}

/// <summary>
/// Decides which keyframe lanes the single-chain timeline shows for the current selection (#1489). Lanes follow
/// the selection and share two slots: a chain or frame shows the Sprite's Offset and Color, a single shape
/// shows that shape's Offset and Size, or a polygon's Offset and Points (labeled with its name), in their place, and several shapes show none.
/// </summary>
public static class TimelineLanePlanner
{
    /// <param name="chain">The chain on the strip, or <c>null</c>.</param>
    /// <param name="selectedShapes">The selected shapes (<c>ISelectedState.SelectedShapes</c>).</param>
    /// <param name="cellWidths">The strip's cell widths, one per frame.</param>
    public static TimelineLanePlan Build(
        AnimationChainSave? chain, IReadOnlyList<object> selectedShapes, IReadOnlyList<double> cellWidths)
    {
        if (chain is null || selectedShapes.Count > 1) return TimelineLanePlan.None;

        if (selectedShapes.Count == 1)
        {
            if (selectedShapes[0] is not ShapeSave shape) return TimelineLanePlan.None;
            // A polygon has no size, so its Points lane takes the second slot.
            if (shape is PolygonSave)
                return new TimelineLanePlan(
                    KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Offset, cellWidths), $"{shape.Name} Offset",
                    KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Points, cellWidths), $"{shape.Name} Points");
            return new TimelineLanePlan(
                KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Offset, cellWidths), $"{shape.Name} Offset",
                KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Size, cellWidths), $"{shape.Name} Size");
        }

        return new TimelineLanePlan(
            KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, cellWidths), "Offset",
            KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, cellWidths), "Color");
    }
}
