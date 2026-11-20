using AnimationEditor.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// What a selection of shapes of different types (a rectangle and a circle, say) shares in the
/// Inspector: Name and the Offset X/Y. A null value means the shapes disagree (shown as "(mixed)").
/// Type-specific fields (rectangle scale, circle radius, polygon points) are not shared, so the
/// Inspector hides them for a <see cref="IsMixedType"/> selection.
/// </summary>
public sealed record ShapeSharedFields(bool IsMixedType, string? Name, float? X, float? Y)
{
    public static ShapeSharedFields For(IReadOnlyList<ShapeSave> shapes, Func<ShapeSave, AnimationChainSave?> chainOf, FrameLengthUnit unit)
    {
        bool mixedType = shapes.Select(s => s.GetType()).Distinct().Count() > 1;
        var offsets = shapes.Select(s => KeyframeInspectorState.DisplayShapeOffset(s, chainOf(s), unit)).ToList();
        return new ShapeSharedFields(
            mixedType,
            MixedState.Common(shapes.Select(s => s.Name ?? "")),
            MixedState.Common(offsets.Select(o => o.X)),
            MixedState.Common(offsets.Select(o => o.Y)));
    }
}
