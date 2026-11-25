using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Utilities;

/// <summary>A kind of data a chain can carry beyond its name, textures and frame rects. See <see cref="ChainData"/>.</summary>
public enum ChainDataKind
{
    Flip,
    SpriteOffset,
    Color,
    CollisionShape,
    Event,
    Keyframe,
    FrameLength,
    /// <summary>A length that rounds to no length at all in whole milliseconds. Shorter rounding is accepted.</summary>
    FrameLengthUnderAMillisecond,
    LoopOff,
    PingPong,
    Reverse,
}

/// <summary>
/// The one list of what a format other than .achx/.achj can fail to store. The .tsx save and the exporters each pass the
/// kinds they do store, so a kind added here is reported by every format until that format says it stores it.
/// </summary>
public static class ChainData
{
    private static readonly (ChainDataKind Kind, string Message, Func<AnimationChainSave, FrameLengthUnit, bool> IsIn)[] Kinds =
    [
        (ChainDataKind.Flip, "flip", AnyFrame(f => f.FlipHorizontal || f.FlipVertical || f.FlipDiagonal)),
        (ChainDataKind.SpriteOffset, "sprite offset", AnyFrame(f => f.RelativeX != 0f || f.RelativeY != 0f)),
        (ChainDataKind.Color, "color", AnyFrame(f => f.Red is not null || f.Green is not null || f.Blue is not null
            || f.Alpha is not null || f.ColorOperation is not null)),
        (ChainDataKind.CollisionShape, "collision shape", AnyFrame(f => f.ShapesSave?.Shapes.Count > 0)),
        (ChainDataKind.Event, "event", AnyFrame(f => f.Events.Count > 0)),
        (ChainDataKind.Keyframe, "keyframe", AnyFrame(f => f.SkipOffsetKeyframe || f.SkipColorKeyframe
            || f.OffsetInterpolation is not null || f.OffsetEasing is not null
            || f.ColorInterpolation is not null || f.ColorEasing is not null)),
        (ChainDataKind.FrameLength, "frame length", AnyFrame(f => f.FrameLength != 0f)),
        (ChainDataKind.FrameLengthUnderAMillisecond, "frame length under a millisecond",
            (chain, unit) => chain.Frames.Any(f => f.FrameLength != 0f && unit.ToMilliseconds(f.FrameLength) == 0)),
        (ChainDataKind.LoopOff, "loop off", (chain, _) => !chain.Loop),
        (ChainDataKind.PingPong, "ping-pong", (chain, _) => chain.PingPong),
        (ChainDataKind.Reverse, "reverse", (chain, _) => chain.Reverse),
    ];

    private static Func<AnimationChainSave, FrameLengthUnit, bool> AnyFrame(Func<AnimationFrameSave, bool> carries) =>
        (chain, _) => chain.Frames.Any(carries);

    /// <summary>What <paramref name="chains"/> carry that a format storing only <paramref name="stored"/> drops, each kind once.</summary>
    public static IReadOnlyList<ChainDataKind> Dropped(
        IReadOnlyList<AnimationChainSave> chains, TimeMeasurementUnit unit, params ChainDataKind[] stored) =>
        Kinds.Where(k => !stored.Contains(k.Kind) && chains.Any(c => k.IsIn(c, new FrameLengthUnit(unit))))
            .Select(k => k.Kind).ToList();

    /// <summary>The kinds as a warning names them: <c>flip, sprite offset</c>.</summary>
    public static string Describe(IReadOnlyList<ChainDataKind> kinds) =>
        string.Join(", ", Kinds.Where(k => kinds.Contains(k.Kind)).Select(k => k.Message));
}
