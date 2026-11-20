using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Adapts an <see cref="AnimationFrameSave"/> to <see cref="IKeyframeFrame"/> for editor-side
/// evaluation: color channels resolved through <see cref="EffectiveFrameColor"/>, curve names parsed
/// into <see cref="KeyframeCurve"/>, frame lengths converted from the file's <see cref="TimeMeasurementUnit"/> to seconds, and an unset length read as the editor's 0.1 s default.
/// </summary>
public readonly struct SaveKeyframeFrame : IKeyframeFrame
{
    private readonly AnimationFrameSave _frame;
    private readonly ResolvedFrameColor _color;
    private readonly double _secondsPerUnit;

    private SaveKeyframeFrame(AnimationFrameSave frame, ResolvedFrameColor color, double secondsPerUnit)
    {
        _frame = frame;
        _color = color;
        _secondsPerUnit = secondsPerUnit;
        OffsetCurve = KeyframeCurve.Parse(frame.OffsetInterpolation, frame.OffsetEasing);
        ColorCurve = KeyframeCurve.Parse(frame.ColorInterpolation, frame.ColorEasing);
        KeyframeShapes = frame.ShapesSave is { Shapes.Count: > 0 } shapes
            ? shapes.Shapes.OfType<ShapeSave>().Select(s => (IKeyframeShape)SaveKeyframeShape.Create(s)).ToArray()
            : Array.Empty<IKeyframeShape>();
    }

    /// <summary>Adapts every frame of <paramref name="chain"/>, resolving color inheritance once.</summary>
    public static SaveKeyframeFrame[] Create(AnimationChainSave chain, TimeMeasurementUnit unit = TimeMeasurementUnit.Second)
    {
        var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
        double secondsPerUnit = unit == TimeMeasurementUnit.Millisecond ? 0.001 : 1.0;
        var result = new SaveKeyframeFrame[chain.Frames.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new SaveKeyframeFrame(chain.Frames[i], colors[i], secondsPerUnit);
        return result;
    }

    /// <inheritdoc/>
    public double DurationSeconds => _frame.FrameLength > 0 ? _frame.FrameLength * _secondsPerUnit : 0.1;

    /// <inheritdoc/>
    public float RelativeX => _frame.RelativeX;

    /// <inheritdoc/>
    public float RelativeY => _frame.RelativeY;

    /// <inheritdoc/>
    public int? Red => _color.Red;

    /// <inheritdoc/>
    public int? Green => _color.Green;

    /// <inheritdoc/>
    public int? Blue => _color.Blue;

    /// <inheritdoc/>
    public int? Alpha => _color.Alpha;

    /// <inheritdoc/>
    public ColorOperation? ColorOperation => _color.Operation;

    /// <inheritdoc/>
    public bool SkipOffsetKeyframe => _frame.SkipOffsetKeyframe;

    /// <inheritdoc/>
    public bool SkipColorKeyframe => _frame.SkipColorKeyframe;

    /// <inheritdoc/>
    public KeyframeCurve OffsetCurve { get; }

    /// <inheritdoc/>
    public KeyframeCurve ColorCurve { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IKeyframeShape> KeyframeShapes { get; }
}
