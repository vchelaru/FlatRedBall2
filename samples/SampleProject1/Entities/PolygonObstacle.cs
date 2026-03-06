using FlatRedBall2;
using FlatRedBall2.Collision;

namespace SampleProject1.Entities;

public class PolygonObstacle : Entity
{
    public Polygon Polygon { get; private set; } = null!;

    public override void CustomInitialize() { }

    /// <summary>
    /// Attaches a polygon to this entity. Call immediately after <c>Factory.Create()</c>
    /// before the entity participates in collision.
    /// </summary>
    public void SetPolygon(Polygon polygon)
    {
        Polygon = polygon;
        Add(polygon);
    }
}
