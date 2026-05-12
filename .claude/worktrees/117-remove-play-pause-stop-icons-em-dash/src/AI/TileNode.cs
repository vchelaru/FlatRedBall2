using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.AI;

/// <summary>
/// A node in a <see cref="TileNodeNetwork"/>, representing one cell on the grid.
/// Nodes are created and linked by the network — do not construct directly.
/// </summary>
public class TileNode
{
    internal readonly List<TileNode> _neighbors = new();

    // A* scratch state — reset by TileNodeNetwork.GetPath before each search
    internal float _gCost;
    internal float _hCost;
    internal TileNode? _parent;
    internal bool _inOpen;
    internal bool _inClosed;

    /// <summary>World-space center of this tile.</summary>
    public Vector2 Position { get; internal set; }

    /// <summary>Optional game-defined data associated with this node (e.g. terrain type, waypoint marker).</summary>
    public object? Tag { get; set; }

    /// <summary>Nodes directly reachable from this node (cardinal and, for 8-way networks, diagonal neighbors).</summary>
    public IReadOnlyList<TileNode> Neighbors => _neighbors;

    internal void ResetAStarState()
    {
        _gCost = 0f;
        _hCost = 0f;
        _parent = null;
        _inOpen = false;
        _inClosed = false;
    }
}
