namespace FlatRedBall2;

public interface IAttachable
{
    Entity? Parent { get; set; }
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
    float AbsoluteX { get; }
    float AbsoluteY { get; }
    float AbsoluteZ { get; }
    void Destroy();
}
