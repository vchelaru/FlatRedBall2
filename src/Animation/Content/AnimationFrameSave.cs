using System.Xml.Serialization;

namespace FlatRedBall2.Animation.Content;

[System.Serializable]
public class AnimationFrameSave
{
    public string TextureName = string.Empty;

    /// <summary>Frame display time. Units depend on <see cref="AnimationChainListSave.TimeMeasurementUnit"/>.</summary>
    public float FrameLength;

    /// <summary>Left texture coordinate. UV (0–1) or pixel, depending on <see cref="AnimationChainListSave.CoordinateType"/>.</summary>
    public float LeftCoordinate;

    /// <summary>Right texture coordinate. UV (0–1) or pixel.</summary>
    public float RightCoordinate = 1f;

    /// <summary>Top texture coordinate. UV (0–1) or pixel.</summary>
    public float TopCoordinate;

    /// <summary>Bottom texture coordinate. UV (0–1) or pixel.</summary>
    public float BottomCoordinate = 1f;

    public bool FlipHorizontal;
    // ShouldSerialize* methods are recognized by XmlSerializer to conditionally omit fields
    public bool ShouldSerializeFlipHorizontal() => FlipHorizontal;

    public bool FlipVertical;
    public bool ShouldSerializeFlipVertical() => FlipVertical;

    public float RelativeX;
    public bool ShouldSerializeRelativeX() => RelativeX != 0f;

    public float RelativeY;
    public bool ShouldSerializeRelativeY() => RelativeY != 0f;
}
