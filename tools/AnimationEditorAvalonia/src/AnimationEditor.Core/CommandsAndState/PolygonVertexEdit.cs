namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Which vertex a live preview gesture changed, so <see cref="IAppCommands.CommitPolygonPoints"/>
/// can repeat the edit on the other selected polygons.
/// </summary>
/// <param name="Index">The vertex moved, or the index the new vertex was inserted at.</param>
/// <param name="Inserted"><c>true</c> when the vertex was inserted at an edge midpoint and then dragged.</param>
public readonly record struct PolygonVertexEdit(int Index, bool Inserted)
{
    public static PolygonVertexEdit Move(int index) => new(index, false);
    public static PolygonVertexEdit Insert(int index) => new(index, true);
}
