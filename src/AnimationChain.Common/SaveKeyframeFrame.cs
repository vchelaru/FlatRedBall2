using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Adapts an <see cref="AnimationFrameSave"/> to <see cref="IKeyframeFrame"/> for editor-side
/// evaluation: color channels resolved through <see cref="EffectiveFrameColor"/>, curve names parsed
/// into <see cref="KeyframeCurve"/>, and an unset frame length read as the editor's 0.1 default.
/// </summary>
public readonly struct SaveKeyframeFrame : IKeyframeFrame
{
    private readonly AnimationFrameSave _frame;
    private readonly ResolvedFrameColor _color;

    private SaveKeyframeFrame(AnimationFrameSave frame, ResolvedFrameColor color)
    {
        _frame = frame;
        _color = color;
        OffsetCurve = KeyframeCurve.Parse(frame.OffsetInterpolation, frame.OffsetEasing);
        ColorCurve = KeyframeCurve.Parse(frame.ColorInterpolation, frame.ColorEasing);
    }

    /// <summary>Adapts every frame of <paramref name="chain"/>, resolving color inheritance once.</summary>
    public static SaveKeyframeFrame[] Create(AnimationChainSave chain)
    {
        var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
        var result = new SaveKeyframeFrame[chain.Frames.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new SaveKeyframeFrame(chain.Frames[i], colors[i]);
        return result;
    }

    /// <inheritdoc/>
    public double DurationSeconds => _frame.FrameLength > 0 ? _frame.FrameLength : 0.1;

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
}
