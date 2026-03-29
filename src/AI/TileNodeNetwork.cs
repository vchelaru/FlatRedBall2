using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using FlatRedBall2.Collision;

namespace FlatRedBall2.AI;

/// <summary>
/// Whether the node network links nodes in 4 cardinal directions or 8 (including diagonals).
/// </summary>
public enum DirectionalType
{
    /// <summary>Links only up, down, left, right.</summary>
    Four,
    /// <summary>Links up, down, left, right, and the four diagonals.</summary>
    Eight
}

/// <summary>
/// A grid-based node network for A* pathfinding on tile maps.
/// </summary>
/// <remarks>
/// Typical usage:
/// <list type="number">
/// <item>Construct with the grid origin, spacing, dimensions, and link mode.</item>
/// <item>Call <see cref="FillCompletely"/> or <see cref="AddAndLinkNode(int,int)"/> to populate walkable tiles.</item>
/// <item>Optionally call <see cref="RemoveNodesOverlapping(AxisAlignedRectangle)"/> to carve out walls, then <see cref="EliminateCutCorners"/> for clean diagonal movement.</item>
/// <item>Call <see cref="GetPath(TileNode,TileNode)"/> to find paths.</item>
/// </list>
/// </remarks>
public class TileNodeNetwork
{
    private readonly TileNode?[][] _grid;       // [x][y]
    private readonly List<TileNode> _nodes = new();

    private readonly float _xOrigin;
    private readonly float _yOrigin;
    private readonly float _gridSpacing;
    private readonly int _xCount;
    private readonly int _yCount;
    private readonly DirectionalType _directionalType;

    /// <summary>World-space X of the center of the leftmost column of tiles.</summary>
    public float XOrigin => _xOrigin;
    /// <summary>World-space Y of the center of the bottom row of tiles.</summary>
    public float YOrigin => _yOrigin;
    /// <summary>Distance between adjacent tile centers (horizontal or vertical).</summary>
    public float GridSpacing => _gridSpacing;
    /// <summary>Number of tile columns.</summary>
    public int XCount => _xCount;
    /// <summary>Number of tile rows.</summary>
    public int YCount => _yCount;

    /// <summary>
    /// Creates an empty TileNodeNetwork. Nodes are added via <see cref="AddAndLinkNode(int,int)"/> or <see cref="FillCompletely"/>.
    /// </summary>
    /// <param name="xOrigin">
    /// World X of the center of the first (leftmost) tile column. When aligning with a
    /// <see cref="FlatRedBall2.Collision.TileShapeCollection"/> whose <c>X</c> is the left edge of the grid,
    /// pass <c>tileShapeCollection.X + gridSpacing / 2</c>. With the standard 16-unit grid this is
    /// <c>tileShapeCollection.X + 8</c>, placing the first node at world X = 8 and each subsequent
    /// column 16 units to the right.
    /// </param>
    /// <param name="yOrigin">
    /// World Y of the center of the bottom tile row. When aligning with a
    /// <see cref="FlatRedBall2.Collision.TileShapeCollection"/> whose <c>Y</c> is the bottom edge of the grid,
    /// pass <c>tileShapeCollection.Y + gridSpacing / 2</c>. With the standard 16-unit grid this is
    /// <c>tileShapeCollection.Y + 8</c>.
    /// </param>
    /// <param name="gridSpacing">
    /// Distance between adjacent tile centers. Should match <see cref="FlatRedBall2.Collision.TileShapeCollection.GridSize"/>.
    /// The standard value for FlatRedBall2 games is 16.
    /// </param>
    /// <param name="xCount">Number of tile columns.</param>
    /// <param name="yCount">Number of tile rows.</param>
    /// <param name="directionalType">Whether to allow diagonal movement between tiles.</param>
    public TileNodeNetwork(float xOrigin, float yOrigin, float gridSpacing, int xCount, int yCount, DirectionalType directionalType)
    {
        _xOrigin = xOrigin;
        _yOrigin = yOrigin;
        _gridSpacing = gridSpacing;
        _xCount = xCount;
        _yCount = yCount;
        _directionalType = directionalType;

        _grid = new TileNode?[xCount][];
        for (int x = 0; x < xCount; x++)
            _grid[x] = new TileNode?[yCount];
    }

    // ── Add / Remove ────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a node at tile index (<paramref name="x"/>, <paramref name="y"/>) and links it to all existing adjacent nodes.
    /// If a node already exists at that index, the existing node is returned without modification.
    /// </summary>
    public TileNode AddAndLinkNode(int x, int y)
    {
        if (_grid[x][y] is { } existing)
            return existing;

        var node = new TileNode { Position = IndexToWorldPosition(x, y) };
        _grid[x][y] = node;
        _nodes.Add(node);

        LinkToNeighborAt(node, x, y + 1);
        LinkToNeighborAt(node, x + 1, y);
        LinkToNeighborAt(node, x, y - 1);
        LinkToNeighborAt(node, x - 1, y);

        if (_directionalType == DirectionalType.Eight)
        {
            LinkToNeighborAt(node, x - 1, y + 1);
            LinkToNeighborAt(node, x + 1, y + 1);
            LinkToNeighborAt(node, x + 1, y - 1);
            LinkToNeighborAt(node, x - 1, y - 1);
        }

        return node;
    }

    /// <summary>
    /// Adds a node at the grid cell that contains the world position (<paramref name="worldX"/>, <paramref name="worldY"/>).
    /// </summary>
    public TileNode AddAndLinkNodeAtWorld(float worldX, float worldY)
    {
        WorldToIndex(worldX, worldY, out int x, out int y);
        return AddAndLinkNode(x, y);
    }

    /// <summary>
    /// Fills every cell in the grid with a linked node.
    /// </summary>
    public void FillCompletely()
    {
        for (int x = 0; x < _xCount; x++)
            for (int y = 0; y < _yCount; y++)
                AddAndLinkNode(x, y);
    }

    /// <summary>
    /// Removes all nodes whose tile cell overlaps the rectangle. The check is cell-based, so a node
    /// may be removed even if its center is not strictly inside the rectangle.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="AxisAlignedRectangle.AbsoluteX"/> and <see cref="AxisAlignedRectangle.AbsoluteY"/>,
    /// so the rectangle's world position is resolved correctly even when it is offset from its parent entity's origin.
    /// </remarks>
    public void RemoveNodesOverlapping(AxisAlignedRectangle rectangle)
    {
        float left   = rectangle.AbsoluteX - rectangle.Width  / 2f;
        float right  = rectangle.AbsoluteX + rectangle.Width  / 2f;
        float bottom = rectangle.AbsoluteY - rectangle.Height / 2f;
        float top    = rectangle.AbsoluteY + rectangle.Height / 2f;

        WorldToIndex(left, bottom, out int startX, out int startY);
        WorldToIndex(right, top,   out int endX,   out int endY);

        for (int y = startY; y <= endY; y++)
            for (int x = startX; x <= endX; x++)
                RemoveAt(x, y);
    }

    /// <summary>
    /// Removes the node at tile index (<paramref name="x"/>, <paramref name="y"/>), if any.
    /// All neighbor links to and from the node are removed.
    /// </summary>
    public void RemoveAt(int x, int y)
    {
        var node = NodeAt(x, y);
        if (node is null) return;
        Remove(node);
    }

    /// <summary>
    /// Removes the node at the cell containing world position (<paramref name="worldX"/>, <paramref name="worldY"/>), if any.
    /// </summary>
    public void RemoveAtWorld(float worldX, float worldY)
    {
        WorldToIndex(worldX, worldY, out int x, out int y);
        RemoveAt(x, y);
    }

    private void Remove(TileNode node)
    {
        // Unlink all neighbors
        foreach (var neighbor in node._neighbors)
            neighbor._neighbors.Remove(node);
        node._neighbors.Clear();

        WorldToIndex(node.Position.X, node.Position.Y, out int tx, out int ty);
        _grid[tx][ty] = null;
        _nodes.Remove(node);
    }

    // ── Diagonal corner cutting ──────────────────────────────────────────────

    /// <summary>
    /// Removes diagonal links that would require cutting through the corner of a missing tile,
    /// preventing entities from squeezing through gaps they shouldn't fit through.
    /// Call after all nodes have been added and walls removed.
    /// Only relevant for <see cref="DirectionalType.Eight"/> networks.
    /// </summary>
    public void EliminateCutCorners()
    {
        for (int x = 0; x < _xCount; x++)
            for (int y = 0; y < _yCount; y++)
                EliminateCutCornersAt(x, y);
    }

    /// <summary>
    /// Removes diagonal links from the node at (<paramref name="x"/>, <paramref name="y"/>) that pass through a missing cardinal neighbor.
    /// </summary>
    public void EliminateCutCornersAt(int x, int y)
    {
        var node = NodeAt(x, y);
        if (node is null) return;

        TryBreakDiagonal(node, NodeAt(x - 1, y + 1), NodeAt(x, y + 1), NodeAt(x - 1, y));
        TryBreakDiagonal(node, NodeAt(x + 1, y + 1), NodeAt(x, y + 1), NodeAt(x + 1, y));
        TryBreakDiagonal(node, NodeAt(x + 1, y - 1), NodeAt(x, y - 1), NodeAt(x + 1, y));
        TryBreakDiagonal(node, NodeAt(x - 1, y - 1), NodeAt(x, y - 1), NodeAt(x - 1, y));
    }

    private static void TryBreakDiagonal(TileNode node, TileNode? diagonal, TileNode? cardinalA, TileNode? cardinalB)
    {
        if (diagonal is null) return;
        if (cardinalA is null || cardinalB is null)
        {
            node._neighbors.Remove(diagonal);
            diagonal._neighbors.Remove(node);
        }
    }

    // ── Query ────────────────────────────────────────────────────────────────

    /// <summary>Returns the node at tile index (<paramref name="x"/>, <paramref name="y"/>), or <c>null</c> if out of bounds or empty.</summary>
    public TileNode? NodeAt(int x, int y)
    {
        if (x < 0 || x >= _xCount || y < 0 || y >= _yCount) return null;
        return _grid[x][y];
    }

    /// <summary>Returns the node at the cell nearest to the world position, or <c>null</c> if that cell is empty or out of bounds.</summary>
    public TileNode? NodeAtWorld(float worldX, float worldY)
    {
        int xi = (int)MathF.Round((worldX - _xOrigin) / _gridSpacing);
        int yi = (int)MathF.Round((worldY - _yOrigin) / _gridSpacing);
        return NodeAt(xi, yi);
    }

    /// <summary>
    /// Returns the nearest occupied node to the world position. Falls back to a linear scan if the cell at
    /// the snapped index is empty. Returns <c>null</c> if the network is empty.
    /// </summary>
    public TileNode? GetClosestNode(float worldX, float worldY)
    {
        int xi = (int)MathF.Round((worldX - _xOrigin) / _gridSpacing);
        int yi = (int)MathF.Round((worldY - _yOrigin) / _gridSpacing);

        xi = System.Math.Clamp(xi, 0, _xCount - 1);
        yi = System.Math.Clamp(yi, 0, _yCount - 1);

        if (_grid[xi][yi] is { } snapped)
            return snapped;

        // Linear fallback — sparse networks may have no node at the snapped cell
        TileNode? closest = null;
        float bestSq = float.MaxValue;
        var target = new Vector2(worldX, worldY);
        foreach (var node in _nodes)
        {
            float dSq = Vector2.DistanceSquared(node.Position, target);
            if (dSq < bestSq) { bestSq = dSq; closest = node; }
        }
        return closest;
    }

    // ── Coordinate conversion ────────────────────────────────────────────────

    /// <summary>Converts a tile index to the world-space center of that cell.</summary>
    public Vector2 IndexToWorldPosition(int x, int y)
        => new Vector2(_xOrigin + x * _gridSpacing, _yOrigin + y * _gridSpacing);

    /// <summary>Converts a world position to the nearest tile index (clamped to grid bounds).</summary>
    public void WorldToIndex(float worldX, float worldY, out int x, out int y)
    {
        x = (int)MathF.Round((worldX - _xOrigin) / _gridSpacing);
        y = (int)MathF.Round((worldY - _yOrigin) / _gridSpacing);
        x = System.Math.Clamp(x, 0, _xCount - 1);
        y = System.Math.Clamp(y, 0, _yCount - 1);
    }

    // ── A* Pathfinding ───────────────────────────────────────────────────────

    /// <summary>
    /// Finds the shortest path from <paramref name="start"/> to <paramref name="end"/> using A*.
    /// Returns a list of world positions from start (exclusive) to end (inclusive), or an empty list if no path exists.
    /// </summary>
    /// <remarks>
    /// Allocates a new <see cref="List{T}"/> on each call. Use <see cref="GetPath(TileNode,TileNode,List{Vector2})"/>
    /// to reuse an existing list and avoid allocation.
    /// </remarks>
    public List<Vector2> GetPath(TileNode start, TileNode end)
    {
        var result = new List<Vector2>();
        GetPath(start, end, result);
        return result;
    }

    /// <summary>
    /// Finds the shortest path from <paramref name="start"/> to <paramref name="end"/> using A*,
    /// writing world positions into <paramref name="result"/> (which is cleared first).
    /// Returns <c>true</c> if a path was found.
    /// </summary>
    /// <remarks>
    /// The start node is not included in the result; the end node is always the last entry.
    /// Reuse <paramref name="result"/> across frames to avoid garbage.
    /// </remarks>
    public bool GetPath(TileNode start, TileNode end, List<Vector2> result)
    {
        result.Clear();

        if (start == end)
            return true;

        // Reset A* state for all nodes
        foreach (var n in _nodes)
            n.ResetAStarState();

        var open = new PriorityQueue<TileNode, float>();

        start._gCost = 0f;
        start._hCost = Heuristic(start, end);
        start._inOpen = true;
        open.Enqueue(start, start._gCost + start._hCost);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current._inClosed) continue;   // stale entry
            current._inOpen = false;
            current._inClosed = true;

            if (current == end)
            {
                ReconstructPath(end, result);
                return true;
            }

            foreach (var neighbor in current._neighbors)
            {
                if (neighbor._inClosed) continue;

                float edgeCost = Vector2.Distance(current.Position, neighbor.Position);
                float tentativeG = current._gCost + edgeCost;

                if (!neighbor._inOpen || tentativeG < neighbor._gCost)
                {
                    neighbor._gCost = tentativeG;
                    neighbor._hCost = Heuristic(neighbor, end);
                    neighbor._parent = current;
                    neighbor._inOpen = true;
                    open.Enqueue(neighbor, neighbor._gCost + neighbor._hCost);
                }
            }
        }

        return false; // no path
    }

    private static float Heuristic(TileNode a, TileNode b)
        => Vector2.Distance(a.Position, b.Position);

    private static void ReconstructPath(TileNode end, List<Vector2> result)
    {
        var current = end;
        while (current._parent is not null)
        {
            result.Add(current.Position);
            current = current._parent;
        }
        result.Reverse();
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private void LinkToNeighborAt(TileNode node, int x, int y)
    {
        var neighbor = NodeAt(x, y);
        if (neighbor is null) return;
        if (!node._neighbors.Contains(neighbor))
        {
            node._neighbors.Add(neighbor);
            neighbor._neighbors.Add(node);
        }
    }
}
