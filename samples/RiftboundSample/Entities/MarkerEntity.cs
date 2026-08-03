using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace RiftboundSample.Entities;

/// <summary>
/// A simple colored rectangle used for map markers (NPCs, doors, shops, inns).
/// No behavior — just visual + collision shape.
/// </summary>
public class MarkerEntity : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;
    public string MarkerType { get; set; } = "";

    private Color _color = Color.White;

    /// <summary>Set before CustomInitialize (i.e., set on the factory result before the next frame).</summary>
    public Color MarkerColor
    {
        get => _color;
        set
        {
            _color = value;
            if (Rectangle != null)
                Rectangle.Color = value;
        }
    }

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 16,
            Height = 16,
            Color = _color,
            IsFilled = true,
            OutlineThickness = 0f,
            IsVisible = true,
        };
        Add(Rectangle);
    }
}
