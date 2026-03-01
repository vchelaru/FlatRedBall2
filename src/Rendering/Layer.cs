namespace FlatRedBall2.Rendering;

public class Layer
{
    public Layer(string name) => Name = name;

    public string Name { get; }
    public bool IsScreenSpace { get; init; }

    public override string ToString() => Name;
}
