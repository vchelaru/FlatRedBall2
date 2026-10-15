using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.AnimationEditorCommon;

namespace FlatRedBall2.Animation;

/// <summary>
/// One frame of a texture-flipping animation. Holds a reference to the texture,
/// the source region in pixel coordinates, flip flags, and how long this frame displays.
/// </summary>
public class AnimationFrame : IKeyframeFrame
{
    /// <summary>The texture to display for this frame.</summary>
    public Texture2D? Texture;

    /// <summary>Name of the source texture file, used during loading.</summary>
    public string TextureName = string.Empty;

    /// <summary>How long this frame is displayed.</summary>
    public TimeSpan FrameLength;

    /// <summary>
    /// The pixel-coordinate region of <see cref="Texture"/> to render.
    /// Null means the entire texture.
    /// </summary>
    public Rectangle? SourceRectangle;

    /// <summary>When <c>true</c>, the source region is mirrored along the X axis at draw time.</summary>
    public bool FlipHorizontal;

    /// <summary>When <c>true</c>, the source region is mirrored along the Y axis at draw time.</summary>
    public bool FlipVertical;

    /// <summary>
    /// Whether the source region should be transposed (swap X/Y), independent of
    /// <see cref="FlipHorizontal"/>/<see cref="FlipVertical"/>. Authored and round-tripped by the
    /// AnimationEditor; the FRB2 <c>SpriteBatch</c> playback path does <b>not</b> apply it — MonoGame's
    /// <c>SpriteEffects</c> has no diagonal option, so a runtime that wants this needs its own
    /// UV-manipulation rendering.
    /// </summary>
    public bool FlipDiagonal;

    /// <summary>
    /// Local X offset applied to the sprite while this frame is displayed.
    /// Replaces the sprite's X each frame switch, so the character can shift
    /// position per-frame (e.g. a kick frame that leans the character forward).
    /// </summary>
    public float RelativeX;

    /// <summary>
    /// Local Y offset applied to the sprite while this frame is displayed.
    /// Replaces the sprite's Y each frame switch.
    /// </summary>
    public float RelativeY;

    /// <summary>
    /// Optional per-frame red channel, -255..255, or <c>null</c> when unset. Applied automatically
    /// by <see cref="FlatRedBall2.Rendering.Sprite.Draw"/> per <see cref="ColorOperation"/> — see
    /// <see cref="FlatRedBall2.Rendering.SpriteFrameColor.Apply(AnimationFrame, Microsoft.Xna.Framework.Color)"/>. See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Optional per-frame green channel, -255..255, or <c>null</c> when unset. See <see cref="Red"/>.</summary>
    public int? Green;

    /// <summary>Optional per-frame blue channel, -255..255, or <c>null</c> when unset. See <see cref="Red"/>.</summary>
    public int? Blue;

    /// <summary>
    /// Optional per-frame alpha (transparency) channel, 0–255, or <c>null</c> when unset. Straight
    /// transparency, independent of <see cref="ColorOperation"/>. Applied automatically by
    /// <see cref="FlatRedBall2.Rendering.Sprite.Draw"/>, multiplied into <see cref="FlatRedBall2.Rendering.Sprite.Alpha"/>.
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// Optional per-frame color operation for how <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>
    /// combine with the texture, or <c>null</c> for none. Applied automatically by
    /// <see cref="FlatRedBall2.Rendering.Sprite"/> — <see cref="FlatRedBall2.Animation.ColorOperation.Multiply"/>
    /// scales the draw color, <see cref="FlatRedBall2.Animation.ColorOperation.Add"/> is assigned to
    /// <see cref="FlatRedBall2.Rendering.Sprite.AddColor"/> (a pixel-shader offset, see
    /// <see cref="FlatRedBall2.Rendering.Batches.WorldSpaceAddColorBatch"/>). A sprite can opt out via
    /// <see cref="FlatRedBall2.Rendering.Sprite.FrameControlledProperties"/>.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// When <c>true</c> this frame is not an offset keyframe: its <see cref="RelativeX"/>/<see cref="RelativeY"/>
    /// and <see cref="OffsetCurve"/> are ignored, and the previous offset keyframe glides across it.
    /// </summary>
    public bool SkipOffsetKeyframe;

    /// <summary>
    /// When <c>true</c> this frame is not a color keyframe: its own color channels and <see cref="ColorCurve"/>
    /// are ignored, and the previous color keyframe glides across it.
    /// </summary>
    public bool SkipColorKeyframe;

    /// <summary>
    /// Curve from this frame's offset to the next offset keyframe's, applied by <see cref="FlatRedBall2.Rendering.Sprite"/>
    /// while <see cref="FlatRedBall2.Rendering.SpriteFrameProperties.Offset"/> is frame-controlled. Default is snap.
    /// </summary>
    public KeyframeCurve OffsetCurve;

    /// <summary>
    /// Curve from this frame's color to the next color keyframe's, applied by <see cref="FlatRedBall2.Rendering.Sprite"/>
    /// while <see cref="FlatRedBall2.Rendering.SpriteFrameProperties.Color"/> is frame-controlled. Default is snap.
    /// </summary>
    public KeyframeCurve ColorCurve;

    double IKeyframeFrame.DurationSeconds => FrameLength.TotalSeconds;
    float IKeyframeFrame.RelativeX => RelativeX;
    float IKeyframeFrame.RelativeY => RelativeY;
    int? IKeyframeFrame.Red => Red;
    int? IKeyframeFrame.Green => Green;
    int? IKeyframeFrame.Blue => Blue;
    int? IKeyframeFrame.Alpha => Alpha;
    ColorOperation? IKeyframeFrame.ColorOperation => ColorOperation;
    bool IKeyframeFrame.SkipOffsetKeyframe => SkipOffsetKeyframe;
    bool IKeyframeFrame.SkipColorKeyframe => SkipColorKeyframe;
    KeyframeCurve IKeyframeFrame.OffsetCurve => OffsetCurve;
    KeyframeCurve IKeyframeFrame.ColorCurve => ColorCurve;
    IReadOnlyList<IKeyframeShape> IKeyframeFrame.KeyframeShapes => Shapes;

    /// <summary>
    /// Per-frame shape definitions reconciled against the parent entity at frame switch time.
    /// Each entry must have a non-empty unique <see cref="AnimationShapeFrame.Name"/>. See
    /// <see cref="AnimationChainList"/> for the ownership rule that decides which entity shapes
    /// the animation system is allowed to touch.
    /// </summary>
    public List<AnimationShapeFrame> Shapes { get; } = new();

    /// <summary>
    /// Named events raised through <see cref="FlatRedBall2.Rendering.Sprite.FrameEventRaised"/> when
    /// playback enters this frame. See that event for skipped-frame, loop, and frame-0 semantics.
    /// </summary>
    public List<AnimationFrameEvent> Events { get; } = new();
}
