using System.Collections.Generic;
using System.Numerics;

namespace FlatRedBall2.Collision;

public class ShapeCollection : ICollidable
{
    private readonly List<ICollidable> _shapes = new();

    public void Add(AxisAlignedRectangle rect) => _shapes.Add(rect);
    public void Add(Circle circle) => _shapes.Add(circle);
    public void Add(Line line) => _shapes.Add(line);
    public void Add(Polygon polygon) => _shapes.Add(polygon);

    public bool CollidesWith(ICollidable other)
    {
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                return true;
        return false;
    }

    public Vector2 GetSeparationVector(ICollidable other)
    {
        foreach (var shape in _shapes)
        {
            var sep = shape.GetSeparationVector(other);
            if (sep != Vector2.Zero) return sep;
        }
        return Vector2.Zero;
    }

    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        // ShapeCollection is typically static geometry; only move 'other'
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                other.SeparateFrom(shape, otherMass, thisMass);
    }

    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
    {
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                other.AdjustVelocityFrom(shape, otherMass, thisMass, elasticity);
    }
}
